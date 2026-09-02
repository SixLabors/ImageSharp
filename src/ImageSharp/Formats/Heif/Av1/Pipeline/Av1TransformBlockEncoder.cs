// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Predicts, transforms, quantizes, and reconstructs finalized AV1 blocks.
/// </summary>
internal static class Av1TransformBlockEncoder
{
    /// <summary>
    /// Encodes and reconstructs one eight-bit lossy DC intra block in contiguous encoder planes.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="reconstruction">The coded reconstruction plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="above">The contiguous top reference samples.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    public static void EncodeIntraDcLossy(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<byte> source,
        Buffer2DRegion<byte> reconstruction,
        Point blockOrigin,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        bool hasLeft,
        bool hasAbove,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        ReadOnlySpan<byte> sourceSamples = GetPlaneSpan(source, blockOrigin);
        Span<byte> reconstructionSamples = GetPlaneSpan(reconstruction, blockOrigin);

        EncodeIntraLossyContiguous(
            workspace,
            sourceSamples,
            source.Stride,
            reconstructionSamples,
            reconstruction.Stride,
            above,
            left,
            hasLeft,
            hasAbove,
            Av1PredictionMode.DC,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            plane,
            ref state);
    }

    /// <summary>
    /// Encodes one eight-bit intra candidate into contiguous decision scratch.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodeIntraLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<byte> source,
        Point blockOrigin,
        Span<byte> reconstruction,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        ReadOnlySpan<byte> sourceSamples = GetPlaneSpan(source, blockOrigin);

        EncodeIntraLossyContiguous(
            workspace,
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            above,
            left,
            hasLeft,
            hasAbove,
            mode,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            plane,
            ref state);

        Av1ResidualBuilder.Subtract(
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            workspace.Residual,
            width,
            width,
            height);

        return Av1ResidualBuilder.SumSquares(workspace.Residual[..transformSize.GetSize2d()]) << 4;
    }

    /// <summary>
    /// Encodes and reconstructs one high-bit-depth lossy DC intra block in contiguous encoder planes.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="reconstruction">The coded reconstruction plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="above">The contiguous top reference samples.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    public static void EncodeIntraDcLossy(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<ushort> source,
        Buffer2DRegion<ushort> reconstruction,
        Point blockOrigin,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        bool hasLeft,
        bool hasAbove,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        ReadOnlySpan<ushort> sourceSamples = GetPlaneSpan(source, blockOrigin);
        Span<ushort> reconstructionSamples = GetPlaneSpan(reconstruction, blockOrigin);

        EncodeIntraLossyContiguous(
            workspace,
            sourceSamples,
            source.Stride,
            reconstructionSamples,
            reconstruction.Stride,
            above,
            left,
            hasLeft,
            hasAbove,
            Av1PredictionMode.DC,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            plane,
            bitDepth,
            ref state);
    }

    /// <summary>
    /// Encodes one high-bit-depth intra candidate into contiguous decision scratch.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodeIntraLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<ushort> source,
        Point blockOrigin,
        Span<ushort> reconstruction,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        ReadOnlySpan<ushort> sourceSamples = GetPlaneSpan(source, blockOrigin);

        EncodeIntraLossyContiguous(
            workspace,
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            above,
            left,
            hasLeft,
            hasAbove,
            mode,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            plane,
            bitDepth,
            ref state);

        Av1ResidualBuilder.Subtract(
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            workspace.Residual,
            width,
            width,
            height);

        long distortion = Av1ResidualBuilder.SumSquares(workspace.Residual[..transformSize.GetSize2d()]);
        int shift = (bitDepth.GetBitCount() - 8) * 2;
        long normalizedDistortion = shift == 0
            ? distortion
            : (distortion + (1L << (shift - 1))) >> shift;

        return normalizedDistortion << 4;
    }

    /// <summary>
    /// Encodes and reconstructs one eight-bit lossy intra block.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The number of source samples between rows.</param>
    /// <param name="reconstruction">The reconstructed frame samples and prediction destination.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    private static void EncodeIntraLossyContiguous(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> reconstruction,
        int reconstructionStride,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Prediction and subtraction stay in their SIMD-first operators while this method owns the required block-stage ordering.
        if (mode == Av1PredictionMode.DC)
        {
            Av1DcIntraPredictor.Predict(hasLeft, hasAbove, reconstruction, reconstructionStride, above, left, width, height);
        }
        else if (mode.IsDirectional())
        {
            // The current encoder disables intra-edge filtering in sequence syntax. Reusing transform workspace for
            // zone-three transposition keeps directional prediction allocation-free before the transform overwrites it.
            Span<byte> directionalScratch = MemoryMarshal.AsBytes(workspace.TransformWorkspace)[..(width * height)];

            Av1DirectionalIntraPredictor.Predict(
                reconstruction,
                reconstructionStride,
                transformSize,
                above,
                left,
                false,
                false,
                mode.ToAngle(),
                directionalScratch);
        }
        else
        {
            Av1NonDirectionalIntraPredictorBase.GetPredictor(mode)
                .Predict(reconstruction, reconstructionStride, above, left, width, height);
        }

        Av1ResidualBuilder.Subtract(source, sourceStride, reconstruction, reconstructionStride, workspace.Residual, width, width, height);

        EncodeLossy(
            workspace,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            Av1BitDepth.EightBit,
            ref state);

        if (state.EndOfBlock > 0)
        {
            // Reconstructing the quantized result makes later predictions use exactly the samples a decoder will reproduce.
            Av1InverseTransformer.Reconstruct8Bit(
                workspace.DequantizedCoefficients,
                reconstruction,
                reconstructionStride,
                transformSize,
                transformType,
                (int)plane,
                state.EndOfBlock,
                false,
                workspace.TransformWorkspace);
        }
    }

    /// <summary>
    /// Encodes and reconstructs one high-bit-depth lossy intra block.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The number of source samples between rows.</param>
    /// <param name="reconstruction">The reconstructed frame samples and prediction destination.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    private static void EncodeIntraLossyContiguous(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> reconstruction,
        int reconstructionStride,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Valid high-bit-depth samples remain below the sign bit, so signed transform lanes can share the unsigned frame storage.
        Span<short> signedReconstruction = MemoryMarshal.Cast<ushort, short>(reconstruction);
        ReadOnlySpan<short> signedAbove = MemoryMarshal.Cast<ushort, short>(above);
        ReadOnlySpan<short> signedLeft = MemoryMarshal.Cast<ushort, short>(left);
        if (mode == Av1PredictionMode.DC)
        {
            Av1DcIntraPredictor.Predict(
                hasLeft,
                hasAbove,
                signedReconstruction,
                reconstructionStride,
                signedAbove,
                signedLeft,
                width,
                height,
                bitDepth.GetBitCount());
        }
        else if (mode.IsDirectional())
        {
            Span<short> directionalScratch = MemoryMarshal.Cast<int, short>(workspace.TransformWorkspace)[..(width * height)];

            Av1DirectionalIntraPredictor.Predict(
                signedReconstruction,
                reconstructionStride,
                transformSize,
                signedAbove,
                signedLeft,
                false,
                false,
                mode.ToAngle(),
                directionalScratch);
        }
        else
        {
            Av1NonDirectionalIntraPredictorBase.GetPredictor(mode)
                .Predict(signedReconstruction, reconstructionStride, signedAbove, signedLeft, width, height);
        }

        Av1ResidualBuilder.Subtract(
            source,
            sourceStride,
            reconstruction,
            reconstructionStride,
            workspace.Residual,
            width,
            width,
            height);

        EncodeLossy(
            workspace,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            ref state);

        if (state.EndOfBlock > 0)
        {
            // Reconstructing the quantized result makes later predictions use exactly the samples a decoder will reproduce.
            Av1InverseTransformer.ReconstructHighBitDepth(
                workspace.DequantizedCoefficients,
                signedReconstruction,
                reconstructionStride,
                transformSize,
                transformType,
                (int)plane,
                state.EndOfBlock,
                false,
                bitDepth,
                workspace.TransformWorkspace);
        }
    }

    /// <summary>
    /// Applies a lossy forward transform and quantization to one residual block.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    public static void EncodeLossy(
        Av1EncoderBlockWorkspace workspace,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        Span<int> transformed = workspace.TransformCoefficients[..coefficientCount];
        Span<int> quantized = quantizedCoefficients[..coefficientCount];
        Span<int> dequantized = workspace.DequantizedCoefficients[..coefficientCount];

        // The encoder keeps transformed, quantized, and reconstructed coefficients separate because mode decision
        // consumes all three while only the quantized values survive in the frame coefficient owner.
        Av1ForwardTransformer.Transform2d(
            workspace.Residual,
            transformed,
            (uint)transformSize.GetWidth(),
            transformType,
            transformSize,
            bitDepth.GetBitCount(),
            workspace.TransformWorkspace);

        state.EndOfBlock = Av1ForwardQuantizer.QuantizeLossy(
            transformed,
            quantized,
            dequantized,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth);

        state.TransformType = transformType;
    }

    private static Span<TSample> GetPlaneSpan<TSample>(Buffer2DRegion<TSample> plane, Point blockOrigin)
        where TSample : unmanaged
    {
        int offset =
            ((plane.Bounds.Y + blockOrigin.Y) * plane.Stride) +
            plane.Bounds.X +
            blockOrigin.X;

        // Encoder planes wrap one contiguous frame owner, so direct segment access retains physical strides without an enumerator or row copy.
        return plane.Buffer.FastMemoryGroup[0].Span[offset..];
    }
}

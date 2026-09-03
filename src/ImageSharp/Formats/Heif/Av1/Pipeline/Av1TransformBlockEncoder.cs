// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
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
            0,
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
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
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
        int angleDelta,
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
            angleDelta,
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
    /// Encodes one eight-bit candidate from a cached prediction and source residual.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="prediction">The contiguous prediction samples.</param>
    /// <param name="residual">The contiguous source-minus-prediction samples.</param>
    /// <param name="reconstruction">The candidate reconstruction.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodePredictionLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<byte> source,
        Point blockOrigin,
        ReadOnlySpan<byte> prediction,
        ReadOnlySpan<short> residual,
        Span<byte> reconstruction,
        int reconstructionStride,
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
        int sampleCount = transformSize.GetSize2d();
        ReadOnlySpan<byte> sourceSamples = GetPlaneSpan(source, blockOrigin);

        // Each transform trial mutates reconstruction and residual scratch, so restore both prepared inputs.
        // Row copies preserve a larger candidate surface without materializing a second compact block.
        for (int row = 0; row < height; row++)
        {
            prediction.Slice(row * width, width).CopyTo(reconstruction.Slice(row * reconstructionStride, width));
        }

        residual[..sampleCount].CopyTo(workspace.Residual);
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

        Av1ResidualBuilder.Subtract(
            sourceSamples,
            source.Stride,
            reconstruction,
            reconstructionStride,
            workspace.Residual,
            width,
            width,
            height);

        return Av1ResidualBuilder.SumSquares(workspace.Residual[..sampleCount]) << 4;
    }

    /// <summary>
    /// Encodes one eight-bit chroma-from-luma candidate into contiguous decision scratch.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
    /// <param name="dc">The cached DC predictor sample shared by every alpha.</param>
    /// <param name="lumaQ3">The zero-mean reconstructed-luma predictor surface.</param>
    /// <param name="alphaQ3">The signed chroma-from-luma multiplier.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected chroma transform dimensions.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodeChromaFromLumaLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<byte> source,
        Point blockOrigin,
        Span<byte> reconstruction,
        byte dc,
        ReadOnlySpan<short> lumaQ3,
        int alphaQ3,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        ReadOnlySpan<byte> sourceSamples = GetPlaneSpan(source, blockOrigin);
        reconstruction[..transformSize.GetSize2d()].Fill(dc);

        // CfL adds its scaled reconstructed-luma AC contribution to the cached DC predictor before residual coding.
        Av1ChromaFromLumaPredictor.Predict(lumaQ3, reconstruction, width, alphaQ3, width, height);
        Av1ResidualBuilder.Subtract(
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            workspace.Residual,
            width,
            width,
            height);

        EncodeLossy(
            workspace,
            quantizedCoefficients,
            transformSize,
            Av1TransformType.DctDct,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            Av1BitDepth.EightBit,
            ref state);

        if (state.EndOfBlock > 0)
        {
            Av1InverseTransformer.Reconstruct8Bit(
                workspace.DequantizedCoefficients,
                reconstruction,
                width,
                transformSize,
                Av1TransformType.DctDct,
                (int)plane,
                state.EndOfBlock,
                false,
                workspace.TransformWorkspace);
        }

        // Final distortion is measured against the samples a decoder reconstructs, not the unquantized predictor.
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
            0,
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
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
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
        int angleDelta,
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
            angleDelta,
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
    /// Encodes one high-bit-depth candidate from a cached prediction and source residual.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="prediction">The contiguous prediction samples.</param>
    /// <param name="residual">The contiguous source-minus-prediction samples.</param>
    /// <param name="reconstruction">The candidate reconstruction.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
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
    public static long EncodePredictionLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<ushort> source,
        Point blockOrigin,
        ReadOnlySpan<ushort> prediction,
        ReadOnlySpan<short> residual,
        Span<ushort> reconstruction,
        int reconstructionStride,
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
        int sampleCount = transformSize.GetSize2d();
        ReadOnlySpan<ushort> sourceSamples = GetPlaneSpan(source, blockOrigin);

        // Each transform trial mutates reconstruction and residual scratch, so restore both prepared inputs.
        // Row copies preserve a larger candidate surface without materializing a second compact block.
        for (int row = 0; row < height; row++)
        {
            prediction.Slice(row * width, width).CopyTo(reconstruction.Slice(row * reconstructionStride, width));
        }

        residual[..sampleCount].CopyTo(workspace.Residual);
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
            Av1InverseTransformer.ReconstructHighBitDepth(
                workspace.DequantizedCoefficients,
                MemoryMarshal.Cast<ushort, short>(reconstruction),
                reconstructionStride,
                transformSize,
                transformType,
                (int)plane,
                state.EndOfBlock,
                false,
                bitDepth,
                workspace.TransformWorkspace);
        }

        Av1ResidualBuilder.Subtract(
            sourceSamples,
            source.Stride,
            reconstruction,
            reconstructionStride,
            workspace.Residual,
            width,
            width,
            height);

        long distortion = Av1ResidualBuilder.SumSquares(workspace.Residual[..sampleCount]);
        int shift = (bitDepth.GetBitCount() - 8) * 2;
        long normalizedDistortion = shift == 0
            ? distortion
            : (distortion + (1L << (shift - 1))) >> shift;

        return normalizedDistortion << 4;
    }

    /// <summary>
    /// Encodes one high-bit-depth chroma-from-luma candidate into contiguous decision scratch.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
    /// <param name="dc">The cached DC predictor sample shared by every alpha.</param>
    /// <param name="lumaQ3">The zero-mean reconstructed-luma predictor surface.</param>
    /// <param name="alphaQ3">The signed chroma-from-luma multiplier.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected chroma transform dimensions.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodeChromaFromLumaLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<ushort> source,
        Point blockOrigin,
        Span<ushort> reconstruction,
        ushort dc,
        ReadOnlySpan<short> lumaQ3,
        int alphaQ3,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
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
        Span<short> signedReconstruction = MemoryMarshal.Cast<ushort, short>(reconstruction);
        reconstruction[..transformSize.GetSize2d()].Fill(dc);

        Av1ChromaFromLumaPredictor.Predict(
            lumaQ3,
            signedReconstruction,
            width,
            alphaQ3,
            bitDepth.GetBitCount(),
            width,
            height);

        Av1ResidualBuilder.Subtract(
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            workspace.Residual,
            width,
            width,
            height);

        EncodeLossy(
            workspace,
            quantizedCoefficients,
            transformSize,
            Av1TransformType.DctDct,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            ref state);

        if (state.EndOfBlock > 0)
        {
            Av1InverseTransformer.ReconstructHighBitDepth(
                workspace.DequantizedCoefficients,
                signedReconstruction,
                width,
                transformSize,
                Av1TransformType.DctDct,
                (int)plane,
                state.EndOfBlock,
                false,
                bitDepth,
                workspace.TransformWorkspace);
        }

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
    /// Builds an eight-bit intra prediction and its compact source residual.
    /// </summary>
    /// <param name="workspace">The reusable prediction scratch.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The number of source samples between rows.</param>
    /// <param name="prediction">The prediction destination.</param>
    /// <param name="predictionStride">The number of prediction samples between rows.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="residual">The compact source-minus-prediction destination.</param>
    /// <param name="transformSize">The prediction dimensions.</param>
    public static void PrepareIntraPrediction(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> prediction,
        int predictionStride,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        int angleDelta,
        Span<short> residual,
        Av1TransformSize transformSize)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Prediction remains in the specialized SIMD-first kernels. This boundary only shares the prepared
        // samples and residual across transform trials that differ in transform size or type.
        if (mode == Av1PredictionMode.DC)
        {
            Av1DcIntraPredictor.Predict(hasLeft, hasAbove, prediction, predictionStride, above, left, width, height);
        }
        else if (mode.IsDirectional())
        {
            // The current encoder disables intra-edge filtering in sequence syntax. Zone-three transposition
            // borrows transform scratch because prediction completes before forward transformation starts.
            Span<byte> directionalScratch = MemoryMarshal.AsBytes(workspace.TransformWorkspace)[..(width * height)];

            Av1DirectionalIntraPredictor.Predict(
                prediction,
                predictionStride,
                transformSize,
                above,
                left,
                false,
                false,
                mode.ToAngle() + (angleDelta * Av1Constants.AngleStep),
                directionalScratch);
        }
        else
        {
            Av1NonDirectionalIntraPredictorBase.GetPredictor(mode)
                .Predict(prediction, predictionStride, above, left, width, height);
        }

        Av1ResidualBuilder.Subtract(
            source,
            sourceStride,
            prediction,
            predictionStride,
            residual,
            width,
            width,
            height);
    }

    /// <summary>
    /// Builds a high-bit-depth intra prediction and its compact source residual.
    /// </summary>
    /// <param name="workspace">The reusable prediction scratch.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The number of source samples between rows.</param>
    /// <param name="prediction">The prediction destination.</param>
    /// <param name="predictionStride">The number of prediction samples between rows.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="residual">The compact source-minus-prediction destination.</param>
    /// <param name="transformSize">The prediction dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    public static void PrepareIntraPrediction(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> prediction,
        int predictionStride,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        int angleDelta,
        Span<short> residual,
        Av1TransformSize transformSize,
        Av1BitDepth bitDepth)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Valid high-bit-depth samples remain below the sign bit, so the predictor kernels can share
        // the unsigned frame storage with their signed transform-domain implementation.
        Span<short> signedPrediction = MemoryMarshal.Cast<ushort, short>(prediction);
        ReadOnlySpan<short> signedAbove = MemoryMarshal.Cast<ushort, short>(above);
        ReadOnlySpan<short> signedLeft = MemoryMarshal.Cast<ushort, short>(left);
        if (mode == Av1PredictionMode.DC)
        {
            Av1DcIntraPredictor.Predict(
                hasLeft,
                hasAbove,
                signedPrediction,
                predictionStride,
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
                signedPrediction,
                predictionStride,
                transformSize,
                signedAbove,
                signedLeft,
                false,
                false,
                mode.ToAngle() + (angleDelta * Av1Constants.AngleStep),
                directionalScratch);
        }
        else
        {
            Av1NonDirectionalIntraPredictorBase.GetPredictor(mode)
                .Predict(signedPrediction, predictionStride, signedAbove, signedLeft, width, height);
        }

        Av1ResidualBuilder.Subtract(
            source,
            sourceStride,
            prediction,
            predictionStride,
            residual,
            width,
            width,
            height);
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
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
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
        int angleDelta,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        PrepareIntraPrediction(
            workspace,
            source,
            sourceStride,
            reconstruction,
            reconstructionStride,
            above,
            left,
            hasLeft,
            hasAbove,
            mode,
            angleDelta,
            workspace.Residual,
            transformSize);

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
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
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
        int angleDelta,
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
        PrepareIntraPrediction(
            workspace,
            source,
            sourceStride,
            reconstruction,
            reconstructionStride,
            above,
            left,
            hasLeft,
            hasAbove,
            mode,
            angleDelta,
            workspace.Residual,
            transformSize,
            bitDepth);

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
                MemoryMarshal.Cast<ushort, short>(reconstruction),
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

    public static Span<TSample> GetPlaneSpan<TSample>(Buffer2DRegion<TSample> plane, Point blockOrigin)
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

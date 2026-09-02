// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Predicts, transforms, quantizes, and reconstructs finalized AV1 blocks.
/// </summary>
internal static class Av1TransformBlockEncoder
{
    /// <summary>
    /// Encodes and reconstructs one eight-bit lossy DC intra block.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The number of source samples between rows.</param>
    /// <param name="reconstruction">The reconstructed frame samples and prediction destination.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
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
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> reconstruction,
        int reconstructionStride,
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
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Prediction and subtraction stay in their SIMD-first operators while this method owns the required block-stage ordering.
        Av1DcIntraPredictor.Predict(hasLeft, hasAbove, reconstruction, reconstructionStride, above, left, width, height);
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
    /// Encodes and reconstructs one high-bit-depth lossy DC intra block.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The number of source samples between rows.</param>
    /// <param name="reconstruction">The reconstructed frame samples and prediction destination.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
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
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> reconstruction,
        int reconstructionStride,
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
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Valid high-bit-depth samples remain below the sign bit, so signed transform lanes can share the unsigned frame storage.
        Span<short> signedReconstruction = MemoryMarshal.Cast<ushort, short>(reconstruction);
        ReadOnlySpan<short> signedAbove = MemoryMarshal.Cast<ushort, short>(above);
        ReadOnlySpan<short> signedLeft = MemoryMarshal.Cast<ushort, short>(left);
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
}

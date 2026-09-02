// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Transforms and quantizes finalized AV1 residual blocks.
/// </summary>
internal static class Av1TransformBlockEncoder
{
    /// <summary>
    /// Applies a lossy forward transform and quantization to one residual block.
    /// </summary>
    /// <param name="residual">The spatial residual samples.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="transformCoefficients">The reusable forward-transform coefficient workspace.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="dequantizedCoefficients">The reusable reconstruction coefficients.</param>
    /// <param name="transformWorkspace">The reusable internal transform workspace.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    public static void EncodeLossy(
        Span<short> residual,
        uint residualStride,
        Span<int> transformCoefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        Span<int> transformWorkspace,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        Span<int> transformed = transformCoefficients[..coefficientCount];
        Span<int> quantized = quantizedCoefficients[..coefficientCount];
        Span<int> dequantized = dequantizedCoefficients[..coefficientCount];

        // The encoder keeps transformed, quantized, and reconstructed coefficients separate because mode decision
        // consumes all three while only the quantized values survive in the frame coefficient owner.
        Av1ForwardTransformer.Transform2d(
            residual,
            transformed,
            residualStride,
            transformType,
            transformSize,
            bitDepth.GetBitCount(),
            transformWorkspace);

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

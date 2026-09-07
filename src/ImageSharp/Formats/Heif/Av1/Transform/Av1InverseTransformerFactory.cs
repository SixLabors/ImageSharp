// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Selects and runs the AV1 inverse-transform pipeline for byte or high-bit-depth sample storage.
/// </summary>
internal static class Av1InverseTransformerFactory
{
    /// <summary>
    /// Applies an inverse transform and adds its residual to eight-bit predicted samples.
    /// </summary>
    /// <param name="coefficients">The dequantized transform coefficients.</param>
    /// <param name="readBuffer">The predicted samples read by reconstruction.</param>
    /// <param name="readStride">The number of read samples between rows.</param>
    /// <param name="writeBuffer">The destination reconstructed samples.</param>
    /// <param name="writeStride">The number of destination samples between rows.</param>
    /// <param name="transformFunctionParameters">The transform type, dimensions, bit depth, and pipeline selection.</param>
    /// <param name="workspace">The reusable transform workspace for the containing decode operation.</param>
    public static void InverseTransformAdd(
        ReadOnlySpan<int> coefficients,
        Span<byte> readBuffer,
        int readStride,
        Span<byte> writeBuffer,
        int writeStride,
        in Av1TransformFunctionParameters transformFunctionParameters,
        Span<int> workspace)
    {
        Guard.MustBeLessThanOrEqualTo(transformFunctionParameters.BitDepth, 8, nameof(transformFunctionParameters));
        Guard.IsFalse(transformFunctionParameters.Is16BitPipeline, nameof(transformFunctionParameters), "Calling 8-bit pipeline while 16-bit is requested.");

        if (transformFunctionParameters.IsLossless)
        {
            Av1InverseWalshHadamardTransformer.TransformAdd(
                coefficients,
                readBuffer,
                readStride,
                writeBuffer,
                writeStride,
                transformFunctionParameters.EndOfBuffer,
                workspace);

            return;
        }

        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(
            transformFunctionParameters.TransformType,
            transformFunctionParameters.TransformSize,
            transformFunctionParameters.BitDepth);

        if (transformFunctionParameters.EndOfBuffer == 1 && transformFunctionParameters.TransformType == Av1TransformType.DctDct)
        {
            Av1Inverse2dTransformer.TransformDcAdd<byte, Av1InverseTransformer.ByteOutputOperator>(
                coefficients[0], readBuffer, readStride, writeBuffer, writeStride, ref config, 8);

            return;
        }

        config.ConfigureInverseSparsity(transformFunctionParameters.EndOfBuffer, transformFunctionParameters.BitDepth);
        Av1Inverse2dTransformer.Transform2dAdd(coefficients, readBuffer, readStride, writeBuffer, writeStride, ref config, workspace);
    }

    /// <summary>
    /// Applies an inverse transform and adds its residual to high-bit-depth predicted samples.
    /// </summary>
    /// <param name="coefficients">The dequantized transform coefficients.</param>
    /// <param name="readBuffer">The predicted samples read by reconstruction.</param>
    /// <param name="readStride">The number of read samples between rows.</param>
    /// <param name="writeBuffer">The destination reconstructed samples.</param>
    /// <param name="writeStride">The number of destination samples between rows.</param>
    /// <param name="transformFunctionParameters">The transform type, dimensions, bit depth, and pipeline selection.</param>
    /// <param name="workspace">The reusable transform workspace for the containing decode operation.</param>
    public static void InverseTransformAdd(
        ReadOnlySpan<int> coefficients,
        Span<short> readBuffer,
        int readStride,
        Span<short> writeBuffer,
        int writeStride,
        in Av1TransformFunctionParameters transformFunctionParameters,
        Span<int> workspace)
    {
        Guard.IsTrue(transformFunctionParameters.Is16BitPipeline, nameof(transformFunctionParameters), "Calling 16-bit pipeline while 8-bit is requested.");

        if (transformFunctionParameters.IsLossless)
        {
            Av1InverseWalshHadamardTransformer.TransformAdd(
                coefficients,
                readBuffer,
                readStride,
                writeBuffer,
                writeStride,
                transformFunctionParameters.EndOfBuffer,
                workspace,
                transformFunctionParameters.BitDepth);

            return;
        }

        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(
            transformFunctionParameters.TransformType,
            transformFunctionParameters.TransformSize,
            transformFunctionParameters.BitDepth);

        if (transformFunctionParameters.EndOfBuffer == 1 && transformFunctionParameters.TransformType == Av1TransformType.DctDct)
        {
            Av1Inverse2dTransformer.TransformDcAdd<short, Av1InverseTransformer.HighBitDepthOutputOperator>(
                coefficients[0], readBuffer, readStride, writeBuffer, writeStride, ref config, transformFunctionParameters.BitDepth);

            return;
        }

        config.ConfigureInverseSparsity(transformFunctionParameters.EndOfBuffer, transformFunctionParameters.BitDepth);
        Av1Inverse2dTransformer.Transform2dAdd(coefficients, readBuffer, readStride, writeBuffer, writeStride, ref config, workspace, transformFunctionParameters.BitDepth);
    }
}

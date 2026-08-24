// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

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
    /// <remarks>Corresponds to <c>svt_av1_inv_txfm_add</c> in the original WIP reference.</remarks>
    public static unsafe void InverseTransformAdd(Span<int> coefficients, Span<byte> readBuffer, int readStride, Span<byte> writeBuffer, int writeStride, Av1TransformFunctionParameters transformFunctionParameters)
    {
        Guard.MustBeLessThanOrEqualTo(transformFunctionParameters.BitDepth, 8, nameof(transformFunctionParameters));
        Guard.IsFalse(transformFunctionParameters.Is16BitPipeline, nameof(transformFunctionParameters), "Calling 8-bit pipeline while 16-bit is requested.");
        int width = transformFunctionParameters.TransformSize.GetWidth();
        int height = transformFunctionParameters.TransformSize.GetHeight();

        // The 2-D transform needs one complete intermediate plane plus one input and output vector for the longer axis.
        Span<int> buffer = new int[(width * height) + (2 * Math.Max(width, height))];
        Av1Transform2dFlipConfiguration config = new(transformFunctionParameters.TransformType, transformFunctionParameters.TransformSize);
        Av1Inverse2dTransformer.Transform2dAdd(coefficients, readBuffer, readStride, writeBuffer, writeStride, config, buffer);
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
    public static unsafe void InverseTransformAdd(Span<int> coefficients, Span<short> readBuffer, int readStride, Span<short> writeBuffer, int writeStride, Av1TransformFunctionParameters transformFunctionParameters)
    {
        Guard.IsTrue(transformFunctionParameters.Is16BitPipeline, nameof(transformFunctionParameters), "Calling 16-bit pipeline while 8-bit is requested.");
        int width = transformFunctionParameters.TransformSize.GetWidth();
        int height = transformFunctionParameters.TransformSize.GetHeight();

        // The 2-D transform needs one complete intermediate plane plus one input and output vector for the longer axis.
        Span<int> buffer = new int[(width * height) + (2 * Math.Max(width, height))];
        Av1Transform2dFlipConfiguration config = new(transformFunctionParameters.TransformType, transformFunctionParameters.TransformSize);
        Av1Inverse2dTransformer.Transform2dAdd(coefficients, readBuffer, readStride, writeBuffer, writeStride, config, buffer, transformFunctionParameters.BitDepth);
    }

    /// <summary>
    /// Creates the inverse-transform implementation for a concrete function and length.
    /// </summary>
    /// <param name="type">The concrete transform function.</param>
    /// <returns>The transform implementation, or <see langword="null"/> for an invalid function.</returns>
    internal static IAv1Transformer1d? GetTransformer(Av1TransformFunctionType type) => type switch
    {
        Av1TransformFunctionType.Dct4 => new Av1Dct4Inverse1dTransformer(),
        Av1TransformFunctionType.Dct8 => new Av1Dct8Inverse1dTransformer(),
        Av1TransformFunctionType.Dct16 => new Av1Dct16Inverse1dTransformer(),
        Av1TransformFunctionType.Dct32 => new Av1Dct32Inverse1dTransformer(),
        Av1TransformFunctionType.Dct64 => new Av1Dct64Inverse1dTransformer(),
        Av1TransformFunctionType.Adst4 => new Av1Adst4Inverse1dTransformer(),
        Av1TransformFunctionType.Adst8 => new Av1Adst8Inverse1dTransformer(),
        Av1TransformFunctionType.Adst16 => new Av1Adst16Inverse1dTransformer(),
        Av1TransformFunctionType.Adst32 => new Av1Adst32Inverse1dTransformer(),
        Av1TransformFunctionType.Identity4 => new Av1Identity4Inverse1dTransformer(),
        Av1TransformFunctionType.Identity8 => new Av1Identity8Inverse1dTransformer(),
        Av1TransformFunctionType.Identity16 => new Av1Identity16Inverse1dTransformer(),
        Av1TransformFunctionType.Identity32 => new Av1Identity32Inverse1dTransformer(),
        Av1TransformFunctionType.Identity64 => new Av1Identity64Inverse1dTransformer(),
        _ => null
    };
}

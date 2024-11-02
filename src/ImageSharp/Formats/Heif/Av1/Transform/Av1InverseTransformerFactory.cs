// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static class Av1InverseTransformerFactory
{
    /// <summary>
    /// SVT: svt_av1_inv_txfm_add
    /// </summary>
    public static unsafe void InverseTransformAdd(Span<int> coefficients, Span<byte> readBuffer, int readStride, Span<byte> writeBuffer, int writeStride, Av1TransformFunctionParameters transformFunctionParameters)
    {
        Guard.MustBeLessThanOrEqualTo(transformFunctionParameters.BitDepth, 8, nameof(transformFunctionParameters));
        Guard.IsFalse(transformFunctionParameters.Is16BitPipeline, nameof(transformFunctionParameters), "Calling 8-bit pipeline while 16-bit is requested.");
        int width = transformFunctionParameters.TransformSize.GetWidth();
        int height = transformFunctionParameters.TransformSize.GetHeight();
        Span<int> buffer = new int[(width * height) + (2 * Math.Max(width, height))];
        Av1Transform2dFlipConfiguration config = new(transformFunctionParameters.TransformType, transformFunctionParameters.TransformSize);
        Av1Inverse2dTransformer.Transform2dAdd(coefficients, readBuffer, readStride, writeBuffer, writeStride, config, buffer);
    }

    public static unsafe void InverseTransformAdd(Span<int> coefficients, Span<short> readBuffer, int readStride, Span<short> writeBuffer, int writeStride, Av1TransformFunctionParameters transformFunctionParameters)
    {
        Guard.IsTrue(transformFunctionParameters.Is16BitPipeline, nameof(transformFunctionParameters), "Calling 16-bit pipeline while 8-bit is requested.");
        int width = transformFunctionParameters.TransformSize.GetWidth();
        int height = transformFunctionParameters.TransformSize.GetHeight();
        Span<int> buffer = new int[(width * height) + (2 * Math.Max(width, height))];
        Av1Transform2dFlipConfiguration config = new(transformFunctionParameters.TransformType, transformFunctionParameters.TransformSize);
        Av1Inverse2dTransformer.Transform2dAdd(coefficients, readBuffer, readStride, writeBuffer, writeStride, config, buffer, transformFunctionParameters.BitDepth);
    }

    internal static IAv1Forward1dTransformer? GetTransformer(Av1TransformFunctionType type) => type switch
    {
        Av1TransformFunctionType.Dct4 => new Av1Dct4Inverse1dTransformer(),
        Av1TransformFunctionType.Adst4 => new Av1Adst4Inverse1dTransformer(),
        Av1TransformFunctionType.Identity4 => new Av1Identity4Inverse1dTransformer(),
        _ => null
    };
}

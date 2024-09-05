// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static class InverseTransformerFactory
{
    internal static unsafe void InverseTransformAdd(ref int coefficients, Span<byte> readBuffer, int readStride, Span<byte> writeBuffer, int writeStride, Av1TransformFunctionParameters transformFunctionParameters)
    {
        switch (transformFunctionParameters.TransformType)
        {
            case Av1TransformType.DctDct:
                Av1DctDctInverseTransformer.InverseTransformAdd(ref coefficients, readBuffer, readStride, writeBuffer, writeStride, transformFunctionParameters);
                break;
            default:
                throw new InvalidImageContentException("Unknown transform type: " + transformFunctionParameters.TransformType);
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1InverseTransformer
{
    /// <summary>
    /// SVT: svt_aom_inv_transform_recon8bit
    /// </summary>
    public static void Reconstruct8Bit(Span<int> coefficientsBuffer, Span<byte> reconstructionBuffer, int reconstructionStride, Av1TransformSize transformSize, Av1TransformType transformType, int plane, int numberOfCoefficients, bool isLossless)
    {
        Av1TransformFunctionParameters transformFunctionParameters = new()
        {
            TransformType = transformType,
            TransformSize = transformSize,
            EndOfBuffer = numberOfCoefficients,
            IsLossless = isLossless,
            BitDepth = 8,
            Is16BitPipeline = false
        };

        Av1InverseTransformerFactory.InverseTransformAdd(
            coefficientsBuffer, reconstructionBuffer, reconstructionStride, reconstructionBuffer, reconstructionStride, transformFunctionParameters);
    }

    /// <summary>
    /// SVT: svt_aom_inv_transform_recon8bit
    /// </summary>
    public static void Reconstruct8Bit(Span<int> coefficientsBuffer, Span<byte> reconstructionBufferRead, int reconstructionReadStride, Span<byte> reconstructionBufferWrite, int reconstructionWriteStride, Av1TransformSize transformSize, Av1TransformType transformType, int plane, int numberOfCoefficients, bool isLossless)
    {
        Av1TransformFunctionParameters transformFunctionParameters = new()
        {
            TransformType = transformType,
            TransformSize = transformSize,
            EndOfBuffer = numberOfCoefficients,
            IsLossless = isLossless,
            BitDepth = 8,
            Is16BitPipeline = false
        };

        /* When output pointers to read and write are differents,
        * then kernel copy also all buffer from read to write,
        * and cannot be limited by End Of Buffer calculations. */
        transformFunctionParameters.EndOfBuffer = Av1InverseTransformMath.GetMaxEndOfBuffer(transformSize);

        Av1InverseTransformerFactory.InverseTransformAdd(
            coefficientsBuffer, reconstructionBufferRead, reconstructionReadStride, reconstructionBufferWrite, reconstructionWriteStride, transformFunctionParameters);
    }
}

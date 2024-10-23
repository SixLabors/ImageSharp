// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1InverseTransformer
{
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

        if (reconstructionBufferRead != reconstructionBufferWrite)
        {
            /* When output pointers to read and write are differents,
             * then kernel copy also all buffer from read to write,
             * and cannot be limited by End Of Buffer calculations. */
            transformFunctionParameters.EndOfBuffer = GetMaxEndOfBuffer(transformSize);
        }

        Av1InverseTransformerFactory.InverseTransformAdd(
            coefficientsBuffer, reconstructionBufferRead, reconstructionReadStride, reconstructionBufferWrite, reconstructionWriteStride, transformFunctionParameters);
    }

    /// <summary>
    /// SVT: av1_get_max_eob
    /// </summary>
    private static int GetMaxEndOfBuffer(Av1TransformSize transformSize)
    {
        if (transformSize is Av1TransformSize.Size64x64 or Av1TransformSize.Size64x32 or Av1TransformSize.Size32x64)
        {
            return 1024;
        }

        if (transformSize is Av1TransformSize.Size16x64 or Av1TransformSize.Size64x16)
        {
            return 512;
        }

        return transformSize.GetSize2d();
    }
}

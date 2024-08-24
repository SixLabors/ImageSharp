// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1InverseTransformer
{
    /// <summary>
    /// SVT: svt_aom_inv_transform_recon8bit
    /// </summary>
    public static void Reconstruct8Bit(Span<int> coefficientsBuffer, Span<byte> transformBlockReconstructionBuffer1, int reconstructionStride1, Span<byte> transformBlockReconstructionBuffer2, int reconstructionStride2, Av1TransformSize transformSize, Av1TransformType transformType, int plane, int numberOfCoefficients, bool isLossless)
    {
        throw new NotImplementedException("Inverse transformation not implemented yet.");
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1InverseQuantizationTests
{
    [Theory]
    [InlineData((int)Av1BitDepth.EightBit, 26, 30, 40, 32767, -32768)]
    [InlineData((int)Av1BitDepth.TenBit, 75, 83, 112, 131071, -131072)]
    [InlineData((int)Av1BitDepth.TwelveBit, 266, 297, 399, 524287, -524288)]
    public void DequantizationMatchesReferenceMatrixAndPrecisionValues(
        int bitDepthValue,
        int dc,
        int ac,
        int weightedAc,
        int maximum,
        int minimum)
    {
        ObuSequenceHeader sequenceHeader = new()
        {
            ColorConfig = new ObuColorConfig { BitDepth = (Av1BitDepth)bitDepthValue }
        };

        ObuFrameHeader frameHeader = new();
        frameHeader.QuantizationParameters.BaseQIndex = 23;
        frameHeader.QuantizationParameters.IsUsingQMatrix = true;
        frameHeader.SegmentationParameters.QMLevel[0][0] = 0;
        Av1InverseQuantizer quantizer = new(sequenceHeader, frameHeader);
        Av1BlockModeInfo mode = new(Av1BlockSize.Block4x4, Point.Empty);
        Av1InverseQuantizer.TransformParameters matrix = new(
            quantizer, mode, Av1TransformType.DctDct, Av1TransformSize.Size4x4, Av1Plane.Y);

        // quant_common.c's qindex-23 tables supply the three DC/AC pairs above. Its level-zero luma matrix
        // begins with weights 32 and 43; the rounded AC values are independently fixed in the theory data.
        Assert.Equal(7 * dc, matrix.Dequantize(7, 0, false));
        Assert.Equal(11 * weightedAc, matrix.Dequantize(11, 1, false));
        Assert.Equal(-11 * weightedAc, matrix.Dequantize(11, 1, true));
        Assert.Equal(maximum, matrix.Dequantize(0xfffff, 1, false));
        Assert.Equal(minimum, matrix.Dequantize(0xfffff, 1, true));

        // Identity and one-dimensional transforms bypass matrix weighting even when the frame enables it.
        Av1InverseQuantizer.TransformParameters identity = new(
            quantizer, mode, Av1TransformType.Identity, Av1TransformSize.Size4x4, Av1Plane.Y);

        Av1InverseQuantizer.TransformParameters horizontal = new(
            quantizer, mode, Av1TransformType.HorizontalAdst, Av1TransformSize.Size4x4, Av1Plane.Y);

        Assert.Equal(11 * ac, identity.Dequantize(11, 1, false));
        Assert.Equal(11 * ac, horizontal.Dequantize(11, 1, false));
    }

    [Fact]
    public void DequantizationPreservesProductMaskAndTransformRounding()
    {
        ObuSequenceHeader sequenceHeader = new()
        {
            ColorConfig = new ObuColorConfig { BitDepth = Av1BitDepth.EightBit }
        };

        ObuFrameHeader frameHeader = new();
        frameHeader.QuantizationParameters.BaseQIndex = 23;
        frameHeader.QuantizationParameters.IsUsingQMatrix = true;
        frameHeader.SegmentationParameters.QMLevel[0][0] = 0;
        Av1InverseQuantizer quantizer = new(sequenceHeader, frameHeader);
        Av1BlockModeInfo mode = new(Av1BlockSize.Block64x64, Point.Empty);
        Av1InverseQuantizer.TransformParameters matrix = new(
            quantizer, mode, Av1TransformType.DctDct, Av1TransformSize.Size4x4, Av1Plane.Y);

        // The final 4x4 matrix weight is 200, giving AC=188. Its product with 89241 is 2^24 + 92:
        // retaining the 24-bit intermediate must produce 92, rather than saturating the unmasked product.
        Assert.Equal(92, matrix.Dequantize(89241, 15, false));
        Assert.Equal(-92, matrix.Dequantize(89241, 15, true));

        frameHeader.QuantizationParameters.IsUsingQMatrix = false;
        Av1InverseQuantizer.TransformParameters scaled32 = new(
            quantizer, mode, Av1TransformType.DctDct, Av1TransformSize.Size32x32, Av1Plane.Y);

        Av1InverseQuantizer.TransformParameters scaled64 = new(
            quantizer, mode, Av1TransformType.DctDct, Av1TransformSize.Size64x64, Av1Plane.Y);

        // A magnitude of 11 with AC=30 gives 330 before scaling. Sign follows truncation of the positive
        // magnitude, so the negative 64x64 result is -82 rather than the arithmetic-right-shift result -83.
        Assert.Equal(165, scaled32.Dequantize(11, 1, false));
        Assert.Equal(82, scaled64.Dequantize(11, 1, false));
        Assert.Equal(-82, scaled64.Dequantize(11, 1, true));

        frameHeader.QuantizationParameters.BaseQIndex = 0;
        frameHeader.QuantizationParameters.IsUsingQMatrix = true;
        frameHeader.LosslessArray[0] = true;
        Av1InverseQuantizer losslessQuantizer = new(sequenceHeader, frameHeader);
        Av1InverseQuantizer.TransformParameters lossless = new(
            losslessQuantizer, mode, Av1TransformType.DctDct, Av1TransformSize.Size4x4, Av1Plane.Y);

        Assert.Equal(44, lossless.Dequantize(11, 1, false));
    }

    [Fact]
    public void MatricesCoverAllLevelsPlanesAndTransformSizes()
    {
        for (int level = 0; level < Av1Constants.QuantificationMatrixLevelCount; level++)
        {
            for (Av1Plane plane = Av1Plane.Y; (int)plane < Av1Constants.MaxPlanes; plane++)
            {
                for (int transformSizeIndex = 0; transformSizeIndex < (int)Av1TransformSize.AllSizes; transformSizeIndex++)
                {
                    Av1TransformSize transformSize = (Av1TransformSize)transformSizeIndex;
                    Av1TransformSize adjustedSize = transformSize.GetAdjusted();
                    int expectedLength = adjustedSize.GetWidth() * adjustedSize.GetHeight();

                    ReadOnlySpan<int> matrix = Av1InverseQuantizationLookup.GetQuantizationMatrix(level, plane, transformSize);

                    Assert.Equal(expectedLength, matrix.Length);
                }
            }
        }
    }
}

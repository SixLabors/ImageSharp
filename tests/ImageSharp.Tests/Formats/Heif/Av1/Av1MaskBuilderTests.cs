// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the wedge and inter-intra mask builders against per-sample definitions of libaom's masters.
/// </summary>
[Trait("Format", "Avif")]
public class Av1MaskBuilderTests
{
    /// <summary>
    /// The hardware configurations covering every vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    private static readonly byte[] MasterObliqueOdd =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 6, 18,
        37, 53, 60, 63, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
    ];

    private static readonly byte[] MasterObliqueEven =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 11, 27,
        46, 58, 62, 63, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
    ];

    private static readonly byte[] MasterVertical =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 7, 21,
        43, 57, 62, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
    ];

    private static readonly byte[] HeightGreaterCodebook =
    [
        2, 4, 4, 3, 4, 4, 4, 4, 4, 5, 4, 4,
        0, 4, 2, 0, 4, 4, 0, 4, 6, 1, 4, 4,
        2, 4, 2, 2, 4, 6, 5, 4, 2, 5, 4, 6,
        3, 2, 4, 3, 6, 4, 4, 2, 4, 4, 6, 4,
    ];

    private static readonly byte[] HeightLessCodebook =
    [
        2, 4, 4, 3, 4, 4, 4, 4, 4, 5, 4, 4,
        1, 2, 4, 1, 4, 4, 1, 6, 4, 0, 4, 4,
        2, 4, 2, 2, 4, 6, 5, 4, 2, 5, 4, 6,
        3, 2, 4, 3, 6, 4, 4, 2, 4, 4, 6, 4,
    ];

    private static readonly byte[] EqualCodebook =
    [
        2, 4, 4, 3, 4, 4, 4, 4, 4, 5, 4, 4,
        0, 4, 2, 0, 4, 6, 1, 2, 4, 1, 6, 4,
        2, 4, 2, 2, 4, 6, 5, 4, 2, 5, 4, 6,
        3, 2, 4, 3, 6, 4, 4, 2, 4, 4, 6, 4,
    ];

    private static readonly byte[] InterIntraWeights =
    [
        60, 58, 56, 54, 52, 50, 48, 47, 45, 44, 42, 41, 39, 38, 37, 35,
        34, 33, 32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 22, 21, 20,
        19, 19, 18, 18, 17, 16, 16, 15, 15, 14, 14, 13, 13, 12, 12, 12,
        11, 11, 10, 10, 10, 9, 9, 9, 8, 8, 8, 8, 7, 7, 7, 7,
        6, 6, 6, 6, 6, 5, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4,
        4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    ];

    /// <summary>
    /// Verifies both mask builders at every hardware tier.
    /// </summary>
    [Fact]
    public void MasksMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertMasks, Configurations);

    private static void AssertMasks()
    {
        AssertWedgeMasks();
        AssertInterIntraMasks();
    }

    private static void AssertWedgeMasks()
    {
        Av1BlockSize[] blockSizes =
        [
            Av1BlockSize.Block8x8, Av1BlockSize.Block8x16, Av1BlockSize.Block16x8, Av1BlockSize.Block16x16,
            Av1BlockSize.Block16x32, Av1BlockSize.Block32x16, Av1BlockSize.Block32x32, Av1BlockSize.Block8x32,
            Av1BlockSize.Block32x8
        ];

        const int stride = 40;
        byte[] expected = new byte[stride * 32];
        byte[] actual = new byte[stride * 32];
        foreach (Av1BlockSize blockSize in blockSizes)
        {
            for (int wedgeIndex = 0; wedgeIndex < 16; wedgeIndex++)
            {
                foreach (bool wedgeSign in new[] { false, true })
                {
                    foreach ((int subX, int subY) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
                    {
                        foreach (bool invert in new[] { false, true })
                        {
                            Array.Fill(expected, (byte)0xAA);
                            Array.Fill(actual, (byte)0xAA);
                            ReferenceWedge(expected, stride, blockSize, wedgeIndex, wedgeSign, subX, subY, invert);
                            Av1WedgeMask.Fill(actual, stride, blockSize, wedgeIndex, wedgeSign, subX, subY, invert);
                            Assert.Equal(expected, actual);
                        }
                    }
                }
            }
        }
    }

    private static void AssertInterIntraMasks()
    {
        const int stride = 40;
        byte[] expected = new byte[stride * 32];
        byte[] actual = new byte[stride * 32];
        foreach (int width in new[] { 4, 8, 16, 32 })
        {
            foreach (int height in new[] { 4, 8, 16, 32 })
            {
                foreach (Av1InterIntraMode mode in Enum.GetValues<Av1InterIntraMode>())
                {
                    foreach (bool invert in new[] { false, true })
                    {
                        Array.Fill(expected, (byte)0xAA);
                        Array.Fill(actual, (byte)0xAA);
                        ReferenceInterIntra(expected, stride, width, height, mode, invert);
                        Av1InterIntraMaskBuilder.FillInterIntraMask(actual, stride, width, height, mode, invert);
                        Assert.Equal(expected, actual);
                    }
                }
            }
        }
    }

    private static void ReferenceInterIntra(byte[] mask, int stride, int width, int height, Av1InterIntraMode mode, bool invert)
    {
        int sizeScale = 128 / Math.Max(width, height);
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int alpha = mode switch
                {
                    Av1InterIntraMode.Vertical => InterIntraWeights[row * sizeScale],
                    Av1InterIntraMode.Horizontal => InterIntraWeights[column * sizeScale],
                    Av1InterIntraMode.Smooth => InterIntraWeights[Math.Min(row, column) * sizeScale],
                    _ => 32,
                };

                mask[(row * stride) + column] = (byte)(invert ? 64 - alpha : alpha);
            }
        }
    }

    private static void ReferenceWedge(
        byte[] destination,
        int stride,
        Av1BlockSize blockSize,
        int wedgeIndex,
        bool wedgeSign,
        int subX,
        int subY,
        bool invert)
    {
        int lumaWidth = blockSize.GetWidth();
        int lumaHeight = blockSize.GetHeight();
        int width = Math.Max(4, lumaWidth >> subX);
        int height = Math.Max(4, lumaHeight >> subY);
        byte[] codebook = lumaHeight > lumaWidth ? HeightGreaterCodebook : lumaHeight < lumaWidth ? HeightLessCodebook : EqualCodebook;
        int direction = codebook[wedgeIndex * 3];
        int horizontalOffset = (codebook[(wedgeIndex * 3) + 1] * lumaWidth) >> 3;
        int verticalOffset = (codebook[(wedgeIndex * 3) + 2] * lumaHeight) >> 3;
        bool negative = wedgeSign ^ GetSignFlip(blockSize, wedgeIndex);
        int masterRow = 32 - verticalOffset;
        int masterColumn = 32 - horizontalOffset;
        for (int row = 0; row < height; row++)
        {
            int lumaRow = row << subY;
            for (int column = 0; column < width; column++)
            {
                int lumaColumn = column << subX;
                int mask = GetMasterValue(direction, negative, masterRow + lumaRow, masterColumn + lumaColumn);
                if (subX != 0)
                {
                    mask += GetMasterValue(direction, negative, masterRow + lumaRow, masterColumn + lumaColumn + 1);
                }

                if (subY != 0)
                {
                    int lowerMask = GetMasterValue(direction, negative, masterRow + lumaRow + 1, masterColumn + lumaColumn);
                    if (subX != 0)
                    {
                        lowerMask += GetMasterValue(direction, negative, masterRow + lumaRow + 1, masterColumn + lumaColumn + 1);
                    }

                    mask += lowerMask;
                }

                int shift = subX + subY;
                if (shift != 0)
                {
                    mask = (mask + (1 << (shift - 1))) >> shift;
                }

                destination[(row * stride) + column] = (byte)(invert ? 64 - mask : mask);
            }
        }
    }

    private static int GetMasterValue(int direction, bool negative, int row, int column)
    {
        int value = direction switch
        {
            0 => MasterVertical[row],
            1 => MasterVertical[column],
            2 => GetOblique63(column, row),
            3 => GetOblique63(row, column),
            4 => 64 - GetOblique63(row, 63 - column),
            _ => 64 - GetOblique63(column, 63 - row),
        };

        return negative ? 64 - value : value;
    }

    private static int GetOblique63(int row, int column)
    {
        bool oddRow = (row & 1) != 0;
        int shift = (oddRow ? 15 : 16) - (row >> 1);
        int sourceColumn = Math.Clamp(column - shift, 0, 63);
        return oddRow ? MasterObliqueOdd[sourceColumn] : MasterObliqueEven[sourceColumn];
    }

    private static bool GetSignFlip(Av1BlockSize blockSize, int wedgeIndex)
    {
        byte[] signFlips = blockSize switch
        {
            Av1BlockSize.Block8x8 or Av1BlockSize.Block16x16 or Av1BlockSize.Block32x32 =>
                [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1],
            Av1BlockSize.Block8x32 =>
                [1, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1],
            Av1BlockSize.Block32x8 =>
                [1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 1, 0, 1],
            _ =>
                [1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1],
        };

        return signFlips[wedgeIndex] != 0;
    }
}

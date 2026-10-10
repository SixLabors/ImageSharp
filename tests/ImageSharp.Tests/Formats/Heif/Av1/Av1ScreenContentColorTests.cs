// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the color counting, dominant value and dilation of the screen-content detector.
/// </summary>
[Trait("Format", "Avif")]
public class Av1ScreenContentColorTests
{
    private const int BlockLength = 16;
    private const int BlockArea = BlockLength * BlockLength;

    /// <summary>
    /// The hardware configurations covering every vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies the counts, dominant values and dilated blocks against the scalar definitions of
    /// av1_count_colors_with_threshold(), av1_find_dominant_value() and av1_dilate_block().
    /// </summary>
    [Fact]
    public void ColorKernelsMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertColorKernels, Configurations);

    private static void AssertColorKernels()
    {
        byte[] block = new byte[BlockArea];
        byte[] dilated = new byte[BlockArea];
        byte[] expected = new byte[BlockArea];
        int seed = 1;
        foreach (int colors in new[] { 1, 2, 3, 4, 5, 6, 7, 12, 40, 41, 64, 256 })
        {
            for (int pattern = 0; pattern < 6; pattern++)
            {
                for (int i = 0; i < BlockArea; i++)
                {
                    seed = (seed * 1103515245) + 12345;
                    int random = (seed >>> 16) & 0x7FFF;
                    int index = pattern switch
                    {
                        0 => random % colors,
                        1 => (i / 37) % colors,
                        2 => random % 8 == 0 ? random % colors : 0,
                        3 => i % colors,
                        4 => (i & BlockLength) == 0 ? random % colors : colors - 1,
                        _ => ((i % BlockLength) * 5 / BlockLength) % colors
                    };

                    block[i] = (byte)((index * 97) + (pattern * 13));
                }

                foreach (int threshold in new[] { 4, 6, 40 })
                {
                    bool expectedUnder = CountColorsReference(block, threshold, out int expectedCount);
                    bool actualUnder = Av1ScreenContentDetector.CountColorsWithThreshold(block, threshold, out int actualCount);
                    Assert.Equal(expectedUnder, actualUnder);
                    Assert.Equal(expectedCount, actualCount);
                }

                byte dominant = FindDominantReference(block);
                Assert.Equal(dominant, Av1ScreenContentDetector.FindDominantValue(block));

                DilateReference(block, dominant, expected);
                Av1ScreenContentDetector.DilateBlock(block, dilated);
                Assert.Equal(expected, dilated);
            }
        }
    }

    private static bool CountColorsReference(byte[] block, int threshold, out int colorCount)
    {
        bool[] seen = new bool[256];
        colorCount = 0;
        foreach (byte value in block)
        {
            if (!seen[value])
            {
                seen[value] = true;
                if (++colorCount > threshold)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static byte FindDominantReference(byte[] block)
    {
        int[] counts = new int[256];
        int dominantCount = 0;
        byte dominant = 0;
        foreach (byte value in block)
        {
            if (++counts[value] > dominantCount)
            {
                dominant = value;
                dominantCount = counts[value];
            }
        }

        return dominant;
    }

    private static void DilateReference(byte[] block, byte dominant, byte[] dilated)
    {
        block.CopyTo(dilated, 0);
        for (int row = 0; row < BlockLength; row++)
        {
            for (int column = 0; column < BlockLength; column++)
            {
                if (block[(row * BlockLength) + column] != dominant)
                {
                    continue;
                }

                for (int y = Math.Max(row - 1, 0); y <= Math.Min(row + 1, BlockLength - 1); y++)
                {
                    for (int x = Math.Max(column - 1, 0); x <= Math.Min(column + 1, BlockLength - 1); x++)
                    {
                        dilated[(y * BlockLength) + x] = dominant;
                    }
                }
            }
        }
    }
}

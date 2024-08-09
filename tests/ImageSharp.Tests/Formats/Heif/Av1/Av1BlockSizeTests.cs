// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1BlockSizeTests
{
    [Theory]
    [MemberData(nameof(GetAllSizes))]
    internal void GetWidthReturnsCorrectWidth(int s)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)s;
        int expectedWidth = blockSize switch
        {
            Av1BlockSize.Block4x4 or Av1BlockSize.Block4x8 or Av1BlockSize.Block4x16 => 4,
            Av1BlockSize.Block8x4 or Av1BlockSize.Block8x8 or Av1BlockSize.Block8x16 or Av1BlockSize.Block8x32 => 8,
            Av1BlockSize.Block16x4 or Av1BlockSize.Block16x8 or Av1BlockSize.Block16x16 or Av1BlockSize.Block16x32 or Av1BlockSize.Block16x64 => 16,
            Av1BlockSize.Block32x8 or Av1BlockSize.Block32x16 or Av1BlockSize.Block32x32 or Av1BlockSize.Block32x64 => 32,
            Av1BlockSize.Block64x16 or Av1BlockSize.Block64x32 or Av1BlockSize.Block64x64 or Av1BlockSize.Block64x128 => 64,
            Av1BlockSize.Block128x64 or Av1BlockSize.Block128x128 => 128,
            _ => -1
        };

        // Act
        int actualWidth = blockSize.GetWidth();

        // Assert
        Assert.Equal(expectedWidth, actualWidth);
    }

    [Theory]
    [MemberData(nameof(GetAllSizes))]
    internal void GetHeightReturnsCorrectHeight(int s)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)s;
        int expectedHeight = blockSize switch
        {
            Av1BlockSize.Block4x4 or Av1BlockSize.Block8x4 or Av1BlockSize.Block16x4 => 4,
            Av1BlockSize.Block4x8 or Av1BlockSize.Block8x8 or Av1BlockSize.Block16x8 or Av1BlockSize.Block32x8 => 8,
            Av1BlockSize.Block4x16 or Av1BlockSize.Block8x16 or Av1BlockSize.Block16x16 or Av1BlockSize.Block32x16 or Av1BlockSize.Block64x16 => 16,
            Av1BlockSize.Block8x32 or Av1BlockSize.Block16x32 or Av1BlockSize.Block32x32 or Av1BlockSize.Block64x32 => 32,
            Av1BlockSize.Block16x64 or Av1BlockSize.Block32x64 or Av1BlockSize.Block64x64 or Av1BlockSize.Block128x64 => 64,
            Av1BlockSize.Block64x128 or Av1BlockSize.Block128x128 => 128,
            _ => -1
        };

        // Act
        int actualHeight = blockSize.GetHeight();

        // Assert
        Assert.Equal(expectedHeight, actualHeight);
    }

    [Theory]
    [MemberData(nameof(GetAllSizes))]
    internal void GetSubSampledReturnsCorrectSize(int s)
    {
        if (s is 0 or 1 or 2 or 16 or 17)
        {
            // Exceptional values, skip for this generic test.
            return;
        }

        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)s;
        int originalWidth = blockSize.GetWidth();
        int originalHeight = blockSize.GetHeight();
        int halfWidth = originalWidth / 2;
        int halfHeight = originalHeight / 2;

        // Act
        Av1BlockSize actualNoNo = blockSize.GetSubsampled(false, false);
        Av1BlockSize actualYesNo = blockSize.GetSubsampled(true, false);
        Av1BlockSize actualNoYes = blockSize.GetSubsampled(false, true);
        Av1BlockSize actualYesYes = blockSize.GetSubsampled(true, true);

        // Assert
        Assert.Equal(originalWidth, actualNoNo.GetWidth());
        Assert.Equal(originalHeight, actualNoNo.GetHeight());

        if (actualYesNo != Av1BlockSize.Invalid)
        {
            Assert.Equal(halfWidth, actualYesNo.GetWidth());
            Assert.Equal(originalHeight, actualYesNo.GetHeight());
        }

        if (actualNoYes != Av1BlockSize.Invalid)
        {
            Assert.Equal(originalWidth, actualNoYes.GetWidth());
            Assert.Equal(halfHeight, actualNoYes.GetHeight());
        }

        Assert.Equal(halfWidth, actualYesYes.GetWidth());
        Assert.Equal(halfHeight, actualYesYes.GetHeight());
    }

    public static TheoryData<int> GetAllSizes()
    {
        TheoryData<int> combinations = [];
        for (int s = 0; s < (int)Av1BlockSize.AllSizes; s++)
        {
            combinations.Add(s);
        }

        return combinations;
    }

    private static int GetRatio(Av1BlockSize blockSize)
    {
        int width = blockSize.GetWidth();
        int height = blockSize.GetHeight();
        int ratio = width >= height ? width / height : -height / width;
        return ratio;
    }
}

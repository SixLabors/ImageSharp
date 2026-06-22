// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1TransformSizeTests
{
    [Theory]
    [MemberData(nameof(GetAllSizes))]
    internal void GetWidthReturnsCorrectWidth(int s)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)s;
        int expectedWidth = transformSize switch
        {
            Av1TransformSize.Size4x4 or Av1TransformSize.Size4x8 or Av1TransformSize.Size4x16 => 4,
            Av1TransformSize.Size8x4 or Av1TransformSize.Size8x8 or Av1TransformSize.Size8x16 or Av1TransformSize.Size8x32 => 8,
            Av1TransformSize.Size16x4 or Av1TransformSize.Size16x8 or Av1TransformSize.Size16x16 or Av1TransformSize.Size16x32 or Av1TransformSize.Size16x64 => 16,
            Av1TransformSize.Size32x8 or Av1TransformSize.Size32x16 or Av1TransformSize.Size32x32 or Av1TransformSize.Size32x64 => 32,
            Av1TransformSize.Size64x16 or Av1TransformSize.Size64x32 or Av1TransformSize.Size64x64 => 64,
            _ => -1
        };

        // Act
        int actualWidth = transformSize.GetWidth();

        // Assert
        Assert.Equal(expectedWidth, actualWidth);
    }

    [Theory]
    [MemberData(nameof(GetAllSizes))]
    internal void GetHeightReturnsCorrectHeight(int s)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)s;
        int expectedHeight = transformSize switch
        {
            Av1TransformSize.Size4x4 or Av1TransformSize.Size8x4 or Av1TransformSize.Size16x4 => 4,
            Av1TransformSize.Size4x8 or Av1TransformSize.Size8x8 or Av1TransformSize.Size16x8 or Av1TransformSize.Size32x8 => 8,
            Av1TransformSize.Size4x16 or Av1TransformSize.Size8x16 or Av1TransformSize.Size16x16 or Av1TransformSize.Size32x16 or Av1TransformSize.Size64x16 => 16,
            Av1TransformSize.Size8x32 or Av1TransformSize.Size16x32 or Av1TransformSize.Size32x32 or Av1TransformSize.Size64x32 => 32,
            Av1TransformSize.Size16x64 or Av1TransformSize.Size32x64 or Av1TransformSize.Size64x64 => 64,
            _ => -1
        };

        // Act
        int actualHeight = transformSize.GetHeight();

        // Assert
        Assert.Equal(expectedHeight, actualHeight);
    }

    [Theory]
    [MemberData(nameof(GetAllSizes))]
    internal void GetSubSizeReturnsCorrectRatio(int s)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)s;
        int ratio = GetRatio(transformSize);
        int expectedRatio = (ratio == 4) ? 2 : 1;

        // Act
        Av1TransformSize actual = transformSize.GetSubSize();
        int actualRatio = GetRatio(actual);

        // Assert
        Assert.Equal(expectedRatio, actualRatio);
    }

    [Theory]
    [MemberData(nameof(GetAllSizes))]
    internal void GetSquareSizeReturnsCorrectRatio(int s)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)s;
        int ratio = GetRatio(transformSize);
        int expectedRatio = 1;
        int expectedSize = Math.Min(transformSize.GetWidth(), transformSize.GetHeight());

        // Act
        Av1TransformSize actual = transformSize.GetSquareSize();
        int actualRatio = GetRatio(actual);

        // Assert
        Assert.Equal(expectedRatio, actualRatio);
        Assert.Equal(expectedSize, actual.GetWidth());
        Assert.Equal(expectedSize, actual.GetHeight());
    }

    [Theory]
    [MemberData(nameof(GetAllSizes))]
    internal void GetSquareUpSizeReturnsCorrectRatio(int s)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)s;
        int ratio = GetRatio(transformSize);
        int expectedRatio = 1;
        int expectedSize = Math.Max(transformSize.GetWidth(), transformSize.GetHeight());

        // Act
        Av1TransformSize actual = transformSize.GetSquareUpSize();
        int actualRatio = GetRatio(actual);

        // Assert
        Assert.Equal(expectedRatio, actualRatio);
        Assert.Equal(expectedSize, actual.GetWidth());
        Assert.Equal(expectedSize, actual.GetHeight());
    }

    [Theory]
    [MemberData(nameof(GetAllSizes))]
    internal void ToBlockSizeReturnsSameWidthAndHeight(int s)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)s;
        int transformWidth = transformSize.GetWidth();
        int transformHeight = transformSize.GetHeight();

        // Act
        Av1BlockSize blockSize = transformSize.ToBlockSize();
        int blockWidth = blockSize.GetWidth();
        int blockHeight = blockSize.GetHeight();

        // Assert
        Assert.Equal(transformWidth, blockWidth);
        Assert.Equal(transformHeight, blockHeight);
    }

    [Theory]
    [MemberData(nameof(GetAllSizes))]
    internal void LogMinus4ReturnsReferenceValues(int s)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)s;
        int expected = ReferenceLog2Minus4(transformSize);

        // Act
        int actual = transformSize.GetLog2Minus4();

        // Assert
        Assert.Equal(expected, actual);
    }

    public static TheoryData<int> GetAllSizes()
    {
        TheoryData<int> combinations = [];
        for (int s = 0; s < (int)Av1TransformSize.AllSizes; s++)
        {
            combinations.Add(s);
        }

        return combinations;
    }

    private static int GetRatio(Av1TransformSize transformSize)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int ratio = width > height ? width / height : height / width;
        return ratio;
    }

    private static int ReferenceLog2Minus4(Av1TransformSize transformSize)
    {
        int widthLog2 = Av1Math.Log2(transformSize.GetWidth());
        int heightLog2 = Av1Math.Log2(transformSize.GetHeight());
        return Math.Min(widthLog2, 5) + Math.Min(heightLog2, 5) - 4;
    }
}

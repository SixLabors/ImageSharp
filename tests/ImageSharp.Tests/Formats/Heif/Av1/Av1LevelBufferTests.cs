// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1LevelBufferTests
{
    [Theory]
    [InlineData(4, 4, 4, 1)]
    [InlineData(4, 4, 5, 1)]
    [InlineData(4, 4, 6, 1)]
    [InlineData(4, 4, 7, 1)]
    [InlineData(8, 4, 7, 0)]
    [InlineData(8, 8, 16, 2)]
    [InlineData(8, 4, 16, 2)]
    public void TestGetPaddedRow(int width, int height, int index, byte expected)
    {
        // Arrange
        Size size = new(width, height);
        Av1LevelBuffer levels = new(Configuration.Default, size);
        for (byte i = 0; i < 4; i++)
        {
            levels.GetRow(i).Fill(i);
        }

        // Act
        Point pos = levels.GetPosition(index);

        // Assert
        Assert.Equal(expected, pos.Y);
        Assert.Equal(expected, levels.GetRow(pos)[0]);
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(8, 4)]
    [InlineData(8, 8)]
    [InlineData(16, 4)]
    public void TestGetRow(int width, int height)
    {
        // Arrange
        Size size = new(width, height);
        Av1LevelBuffer levels = new(Configuration.Default, size);
        for (byte i = 0; i < height; i++)
        {
            levels.GetRow(i).Fill(i);
        }

        for (int j = 0; j < height; j++)
        {
            // Act
            Span<byte> actual = levels.GetRow(j);

            // Assert
            Assert.Equal(j, actual[0]);
            Assert.True(actual.Length >= width);
        }
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(8, 4)]
    [InlineData(8, 8)]
    [InlineData(16, 4)]
    public void TestClear(int width, int height)
    {
        // Arrange
        Size size = new(width, height);
        Av1LevelBuffer levels = new(Configuration.Default, size);
        for (byte i = 0; i < height; i++)
        {
            levels.GetRow(i).Fill(i);
        }

        // Act
        levels.Clear();

        // Assert
        for (int j = 0; j < height; j++)
        {
            Span<byte> rowSpan = levels.GetRow(j);
            for (int k = 0; k < width; k++)
            {
                Assert.Equal(0, rowSpan[k]);
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1TransformSizeTests
{
    /// <summary>
    /// Verifies the dimension, sub-size, square-size, block-size, and log2 lookup tables for every AV1 transform size.
    /// </summary>
    [Fact]
    public void TransformSizeTablesMatchDefinition()
    {
        for (int s = 0; s < (int)Av1TransformSize.AllSizes; s++)
        {
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

            int expectedHeight = transformSize switch
            {
                Av1TransformSize.Size4x4 or Av1TransformSize.Size8x4 or Av1TransformSize.Size16x4 => 4,
                Av1TransformSize.Size4x8 or Av1TransformSize.Size8x8 or Av1TransformSize.Size16x8 or Av1TransformSize.Size32x8 => 8,
                Av1TransformSize.Size4x16 or Av1TransformSize.Size8x16 or Av1TransformSize.Size16x16 or Av1TransformSize.Size32x16 or Av1TransformSize.Size64x16 => 16,
                Av1TransformSize.Size8x32 or Av1TransformSize.Size16x32 or Av1TransformSize.Size32x32 or Av1TransformSize.Size64x32 => 32,
                Av1TransformSize.Size16x64 or Av1TransformSize.Size32x64 or Av1TransformSize.Size64x64 => 64,
                _ => -1
            };

            Assert.Equal(expectedWidth, transformSize.GetWidth());
            Assert.Equal(expectedHeight, transformSize.GetHeight());

            // A 4:1 transform splits into a 2:1 sub-size; every other size splits into a square.
            Assert.Equal(GetRatio(transformSize) == 4 ? 2 : 1, GetRatio(transformSize.GetSubSize()));

            int minimumSize = Math.Min(expectedWidth, expectedHeight);
            Av1TransformSize square = transformSize.GetSquareSize();
            Assert.Equal(minimumSize, square.GetWidth());
            Assert.Equal(minimumSize, square.GetHeight());

            int maximumSize = Math.Max(expectedWidth, expectedHeight);
            Av1TransformSize squareUp = transformSize.GetSquareUpSize();
            Assert.Equal(maximumSize, squareUp.GetWidth());
            Assert.Equal(maximumSize, squareUp.GetHeight());

            Av1BlockSize blockSize = transformSize.ToBlockSize();
            Assert.Equal(expectedWidth, blockSize.GetWidth());
            Assert.Equal(expectedHeight, blockSize.GetHeight());

            int expectedLog2Minus4 = Math.Min(Av1Math.Log2(expectedWidth), 5) + Math.Min(Av1Math.Log2(expectedHeight), 5) - 4;
            Assert.Equal(expectedLog2Minus4, transformSize.GetLog2Minus4());
        }
    }

    private static int GetRatio(Av1TransformSize transformSize)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        return width > height ? width / height : height / width;
    }
}

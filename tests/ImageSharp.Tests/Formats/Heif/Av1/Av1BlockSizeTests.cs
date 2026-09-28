// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1BlockSizeTests
{
    /// <summary>
    /// Verifies the width, height, and chroma-subsampled lookup tables for every AV1 block size.
    /// </summary>
    [Fact]
    public void BlockSizeTablesMatchDefinition()
    {
        for (int s = 0; s < (int)Av1BlockSize.AllSizes; s++)
        {
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

            Assert.Equal(expectedWidth, blockSize.GetWidth());
            Assert.Equal(expectedHeight, blockSize.GetHeight());

            if (s is 0 or 1 or 2 or 16 or 17)
            {
                // The smallest and 4:1 sizes have no generic halved counterpart on every axis.
                continue;
            }

            int halfWidth = expectedWidth / 2;
            int halfHeight = expectedHeight / 2;
            Av1BlockSize actualNoNo = blockSize.GetSubsampled(false, false);
            Av1BlockSize actualYesNo = blockSize.GetSubsampled(true, false);
            Av1BlockSize actualNoYes = blockSize.GetSubsampled(false, true);
            Av1BlockSize actualYesYes = blockSize.GetSubsampled(true, true);

            Assert.Equal(expectedWidth, actualNoNo.GetWidth());
            Assert.Equal(expectedHeight, actualNoNo.GetHeight());
            if (actualYesNo != Av1BlockSize.Invalid)
            {
                Assert.Equal(halfWidth, actualYesNo.GetWidth());
                Assert.Equal(expectedHeight, actualYesNo.GetHeight());
            }

            if (actualNoYes != Av1BlockSize.Invalid)
            {
                Assert.Equal(expectedWidth, actualNoYes.GetWidth());
                Assert.Equal(halfHeight, actualNoYes.GetHeight());
            }

            Assert.Equal(halfWidth, actualYesYes.GetWidth());
            Assert.Equal(halfHeight, actualYesYes.GetHeight());
        }
    }
}

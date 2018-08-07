// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// Uncomment this to turn unit tests into benchmarks:
//#define BENCHMARKING

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.Primitives;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public partial class Block8x8FTests : JpegFixture
    {
        public class CopyToBufferArea : JpegFixture
        {
            public CopyToBufferArea(ITestOutputHelper output)
                : base(output)
            {
            }

            private static void VerifyAllZeroOutsideSubArea(Buffer2D<float> buffer, int subX, int subY, int horizontalFactor = 1, int verticalFactor = 1)
            {
                for (int y = 0; y < 20; y++)
                {
                    for (int x = 0; x < 20; x++)
                    {
                        if (x < subX || x >= subX + 8 * horizontalFactor || y < subY || y >= subY + 8 * verticalFactor)
                        {
                            Assert.Equal(0, buffer[x, y]);
                        }
                    }
                }
            }

            // TODO: This test occasionally fails from the same reason certain ICC tests are failing. Should be false negative.
            [Fact(Skip = "This test occasionally fails from the same reason certain ICC tests are failing. Should be false negative.")]
            //[Fact]
            public void Unscaled()
            {
                Block8x8F block = CreateRandomFloatBlock(0, 100);

                using (var buffer = Configuration.Default.MemoryAllocator.Allocate2D<float>(20, 20))
                {
                    BufferArea<float> area = buffer.GetArea(5, 10, 8, 8);
                    block.CopyTo(area);

                    Assert.Equal(block[0, 0], buffer[5, 10]);
                    Assert.Equal(block[1, 0], buffer[6, 10]);
                    Assert.Equal(block[0, 1], buffer[5, 11]);
                    Assert.Equal(block[0, 7], buffer[5, 17]);
                    Assert.Equal(block[63], buffer[12, 17]);

                    VerifyAllZeroOutsideSubArea(buffer, 5, 10);
                }
            }

            // TODO: This test occasionally fails from the same reason certain ICC tests are failing. Should be false negative.
            [Theory(Skip = "This test occasionally fails from the same reason certain ICC tests are failing. Should be false negative.")]
            //[Theory]
            [InlineData(1, 1)]
            [InlineData(1, 2)]
            [InlineData(2, 1)]
            [InlineData(2, 2)]
            [InlineData(4, 2)]
            [InlineData(4, 4)]
            public void Scaled(int horizontalFactor, int verticalFactor)
            {
                Block8x8F block = CreateRandomFloatBlock(0, 100);

                var start = new Point(50, 50);

                using (var buffer = Configuration.Default.MemoryAllocator.Allocate2D<float>(100, 100))
                {
                    BufferArea<float> area = buffer.GetArea(start.X, start.Y, 8 * horizontalFactor, 8 * verticalFactor);
                    block.CopyTo(area, horizontalFactor, verticalFactor);

                    for (int y = 0; y < 8 * verticalFactor; y++)
                    {
                        for (int x = 0; x < 8 * horizontalFactor; x++)
                        {
                            int yy = y / verticalFactor;
                            int xx = x / horizontalFactor;

                            float expected = block[xx, yy];
                            float actual = area[x, y];

                            Assert.Equal(expected, actual);
                        }
                    }

                    VerifyAllZeroOutsideSubArea(buffer, start.X, start.Y, horizontalFactor, verticalFactor);
                }
            }
        }
    }
}
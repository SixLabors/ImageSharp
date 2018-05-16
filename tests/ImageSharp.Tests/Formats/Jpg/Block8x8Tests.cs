// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class Block8x8Tests : JpegFixture
    {
        public Block8x8Tests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void Construct_And_Indexer_Get()
        {
            short[] data = Create8x8ShortData();

            var block = new Block8x8(data);

            for (int i = 0; i < Block8x8.Size; i++)
            {
                Assert.Equal(data[i], block[i]);
            }
        }

        [Fact]
        public void Indexer_Set()
        {
            Block8x8 block = default;

            block[17] = 17;
            block[42] = 42;

            Assert.Equal(0, block[0]);
            Assert.Equal(17, block[17]);
            Assert.Equal(42, block[42]);
        }

        [Fact]
        public unsafe void Indexer_GetScalarAt_SetScalarAt()
        {
            int sum = 0;
            var block = default(Block8x8);

            for (int i = 0; i < Block8x8.Size; i++)
            {
                Block8x8.SetScalarAt(&block, i, (short)i);
            }

            sum = 0;
            for (int i = 0; i < Block8x8.Size; i++)
            {
                sum += Block8x8.GetScalarAt(&block, i);
            }
            Assert.Equal(sum, 64 * 63 / 2);
        }


        [Fact]
        public void AsFloatBlock()
        {
            short[] data = Create8x8ShortData();

            var source = new Block8x8(data);

            Block8x8F dest = source.AsFloatBlock();

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                Assert.Equal((float)data[i], dest[i]);
            }
        }

        [Fact]
        public void ToArray()
        {
            short[] data = Create8x8ShortData();
            var block = new Block8x8(data);

            short[] result = block.ToArray();

            Assert.Equal(data, result);
        }

        [Fact]
        public void Equality_WhenTrue()
        {
            short[] data = Create8x8ShortData();
            var block1 = new Block8x8(data);
            var block2 = new Block8x8(data);

            block1[0] = 42;
            block2[0] = 42;

            Assert.Equal(block1, block2);
            Assert.Equal(block1.GetHashCode(), block2.GetHashCode());
        }

        [Fact]
        public void Equality_WhenFalse()
        {
            short[] data = Create8x8ShortData();
            var block1 = new Block8x8(data);
            var block2 = new Block8x8(data);

            block1[0] = 42;
            block2[0] = 666;

            Assert.NotEqual(block1, block2);
        }

        [Fact]
        public void IndexerXY()
        {
            Block8x8 block = default;
            block[8 * 3 + 5] = 42;

            short value = block[5, 3];

            Assert.Equal(42, value);
        }

        [Fact]
        public void TotalDifference()
        {
            short[] data = Create8x8ShortData();
            var block1 = new Block8x8(data);
            var block2 = new Block8x8(data);

            block2[10] += 7;
            block2[63] += 8;

            long d = Block8x8.TotalDifference(ref block1, ref block2);

            Assert.Equal(15, d);
        }
    }
}
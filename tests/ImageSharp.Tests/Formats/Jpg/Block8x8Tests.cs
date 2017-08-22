// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    using Moq;

    using SixLabors.ImageSharp.Formats.Jpeg.Common;

    using Xunit;
    using Xunit.Abstractions;

    public class Block8x8Tests : JpegUtilityTestFixture
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
            var block = default(Block8x8);

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
    }
}
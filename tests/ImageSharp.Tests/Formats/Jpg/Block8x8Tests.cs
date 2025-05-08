// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
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

        var block = Block8x8.Load(data);

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
    public void AsFloatBlock()
    {
        short[] data = Create8x8ShortData();

        var source = Block8x8.Load(data);

        Block8x8F dest = source.AsFloatBlock();

        for (int i = 0; i < Block8x8F.Size; i++)
        {
            Assert.Equal(data[i], dest[i]);
        }
    }

    [Fact]
    public void ToArray()
    {
        short[] data = Create8x8ShortData();
        var block = Block8x8.Load(data);

        short[] result = block.ToArray();

        Assert.Equal(data, result);
    }

    [Fact]
    public void Equality_WhenFalse()
    {
        short[] data = Create8x8ShortData();
        var block1 = Block8x8.Load(data);
        var block2 = Block8x8.Load(data);

        block1[0] = 42;
        block2[0] = 666;

        Assert.NotEqual(block1, block2);
    }

    [Fact]
    public void IndexerXY()
    {
        Block8x8 block = default;
        block[(8 * 3) + 5] = 42;

        short value = block[5, 3];

        Assert.Equal(42, value);
    }

    [Fact]
    public void TotalDifference()
    {
        short[] data = Create8x8ShortData();
        var block1 = Block8x8.Load(data);
        var block2 = Block8x8.Load(data);

        block2[10] += 7;
        block2[63] += 8;

        long d = Block8x8.TotalDifference(ref block1, ref block2);

        Assert.Equal(15, d);
    }

    [Fact]
    public void GetLastNonZeroIndex_AllZero()
    {
        static void RunTest()
        {
            Block8x8 data = default;

            nint expected = -1;

            nint actual = data.GetLastNonZeroIndex();

            Assert.Equal(expected, actual);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
    }

    [Fact]
    public void GetLastNonZeroIndex_AllNonZero()
    {
        static void RunTest()
        {
            Block8x8 data = default;
            for (int i = 0; i < Block8x8.Size; i++)
            {
                data[i] = 10;
            }

            nint expected = Block8x8.Size - 1;

            nint actual = data.GetLastNonZeroIndex();

            Assert.Equal(expected, actual);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GetLastNonZeroIndex_RandomFilledSingle(int seed)
    {
        static void RunTest(string seedSerialized)
        {
            int seed = FeatureTestRunner.Deserialize<int>(seedSerialized);
            var rng = new Random(seed);

            for (int i = 0; i < 1000; i++)
            {
                Block8x8 data = default;

                int setIndex = rng.Next(1, Block8x8.Size);
                data[setIndex] = (short)rng.Next(-2000, 2000);

                nint expected = setIndex;

                nint actual = data.GetLastNonZeroIndex();

                Assert.Equal(expected, actual);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            seed,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GetLastNonZeroIndex_RandomFilledPartially(int seed)
    {
        static void RunTest(string seedSerialized)
        {
            int seed = FeatureTestRunner.Deserialize<int>(seedSerialized);
            var rng = new Random(seed);

            for (int i = 0; i < 1000; i++)
            {
                Block8x8 data = default;

                int lastIndex = rng.Next(1, Block8x8.Size);
                short fillValue = (short)rng.Next(-2000, 2000);
                for (int dataIndex = 0; dataIndex <= lastIndex; dataIndex++)
                {
                    data[dataIndex] = fillValue;
                }

                int expected = lastIndex;

                nint actual = data.GetLastNonZeroIndex();

                Assert.Equal(expected, actual);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            seed,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GetLastNonZeroIndex_RandomFilledFragmented(int seed)
    {
        static void RunTest(string seedSerialized)
        {
            int seed = FeatureTestRunner.Deserialize<int>(seedSerialized);
            var rng = new Random(seed);

            for (int i = 0; i < 1000; i++)
            {
                Block8x8 data = default;

                short fillValue = (short)rng.Next(-2000, 2000);

                // first filled chunk
                int firstChunkStart = rng.Next(0, Block8x8.Size / 2);
                int firstChunkEnd = rng.Next(firstChunkStart, Block8x8.Size / 2);
                for (int dataIdx = firstChunkStart; dataIdx <= firstChunkEnd; dataIdx++)
                {
                    data[dataIdx] = fillValue;
                }

                // second filled chunk, there might be a spot with zero(s) between first and second chunk
                int secondChunkStart = rng.Next(firstChunkEnd, Block8x8.Size);
                int secondChunkEnd = rng.Next(secondChunkStart, Block8x8.Size);
                for (int dataIdx = secondChunkStart; dataIdx <= secondChunkEnd; dataIdx++)
                {
                    data[dataIdx] = fillValue;
                }

                int expected = secondChunkEnd;

                nint actual = data.GetLastNonZeroIndex();

                Assert.True(expected == actual, $"Expected: {expected}\nActual: {actual}\nInput matrix: {data}");
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            seed,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
    }

    [Fact]
    public void TransposeInplace()
    {
        static void RunTest()
        {
            short[] expected = Create8x8ShortData();
            ReferenceImplementations.Transpose8x8(expected);

            Block8x8 block8x8 = Block8x8.Load(Create8x8ShortData());

            block8x8.TransposeInPlace();

            short[] actual = new short[64];
            block8x8.CopyTo(actual);

            Assert.Equal(expected, actual);
        }

        // This method has only 1 implementation:
        // 1. Scalar
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.DisableHWIntrinsic);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities;

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

        Block8x8 block = Block8x8.Load(data);

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

        Block8x8 source = Block8x8.Load(data);

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
        Block8x8 block = Block8x8.Load(data);

        short[] result = block.ToArray();

        Assert.Equal(data, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(63)]
    public void Equality_WhenFalse(int index)
    {
        // Two blocks that differ in one value are not equal, at any position of the 64 values.
        short[] data = Create8x8ShortData();
        Block8x8 block1 = Block8x8.Load(data);
        Block8x8 block2 = Block8x8.Load(data);

        block1[index] = 42;
        block2[index] = 666;

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
        Block8x8 block1 = Block8x8.Load(data);
        Block8x8 block2 = Block8x8.Load(data);

        block2[10] += 7;
        block2[63] += 8;

        long d = Block8x8.TotalDifference(ref block1, ref block2);

        Assert.Equal(15, d);
    }

    [Fact]
    public void IsDcOnly_IsFalseForAnyNonZeroAcCoefficient()
    {
        static void RunTest()
        {
            // Zero and DC-only blocks have no AC coefficients.
            Block8x8 data = default;
            Assert.True(data.IsDcOnly());

            data[0] = -1;
            Assert.True(data.IsDcOnly());

            // If the method gives a false positive, the decoder deletes the AC coefficients of the block.
            for (int i = 1; i < Block8x8.Size; i++)
            {
                Block8x8 block = default;
                block[0] = 1;
                block[i] = short.MinValue;
                Assert.False(block.IsDcOnly(), $"Index {i}, Vector256 {Vector256.IsHardwareAccelerated}, Vector128 {Vector128.IsHardwareAccelerated}");
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    [Fact]
    public void GetNonZeroMask_SetsTheBitOfEachNonZeroElement()
    {
        static void RunTest()
        {
            Assert.Equal(0UL, default(Block8x8).GetNonZeroMask());

            // The Huffman encoder emits a coefficient only when its bit is set. Each element must map to its own bit.
            for (int i = 0; i < Block8x8.Size; i++)
            {
                Block8x8 block = default;
                block[i] = short.MinValue;
                Assert.Equal(1UL << i, block.GetNonZeroMask());

                block[i] = 1;
                Assert.Equal(1UL << i, block.GetNonZeroMask());
            }

            short[] data = Create8x8ShortData();
            Block8x8 mixed = Block8x8.Load(data);
            ulong expected = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    expected |= 1UL << i;
                }
            }

            Assert.Equal(expected, mixed.GetNonZeroMask());
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }
}

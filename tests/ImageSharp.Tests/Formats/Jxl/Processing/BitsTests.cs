// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class BitsTests
{
    [Fact]
    public void TestNumZeroBits()
    {
        Assert.Equal(32u, JxlMath.Num0BitsAboveMS1Bit(0u));
        Assert.Equal(64u, JxlMath.Num0BitsAboveMS1Bit(0uL));
        Assert.Equal(32u, JxlMath.Num0BitsBelowLS1Bit(0u));
        Assert.Equal(64u, JxlMath.Num0BitsBelowLS1Bit(0uL));

        Assert.Equal(31u, JxlMath.Num0BitsAboveMS1Bit(1u));
        Assert.Equal(30u, JxlMath.Num0BitsAboveMS1Bit(2u));
        Assert.Equal(63u, JxlMath.Num0BitsAboveMS1Bit(1uL));
        Assert.Equal(62u, JxlMath.Num0BitsAboveMS1Bit(2uL));

        Assert.Equal(0u, JxlMath.Num0BitsBelowLS1Bit(1u));
        Assert.Equal(0u, JxlMath.Num0BitsBelowLS1Bit(1uL));
        Assert.Equal(1u, JxlMath.Num0BitsBelowLS1Bit(2u));
        Assert.Equal(1u, JxlMath.Num0BitsBelowLS1Bit(2uL));

        Assert.Equal(0u, JxlMath.Num0BitsAboveMS1Bit(0x80000000u));
        Assert.Equal(0u, JxlMath.Num0BitsAboveMS1Bit(0x8000000000000000uL));
        Assert.Equal(31u, JxlMath.Num0BitsBelowLS1Bit(0x80000000u));
        Assert.Equal(63u, JxlMath.Num0BitsBelowLS1Bit(0x8000000000000000uL));
    }

    [Fact]
    public void TestFloorLog2()
    {
        Span<int> expected = [0, 1, 1, 2, 2, 2, 2];

        for (int i = 1; i <= 7; ++i)
        {
            Assert.Equal(expected[i - 1], JxlMath.FloorLog2Nonzero(i));
            Assert.Equal((ulong)expected[i - 1], JxlMath.FloorLog2Nonzero((ulong)i));
        }

        Assert.Equal(11u, JxlMath.FloorLog2Nonzero(0x00000fffu));  // 4095
        Assert.Equal(12u, JxlMath.FloorLog2Nonzero(0x00001000u));  // 4096
        Assert.Equal(12u, JxlMath.FloorLog2Nonzero(0x00001001u));  // 4097

        Assert.Equal(31u, JxlMath.FloorLog2Nonzero(0x80000000u));
        Assert.Equal(31u, JxlMath.FloorLog2Nonzero(0x80000001u));
        Assert.Equal(31u, JxlMath.FloorLog2Nonzero(0xFFFFFFFFu));

        Assert.Equal(31u, JxlMath.FloorLog2Nonzero(0x80000000uL));
        Assert.Equal(31u, JxlMath.FloorLog2Nonzero(0x80000001uL));
        Assert.Equal(31u, JxlMath.FloorLog2Nonzero(0xFFFFFFFFuL));

        Assert.Equal(63u, JxlMath.FloorLog2Nonzero(0x8000000000000000uL));
        Assert.Equal(63u, JxlMath.FloorLog2Nonzero(0x8000000000000001uL));
        Assert.Equal(63u, JxlMath.FloorLog2Nonzero(0xFFFFFFFFFFFFFFFFuL));
    }

    [Fact]
    public void TestCeilLog2()
    {
        Span<int> expected = [0, 1, 2, 2, 3, 3, 3];

        for (int i = 1; i <= 7; ++i)
        {
            Assert.Equal(expected[i - 1], JxlMath.CeilLog2Nonzero(i));
            Assert.Equal((ulong)expected[i - 1], JxlMath.CeilLog2Nonzero((ulong)i));
        }

        Assert.Equal(12u, JxlMath.CeilLog2Nonzero(0x00000fffu));  // 4095
        Assert.Equal(12u, JxlMath.CeilLog2Nonzero(0x00001000u));  // 4096
        Assert.Equal(13u, JxlMath.CeilLog2Nonzero(0x00001001u));  // 4097

        Assert.Equal(31u, JxlMath.CeilLog2Nonzero(0x80000000u));
        Assert.Equal(32u, JxlMath.CeilLog2Nonzero(0x80000001u));
        Assert.Equal(32u, JxlMath.CeilLog2Nonzero(0xFFFFFFFFu));

        Assert.Equal(31u, JxlMath.CeilLog2Nonzero(0x80000000uL));
        Assert.Equal(32u, JxlMath.CeilLog2Nonzero(0x80000001uL));
        Assert.Equal(32u, JxlMath.CeilLog2Nonzero(0xFFFFFFFFuL));

        Assert.Equal(63u, JxlMath.CeilLog2Nonzero(0x8000000000000000uL));
        Assert.Equal(64u, JxlMath.CeilLog2Nonzero(0x8000000000000001uL));
        Assert.Equal(64u, JxlMath.CeilLog2Nonzero(0xFFFFFFFFFFFFFFFFuL));
    }
}

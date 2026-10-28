// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class BitsTests
{
    /// <summary>
    /// Checks zero-width, partial-width, and full-width unsigned fields.
    /// </summary>
    /// <param name="bits">The field width.</param>
    /// <param name="expected">The expected low-bit value.</param>
    [Theory]
    [InlineData(0UL, 0UL)]
    [InlineData(1UL, 1UL)]
    [InlineData(31UL, 0x7FFFFFFFUL)]
    [InlineData(32UL, 0xFFFFFFFFUL)]
    [InlineData(63UL, 0x7FFFFFFFFFFFFFFFUL)]
    [InlineData(64UL, 0xFFFFFFFFFFFFFFFFUL)]
    public void ReadBits64_PreservesFieldWidth(ulong bits, ulong expected)
    {
        using MemoryStream input = new([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);
        JxlBitReader reader = new(input);

        Assert.Equal(0, reader.TotalBitsConsumed);
        Assert.Equal(expected, reader.PeekBits64(bits));
        Assert.Equal(0, reader.TotalBitsConsumed);
        Assert.Equal(expected, reader.ReadBits64(bits));
        Assert.Equal((long)bits, reader.TotalBitsConsumed);
    }

    /// <summary>
    /// Checks that cross-block peeks preserve both bits and logical position on short-read streams.
    /// </summary>
    /// <param name="shortReads">Whether each stream read returns at most one byte.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PeekBits64_PreservesCrossBlockInput(bool shortReads)
    {
        byte[] bytes = [0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01, 0xF1, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE];
        using MemoryStream input = shortReads ? new SingleByteReadStream(bytes) : new MemoryStream(bytes);
        JxlBitReader reader = new(input);

        Assert.Equal(0UL, reader.ReadBits64(0));
        Assert.Equal(0, input.Position);
        Assert.Equal(0xFU, reader.ReadBits32(4));

        // The next 64 bits contain 60 bits from the first block and the low nibble of 0xF1.
        Assert.Equal(0x10123456789ABCDEUL, reader.PeekBits64(64));
        Assert.Equal(0x10123456789ABCDEUL, reader.PeekBits64(64));
        Assert.Equal(4, reader.TotalBitsConsumed);
        Assert.Equal(0x10123456789ABCDEUL, reader.ReadBits64(64));
        Assert.Equal(0xFU, reader.ReadBits32(4));
        Assert.Equal(0xFEDCBA98765432UL, reader.ReadBits64(56));
        Assert.Equal(128, reader.TotalBitsConsumed);
        Assert.Throws<EndOfStreamException>(() => reader.ReadBoolean());
        Assert.Equal(128, reader.TotalBitsConsumed);
    }

    /// <summary>
    /// Checks that an incomplete field does not consume or wrap the available bit count.
    /// </summary>
    [Fact]
    public void ReadBits_TruncatedFieldRetainsAvailableBits()
    {
        using MemoryStream input = new([0x12, 0x34]);
        JxlBitReader reader = new(input);

        Assert.Throws<EndOfStreamException>(() => reader.ReadBits32(17));
        Assert.Equal(0, reader.TotalBitsConsumed);
        Assert.Equal(0x3412U, reader.ReadBits32(16));
        Assert.Equal(16, reader.TotalBitsConsumed);
    }

    /// <summary>
    /// Checks skipping beyond one reservoir block and validating byte-alignment padding.
    /// </summary>
    [Fact]
    public void SkipBits64_PreservesFollowingBytes()
    {
        using MemoryStream input = new([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]);
        JxlBitReader reader = new(input);

        reader.SkipBits64(72);
        Assert.Equal(72, reader.TotalBitsConsumed);
        Assert.Equal(9U, reader.ReadBits32(8));
        Assert.Equal(80, reader.TotalBitsConsumed);
    }

    /// <summary>
    /// Checks the format rule that byte-alignment padding contains only zero bits.
    /// </summary>
    [Fact]
    public void JumpToByteBoundary_RequiresZeroPadding()
    {
        using MemoryStream validInput = new([0x01]);
        JxlBitReader valid = new(validInput);
        Assert.True(valid.ReadBoolean());
        valid.JumpToByteBoundary();
        Assert.Equal(8, valid.TotalBitsConsumed);

        using MemoryStream invalidInput = new([0x03]);
        JxlBitReader invalid = new(invalidInput);
        Assert.True(invalid.ReadBoolean());
        Assert.Throws<InvalidImageContentException>(() => invalid.JumpToByteBoundary());
    }

    /// <summary>
    /// Returns at most one byte per read to exercise the Stream short-read contract.
    /// </summary>
    /// <param name="bytes">The fixed input bytes.</param>
    private sealed class SingleByteReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        /// <summary>
        /// Reads at most one byte into the supplied buffer.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <returns>The number of bytes read.</returns>
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);
    }

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

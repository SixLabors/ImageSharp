// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.Common;

public class BufferedReadStreamExtensionsTests
{
    [Theory]
    [InlineData(0L, 8UL, true)]
    [InlineData(8L, 0UL, true)]
    [InlineData(7L, 2UL, false)]
    [InlineData(9L, 0UL, false)]
    [InlineData(-1L, 1UL, false)]
    [InlineData(long.MaxValue, ulong.MaxValue, false)]
    [InlineData(0L, ulong.MaxValue, false)]
    public void IsReadRangeValid_ChecksCompleteExtent(long offset, ulong length, bool expected)
    {
        using MemoryStream input = new(new byte[8]);
        using BufferedReadStream stream = new(Configuration.Default, input);

        Assert.Equal(expected, stream.IsReadRangeValid(offset, length));
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData(0UL, true, 0)]
    [InlineData(6UL, true, 6)]
    [InlineData(7UL, false, 0)]
    [InlineData(1073741824UL, false, 0)]
    [InlineData(4294967294UL, false, 0)]
    [InlineData(4294967296UL, false, 0)]
    [InlineData(ulong.MaxValue, false, 0)]
    public void TryGetReadLength_ReturnsResultWithoutMovingStream(ulong length, bool expected, int expectedLength)
    {
        using MemoryStream input = new(new byte[8]);
        using BufferedReadStream stream = new(Configuration.Default, input);
        stream.Position = 2;

        Assert.Equal(expected, stream.TryGetReadLength(length, out int bufferLength));
        Assert.Equal(expectedLength, bufferLength);
        Assert.Equal(2, stream.Position);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Skip_CountZeroOrLower_PositionNotChanged(int count)
    {
        using MemoryStream input = new(new byte[8]);
        using BufferedReadStream stream = new(Configuration.Default, input);
        stream.Position = 4;

        stream.Skip(count);

        Assert.Equal(4, stream.Position);
        Assert.Equal(0, stream.ReadByte());
    }
}

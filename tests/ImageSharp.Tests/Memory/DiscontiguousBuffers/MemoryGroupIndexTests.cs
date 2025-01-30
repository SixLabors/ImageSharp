// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers;

public class MemoryGroupIndexTests
{
    [Fact]
    public void Equal()
    {
        MemoryGroupIndex a = new(10, 1, 3);
        MemoryGroupIndex b = new(10, 1, 3);

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.False(a < b);
        Assert.False(a > b);
    }

    [Fact]
    public void SmallerBufferIndex()
    {
        MemoryGroupIndex a = new(10, 3, 3);
        MemoryGroupIndex b = new(10, 5, 3);

        Assert.False(a == b);
        Assert.True(a != b);
        Assert.True(a < b);
        Assert.False(a > b);
    }

    [Fact]
    public void SmallerElementIndex()
    {
        MemoryGroupIndex a = new(10, 3, 3);
        MemoryGroupIndex b = new(10, 3, 9);

        Assert.False(a == b);
        Assert.True(a != b);
        Assert.True(a < b);
        Assert.False(a > b);
    }

    [Fact]
    public void Increment()
    {
        MemoryGroupIndex a = new(10, 3, 3);
        a += 1;
        Assert.Equal(new(10, 3, 4), a);
    }

    [Fact]
    public void Increment_OverflowBuffer()
    {
        MemoryGroupIndex a = new(10, 5, 3);
        MemoryGroupIndex b = new(10, 5, 9);
        a += 8;
        b += 1;

        Assert.Equal(new(10, 6, 1), a);
        Assert.Equal(new(10, 6, 0), b);
    }
}

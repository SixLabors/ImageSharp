// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
using SixLabors.ImageSharp.Memory;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory;

public class AutoExpandingMemoryTests
{
    private readonly Configuration configurtion = Configuration.Default;

    [Theory]
    [InlineData(1000, 2000)]
    [InlineData(1000, 1000)]
    [InlineData(200, 1000)]
    [InlineData(200, 200)]
    [InlineData(200, 100)]
    public void ExpandToRequestedCapacity(int initialCapacity, int requestedCapacity)
    {
        AutoExpandingMemory<byte> memory = new(this.configurtion, initialCapacity);
        Span<byte> span = memory.GetSpan(requestedCapacity);
        Assert.Equal(requestedCapacity, span.Length);
    }

    [Theory]
    [InlineData(1000, 2000)]
    [InlineData(1000, 1000)]
    [InlineData(200, 1000)]
    [InlineData(200, 200)]
    [InlineData(200, 100)]
    public void KeepDataWhileExpanding(int initialCapacity, int requestedCapacity)
    {
        AutoExpandingMemory<byte> memory = new(this.configurtion, initialCapacity);
        Span<byte> firstSpan = memory.GetSpan(initialCapacity);
        firstSpan[1] = 1;
        firstSpan[2] = 2;
        firstSpan[3] = 3;
        Span<byte> expandedSpan = memory.GetSpan(requestedCapacity);
        Assert.Equal(3, firstSpan[3]);
        Assert.Equal(firstSpan[3], expandedSpan[3]);
    }

    [Theory]
    [InlineData(1, -1)]
    [InlineData(-2, 1)]
    [InlineData(-2, 0)]
    public void Guards(int initialCapacity, int requestedCapacity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            AutoExpandingMemory<byte> memory = new(this.configurtion, initialCapacity);
            _ = memory.GetSpan(requestedCapacity);
        });
}

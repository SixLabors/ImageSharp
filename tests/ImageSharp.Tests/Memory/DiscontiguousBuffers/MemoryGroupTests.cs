// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Collections;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers;

public partial class MemoryGroupTests : MemoryGroupTestsBase
{
    [Fact]
    public void IsValid_TrueAfterCreation()
    {
        using MemoryGroup<byte> g = MemoryGroup<byte>.Allocate(this.MemoryAllocator, 10, 100);

        Assert.True(g.IsValid);
    }

    [Fact]
    public void IsValid_FalseAfterDisposal()
    {
        using MemoryGroup<byte> g = MemoryGroup<byte>.Allocate(this.MemoryAllocator, 10, 100);

        g.Dispose();
        Assert.False(g.IsValid);
    }

    [Fact]
    public void Wrap()
    {
        int[] data0 = { 1, 2, 3, 4 };
        int[] data1 = { 5, 6, 7, 8 };
        int[] data2 = { 9, 10 };
        using TestMemoryManager<int> mgr0 = new(data0);
        using TestMemoryManager<int> mgr1 = new(data1);

        using MemoryGroup<int> group = MemoryGroup<int>.Wrap(mgr0.Memory, mgr1.Memory, data2);

        Assert.Equal(3, group.Count);
        Assert.Equal(4, group.BufferLength);
        Assert.Equal(10, group.TotalLength);

        Assert.True(group[0].Span.SequenceEqual(data0));
        Assert.True(group[1].Span.SequenceEqual(data1));
        Assert.True(group[2].Span.SequenceEqual(data2));

        int cnt = 0;
        int[][] allData = { data0, data1, data2 };
        foreach (Memory<int> memory in group)
        {
            Assert.True(memory.Span.SequenceEqual(allData[cnt]));
            cnt++;
        }
    }

    public static TheoryData<long, int, long, int> GetBoundedSlice_SuccessData = new()
    {
        { 300, 100, 110, 80 },
        { 300, 100, 100, 100 },
        { 280, 100, 201, 79 },
        { 42, 7, 0, 0 },
        { 42, 7, 0, 1 },
        { 42, 7, 0, 7 },
        { 42, 9, 9, 9 },
    };

    [Theory]
    [MemberData(nameof(GetBoundedSlice_SuccessData))]
    public void GetBoundedSlice_WhenArgsAreCorrect(long totalLength, int bufferLength, long start, int length)
    {
        using MemoryGroup<int> group = this.CreateTestGroup(totalLength, bufferLength, true);

        Memory<int> slice = group.GetBoundedMemorySlice(start, length);

        Assert.Equal(length, slice.Length);

        int expected = (int)start + 1;
        foreach (int val in slice.Span)
        {
            Assert.Equal(expected, val);
            expected++;
        }
    }

    public static TheoryData<long, int, long, int> GetBoundedSlice_ErrorData = new()
    {
        { 300, 100, -1, 91 },
        { 300, 100, 110, 91 },
        { 42, 7, 0, 8 },
        { 42, 7, 1, 7 },
        { 42, 7, 1, 30 },
    };

    [Theory]
    [MemberData(nameof(GetBoundedSlice_ErrorData))]
    public void GetBoundedSlice_WhenOverlapsBuffers_Throws(long totalLength, int bufferLength, long start, int length)
    {
        using MemoryGroup<int> group = this.CreateTestGroup(totalLength, bufferLength, true);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => group.GetBoundedMemorySlice(start, length));
    }

    [Fact]
    public void FillWithFastEnumerator()
    {
        using MemoryGroup<int> group = this.CreateTestGroup(100, 10, true);
        group.Fill(42);

        int[] expectedRow = Enumerable.Repeat(42, 10).ToArray();
        foreach (Memory<int> memory in group)
        {
            Assert.True(memory.Span.SequenceEqual(expectedRow));
        }
    }

    [Fact]
    public void FillWithSlowGenericEnumerator()
    {
        using MemoryGroup<int> group = this.CreateTestGroup(100, 10, true);
        group.Fill(42);

        int[] expectedRow = Enumerable.Repeat(42, 10).ToArray();
        IReadOnlyList<Memory<int>> groupAsList = group;
        foreach (Memory<int> memory in groupAsList)
        {
            Assert.True(memory.Span.SequenceEqual(expectedRow));
        }
    }

    [Fact]
    public void FillWithSlowEnumerator()
    {
        using MemoryGroup<int> group = this.CreateTestGroup(100, 10, true);
        group.Fill(42);

        int[] expectedRow = Enumerable.Repeat(42, 10).ToArray();
        IEnumerable groupAsList = group;
        foreach (Memory<int> memory in groupAsList)
        {
            Assert.True(memory.Span.SequenceEqual(expectedRow));
        }
    }

    [Fact]
    public void Clear()
    {
        using MemoryGroup<int> group = this.CreateTestGroup(100, 10, true);
        group.Clear();

        int[] expectedRow = new int[10];
        foreach (Memory<int> memory in group)
        {
            Assert.True(memory.Span.SequenceEqual(expectedRow));
        }
    }

}

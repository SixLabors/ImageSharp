// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory;

public partial class Buffer2DTests
{
    [Fact]
    public void WrapMemory_Packed_DangerousTryGetSinglePixelMemory_ReturnsTrue()
    {
        int[] data = [1, 2, 3, 4, 5, 6];

        using Buffer2D<int> buffer = Buffer2D<int>.WrapMemory(data.AsMemory(), width: 3, height: 2, stride: 3);

        Assert.True(buffer.DangerousTryGetSingleMemory(out Memory<int> memory));
        Assert.Equal(3, buffer.RowStride);
        Assert.Equal(6, memory.Length);
        Assert.True(Unsafe.AreSame(ref data[0], ref memory.Span[0]));
        Assert.SpanPointsTo(buffer.DangerousGetRowSpan(1), data.AsMemory(), bufferOffset: 3);
    }

    [Fact]
    public void WrapMemory_Strided_DangerousTryGetSinglePixelMemory_ReturnsFalse()
    {
        int[] data = [1, 2, 3, 777, 4, 5, 6, 888];

        using Buffer2D<int> buffer = Buffer2D<int>.WrapMemory(data.AsMemory(), width: 3, height: 2, stride: 4);

        Assert.False(buffer.DangerousTryGetSingleMemory(out Memory<int> _));
        Assert.Equal(4, buffer.RowStride);
        Assert.Equal(4, new Buffer2DRegion<int>(buffer).Stride);

        Span<int> row0 = buffer.DangerousGetRowSpan(0);
        Span<int> row1 = buffer.DangerousGetRowSpan(1);

        Assert.Equal(3, row0.Length);
        Assert.Equal(3, row1.Length);
        Assert.Equal(1, row0[0]);
        Assert.Equal(3, row0[2]);
        Assert.Equal(4, row1[0]);
        Assert.Equal(6, row1[2]);

        Assert.SpanPointsTo(row0, data.AsMemory(), bufferOffset: 0);
        Assert.SpanPointsTo(row1, data.AsMemory(), bufferOffset: 4);
    }

    [Fact]
    public void WrapMemory_Packed_WithTrailingData_DangerousTryGetSinglePixelMemory_IsLogicalSize()
    {
        int[] data = [1, 2, 3, 4, 5, 6, 777, 888];

        using Buffer2D<int> buffer = Buffer2D<int>.WrapMemory(data.AsMemory(), width: 3, height: 2, stride: 3);

        Assert.True(buffer.DangerousTryGetSingleMemory(out Memory<int> memory));
        Assert.Equal(6, memory.Length);
        Assert.True(Unsafe.AreSame(ref data[0], ref memory.Span[0]));
    }

    [Fact]
    public void WrapMemory_Strided_ThrowsWhenStrideIsLessThanWidth()
    {
        int[] data = new int[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Buffer2D<int>.WrapMemory(data.AsMemory(), width: 3, height: 2, stride: 2));
    }

    [Fact]
    public void WrapMemory_Strided_ThrowsWhenInputMemoryTooSmall()
    {
        int[] data = new int[6];

        Assert.Throws<ArgumentException>(() => Buffer2D<int>.WrapMemory(data.AsMemory(), width: 3, height: 2, stride: 4));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void WrapMemory_ThrowsWhenWidthOrHeightIsNotPositive(int width, int height)
    {
        int[] data = new int[16];

        Assert.Throws<ArgumentOutOfRangeException>(() => Buffer2D<int>.WrapMemory(data.AsMemory(), width, height));
        Assert.Throws<ArgumentOutOfRangeException>(() => Buffer2D<int>.WrapMemory(data.AsMemory(), width, height, stride: 4));
    }
}

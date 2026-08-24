// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory;

public partial class Buffer2DTests
{
    [Fact]
    public void CopyToSpan_StridedSource_WritesDestinationUsingSourceStride()
    {
        int[] source = [1, 2, 3, 777, 4, 5, 6, 888];
        int[] destination = [-1, -1, -1, -1, -1, -1, -1];

        using Buffer2D<int> buffer = Buffer2D<int>.WrapMemory(source.AsMemory(), width: 3, height: 2, stride: 4);
        buffer.CopyTo(destination);

        Assert.Equal(new[] { 1, 2, 3, -1, 4, 5, 6 }, destination);
    }

    [Fact]
    public void CopyToSpan_PackedSource_WritesPackedDestination()
    {
        int[] source = [1, 2, 3, 4, 5, 6];
        int[] destination = new int[6];

        using Buffer2D<int> buffer = Buffer2D<int>.WrapMemory(source.AsMemory(), width: 3, height: 2);
        buffer.CopyTo(destination);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, destination);
    }

    [Fact]
    public void CopyToSpan_StridedSource_ThrowsWhenDestinationTooShort()
    {
        int[] source = [1, 2, 3, 777, 4, 5, 6, 888];
        int[] destination = new int[6];

        using Buffer2D<int> buffer = Buffer2D<int>.WrapMemory(source.AsMemory(), width: 3, height: 2, stride: 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.CopyTo(destination));
    }
}

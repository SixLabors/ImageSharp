// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Tests.Processing.Convolution;

[Trait("Category", "Processors")]
public class KernelSamplingMapTest
{
    [Fact]
    public void KernalSamplingMap_Kernel5Image7x7RepeatBorder()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(0, 0, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Repeat;
        int[] expected =
        [
            0, 0, 0, 1, 2,
            0, 0, 1, 2, 3,
            0, 1, 2, 3, 4,
            1, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 6,
            4, 5, 6, 6, 6
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel5Image7x7BounceBorder()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(0, 0, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Bounce;
        int[] expected =
        [
            2, 1, 0, 1, 2,
            1, 0, 1, 2, 3,
            0, 1, 2, 3, 4,
            1, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 5,
            4, 5, 6, 5, 4
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel5Image7x7MirrorBorder()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(0, 0, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Mirror;
        int[] expected =
        [
            1, 0, 0, 1, 2,
            0, 0, 1, 2, 3,
            0, 1, 2, 3, 4,
            1, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 6,
            4, 5, 6, 6, 5
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel5Image7x7WrapBorder()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(0, 0, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Wrap;
        int[] expected =
        [
            5, 6, 0, 1, 2,
            6, 0, 1, 2, 3,
            0, 1, 2, 3, 4,
            1, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 0,
            4, 5, 6, 0, 1
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel5Image9x9BounceBorder()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(1, 1, 9, 9);
        BorderWrappingMode mode = BorderWrappingMode.Bounce;
        int[] expected =
        [
            3, 2, 1, 2, 3,
            2, 1, 2, 3, 4,
            1, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 7,
            4, 5, 6, 7, 8,
            5, 6, 7, 8, 9,
            6, 7, 8, 9, 8,
            7, 8, 9, 8, 7
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel5Image9x9MirrorBorder()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(1, 1, 9, 9);
        BorderWrappingMode mode = BorderWrappingMode.Mirror;
        int[] expected =
        [
            2, 1, 1, 2, 3,
            1, 1, 2, 3, 4,
            1, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 7,
            4, 5, 6, 7, 8,
            5, 6, 7, 8, 9,
            6, 7, 8, 9, 9,
            7, 8, 9, 9, 8
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel5Image9x9WrapBorder()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(1, 1, 9, 9);
        BorderWrappingMode mode = BorderWrappingMode.Wrap;
        int[] expected =
        [
            8, 9, 1, 2, 3,
            9, 1, 2, 3, 4,
            1, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 7,
            4, 5, 6, 7, 8,
            5, 6, 7, 8, 9,
            6, 7, 8, 9, 1,
            7, 8, 9, 1, 2
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel5Image7x7RepeatBorderTile()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(2, 2, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Repeat;
        int[] expected =
        [
            2, 2, 2, 3, 4,
            2, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 7,
            4, 5, 6, 7, 8,
            5, 6, 7, 8, 8,
            6, 7, 8, 8, 8
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel5Image7x7BounceBorderTile()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(2, 2, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Bounce;
        int[] expected =
        [
            4, 3, 2, 3, 4,
            3, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 7,
            4, 5, 6, 7, 8,
            5, 6, 7, 8, 7,
            6, 7, 8, 7, 6
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel5Image7x7MirrorBorderTile()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(2, 2, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Mirror;
        int[] expected =
        [
            3, 2, 2, 3, 4,
            2, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 7,
            4, 5, 6, 7, 8,
            5, 6, 7, 8, 8,
            6, 7, 8, 8, 7
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel5Image7x7WrapBorderTile()
    {
        Size kernelSize = new(5, 5);
        Rectangle bounds = new(2, 2, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Wrap;
        int[] expected =
        [
            7, 8, 2, 3, 4,
            8, 2, 3, 4, 5,
            2, 3, 4, 5, 6,
            3, 4, 5, 6, 7,
            4, 5, 6, 7, 8,
            5, 6, 7, 8, 2,
            6, 7, 8, 2, 3
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel3Image7x7RepeatBorder()
    {
        Size kernelSize = new(3, 3);
        Rectangle bounds = new(0, 0, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Repeat;
        int[] expected =
        [
            0, 0, 1,
            0, 1, 2,
            1, 2, 3,
            2, 3, 4,
            3, 4, 5,
            4, 5, 6,
            5, 6, 6
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel3Image7x7BounceBorder()
    {
        Size kernelSize = new(3, 3);
        Rectangle bounds = new(0, 0, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Bounce;
        int[] expected =
        [
            1, 0, 1,
            0, 1, 2,
            1, 2, 3,
            2, 3, 4,
            3, 4, 5,
            4, 5, 6,
            5, 6, 5
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel3Image7x7MirrorBorder()
    {
        Size kernelSize = new(3, 3);
        Rectangle bounds = new(0, 0, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Mirror;
        int[] expected =
        [
            0, 0, 1,
            0, 1, 2,
            1, 2, 3,
            2, 3, 4,
            3, 4, 5,
            4, 5, 6,
            5, 6, 6
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel3Image7x7WrapBorder()
    {
        Size kernelSize = new(3, 3);
        Rectangle bounds = new(0, 0, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Wrap;
        int[] expected =
        [
            6, 0, 1,
            0, 1, 2,
            1, 2, 3,
            2, 3, 4,
            3, 4, 5,
            4, 5, 6,
            5, 6, 0
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel3Image7x7RepeatBorderTile()
    {
        Size kernelSize = new(3, 3);
        Rectangle bounds = new(2, 2, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Repeat;
        int[] expected =
        [
            2, 2, 3,
            2, 3, 4,
            3, 4, 5,
            4, 5, 6,
            5, 6, 7,
            6, 7, 8,
            7, 8, 8
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel3Image7BounceBorderTile()
    {
        Size kernelSize = new(3, 3);
        Rectangle bounds = new(2, 2, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Bounce;
        int[] expected =
        [
            3, 2, 3,
            2, 3, 4,
            3, 4, 5,
            4, 5, 6,
            5, 6, 7,
            6, 7, 8,
            7, 8, 7
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel3Image7MirrorBorderTile()
    {
        Size kernelSize = new(3, 3);
        Rectangle bounds = new(2, 2, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Mirror;
        int[] expected =
        [
            2, 2, 3,
            2, 3, 4,
            3, 4, 5,
            4, 5, 6,
            5, 6, 7,
            6, 7, 8,
            7, 8, 8
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel3Image7x7WrapBorderTile()
    {
        Size kernelSize = new(3, 3);
        Rectangle bounds = new(2, 2, 7, 7);
        BorderWrappingMode mode = BorderWrappingMode.Wrap;
        int[] expected =
        [
            8, 2, 3,
            2, 3, 4,
            3, 4, 5,
            4, 5, 6,
            5, 6, 7,
            6, 7, 8,
            7, 8, 2
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, expected, expected);
    }

    [Fact]
    public void KernalSamplingMap_Kernel3Image7x5WrapBorderTile()
    {
        Size kernelSize = new(3, 3);
        Rectangle bounds = new(2, 2, 7, 5);
        BorderWrappingMode mode = BorderWrappingMode.Wrap;
        int[] xExpected =
        [
            8, 2, 3,
            2, 3, 4,
            3, 4, 5,
            4, 5, 6,
            5, 6, 7,
            6, 7, 8,
            7, 8, 2
        ];
        int[] yExpected =
        [
            6, 2, 3,
            2, 3, 4,
            3, 4, 5,
            4, 5, 6,
            5, 6, 2
        ];
        this.AssertOffsets(kernelSize, bounds, mode, mode, xExpected, yExpected);
    }

    private void AssertOffsets(Size kernelSize, Rectangle bounds, BorderWrappingMode xBorderMode, BorderWrappingMode yBorderMode, int[] xExpected, int[] yExpected)
    {
        // Arrange
        KernelSamplingMap map = new(Configuration.Default.MemoryAllocator);

        // Act
        map.BuildSamplingOffsetMap(kernelSize.Height, kernelSize.Width, bounds, xBorderMode, yBorderMode);

        // Assert
        int[] xOffsets = map.GetColumnOffsetSpan().ToArray();
        Assert.Equal(xExpected, xOffsets);
        int[] yOffsets = map.GetRowOffsetSpan().ToArray();
        Assert.Equal(yExpected, yOffsets);
    }
}

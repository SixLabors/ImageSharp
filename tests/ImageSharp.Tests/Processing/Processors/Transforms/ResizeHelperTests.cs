// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;

[Trait("Category", "Processors")]
public class ResizeHelperTests
{
    [Theory]
    [InlineData(20, 100, 1, 2)]
    [InlineData(20, 100, 20 * 100 * 16, 2)]
    [InlineData(20, 100, 40 * 100 * 16, 2)]
    [InlineData(20, 100, 59 * 100 * 16, 2)]
    [InlineData(20, 100, 60 * 100 * 16, 3)]
    [InlineData(17, 63, 5 * 17 * 63 * 16, 5)]
    [InlineData(17, 63, (5 * 17 * 63 * 16) + 1, 5)]
    [InlineData(17, 63, (6 * 17 * 63 * 16) - 1, 5)]
    [InlineData(33, 400, 1 * 1024 * 1024, 4)]
    [InlineData(33, 400, 8 * 1024 * 1024, 39)]
    [InlineData(50, 300, 1 * 1024 * 1024, 4)]
    public void CalculateResizeWorkerHeightInWindowBands(
        int windowDiameter,
        int width,
        int sizeLimitHintInBytes,
        int expectedCount)
    {
        int actualCount = ResizeHelper.CalculateResizeWorkerHeightInWindowBands(windowDiameter, width, sizeLimitHintInBytes);
        Assert.Equal(expectedCount, actualCount);
    }

    [Fact]
    public void CalculateMinRectangleWhenSourceIsSmallerThanTarget()
    {
        Size sourceSize = new Size(200, 100);
        Size target = new Size(400, 200);

        (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(
            sourceSize,
            new ResizeOptions
            {
                Mode = ResizeMode.Min,
                Size = target
            });

        Assert.Equal(sourceSize, size);
        Assert.Equal(new Rectangle(0, 0, sourceSize.Width, sourceSize.Height), rectangle);
    }

    [Fact]
    public void MaxSizeAndRectangleAreCorrect()
    {
        Size sourceSize = new Size(5072, 6761);
        Size target = new Size(0, 450);

        Size expectedSize = new Size(338, 450);
        Rectangle expectedRectangle = new Rectangle(Point.Empty, expectedSize);

        (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(
            sourceSize,
            new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = target
            });

        Assert.Equal(expectedSize, size);
        Assert.Equal(expectedRectangle, rectangle);
    }

    [Fact]
    public void CropSizeAndRectangleAreCorrect()
    {
        Size sourceSize = new Size(100, 100);
        Size target = new Size(25, 50);

        Size expectedSize = new Size(25, 50);
        Rectangle expectedRectangle = new Rectangle(-12, 0, 50, 50);

        (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(
            sourceSize,
            new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Size = target
            });

        Assert.Equal(expectedSize, size);
        Assert.Equal(expectedRectangle, rectangle);
    }

    [Fact]
    public void BoxPadSizeAndRectangleAreCorrect()
    {
        Size sourceSize = new Size(100, 100);
        Size target = new Size(120, 110);

        Size expectedSize = new Size(120, 110);
        Rectangle expectedRectangle = new Rectangle(10, 5, 100, 100);

        (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(
            sourceSize,
            new ResizeOptions
            {
                Mode = ResizeMode.BoxPad,
                Size = target
            });

        Assert.Equal(expectedSize, size);
        Assert.Equal(expectedRectangle, rectangle);
    }

    [Fact]
    public void PadSizeAndRectangleAreCorrect()
    {
        Size sourceSize = new Size(100, 100);
        Size target = new Size(120, 110);

        Size expectedSize = new Size(120, 110);
        Rectangle expectedRectangle = new Rectangle(5, 0, 110, 110);

        (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(
            sourceSize,
            new ResizeOptions
            {
                Mode = ResizeMode.Pad,
                Size = target
            });

        Assert.Equal(expectedSize, size);
        Assert.Equal(expectedRectangle, rectangle);
    }

    [Fact]
    public void StretchSizeAndRectangleAreCorrect()
    {
        Size sourceSize = new Size(100, 100);
        Size target = new Size(57, 32);

        Size expectedSize = new Size(57, 32);
        Rectangle expectedRectangle = new Rectangle(Point.Empty, expectedSize);

        (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(
            sourceSize,
            new ResizeOptions
            {
                Mode = ResizeMode.Stretch,
                Size = target
            });

        Assert.Equal(expectedSize, size);
        Assert.Equal(expectedRectangle, rectangle);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms;

[Trait("Category", "Processors")]
public class CropTest : BaseImageOperationsExtensionTest
{
    [Theory]
    [InlineData(10, 10)]
    [InlineData(12, 123)]
    public void CropWidthHeightCropProcessorWithRectangleSet(int width, int height)
    {
        this.operations.Crop(width, height);
        CropProcessor processor = this.Verify<CropProcessor>();

        Assert.Equal(new Rectangle(0, 0, width, height), processor.CropRectangle);
    }

    [Theory]
    [InlineData(10, 10, 2, 6)]
    [InlineData(12, 123, 6, 2)]
    public void CropRectangleCropProcessorWithRectangleSet(int x, int y, int width, int height)
    {
        Rectangle cropRectangle = new(x, y, width, height);
        this.operations.Crop(cropRectangle);
        CropProcessor processor = this.Verify<CropProcessor>();

        Assert.Equal(cropRectangle, processor.CropRectangle);
    }

    [Fact]
    public void CropRectangleWithInvalidBoundsThrowsException()
    {
        Rectangle cropRectangle = Rectangle.Inflate(this.SourceBounds(), 5, 5);
        Assert.Throws<ArgumentException>(() => this.operations.Crop(cropRectangle));
    }
}

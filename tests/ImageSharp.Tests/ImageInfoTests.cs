// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Tests;

public class ImageInfoTests
{
    [Fact]
    public void ImageInfoInitializesCorrectly()
    {
        const int width = 50;
        const int height = 60;
        Size size = new(width, height);
        Rectangle rectangle = new(0, 0, width, height);
        PixelTypeInfo pixelType = new(8);
        ImageMetadata meta = new();

        ImageInfo info = new(pixelType, width, height, meta);

        Assert.Equal(pixelType, info.PixelType);
        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
        Assert.Equal(size, info.Size);
        Assert.Equal(rectangle, info.Bounds);
        Assert.Equal(meta, info.Metadata);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png;
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

        // Initialize the metadata to match standard decoding behavior.
        ImageMetadata meta = new() { DecodedImageFormat = PngFormat.Instance };
        meta.GetPngMetadata();

        ImageInfo info = new(size, meta);

        Assert.NotEqual(default, info.PixelType);
        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
        Assert.Equal(size, info.Size);
        Assert.Equal(rectangle, info.Bounds);
        Assert.Equal(meta, info.Metadata);
    }

    [Fact]
    public void ImageInfoInitializesCorrectlyWithFrameMetadata()
    {
        const int width = 50;
        const int height = 60;
        Size size = new(width, height);
        Rectangle rectangle = new(0, 0, width, height);

        // Initialize the metadata to match standard decoding behavior.
        ImageMetadata meta = new() { DecodedImageFormat = PngFormat.Instance };
        meta.GetPngMetadata();

        IReadOnlyList<ImageFrameMetadata> frameMetadata = [new()];

        ImageInfo info = new(size, meta, frameMetadata);

        Assert.NotEqual(default, info.PixelType);
        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
        Assert.Equal(size, info.Size);
        Assert.Equal(rectangle, info.Bounds);
        Assert.Equal(meta, info.Metadata);
        Assert.Equal(frameMetadata.Count, info.FrameMetadataCollection.Count);
    }
}

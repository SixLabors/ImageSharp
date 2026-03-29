// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.Formats.Exr.Constants;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
public class ExrMetadataTests
{
    [Fact]
    public void CloneIsDeep()
    {
        ExrMetadata meta = new()
        { ImageDataType = ExrImageDataType.Rgb, PixelType = ExrPixelType.Half };
        ExrMetadata clone = (ExrMetadata)meta.DeepClone();

        clone.ImageDataType = ExrImageDataType.Gray;
        clone.PixelType = ExrPixelType.Float;

        Assert.False(meta.ImageDataType.Equals(clone.ImageDataType));
        Assert.False(meta.PixelType.Equals(clone.PixelType));
    }

    [Theory]
    [InlineData(TestImages.Exr.Uncompressed, 199, 297)]
    public void Identify_DetectsCorrectWidthAndHeight<TPixel>(string imagePath, int expectedWidth, int expectedHeight)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);

        Assert.NotNull(imageInfo);
        Assert.Equal(expectedWidth, imageInfo.Width);
        Assert.Equal(expectedHeight, imageInfo.Height);
    }

    [Theory]
    [InlineData(TestImages.Exr.Uncompressed, ExrPixelType.Half)]
    [InlineData(TestImages.Exr.UncompressedFloatRgb, ExrPixelType.Float)]
    [InlineData(TestImages.Exr.UncompressedUintRgb, ExrPixelType.UnsignedInt)]
    public void Identify_DetectsCorrectPixelType(string imagePath, ExrPixelType expectedPixelType)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);

        Assert.NotNull(imageInfo);
        ExrMetadata metadata = imageInfo.Metadata.GetExrMetadata();
        Assert.NotNull(metadata);
        Assert.Equal(expectedPixelType, metadata.PixelType);
    }

    [Theory]
    [InlineData(TestImages.Exr.UncompressedRgba, ExrImageDataType.Rgba)]
    [InlineData(TestImages.Exr.Rgb, ExrImageDataType.Rgb)]
    [InlineData(TestImages.Exr.Gray, ExrImageDataType.Gray)]
    public void Identify_DetectsCorrectImageDataType(string imagePath, ExrImageDataType expectedImageDataType)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);

        Assert.NotNull(imageInfo);
        ExrMetadata metadata = imageInfo.Metadata.GetExrMetadata();
        Assert.NotNull(metadata);
        Assert.Equal(expectedImageDataType, metadata.ImageDataType);
    }
}

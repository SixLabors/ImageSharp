// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
public class ExrMetadataTests
{
    [Fact]
    public void CloneIsDeep()
    {
        ExrMetadata meta = new()
        { ImageDataType = ExrImageDataType.Rgb, PixelType = ExrPixelType.Half, Compression = ExrCompression.None };
        ExrMetadata clone = (ExrMetadata)meta.DeepClone();

        clone.ImageDataType = ExrImageDataType.Gray;
        clone.PixelType = ExrPixelType.Float;
        clone.Compression = ExrCompression.Zip;

        Assert.False(meta.ImageDataType.Equals(clone.ImageDataType));
        Assert.False(meta.PixelType.Equals(clone.PixelType));
        Assert.False(meta.Compression.Equals(clone.Compression));
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

    [Theory]
    [InlineData(TestImages.Exr.UncompressedRgba, ExrCompression.None)]
    [InlineData(TestImages.Exr.B44, ExrCompression.B44)]
    [InlineData(TestImages.Exr.Rle, ExrCompression.RunLengthEncoded)]
    public void Identify_DetectsCorrectCompression(string imagePath, ExrCompression expectedCompression)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);

        Assert.NotNull(imageInfo);
        ExrMetadata metadata = imageInfo.Metadata.GetExrMetadata();
        Assert.NotNull(metadata);
        Assert.Equal(expectedCompression, metadata.Compression);
    }

    [Theory]
    [InlineData(PixelColorType.Binary, 1, ExrImageDataType.Unknown, ExrPixelType.Half)]
    [InlineData(PixelColorType.Indexed, 8, ExrImageDataType.Unknown, ExrPixelType.Half)]
    [InlineData(PixelColorType.Luminance, 16, ExrImageDataType.Gray, ExrPixelType.Half)]
    [InlineData(PixelColorType.RGB, 48, ExrImageDataType.Rgb, ExrPixelType.Float)]
    [InlineData(PixelColorType.BGR, 48, ExrImageDataType.Rgb, ExrPixelType.Float)]
    [InlineData(PixelColorType.RGB | PixelColorType.Alpha, 64, ExrImageDataType.Rgba, ExrPixelType.Float)]
    [InlineData(PixelColorType.BGR | PixelColorType.Alpha, 64, ExrImageDataType.Rgba, ExrPixelType.Float)]
    [InlineData(PixelColorType.Luminance | PixelColorType.Alpha, 32, ExrImageDataType.Rgba, ExrPixelType.Float)]
    [InlineData(PixelColorType.YCbCr, 48, ExrImageDataType.Unknown, ExrPixelType.Float)]
    [InlineData(PixelColorType.CMYK, 64, ExrImageDataType.Unknown, ExrPixelType.Float)]
    [InlineData(PixelColorType.YCCK, 64, ExrImageDataType.Unknown, ExrPixelType.Float)]
    public void FromFormatConnectingMetadata_ConvertColorTypeAsExpected(PixelColorType pixelColorType, int bitsPerPixel, ExrImageDataType expectedImageDataType, ExrPixelType expectedPixelType)
    {
        FormatConnectingMetadata formatConnectingMetadata = new()
        {
            PixelTypeInfo = new PixelTypeInfo(bitsPerPixel)
            {
                ColorType = pixelColorType,
            },
        };

        ExrMetadata actual = ExrMetadata.FromFormatConnectingMetadata(formatConnectingMetadata);

        Assert.Equal(expectedImageDataType, actual.ImageDataType);
        Assert.Equal(expectedPixelType, actual.PixelType);
    }
}

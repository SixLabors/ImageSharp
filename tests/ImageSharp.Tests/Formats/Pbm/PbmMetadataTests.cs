// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Pbm;
using static SixLabors.ImageSharp.Tests.TestImages.Pbm;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Pbm;

[Trait("Format", "Pbm")]
public class PbmMetadataTests
{
    [Fact]
    public void CloneIsDeep()
    {
        PbmMetadata meta = new()
        { ColorType = PbmColorType.Grayscale };
        PbmMetadata clone = (PbmMetadata)meta.DeepClone();

        clone.ColorType = PbmColorType.Rgb;
        clone.ComponentType = PbmComponentType.Short;

        Assert.False(meta.ColorType.Equals(clone.ColorType));
        Assert.False(meta.ComponentType.Equals(clone.ComponentType));
    }

    [Theory]
    [InlineData(BlackAndWhitePlain, PbmEncoding.Plain)]
    [InlineData(BlackAndWhiteBinary, PbmEncoding.Binary)]
    [InlineData(GrayscaleBinary, PbmEncoding.Binary)]
    [InlineData(GrayscaleBinaryWide, PbmEncoding.Binary)]
    [InlineData(GrayscalePlain, PbmEncoding.Plain)]
    [InlineData(RgbBinary, PbmEncoding.Binary)]
    [InlineData(RgbPlain, PbmEncoding.Plain)]
    public void Identify_DetectsCorrectEncoding(string imagePath, PbmEncoding expectedEncoding)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        Assert.NotNull(imageInfo);
        PbmMetadata bitmapMetadata = imageInfo.Metadata.GetPbmMetadata();
        Assert.NotNull(bitmapMetadata);
        Assert.Equal(expectedEncoding, bitmapMetadata.Encoding);
    }

    [Theory]
    [InlineData(BlackAndWhitePlain, PbmColorType.BlackAndWhite)]
    [InlineData(BlackAndWhiteBinary, PbmColorType.BlackAndWhite)]
    [InlineData(GrayscaleBinary, PbmColorType.Grayscale)]
    [InlineData(GrayscaleBinaryWide, PbmColorType.Grayscale)]
    [InlineData(GrayscalePlain, PbmColorType.Grayscale)]
    [InlineData(RgbBinary, PbmColorType.Rgb)]
    [InlineData(RgbPlain, PbmColorType.Rgb)]
    public void Identify_DetectsCorrectColorType(string imagePath, PbmColorType expectedColorType)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        Assert.NotNull(imageInfo);
        PbmMetadata bitmapMetadata = imageInfo.Metadata.GetPbmMetadata();
        Assert.NotNull(bitmapMetadata);
        Assert.Equal(expectedColorType, bitmapMetadata.ColorType);
    }

    [Theory]
    [InlineData(BlackAndWhitePlain, PbmComponentType.Bit)]
    [InlineData(BlackAndWhiteBinary, PbmComponentType.Bit)]
    [InlineData(GrayscaleBinary, PbmComponentType.Byte)]
    [InlineData(GrayscaleBinaryWide, PbmComponentType.Short)]
    [InlineData(GrayscalePlain, PbmComponentType.Byte)]
    [InlineData(RgbBinary, PbmComponentType.Byte)]
    [InlineData(RgbPlain, PbmComponentType.Byte)]
    public void Identify_DetectsCorrectComponentType(string imagePath, PbmComponentType expectedComponentType)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        Assert.NotNull(imageInfo);
        PbmMetadata bitmapMetadata = imageInfo.Metadata.GetPbmMetadata();
        Assert.NotNull(bitmapMetadata);
        Assert.Equal(expectedComponentType, bitmapMetadata.ComponentType);
    }

    [Fact]
    public void Identify_HandlesCraftedDenialOfServiceString()
    {
        byte[] bytes = Convert.FromBase64String("UDEjWAAACQAAAAA=");
        ImageInfo info = Image.Identify(bytes);
        Assert.Equal(default, info.Size);
        Configuration.Default.ImageFormatsManager.TryFindFormatByFileExtension("pbm", out IImageFormat format);
        Assert.Equal(format!, info.Metadata.DecodedImageFormat);
    }
}

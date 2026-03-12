// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Qoi;

[Trait("Format", "Qoi")]
[ValidateDisposedMemoryAllocations]
public class QoiDecoderTests
{
    [Theory]
    [InlineData(TestImages.Qoi.Dice, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.EdgeCase, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.Kodim10, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.Kodim23, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.QoiLogo, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.TestCard, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.TestCardRGBA, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.Wikipedia008, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    public void Identify(string imagePath, QoiChannels channels, QoiColorSpace colorSpace)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);
        QoiMetadata qoiMetadata = imageInfo.Metadata.GetQoiMetadata();

        Assert.NotNull(imageInfo);
        Assert.Equal(imageInfo.Metadata.DecodedImageFormat, QoiFormat.Instance);
        Assert.Equal(qoiMetadata.Channels, channels);
        Assert.Equal(qoiMetadata.ColorSpace, colorSpace);
    }

    [Theory]
    [WithFile(TestImages.Qoi.Dice, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.EdgeCase, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.Kodim10, PixelTypes.Rgba32, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.Kodim23, PixelTypes.Rgba32, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.QoiLogo, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.TestCard, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.TestCardRGBA, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.Wikipedia008, PixelTypes.Rgba32, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    public void Decode<TPixel>(TestImageProvider<TPixel> provider, QoiChannels channels, QoiColorSpace colorSpace)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        QoiMetadata qoiMetadata = image.Metadata.GetQoiMetadata();
        image.DebugSave(provider);

        image.CompareToReferenceOutput(provider);
        Assert.Equal(qoiMetadata.Channels, channels);
        Assert.Equal(qoiMetadata.ColorSpace, colorSpace);
    }
}

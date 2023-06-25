// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Qoi;

[Trait("Format", "Qoi")]
[ValidateDisposedMemoryAllocations]
public class QoiDecoderTests
{
    [Theory]
    [InlineData(TestImages.Qoi.Dice)]
    [InlineData(TestImages.Qoi.EdgeCase)]
    [InlineData(TestImages.Qoi.Kodim10)]
    [InlineData(TestImages.Qoi.Kodim23)]
    [InlineData(TestImages.Qoi.QoiLogo)]
    [InlineData(TestImages.Qoi.TestCard)]
    [InlineData(TestImages.Qoi.TestCardRGBA)]
    [InlineData(TestImages.Qoi.Wikipedia008)]
    public void Identify(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);

        Assert.NotNull(imageInfo);
        Assert.Equal(imageInfo.Metadata.DecodedImageFormat, ImageSharp.Formats.Qoi.QoiFormat.Instance);
    }

    [Theory]
    [WithFile(TestImages.Qoi.Dice, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.EdgeCase, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.Kodim10, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.Kodim23, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.QoiLogo, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.TestCard, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.TestCardRGBA, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.Wikipedia008, PixelTypes.Rgba32)]
    public void Decode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);

        image.CompareToReferenceOutput(provider);
    }
}

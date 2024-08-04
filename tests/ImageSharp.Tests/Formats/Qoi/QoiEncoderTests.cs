// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Qoi;

[Trait("Format", "Qoi")]
[ValidateDisposedMemoryAllocations]
public class QoiEncoderTests
{
    [Theory]
    [WithFile(TestImages.Qoi.Dice, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.EdgeCase, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.Kodim10, PixelTypes.Rgba32, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.Kodim23, PixelTypes.Rgba32, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.QoiLogo, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.TestCard, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.TestCardRGBA, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.Wikipedia008, PixelTypes.Rgba32, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    public static void Encode<TPixel>(TestImageProvider<TPixel> provider, QoiChannels channels, QoiColorSpace colorSpace)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(new MagickReferenceDecoder(QoiFormat.Instance));
        using MemoryStream stream = new();
        QoiEncoder encoder = new()
        {
            Channels = channels,
            ColorSpace = colorSpace
        };
        image.Save(stream, encoder);
        stream.Position = 0;

        using Image<TPixel> encodedImage = Image.Load<TPixel>(stream);
        QoiMetadata qoiMetadata = encodedImage.Metadata.GetQoiMetadata();

        ImageComparer.Exact.CompareImages(image, encodedImage);
        Assert.Equal(qoiMetadata.Channels, channels);
        Assert.Equal(qoiMetadata.ColorSpace, colorSpace);
    }
}

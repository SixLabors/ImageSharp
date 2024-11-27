// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
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

    [Fact]
    public void Encode_WithTransparentColorBehaviorClear_Works()
    {
        // arrange
        using Image<Rgba32> image = new(50, 50);
        QoiEncoder encoder = new()
        {
            TransparentColorMode = TransparentColorMode.Clear,
        };
        Rgba32 rgba32 = Color.Blue.ToPixel<Rgba32>();
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgba32> rowSpan = accessor.GetRowSpan(y);

                // Half of the test image should be transparent.
                if (y > 25)
                {
                    rgba32.A = 0;
                }

                for (int x = 0; x < image.Width; x++)
                {
                    rowSpan[x] = Rgba32.FromRgba32(rgba32);
                }
            }
        });

        // act
        using MemoryStream memStream = new();
        image.Save(memStream, encoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> actual = Image.Load<Rgba32>(memStream);
        Rgba32 expectedColor = Color.Blue.ToPixel<Rgba32>();

        actual.ProcessPixelRows(accessor =>
        {
            Rgba32 transparent = Color.Transparent.ToPixel<Rgba32>();
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> rowSpan = accessor.GetRowSpan(y);

                if (y > 25)
                {
                    expectedColor = transparent;
                }

                for (int x = 0; x < accessor.Width; x++)
                {
                    Assert.Equal(expectedColor, rowSpan[x]);
                }
            }
        });
    }
}

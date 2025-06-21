// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

[Trait("Format", "Tiff")]
public class TiffEncoderMultiframeTests : TiffEncoderBaseTester
{
    [Theory]
    [WithFile(MultiframeLzwPredictor, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeMultiframe_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb);

    [Theory]
    [WithFile(MultiframeDifferentVariants, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeMultiframe_NotSupport<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<NotSupportedException>(() => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb));

    [Theory]
    [WithFile(MultiframeDeflateWithPreview, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeMultiframe_WithPreview<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb);

    [Theory]
    [WithFile(TestImages.Gif.Receipt, PixelTypes.Rgb24)]

    // MAGICK decoder makes the same mistake we did and clones the proceeding frame overwriting the differences.
    // [WithFile(TestImages.Gif.Issues.BadDescriptorWidth, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeMultiframe_Convert<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit48, TiffPhotometricInterpretation.Rgb);

    [Theory]
    [WithFile(MultiframeLzwPredictor, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeMultiframe_RemoveFrames<TPixel>(TestImageProvider<TPixel> provider)
     where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        Assert.True(image.Frames.Count > 1);

        image.Frames.RemoveFrame(0);

        TiffBitsPerPixel bitsPerPixel = TiffBitsPerPixel.Bit24;
        TiffEncoder encoder = new TiffEncoder
        {
            PhotometricInterpretation = TiffPhotometricInterpretation.Rgb,
            BitsPerPixel = bitsPerPixel,
            Compression = TiffCompression.Deflate
        };

        image.VerifyEncoder(
           provider,
           "tiff",
           bitsPerPixel,
           encoder,
           ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.Bike, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeMultiframe_AddFrames<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        Assert.Equal(1, image.Frames.Count);

        using Image<Rgba32> image1 = new Image<Rgba32>(image.Width, image.Height, Color.Green.ToPixel<Rgba32>());

        using Image<Rgba32> image2 = new Image<Rgba32>(image.Width, image.Height, Color.Yellow.ToPixel<Rgba32>());

        image.Frames.AddFrame(image1.Frames.RootFrame);
        image.Frames.AddFrame(image2.Frames.RootFrame);

        TiffBitsPerPixel bitsPerPixel = TiffBitsPerPixel.Bit24;
        TiffEncoder encoder = new TiffEncoder
        {
            PhotometricInterpretation = TiffPhotometricInterpretation.Rgb,
            BitsPerPixel = bitsPerPixel,
            Compression = TiffCompression.Deflate
        };

        using (MemoryStream ms = new System.IO.MemoryStream())
        {
            image.Save(ms, encoder);

            ms.Position = 0;
            using Image<Rgba32> output = Image.Load<Rgba32>(ms);

            Assert.Equal(3, output.Frames.Count);

            ImageFrame<Rgba32> frame1 = output.Frames[1];
            ImageFrame<Rgba32> frame2 = output.Frames[2];

            Assert.Equal(Color.Green.ToPixel<Rgba32>(), frame1[10, 10]);
            Assert.Equal(Color.Yellow.ToPixel<Rgba32>(), frame2[10, 10]);

            Assert.Equal(TiffCompression.Deflate, frame1.Metadata.GetTiffMetadata().Compression);
            Assert.Equal(TiffCompression.Deflate, frame1.Metadata.GetTiffMetadata().Compression);

            Assert.Equal(TiffPhotometricInterpretation.Rgb, frame1.Metadata.GetTiffMetadata().PhotometricInterpretation);
            Assert.Equal(TiffPhotometricInterpretation.Rgb, frame2.Metadata.GetTiffMetadata().PhotometricInterpretation);
        }

        image.VerifyEncoder(
            provider,
            "tiff",
            bitsPerPixel,
            encoder,
            ImageComparer.Exact);
    }

    [Theory]
    [WithBlankImages(100, 100, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeMultiframe_Create<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        using Image<Rgba32> image0 = new Image<Rgba32>(image.Width, image.Height, Color.Red.ToPixel<Rgba32>());

        using Image<Rgba32> image1 = new Image<Rgba32>(image.Width, image.Height, Color.Green.ToPixel<Rgba32>());

        using Image<Rgba32> image2 = new Image<Rgba32>(image.Width, image.Height, Color.Yellow.ToPixel<Rgba32>());

        image.Frames.AddFrame(image0.Frames.RootFrame);
        image.Frames.AddFrame(image1.Frames.RootFrame);
        image.Frames.AddFrame(image2.Frames.RootFrame);
        image.Frames.RemoveFrame(0);

        TiffBitsPerPixel bitsPerPixel = TiffBitsPerPixel.Bit8;
        TiffEncoder encoder = new TiffEncoder
        {
            PhotometricInterpretation = TiffPhotometricInterpretation.PaletteColor,
            BitsPerPixel = bitsPerPixel,
            Compression = TiffCompression.Lzw
        };

        using (MemoryStream ms = new System.IO.MemoryStream())
        {
            image.Save(ms, encoder);

            ms.Position = 0;
            using Image<Rgba32> output = Image.Load<Rgba32>(ms);

            Assert.Equal(3, output.Frames.Count);

            ImageFrame<Rgba32> frame0 = output.Frames[0];
            ImageFrame<Rgba32> frame1 = output.Frames[1];
            ImageFrame<Rgba32> frame2 = output.Frames[2];

            Assert.Equal(Color.Red.ToPixel<Rgba32>(), frame0[10, 10]);
            Assert.Equal(Color.Green.ToPixel<Rgba32>(), frame1[10, 10]);
            Assert.Equal(Color.Yellow.ToPixel<Rgba32>(), frame2[10, 10]);

            Assert.Equal(TiffCompression.Lzw, frame0.Metadata.GetTiffMetadata().Compression);
            Assert.Equal(TiffCompression.Lzw, frame1.Metadata.GetTiffMetadata().Compression);
            Assert.Equal(TiffCompression.Lzw, frame1.Metadata.GetTiffMetadata().Compression);

            Assert.Equal(TiffPhotometricInterpretation.PaletteColor, frame0.Metadata.GetTiffMetadata().PhotometricInterpretation);
            Assert.Equal(TiffPhotometricInterpretation.PaletteColor, frame1.Metadata.GetTiffMetadata().PhotometricInterpretation);
            Assert.Equal(TiffPhotometricInterpretation.PaletteColor, frame2.Metadata.GetTiffMetadata().PhotometricInterpretation);
        }

        image.VerifyEncoder(
            provider,
            "tiff",
            bitsPerPixel,
            encoder,
            ImageComparer.Exact);
    }
}

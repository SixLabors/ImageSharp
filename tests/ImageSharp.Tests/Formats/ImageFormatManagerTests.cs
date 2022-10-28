// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Moq;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats;

public class ImageFormatManagerTests
{
    public ImageFormatManager FormatsManagerEmpty { get; }

    public ImageFormatManager DefaultFormatsManager { get; }

    public ImageFormatManagerTests()
    {
        this.DefaultFormatsManager = Configuration.CreateDefaultInstance().ImageFormatsManager;
        this.FormatsManagerEmpty = new ImageFormatManager();
    }

    [Fact]
    public void IfAutoLoadWellKnownFormatsIsTrueAllFormatsAreLoaded()
    {
        Assert.Equal(1, this.DefaultFormatsManager.ImageEncoders.Select(item => item.Value).OfType<PbmEncoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageEncoders.Select(item => item.Value).OfType<PngEncoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageEncoders.Select(item => item.Value).OfType<BmpEncoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageEncoders.Select(item => item.Value).OfType<JpegEncoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageEncoders.Select(item => item.Value).OfType<GifEncoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageEncoders.Select(item => item.Value).OfType<TgaEncoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageEncoders.Select(item => item.Value).OfType<TiffEncoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageEncoders.Select(item => item.Value).OfType<WebpEncoder>().Count());

        Assert.Equal(1, this.DefaultFormatsManager.ImageDecoders.Select(item => item.Value).OfType<PbmDecoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageDecoders.Select(item => item.Value).OfType<PngDecoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageDecoders.Select(item => item.Value).OfType<BmpDecoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageDecoders.Select(item => item.Value).OfType<JpegDecoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageDecoders.Select(item => item.Value).OfType<BmpDecoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageDecoders.Select(item => item.Value).OfType<TgaDecoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageDecoders.Select(item => item.Value).OfType<TiffDecoder>().Count());
        Assert.Equal(1, this.DefaultFormatsManager.ImageDecoders.Select(item => item.Value).OfType<WebpDecoder>().Count());
    }

    [Fact]
    public void AddImageFormatDetectorNullThrows()
        => Assert.Throws<ArgumentNullException>(() => this.DefaultFormatsManager.AddImageFormatDetector(null));

    [Fact]
    public void RegisterNullMimeTypeEncoder()
    {
        Assert.Throws<ArgumentNullException>(() => this.DefaultFormatsManager.SetEncoder(null, new Mock<ImageEncoder>().Object));
        Assert.Throws<ArgumentNullException>(() => this.DefaultFormatsManager.SetEncoder(BmpFormat.Instance, null));
        Assert.Throws<ArgumentNullException>(() => this.DefaultFormatsManager.SetEncoder(null, null));
    }

    [Fact]
    public void RegisterNullSetDecoder()
    {
        Assert.Throws<ArgumentNullException>(() => this.DefaultFormatsManager.SetDecoder(null, new Mock<ImageDecoder>().Object));
        Assert.Throws<ArgumentNullException>(() => this.DefaultFormatsManager.SetDecoder(BmpFormat.Instance, null));
        Assert.Throws<ArgumentNullException>(() => this.DefaultFormatsManager.SetDecoder(null, null));
    }

    [Fact]
    public void RegisterMimeTypeEncoderReplacesLast()
    {
        ImageEncoder encoder1 = new Mock<ImageEncoder>().Object;
        this.FormatsManagerEmpty.SetEncoder(TestFormat.GlobalTestFormat, encoder1);
        ImageEncoder found = this.FormatsManagerEmpty.FindEncoder(TestFormat.GlobalTestFormat);
        Assert.Equal(encoder1, found);

        ImageEncoder encoder2 = new Mock<ImageEncoder>().Object;
        this.FormatsManagerEmpty.SetEncoder(TestFormat.GlobalTestFormat, encoder2);
        ImageEncoder found2 = this.FormatsManagerEmpty.FindEncoder(TestFormat.GlobalTestFormat);
        Assert.Equal(encoder2, found2);
        Assert.NotEqual(found, found2);
    }

    [Fact]
    public void RegisterMimeTypeDecoderReplacesLast()
    {
        ImageDecoder decoder1 = new Mock<ImageDecoder>().Object;
        this.FormatsManagerEmpty.SetDecoder(TestFormat.GlobalTestFormat, decoder1);
        ImageDecoder found = this.FormatsManagerEmpty.FindDecoder(TestFormat.GlobalTestFormat);
        Assert.Equal(decoder1, found);

        ImageDecoder decoder2 = new Mock<ImageDecoder>().Object;
        this.FormatsManagerEmpty.SetDecoder(TestFormat.GlobalTestFormat, decoder2);
        ImageDecoder found2 = this.FormatsManagerEmpty.FindDecoder(TestFormat.GlobalTestFormat);
        Assert.Equal(decoder2, found2);
        Assert.NotEqual(found, found2);
    }

    [Fact]
    public void AddFormatCallsConfig()
    {
        Mock<IConfigurationModule> provider = new();
        Configuration config = new();
        config.Configure(provider.Object);

        provider.Verify(x => x.Configure(config));
    }

    [Fact]
    public void DetectFormatAllocatesCleanBuffer()
    {
        byte[] jpegImage;
        using (MemoryStream buffer = new())
        {
            using Image<Rgba32> image = new(100, 100);
            image.SaveAsJpeg(buffer);
            jpegImage = buffer.ToArray();
        }

        byte[] invalidImage = { 1, 2, 3 };

        Assert.Equal(Image.DetectFormat(jpegImage), JpegFormat.Instance);
        Assert.True(Image.DetectFormat(invalidImage) is null);
    }
}

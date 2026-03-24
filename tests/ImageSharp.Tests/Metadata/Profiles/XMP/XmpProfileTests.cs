// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;
using System.Xml.Linq;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Xmp;

public class XmpProfileTests
{
    [Theory]
    [WithFile(TestImages.Gif.Receipt, PixelTypes.Rgba32)]
    public async Task ReadXmpMetadata_FromGif_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = await provider.GetImageAsync(GifDecoder.Instance))
        {
            XmpProfile actual = image.Metadata.XmpProfile ?? image.Frames.RootFrame.Metadata.XmpProfile;
            XmpProfileContainsExpectedValues(actual);
        }
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Lake, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.Baseline.Metadata, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.Baseline.ExtendedXmp, PixelTypes.Rgba32)]
    public async Task ReadXmpMetadata_FromJpg_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = await provider.GetImageAsync(JpegDecoder.Instance))
        {
            XmpProfile actual = image.Metadata.XmpProfile ?? image.Frames.RootFrame.Metadata.XmpProfile;
            XmpProfileContainsExpectedValues(actual);
        }
    }

    [Theory]
    [WithFile(TestImages.Png.XmpColorPalette, PixelTypes.Rgba32)]
    public async Task ReadXmpMetadata_FromPng_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = await provider.GetImageAsync(PngDecoder.Instance))
        {
            XmpProfile actual = image.Metadata.XmpProfile ?? image.Frames.RootFrame.Metadata.XmpProfile;
            XmpProfileContainsExpectedValues(actual);
        }
    }

    [Theory]
    [WithFile(TestImages.Tiff.SampleMetadata, PixelTypes.Rgba32)]
    public async Task ReadXmpMetadata_FromTiff_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = await provider.GetImageAsync(TiffDecoder.Instance))
        {
            XmpProfile actual = image.Metadata.XmpProfile ?? image.Frames.RootFrame.Metadata.XmpProfile;
            XmpProfileContainsExpectedValues(actual);
        }
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossy.WithXmp, PixelTypes.Rgba32)]
    public async Task ReadXmpMetadata_FromWebp_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = await provider.GetImageAsync(WebpDecoder.Instance))
        {
            XmpProfile actual = image.Metadata.XmpProfile ?? image.Frames.RootFrame.Metadata.XmpProfile;
            XmpProfileContainsExpectedValues(actual);
        }
    }

    [Fact]
    public void XmpProfile_CtorFromXDocument_Works()
    {
        // arrange
        XDocument document = CreateMinimalXDocument();

        // act
        XmpProfile profile = new(document);

        // assert
        XmpProfileContainsExpectedValues(profile);
    }

    [Fact]
    public void XmpProfile_ToXDocument_ReturnsValidDocument()
    {
        // arrange
        XmpProfile profile = CreateMinimalXmlProfile();

        // act
        XDocument document = profile.ToXDocument();

        // assert
        Assert.NotNull(document);
        Assert.Equal("xmpmeta", document.Root.Name.LocalName);
        Assert.Equal("adobe:ns:meta/", document.Root.Name.NamespaceName);
    }

    [Fact]
    public void XmpProfile_ToFromByteArray_ReturnsClone()
    {
        // arrange
        XmpProfile profile = CreateMinimalXmlProfile();
        byte[] original = profile.ToByteArray();

        // act
        byte[] actual = profile.ToByteArray();

        // assert
        Assert.False(ReferenceEquals(original, actual));
    }

    [Fact]
    public void XmpProfile_CloneIsDeep()
    {
        // arrange
        XmpProfile profile = CreateMinimalXmlProfile();
        byte[] original = profile.Data;

        // act
        XmpProfile clone = profile.DeepClone();
        byte[] actual = clone.Data;

        // assert
        Assert.False(ReferenceEquals(original, actual));
    }

    [Fact]
    public void WritingGif_PreservesXmpProfile()
    {
        // arrange
        using Image<Rgba32> image = new(1, 1);
        XmpProfile original = CreateMinimalXmlProfile();
        image.Metadata.XmpProfile = original;
        GifEncoder encoder = new();

        // act
        using Image<Rgba32> reloadedImage = WriteAndRead(image, encoder);

        // assert
        XmpProfile actual = reloadedImage.Metadata.XmpProfile ?? reloadedImage.Frames.RootFrame.Metadata.XmpProfile;
        XmpProfileContainsExpectedValues(actual);
        Assert.Equal(original.Data, actual.Data);
    }

    [Fact]
    public void WritingJpeg_PreservesXmpProfile()
    {
        // arrange
        using Image<Rgba32> image = new(1, 1);
        XmpProfile original = CreateMinimalXmlProfile();
        image.Metadata.XmpProfile = original;
        JpegEncoder encoder = new();

        // act
        using Image<Rgba32> reloadedImage = WriteAndRead(image, encoder);

        // assert
        XmpProfile actual = reloadedImage.Metadata.XmpProfile ?? reloadedImage.Frames.RootFrame.Metadata.XmpProfile;
        XmpProfileContainsExpectedValues(actual);
        Assert.Equal(original.Data, actual.Data);
    }

    [Fact]
    public async Task WritingJpeg_PreservesExtendedXmpProfile()
    {
        // arrange
        TestImageProvider<Rgba32> provider = TestImageProvider<Rgba32>.File(TestImages.Jpeg.Baseline.ExtendedXmp);
        using Image<Rgba32> image = await provider.GetImageAsync(JpegDecoder.Instance);
        XmpProfile original = image.Metadata.XmpProfile;
        JpegEncoder encoder = new();

        // act
        using Image<Rgba32> reloadedImage = WriteAndRead(image, encoder);

        // assert
        XmpProfile actual = reloadedImage.Metadata.XmpProfile ?? reloadedImage.Frames.RootFrame.Metadata.XmpProfile;
        XmpProfileContainsExpectedValues(actual);
        Assert.Equal(original.Data, actual.Data);
    }

    [Fact]
    public void WritingPng_PreservesXmpProfile()
    {
        // arrange
        using Image<Rgba32> image = new(1, 1);
        XmpProfile original = CreateMinimalXmlProfile();
        image.Metadata.XmpProfile = original;
        PngEncoder encoder = new();

        // act
        using Image<Rgba32> reloadedImage = WriteAndRead(image, encoder);

        // assert
        XmpProfile actual = reloadedImage.Metadata.XmpProfile ?? reloadedImage.Frames.RootFrame.Metadata.XmpProfile;
        XmpProfileContainsExpectedValues(actual);
        Assert.Equal(original.Data, actual.Data);
    }

    [Fact]
    public void WritingTiff_PreservesXmpProfile()
    {
        // arrange
        using Image<Rgba32> image = new(1, 1);
        XmpProfile original = CreateMinimalXmlProfile();
        image.Frames.RootFrame.Metadata.XmpProfile = original;
        TiffEncoder encoder = new();

        // act
        using Image<Rgba32> reloadedImage = WriteAndRead(image, encoder);

        // assert
        XmpProfile actual = reloadedImage.Metadata.XmpProfile ?? reloadedImage.Frames.RootFrame.Metadata.XmpProfile;
        XmpProfileContainsExpectedValues(actual);
        Assert.Equal(original.Data, actual.Data);
    }

    [Fact]
    public void WritingWebp_PreservesXmpProfile()
    {
        // arrange
        using Image<Rgba32> image = new(1, 1);
        XmpProfile original = CreateMinimalXmlProfile();
        image.Metadata.XmpProfile = original;
        WebpEncoder encoder = new();

        // act
        using Image<Rgba32> reloadedImage = WriteAndRead(image, encoder);

        // assert
        XmpProfile actual = reloadedImage.Metadata.XmpProfile ?? reloadedImage.Frames.RootFrame.Metadata.XmpProfile;
        XmpProfileContainsExpectedValues(actual);
        Assert.Equal(original.Data, actual.Data);
    }

    private static void XmpProfileContainsExpectedValues(XmpProfile xmp)
    {
        Assert.NotNull(xmp);
        XDocument document = xmp.ToXDocument();
        Assert.NotNull(document);
        Assert.Equal("xmpmeta", document.Root.Name.LocalName);
        Assert.Equal("adobe:ns:meta/", document.Root.Name.NamespaceName);
    }

    private static XmpProfile CreateMinimalXmlProfile()
    {
        string content = $"<?xpacket begin='' id='{Guid.NewGuid()}'?><x:xmpmeta xmlns:x='adobe:ns:meta/'></x:xmpmeta><?xpacket end='w'?> ";
        byte[] data = Encoding.UTF8.GetBytes(content);
        XmpProfile profile = new(data);
        return profile;
    }

    private static XDocument CreateMinimalXDocument() => CreateMinimalXmlProfile().ToXDocument();

    private static Image<Rgba32> WriteAndRead(Image<Rgba32> image, IImageEncoder encoder)
    {
        using (MemoryStream memStream = new())
        {
            image.Save(memStream, encoder);
            image.Dispose();

            memStream.Position = 0;
            return Image.Load<Rgba32>(memStream);
        }
    }
}

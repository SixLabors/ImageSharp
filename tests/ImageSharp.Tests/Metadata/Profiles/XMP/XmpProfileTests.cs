// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Xmp
{
    public class XmpProfileTests
    {
        private static GifDecoder GifDecoder => new() { IgnoreMetadata = false };

        private static JpegDecoder JpegDecoder => new() { IgnoreMetadata = false };

        private static PngDecoder PngDecoder => new() { IgnoreMetadata = false };

        private static TiffDecoder TiffDecoder => new() { IgnoreMetadata = false };

        private static WebpDecoder WebpDecoder => new() { IgnoreMetadata = false };

        [Theory]
        [WithFile(TestImages.Gif.Receipt, PixelTypes.Rgba32)]
        public async Task ReadXmpMetadata_FromGif_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = await provider.GetImageAsync(GifDecoder))
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
            using (Image<TPixel> image = await provider.GetImageAsync(JpegDecoder))
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
            using (Image<TPixel> image = await provider.GetImageAsync(PngDecoder))
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
            using (Image<TPixel> image = await provider.GetImageAsync(TiffDecoder))
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
            using (Image<TPixel> image = await provider.GetImageAsync(WebpDecoder))
            {
                XmpProfile actual = image.Metadata.XmpProfile ?? image.Frames.RootFrame.Metadata.XmpProfile;
                XmpProfileContainsExpectedValues(actual);
            }
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
            byte[] original = profile.ToByteArray();

            // act
            XmpProfile clone = profile.DeepClone();
            byte[] actual = clone.ToByteArray();

            // assert
            Assert.False(ReferenceEquals(original, actual));
        }

        [Fact]
        public void WritingGif_PreservesXmpProfile()
        {
            // arrange
            var image = new Image<Rgba32>(1, 1);
            XmpProfile original = CreateMinimalXmlProfile();
            image.Metadata.XmpProfile = original;
            var encoder = new GifEncoder();

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
            var image = new Image<Rgba32>(1, 1);
            XmpProfile original = CreateMinimalXmlProfile();
            image.Metadata.XmpProfile = original;
            var encoder = new JpegEncoder();

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
            var provider = TestImageProvider<Rgba32>.File(TestImages.Jpeg.Baseline.ExtendedXmp);
            using Image<Rgba32> image = await provider.GetImageAsync(JpegDecoder);
            XmpProfile original = image.Metadata.XmpProfile;
            var encoder = new JpegEncoder();

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
            var image = new Image<Rgba32>(1, 1);
            XmpProfile original = CreateMinimalXmlProfile();
            image.Metadata.XmpProfile = original;
            var encoder = new PngEncoder();

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
            var image = new Image<Rgba32>(1, 1);
            XmpProfile original = CreateMinimalXmlProfile();
            image.Frames.RootFrame.Metadata.XmpProfile = original;
            var encoder = new TiffEncoder();

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
            var image = new Image<Rgba32>(1, 1);
            XmpProfile original = CreateMinimalXmlProfile();
            image.Metadata.XmpProfile = original;
            var encoder = new WebpEncoder();

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
            XDocument document = xmp.GetDocument();
            Assert.NotNull(document);
            Assert.Equal("xmpmeta", document.Root.Name.LocalName);
            Assert.Equal("adobe:ns:meta/", document.Root.Name.NamespaceName);
        }

        private static XmpProfile CreateMinimalXmlProfile()
        {
            string content = $"<?xpacket begin='' id='{Guid.NewGuid()}'?><x:xmpmeta xmlns:x='adobe:ns:meta/'></x:xmpmeta><?xpacket end='w'?> ";
            byte[] data = Encoding.UTF8.GetBytes(content);
            var profile = new XmpProfile(data);
            return profile;
        }

        private static Image<Rgba32> WriteAndRead(Image<Rgba32> image, IImageEncoder encoder)
        {
            using (var memStream = new MemoryStream())
            {
                image.Save(memStream, encoder);
                image.Dispose();

                memStream.Position = 0;
                return Image.Load<Rgba32>(memStream);
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Experimental.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Webp
{
    [Trait("Format", "Webp")]
    public class WebpMetaDataTests
    {
        private readonly Configuration configuration;

        private static WebpDecoder WebpDecoder => new WebpDecoder() { IgnoreMetadata = false };

        public WebpMetaDataTests()
        {
            this.configuration = new Configuration();
            this.configuration.AddWebp();
        }

        [Theory]
        [WithFile(TestImages.WebP.Lossy.WithExif, PixelTypes.Rgba32, false)]
        [WithFile(TestImages.WebP.Lossy.WithExif, PixelTypes.Rgba32, true)]
        [WithFile(TestImages.WebP.Lossless.WithExif, PixelTypes.Rgba32, false)]
        [WithFile(TestImages.WebP.Lossless.WithExif, PixelTypes.Rgba32, true)]
        public void IgnoreMetadata_ControlsWhetherExifIsParsed<TPixel>(TestImageProvider<TPixel> provider, bool ignoreMetadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new WebpDecoder { IgnoreMetadata = ignoreMetadata };

            using Image<TPixel> image = provider.GetImage(decoder);
            if (ignoreMetadata)
            {
                Assert.Null(image.Metadata.ExifProfile);
            }
            else
            {
                ExifProfile exifProfile = image.Metadata.ExifProfile;
                Assert.NotNull(exifProfile);
                Assert.NotEmpty(exifProfile.Values);
                Assert.Contains(exifProfile.Values, m => m.Tag.Equals(ExifTag.Make) && m.GetValue().Equals("Canon"));
                Assert.Contains(exifProfile.Values, m => m.Tag.Equals(ExifTag.Model) && m.GetValue().Equals("Canon PowerShot S40"));
                Assert.Contains(exifProfile.Values, m => m.Tag.Equals(ExifTag.Software) && m.GetValue().Equals("GIMP 2.10.2"));
            }
        }

        [Theory]
        [WithFile(TestImages.WebP.Lossy.WithIccp, PixelTypes.Rgba32, false)]
        [WithFile(TestImages.WebP.Lossy.WithIccp, PixelTypes.Rgba32, true)]
        [WithFile(TestImages.WebP.Lossless.WithIccp, PixelTypes.Rgba32, false)]
        [WithFile(TestImages.WebP.Lossless.WithIccp, PixelTypes.Rgba32, true)]
        public void IgnoreMetadata_ControlsWhetherIccpIsParsed<TPixel>(TestImageProvider<TPixel> provider, bool ignoreMetadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new WebpDecoder { IgnoreMetadata = ignoreMetadata };

            using Image<TPixel> image = provider.GetImage(decoder);
            if (ignoreMetadata)
            {
                Assert.Null(image.Metadata.IccProfile);
            }
            else
            {
                Assert.NotNull(image.Metadata.IccProfile);
                Assert.NotEmpty(image.Metadata.IccProfile.Entries);
            }
        }

        [Theory]
        [WithFile(TestImages.WebP.Lossy.WithExif, PixelTypes.Rgba32)]
        public void EncodeLossyWebp_PreservesExif<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // arrange
            using Image<TPixel> input = provider.GetImage(WebpDecoder);
            using var memoryStream = new MemoryStream();
            ExifProfile expectedExif = input.Metadata.ExifProfile;

            // act
            input.Save(memoryStream, new WebpEncoder() { Lossy = true });
            memoryStream.Position = 0;

            // assert
            using Image<Rgba32> image = WebpDecoder.Decode<Rgba32>(this.configuration, memoryStream);
            ExifProfile actualExif = image.Metadata.ExifProfile;
            Assert.NotNull(actualExif);
            Assert.Equal(expectedExif.Values.Count, actualExif.Values.Count);
        }

        [Theory]
        [WithFile(TestImages.WebP.Lossless.WithExif, PixelTypes.Rgba32)]
        public void EncodeLosslessWebp_PreservesExif<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // arrange
            using Image<TPixel> input = provider.GetImage(WebpDecoder);
            using var memoryStream = new MemoryStream();
            ExifProfile expectedExif = input.Metadata.ExifProfile;

            // act
            input.Save(memoryStream, new WebpEncoder() { Lossy = false });
            memoryStream.Position = 0;

            // assert
            using Image<Rgba32> image = WebpDecoder.Decode<Rgba32>(this.configuration, memoryStream);
            ExifProfile actualExif = image.Metadata.ExifProfile;
            Assert.NotNull(actualExif);
            Assert.Equal(expectedExif.Values.Count, actualExif.Values.Count);
        }
    }
}

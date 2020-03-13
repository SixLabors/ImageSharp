// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.WebP;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    public class WebPMetadataTests
    {
        [Theory]
        [WithFile(TestImages.WebP.Lossless.WithExif, PixelTypes.Rgba32, false)]
        [WithFile(TestImages.WebP.Lossy.WithExif, PixelTypes.Rgba32, true)]
        public void IgnoreMetadata_ControlsWhetherExifIsParsed<TPixel>(TestImageProvider<TPixel> provider, bool ignoreMetadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new WebPDecoder { IgnoreMetadata = ignoreMetadata };

            using (Image<TPixel> image = provider.GetImage(decoder))
            {
                if (ignoreMetadata)
                {
                    Assert.Null(image.Metadata.ExifProfile);
                }
                else
                {
                    Assert.NotNull(image.Metadata.ExifProfile);
                    Assert.NotEmpty(image.Metadata.ExifProfile.Values);
                }
            }
        }

        [Theory]
        [WithFile(TestImages.WebP.Lossless.WithIccp, PixelTypes.Rgba32, false)]
        [WithFile(TestImages.WebP.Lossy.WithIccp, PixelTypes.Rgba32, true)]
        public void IgnoreMetadata_ControlsWhetherIccpIsParsed<TPixel>(TestImageProvider<TPixel> provider, bool ignoreMetadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new WebPDecoder { IgnoreMetadata = ignoreMetadata };

            using (Image<TPixel> image = provider.GetImage(decoder))
            {
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
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Category", "Tiff.BlackBox.Decoder")]
    [Trait("Category", "Tiff")]
    public class TiffDecoderTests
    {
        public static readonly string[] SingleTestImages = TestImages.Tiff.All;

        public static readonly string[] MultiframeTestImages = TestImages.Tiff.Multiframes;

        public static readonly string[] NotSupportedImages = TestImages.Tiff.NotSupported;

        [Theory]
        [WithFileCollection(nameof(NotSupportedImages), PixelTypes.Rgba32)]
        public void ThrowsNotSupported<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        {
            Assert.Throws<NotSupportedException>(() => provider.GetImage(new TiffDecoder()));
        }

        [Theory]
        [WithFileCollection(nameof(SingleTestImages), PixelTypes.Rgba32)]
        public void Decode<TPixel>(TestImageProvider<TPixel> provider)
          where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TiffDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFileCollection(nameof(MultiframeTestImages), PixelTypes.Rgba32)]
        public void DecodeMultiframe<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TiffDecoder()))
            {
                Assert.True(image.Frames.Count > 1);

                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact, new MagickReferenceDecoder());

                image.DebugSaveMultiFrame(provider);
                image.CompareToOriginalMultiFrame(provider, ImageComparer.Exact, new MagickReferenceDecoder());
            }
        }
    }
}

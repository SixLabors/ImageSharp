// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming


using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Category", "Tiff.BlackBox")]
    public class TiffDecoderTests
    {
        public static readonly string[] CommonTestImages = TestImages.Tiff.All;

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32)]
        public void Decode<TPixel>(TestImageProvider<TPixel> provider)
          where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TiffDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact, new MagickReferenceDecoder());
            }
        }
    }
}

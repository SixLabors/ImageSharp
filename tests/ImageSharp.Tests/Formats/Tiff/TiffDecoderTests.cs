// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System.IO;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
#if DEBUG
    [Trait("Category", "Tiff_BlackBox")]
    public class TiffDecoderTests
    {
        public static readonly string[] CommonTestImages = TestImages.Tiff.All;

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32)]
        public void Decode<TPixel>(TestImageProvider<TPixel> provider)
          where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TiffDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Fact]
        public void DecodeManual()
        {
            string path = @"C:\Work\GitHub\SixLabors.ImageSharp\tests\Images\Input\Tiff\";
            string file = path + "jpeg444_small_rgb_deflate.tiff";
            string outFile = path + "jpeg444_small_rgb_deflate.tiff__.png";
            using (var fs = new FileStream(file, FileMode.Open))
            using (var outfs = new FileStream(outFile, FileMode.Create))
            using (var image = new TiffDecoder().Decode<Rgba32>(Configuration.Default, fs))
            {
                image.Save(outfs, new PngEncoder());
            }
        }
    }
#endif
}

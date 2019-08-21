// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming


using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Category", "Tiff_BlackBox")]
    public class TiffDecoderTests
    {
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
}

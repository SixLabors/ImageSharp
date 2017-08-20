// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class PngEncoderTests : FileTestBase
    {
        private const PixelTypes PixelTypes = Tests.PixelTypes.Rgba32 | Tests.PixelTypes.RgbaVector | Tests.PixelTypes.Argb32;

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes, PngColorType.RgbWithAlpha)]
        [WithTestPatternImages(100, 100, PixelTypes, PngColorType.Rgb)]
        [WithTestPatternImages(100, 100, PixelTypes, PngColorType.Palette)]
        [WithTestPatternImages(100, 100, PixelTypes, PngColorType.Grayscale)]
        [WithTestPatternImages(100, 100, PixelTypes, PngColorType.GrayscaleWithAlpha)]
        public void EncodeGeneratedPatterns<TPixel>(TestImageProvider<TPixel> provider, PngColorType pngColorType)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                PngEncoder options = new PngEncoder()
                                                {
                                                    PngColorType = pngColorType
                                                };
                provider.Utility.TestName += "_" + pngColorType;

                provider.Utility.SaveTestOutputFile(image, "png", options);
            }
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.All)]
        public void WritesFileMarker<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, new PngEncoder());
                
                byte[] data = ms.ToArray().Take(8).ToArray(); 
                byte[] expected = {
                    0x89, // Set the high bit.
                    0x50, // P
                    0x4E, // N
                    0x47, // G
                    0x0D, // Line ending CRLF
                    0x0A, // Line ending CRLF
                    0x1A, // EOF
                    0x0A // LF
                };

                Assert.Equal(expected, data);
            }
        }
    }
}
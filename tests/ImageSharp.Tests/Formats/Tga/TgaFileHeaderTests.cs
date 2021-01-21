// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tga;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tga
{
    [Trait("Format", "Tga")]
    public class TgaFileHeaderTests
    {
        [Theory]
        [InlineData(new byte[] { 0, 0, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 250, 0, 195, 0, 32, 8 })] // invalid tga image type.
        [InlineData(new byte[] { 0, 3, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 250, 0, 195, 0, 32, 8 })] // invalid colormap type.
        [InlineData(new byte[] { 0, 0, 1, 5, 5, 5, 5, 5, 0, 0, 0, 0, 250, 0, 195, 0, 32, 8 })] // valid colormap type (0), but colomap spec bytes should all be zero.
        [InlineData(new byte[] { 0, 0, 1, 0, 0, 0, 0, 8, 0, 0, 0, 0, 250, 0, 195, 0, 32, 8 })] // valid colormap type (0), but colomap spec bytes should all be zero.
        [InlineData(new byte[] { 0, 0, 1, 0, 0, 0, 7, 0, 0, 0, 0, 0, 250, 0, 195, 0, 32, 8 })] // valid colormap type (0), but colomap spec bytes should all be zero.
        [InlineData(new byte[] { 0, 0, 1, 0, 0, 6, 0, 0, 0, 0, 0, 0, 250, 0, 195, 0, 32, 8 })] // valid colormap type (0), but colomap spec bytes should all be zero.
        [InlineData(new byte[] { 0, 0, 1, 0, 5, 0, 0, 0, 0, 0, 0, 0, 250, 0, 195, 0, 32, 8 })] // valid colormap type (0), but colomap spec bytes should all be zero.
        [InlineData(new byte[] { 0, 0, 0, 12, 106, 80, 32, 32, 13, 10, 135, 10, 0, 0, 0, 20, 102, 116 })] // jp2 image header
        [InlineData(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 195, 0, 32, 8 })] // invalid width
        [InlineData(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 250, 0, 0, 0, 32, 8 })] // invalid height
        public void ImageLoad_WithNoValidTgaHeaderBytes_Throws_UnknownImageFormatException(byte[] data)
        {
            using var stream = new MemoryStream(data);

            Assert.Throws<UnknownImageFormatException>(() =>
            {
                using (Image.Load(Configuration.Default, stream, out IImageFormat _))
                {
                }
            });
        }

        [Theory]
        [InlineData(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 250, 0, 195, 0, 32, 8 }, 250, 195, TgaBitsPerPixel.Pixel32)]
        [InlineData(new byte[] { 26, 1, 9, 0, 0, 0, 1, 16, 0, 0, 0, 0, 128, 0, 128, 0, 8, 0 }, 128, 128, TgaBitsPerPixel.Pixel8)]
        [InlineData(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 220, 0, 220, 0, 16, 0 }, 220, 220, TgaBitsPerPixel.Pixel16)]
        [InlineData(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 124, 0, 124, 0, 24, 32 }, 124, 124, TgaBitsPerPixel.Pixel24)]
        public void Identify_WithValidData_Works(byte[] data, int width, int height, TgaBitsPerPixel bitsPerPixel)
        {
            using var stream = new MemoryStream(data);

            IImageInfo info = Image.Identify(stream);
            TgaMetadata tgaData = info.Metadata.GetTgaMetadata();
            Assert.Equal(bitsPerPixel, tgaData.BitsPerPixel);
            Assert.Equal(width, info.Width);
            Assert.Equal(height, info.Height);
        }
    }
}

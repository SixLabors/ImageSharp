// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tga
{
    using static TestImages.Tga;

    public class TgaEncoderTests
    {
        public static readonly TheoryData<string, TgaBitsPerPixel> TgaBitsPerPixelFiles =
            new TheoryData<string, TgaBitsPerPixel>
            {
                { Grey, TgaBitsPerPixel.Pixel8 },
                { Bit32, TgaBitsPerPixel.Pixel32 },
                { Bit24, TgaBitsPerPixel.Pixel24 },
                { Bit16, TgaBitsPerPixel.Pixel16 },
            };

        [Theory]
        [MemberData(nameof(TgaBitsPerPixelFiles))]
        public void Encode_PreserveBitsPerPixel(string imagePath, TgaBitsPerPixel bmpBitsPerPixel)
        {
            var options = new TgaEncoder();

            TestFile testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);
                    memStream.Position = 0;
                    using (Image<Rgba32> output = Image.Load<Rgba32>(memStream))
                    {
                        TgaMetadata meta = output.Metadata.GetFormatMetadata(TgaFormat.Instance);
                        Assert.Equal(bmpBitsPerPixel, meta.BitsPerPixel);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(TgaBitsPerPixelFiles))]
        public void Encode_WithCompression_PreserveBitsPerPixel(string imagePath, TgaBitsPerPixel bmpBitsPerPixel)
        {
            var options = new TgaEncoder()
                          {
                              Compress = true
                          };

            TestFile testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);
                    memStream.Position = 0;
                    using (Image<Rgba32> output = Image.Load<Rgba32>(memStream))
                    {
                        TgaMetadata meta = output.Metadata.GetFormatMetadata(TgaFormat.Instance);
                        Assert.Equal(bmpBitsPerPixel, meta.BitsPerPixel);
                    }
                }
            }
        }
    }
}

// <copyright file="PngEncoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using ImageSharp.Formats;

namespace ImageSharp.Tests
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using ImageSharp.IO;
    using Xunit;

    public class PngEncoderTests : FileTestBase
    {
        [Fact(Skip ="Slow intergration test")]
        public void ImageCanSaveIndexedPng()
        {
            string path = CreateOutputDirectory("Png", "Indexed");

            foreach (TestFile file in Files)
            {
                using (Image image = file.CreateImage())
                {
                    using (FileStream output = File.OpenWrite($"{path}/{file.FileNameWithoutExtension}.png"))
                    {
                        image.MetaData.Quality = 256;
                        image.Save(output, new PngFormat());
                    }
                }
            }
        }

        [Fact(Skip = "Slow intergration test")]
        public void ImageCanSavePngInParallel()
        {
            string path = this.CreateOutputDirectory("Png");

            Parallel.ForEach(
                Files,
                file =>
                    {
                        using (Image image = file.CreateImage())
                        {
                            using (FileStream output = File.OpenWrite($"{path}/{file.FileNameWithoutExtension}.png"))
                            {
                                image.SaveAsPng(output);
                            }
                        }
                    });
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.All)]
        public void WritesFileMarker<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPixel<TColor>
        {
            using (Image<TColor> image = provider.GetImage())
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
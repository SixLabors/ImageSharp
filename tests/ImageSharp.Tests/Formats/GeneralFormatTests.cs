// <copyright file="GeneralFormatTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using ImageSharp.PixelFormats;

    using Xunit;

    public class GeneralFormatTests : FileTestBase
    {
        [Theory]
        [WithFileCollection(nameof(DefaultFiles), DefaultPixelType)]
        public void ResolutionShouldChange<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.MetaData.VerticalResolution = 150;
                image.MetaData.HorizontalResolution = 150;
                image.DebugSave(provider);
            }
        }

        [Fact]
        public void ImageCanEncodeToString()
        {
            string path = this.CreateOutputDirectory("ToString");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateImage())
                {
                    string filename = path + "/" + file.FileNameWithoutExtension + ".txt";
                    File.WriteAllText(filename, image.ToBase64String());
                }
            }
        }

        [Fact]
        public void DecodeThenEncodeImageFromStreamShouldSucceed()
        {
            string path = this.CreateOutputDirectory("Encode");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateImage())
                {
                    using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                    {
                        image.Save(output);
                    }
                }
            }
        }

        [Fact]
        public void QuantizeImageShouldPreserveMaximumColorPrecision()
        {
            string path = this.CreateOutputDirectory("Quantize");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> srcImage = file.CreateImage())
                {
                    using (Image<Rgba32> image = new Image<Rgba32>(srcImage))
                    {
                        using (FileStream output = File.OpenWrite($"{path}/Octree-{file.FileName}"))
                        {
                            image.Quantize(Quantization.Octree)
                                .Save(output, image.CurrentImageFormat);

                        }
                    }

                    using (Image<Rgba32> image = new Image<Rgba32>(srcImage))
                    {
                        using (FileStream output = File.OpenWrite($"{path}/Wu-{file.FileName}"))
                        {
                            image.Quantize(Quantization.Wu)
                                .Save(output, image.CurrentImageFormat);
                        }
                    }

                    using (Image<Rgba32> image = new Image<Rgba32>(srcImage))
                    {
                        using (FileStream output = File.OpenWrite($"{path}/Palette-{file.FileName}"))
                        {
                            image.Quantize(Quantization.Palette)
                                .Save(output, image.CurrentImageFormat);
                        }
                    }
                }
            }
        }

        [Fact]
        public void ImageCanConvertFormat()
        {
            string path = this.CreateOutputDirectory("Format");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateImage())
                {
                    using (FileStream output = File.OpenWrite($"{path}/{file.FileNameWithoutExtension}.bmp"))
                    {
                        image.SaveAsBmp(output);
                    }

                    using (FileStream output = File.OpenWrite($"{path}/{file.FileNameWithoutExtension}.jpg"))
                    {
                        image.SaveAsJpeg(output);
                    }

                    using (FileStream output = File.OpenWrite($"{path}/{file.FileNameWithoutExtension}.png"))
                    {
                        image.SaveAsPng(output);
                    }

                    using (FileStream output = File.OpenWrite($"{path}/{file.FileNameWithoutExtension}.gif"))
                    {
                        image.SaveAsGif(output);
                    }
                }
            }
        }

        [Fact]
        public void ImageShouldPreservePixelByteOrderWhenSerialized()
        {
            string path = this.CreateOutputDirectory("Serialized");

            foreach (TestFile file in Files)
            {
                byte[] serialized;
                using (Image<Rgba32> image = file.CreateImage())
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    image.Save(memoryStream);
                    memoryStream.Flush();
                    serialized = memoryStream.ToArray();
                }

                using (Image<Rgba32> image2 = Image.Load<Rgba32>(serialized))
                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image2.Save(output);
                }
            }
        }
    }
}
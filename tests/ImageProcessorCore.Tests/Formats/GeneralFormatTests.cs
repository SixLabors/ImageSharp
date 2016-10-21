// <copyright file="GeneralFormatTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System;
    using System.IO;

    using Xunit;

    public class GeneralFormatTests : FileTestBase
    {
        [Fact]
        public void ResolutionShouldChange()
        {
            string path = CreateOutputDirectory("Resolution");

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.VerticalResolution = 150;
                    image.HorizontalResolution = 150;
                    image.Save(output);
                }
            }
        }

        [Fact]
        public void ImageCanEncodeToString()
        {
            string path = CreateOutputDirectory("ToString");

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                string filename = path + "/" + file.FileNameWithoutExtension + ".txt";
                File.WriteAllText(filename, image.ToString());
            }
        }

        [Fact]
        public void DecodeThenEncodeImageFromStreamShouldSucceed()
        {
            string path = CreateOutputDirectory("Encode");

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Save(output);
                }
            }
        }

        [Fact]
        public void QuantizeImageShouldPreserveMaximumColorPrecision()
        {
            string path = CreateOutputDirectory("Quantize");

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                // Copy the original pixels to save decoding time.
                Color[] pixels = new Color[image.Width * image.Height];
                Array.Copy(image.Pixels, pixels, image.Pixels.Length);

                using (FileStream output = File.OpenWrite($"{path}/Octree-{file.FileName}"))
                {
                    image.Quantize(Quantization.Octree)
                          .Save(output, image.CurrentImageFormat);

                }

                image.SetPixels(image.Width, image.Height, pixels);
                using (FileStream output = File.OpenWrite($"{path}/Wu-{file.FileName}"))
                {
                    image.Quantize(Quantization.Wu)
                          .Save(output, image.CurrentImageFormat);
                }

                image.SetPixels(image.Width, image.Height, pixels);
                using (FileStream output = File.OpenWrite($"{path}/Palette-{file.FileName}"))
                {
                    image.Quantize(Quantization.Palette)
                          .Save(output, image.CurrentImageFormat);
                }
            }
        }

        [Fact]
        public void ImageCanConvertFormat()
        {
            string path = CreateOutputDirectory("Format");

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileNameWithoutExtension}.gif"))
                {
                    image.SaveAsGif(output);
                }

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
            }
        }

        [Fact]
        public void ImageShouldPreservePixelByteOrderWhenSerialized()
        {
            string path = CreateOutputDirectory("Serialized");

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                byte[] serialized;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    image.Save(memoryStream);
                    memoryStream.Flush();
                    serialized = memoryStream.ToArray();
                }

                using (MemoryStream memoryStream = new MemoryStream(serialized))
                {
                    Image image2 = new Image(memoryStream);
                    using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                    {
                        image2.Save(output);
                    }
                }
            }
        }
    }
}
// <copyright file="EncoderDecoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.Diagnostics;
    using System.IO;

    using Formats;

    using Xunit;
    using System.Linq;

    using ImageProcessorCore.Quantizers;

    public class EncoderDecoderTests : FileTestBase
    {
        [Fact]
        public void ImageCanEncodeToString()
        {
            if (!Directory.Exists("TestOutput/ToString"))
            {
                Directory.CreateDirectory("TestOutput/ToString");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();

                    Image image = new Image(stream);
                    string filename = "TestOutput/ToString/" + Path.GetFileNameWithoutExtension(file) + ".txt";
                    File.WriteAllText(filename, image.ToString());

                    Trace.WriteLine($"{watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void DecodeThenEncodeImageFromStreamShouldSucceed()
        {
            if (!Directory.Exists("TestOutput/Encode"))
            {
                Directory.CreateDirectory("TestOutput/Encode");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();

                    Image image = new Image(stream);
                    string encodeFilename = "TestOutput/Encode/" + Path.GetFileName(file);

                    using (FileStream output = File.OpenWrite(encodeFilename))
                    {
                        image.Save(output);
                    }

                    Trace.WriteLine($"{file} : {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void QuantizeImageShouldPreserveMaximumColorPrecision()
        {
            if (!Directory.Exists("TestOutput/Quantize"))
            {
                Directory.CreateDirectory("TestOutput/Quantize");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Image image = new Image(stream);
                    IQuantizer quantizer = new OctreeQuantizer();
                    QuantizedImage quantizedImage = quantizer.Quantize(image, 256);

                    using (FileStream output = File.OpenWrite($"TestOutput/Quantize/Octree-{Path.GetFileName(file)}"))
                    {
                        Image qi = quantizedImage.ToImage();
                        qi.Save(output, image.CurrentImageFormat);

                    }

                    quantizer = new WuQuantizer();
                    quantizedImage = quantizer.Quantize(image, 256);

                    using (FileStream output = File.OpenWrite($"TestOutput/Quantize/Wu-{Path.GetFileName(file)}"))
                    {
                        quantizedImage.ToImage().Save(output, image.CurrentImageFormat);
                    }

                    quantizer = new PaletteQuantizer();
                    quantizedImage = quantizer.Quantize(image, 256);

                    using (FileStream output = File.OpenWrite($"TestOutput/Quantize/Palette-{Path.GetFileName(file)}"))
                    {
                        Image qi = quantizedImage.ToImage();
                        qi.Save(output, image.CurrentImageFormat);
                    }
                }
            }
        }

        [Fact]
        public void ImageCanConvertFormat()
        {
            if (!Directory.Exists("TestOutput/Format"))
            {
                Directory.CreateDirectory("TestOutput/Format");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/Format/{Path.GetFileNameWithoutExtension(file)}.gif"))
                    {
                        image.SaveAsGif(output);
                    }

                    using (FileStream output = File.OpenWrite($"TestOutput/Format/{Path.GetFileNameWithoutExtension(file)}.bmp"))
                    {
                        image.SaveAsBmp(output);
                    }

                    using (FileStream output = File.OpenWrite($"TestOutput/Format/{Path.GetFileNameWithoutExtension(file)}.jpg"))
                    {
                        image.SaveAsJpeg(output);
                    }

                    using (FileStream output = File.OpenWrite($"TestOutput/Format/{Path.GetFileNameWithoutExtension(file)}.png"))
                    {
                        image.SaveAsPng(output);
                    }
                }
            }
        }

        [Fact]
        public void ImageShouldPreservePixelByteOrderWhenSerialized()
        {
            if (!Directory.Exists("TestOutput/Serialized"))
            {
                Directory.CreateDirectory("TestOutput/Serialized");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Image image = new Image(stream);
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
                        using (FileStream output = File.OpenWrite($"TestOutput/Serialized/{Path.GetFileName(file)}"))
                        {
                            image2.Save(output);
                        }
                    }
                }
            }
        }
    }
}
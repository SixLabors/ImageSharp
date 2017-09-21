// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
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
            string path = TestEnvironment.CreateOutputDirectory("ToString");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateImage())
                {
                    string filename = path + "/" + file.FileNameWithoutExtension + ".txt";
                    File.WriteAllText(filename, image.ToBase64String(ImageFormats.Png));
                }
            }
        }

        [Fact]
        public void DecodeThenEncodeImageFromStreamShouldSucceed()
        {
            string path = TestEnvironment.CreateOutputDirectory("Encode");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateImage())
                {
                    image.Save($"{path}/{file.FileName}");
                }
            }
        }

        [Fact]
        public void QuantizeImageShouldPreserveMaximumColorPrecision()
        {
            string path = TestEnvironment.CreateOutputDirectory("Quantize");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> srcImage = Image.Load<Rgba32>(file.Bytes, out var mimeType))
                {
                    using (Image<Rgba32> image = srcImage.Clone())
                    {
                        using (FileStream output = File.OpenWrite($"{path}/Octree-{file.FileName}"))
                        {
                            image.Mutate(x => x.Quantize(Quantization.Octree));
                            image.Save(output, mimeType);

                        }
                    }

                    using (Image<Rgba32> image = srcImage.Clone())
                    {
                        using (FileStream output = File.OpenWrite($"{path}/Wu-{file.FileName}"))
                        {
                            image.Mutate(x => x.Quantize(Quantization.Wu));
                            image.Save(output, mimeType);
                        }
                    }

                    using (Image<Rgba32> image = srcImage.Clone())
                    {
                        using (FileStream output = File.OpenWrite($"{path}/Palette-{file.FileName}"))
                        {
                            image.Mutate(x => x.Quantize(Quantization.Palette));
                            image.Save(output, mimeType);
                        }
                    }
                }
            }
        }

        [Fact]
        public void ImageCanConvertFormat()
        {
            string path = TestEnvironment.CreateOutputDirectory("Format");

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
            string path = TestEnvironment.CreateOutputDirectory("Serialized");

            foreach (TestFile file in Files)
            {
                byte[] serialized;
                using (Image<Rgba32> image = Image.Load(file.Bytes, out IImageFormat mimeType))
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    image.Save(memoryStream, mimeType);
                    memoryStream.Flush();
                    serialized = memoryStream.ToArray();
                }

                using (Image<Rgba32> image2 = Image.Load<Rgba32>(serialized))
                {
                    image2.Save($"{path}/{file.FileName}");
                }
            }
        }
    }
}
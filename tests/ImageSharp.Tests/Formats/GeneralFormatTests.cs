// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.Reflection;

    using SixLabors.Memory;
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Processors.Quantization;

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
                    File.WriteAllText(filename, image.ToBase64String(PngFormat.Instance));
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

        public static readonly TheoryData<string> QuantizerNames =
            new TheoryData<string>
                {
                    nameof(KnownQuantizers.Octree),
                    nameof(KnownQuantizers.Palette),
                    nameof(KnownQuantizers.Wu)
                };

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, nameof(QuantizerNames), PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Bike, nameof(QuantizerNames), PixelTypes.Rgba32)]
        public void QuantizeImageShouldPreserveMaximumColorPrecision<TPixel>(TestImageProvider<TPixel> provider, string quantizerName)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.Configuration.MemoryAllocator = ArrayPoolMemoryAllocator.CreateWithModeratePooling();

            IQuantizer quantizer = GetQuantizer(quantizerName);

            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSave(provider, new PngEncoder() { ColorType = PngColorType.Palette, Quantizer = quantizer }, testOutputDetails: quantizerName);
            }

            provider.Configuration.MemoryAllocator.ReleaseRetainedResources();
        }

        private static IQuantizer GetQuantizer(string name)
        {
            PropertyInfo property = typeof(KnownQuantizers).GetTypeInfo().GetProperty(name);
            return (IQuantizer)property.GetMethod.Invoke(null, new object[0]);
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

        [Theory]
        [InlineData(10, 10, "png")]
        [InlineData(100, 100, "png")]
        [InlineData(100, 10, "png")]
        [InlineData(10, 100, "png")]
        [InlineData(10, 10, "gif")]
        [InlineData(100, 100, "gif")]
        [InlineData(100, 10, "gif")]
        [InlineData(10, 100, "gif")]
        [InlineData(10, 10, "bmp")]
        [InlineData(100, 100, "bmp")]
        [InlineData(100, 10, "bmp")]
        [InlineData(10, 100, "bmp")]
        [InlineData(10, 10, "jpg")]
        [InlineData(100, 100, "jpg")]
        [InlineData(100, 10, "jpg")]
        [InlineData(10, 100, "jpg")]
        public void CanIdentifyImageLoadedFromBytes(int width, int height, string format)
        {
            using (Image<Rgba32> image = Image.LoadPixelData(new Rgba32[width * height], width, height))
            {
                using (var memoryStream = new MemoryStream())
                {
                    image.Save(memoryStream, GetEncoder(format));
                    memoryStream.Position = 0;

                    var imageInfo = Image.Identify(memoryStream);

                    Assert.Equal(imageInfo.Width, width);
                    Assert.Equal(imageInfo.Height, height);
                }
            }
        }

        private static IImageEncoder GetEncoder(string format)
        {
            switch (format)
            {
                case "png":
                    return new PngEncoder();
                case "gif":
                    return new GifEncoder();
                case "bmp":
                    return new BmpEncoder();
                case "jpg":
                    return new JpegEncoder();
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
    }
}
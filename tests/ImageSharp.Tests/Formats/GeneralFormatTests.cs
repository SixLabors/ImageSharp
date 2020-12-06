// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats
{
    public class GeneralFormatTests
    {
        /// <summary>
        /// A collection made up of one file for each image format
        /// </summary>
        public static readonly IEnumerable<string> DefaultFiles =
            new[]
            {
                TestImages.Bmp.Car,
                TestImages.Jpeg.Baseline.Calliphora,
                TestImages.Png.Splash,
                TestImages.Gif.Trans
            };

        /// <summary>
        /// The collection of image files to test against.
        /// </summary>
        protected static readonly List<TestFile> Files = new List<TestFile>
        {
            TestFile.Create(TestImages.Jpeg.Baseline.Calliphora),
            TestFile.Create(TestImages.Bmp.Car),
            TestFile.Create(TestImages.Png.Splash),
            TestFile.Create(TestImages.Gif.Rings),
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), PixelTypes.Rgba32)]
        public void ResolutionShouldChange<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Metadata.VerticalResolution = 150;
                image.Metadata.HorizontalResolution = 150;
                image.DebugSave(provider);
            }
        }

        [Fact]
        public void ImageCanEncodeToString()
        {
            string path = TestEnvironment.CreateOutputDirectory("ToString");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateRgba32Image())
                {
                    string filename = Path.Combine(path, $"{file.FileNameWithoutExtension}.txt");
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
                using (Image<Rgba32> image = file.CreateRgba32Image())
                {
                    image.Save(Path.Combine(path, file.FileName));
                }
            }
        }

        public static readonly TheoryData<string> QuantizerNames =
            new TheoryData<string>
                {
                    nameof(KnownQuantizers.Octree),
                    nameof(KnownQuantizers.WebSafe),
                    nameof(KnownQuantizers.Werner),
                    nameof(KnownQuantizers.Wu)
                };

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, nameof(QuantizerNames), PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Bike, nameof(QuantizerNames), PixelTypes.Rgba32)]
        public void QuantizeImageShouldPreserveMaximumColorPrecision<TPixel>(TestImageProvider<TPixel> provider, string quantizerName)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.Configuration.MemoryAllocator = ArrayPoolMemoryAllocator.CreateWithModeratePooling();

            IQuantizer quantizer = GetQuantizer(quantizerName);

            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSave(provider, new PngEncoder { ColorType = PngColorType.Palette, Quantizer = quantizer }, testOutputDetails: quantizerName);
            }

            provider.Configuration.MemoryAllocator.ReleaseRetainedResources();
        }

        private static IQuantizer GetQuantizer(string name)
        {
            PropertyInfo property = typeof(KnownQuantizers).GetTypeInfo().GetProperty(name);
            return (IQuantizer)property.GetMethod.Invoke(null, Array.Empty<object>());
        }

        [Fact]
        public void ImageCanConvertFormat()
        {
            string path = TestEnvironment.CreateOutputDirectory("Format");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateRgba32Image())
                {
                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}.bmp")))
                    {
                        image.SaveAsBmp(output);
                    }

                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}.jpg")))
                    {
                        image.SaveAsJpeg(output);
                    }

                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}.png")))
                    {
                        image.SaveAsPng(output);
                    }

                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}.gif")))
                    {
                        image.SaveAsGif(output);
                    }

                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}.tga")))
                    {
                        image.SaveAsTga(output);
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
                using (var image = Image.Load(file.Bytes, out IImageFormat mimeType))
                using (var memoryStream = new MemoryStream())
                {
                    image.Save(memoryStream, mimeType);
                    memoryStream.Flush();
                    serialized = memoryStream.ToArray();
                }

                using (var image2 = Image.Load<Rgba32>(serialized))
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
        [InlineData(100, 100, "tga")]
        [InlineData(100, 10, "tga")]
        [InlineData(10, 100, "tga")]
        public void CanIdentifyImageLoadedFromBytes(int width, int height, string extension)
        {
            using (var image = Image.LoadPixelData(new Rgba32[width * height], width, height))
            {
                using (var memoryStream = new MemoryStream())
                {
                    IImageFormat format = GetFormat(extension);
                    image.Save(memoryStream, format);
                    memoryStream.Position = 0;

                    IImageInfo imageInfo = Image.Identify(memoryStream);

                    Assert.Equal(imageInfo.Width, width);
                    Assert.Equal(imageInfo.Height, height);
                    memoryStream.Position = 0;

                    imageInfo = Image.Identify(memoryStream, out IImageFormat detectedFormat);

                    Assert.Equal(format, detectedFormat);
                }
            }
        }

        [Fact]
        public void IdentifyReturnsNullWithInvalidStream()
        {
            var invalid = new byte[10];

            using (var memoryStream = new MemoryStream(invalid))
            {
                IImageInfo imageInfo = Image.Identify(memoryStream, out IImageFormat format);

                Assert.Null(imageInfo);
                Assert.Null(format);
            }
        }

        private static IImageFormat GetFormat(string format)
            => Configuration.Default.ImageFormats
            .FirstOrDefault(x => x.FileExtensions.Contains(format));
    }
}

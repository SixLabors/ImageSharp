// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
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
    // [Collection("RunSerial")]
    public abstract class GeneralFormatTests
    {
        /// <summary>
        /// A collection made up of one file for each image format.
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
                image.DebugSave(provider, testOutputDetails: this.GetType().Name);
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
                    string filename = Path.Combine(path, $"{file.FileNameWithoutExtension}-{this.GetType().Name}.txt");
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
                    image.Save(Path.Combine(path, $"_{this.GetType().Name}-{file.FileName}"));
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
            IQuantizer quantizer = GetQuantizer(quantizerName);

            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSave(provider, new PngEncoder { ColorType = PngColorType.Palette, Quantizer = quantizer }, testOutputDetails: $"{quantizerName}-{this.GetType().Name}");
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
                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}-{this.GetType().Name}.bmp")))
                    {
                        image.SaveAsBmp(output);
                    }

                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}-{this.GetType().Name}.jpg")))
                    {
                        image.SaveAsJpeg(output);
                    }

                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}-{this.GetType().Name}.png")))
                    {
                        image.SaveAsPng(output);
                    }

                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}-{this.GetType().Name}.gif")))
                    {
                        image.SaveAsGif(output);
                    }

                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}-{this.GetType().Name}.tga")))
                    {
                        image.SaveAsTga(output);
                    }

                    using (FileStream output = File.OpenWrite(Path.Combine(path, $"{file.FileNameWithoutExtension}-{this.GetType().Name}.tiff")))
                    {
                        image.SaveAsTiff(output);
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
                    image2.Save($"{path}{Path.DirectorySeparatorChar}{this.GetType().Name}-{file.FileName}");
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
        [InlineData(100, 100, "tiff")]
        [InlineData(100, 10, "tiff")]
        [InlineData(10, 100, "tiff")]

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

    public class GeneralFormatTests_00 : GeneralFormatTests { }
    public class GeneralFormatTests_01 : GeneralFormatTests { }
    public class GeneralFormatTests_02 : GeneralFormatTests { }
    public class GeneralFormatTests_03 : GeneralFormatTests { }
    public class GeneralFormatTests_04 : GeneralFormatTests { }
    public class GeneralFormatTests_05 : GeneralFormatTests { }
    public class GeneralFormatTests_06 : GeneralFormatTests { }
    public class GeneralFormatTests_07 : GeneralFormatTests { }
    public class GeneralFormatTests_08 : GeneralFormatTests { }
    public class GeneralFormatTests_09 : GeneralFormatTests { }
    public class GeneralFormatTests_10 : GeneralFormatTests { }
    public class GeneralFormatTests_11 : GeneralFormatTests { }
    public class GeneralFormatTests_12 : GeneralFormatTests { }
    public class GeneralFormatTests_13 : GeneralFormatTests { }
    public class GeneralFormatTests_14 : GeneralFormatTests { }
    public class GeneralFormatTests_15 : GeneralFormatTests { }
    public class GeneralFormatTests_16 : GeneralFormatTests { }
    public class GeneralFormatTests_17 : GeneralFormatTests { }
    public class GeneralFormatTests_18 : GeneralFormatTests { }
    public class GeneralFormatTests_19 : GeneralFormatTests { }


    public abstract class Thrash
    {
        [Theory]
        [InlineData(395307)]
        [InlineData(1185921)]
        [InlineData((1185921 * 3) + 7)]
        public void ThreshIt(int length)
        {
            using IMemoryOwner<short> buffer = MemoryAllocator.Default.Allocate<short>(length);
            buffer.Memory.Span.Fill(-1);
        }
    }

    public class Thrash_00 : Thrash { }
    public class Thrash_01 : Thrash { }
    public class Thrash_02 : Thrash { }
    public class Thrash_03 : Thrash { }
    public class Thrash_04 : Thrash { }
    public class Thrash_05 : Thrash { }
    public class Thrash_06 : Thrash { }
    public class Thrash_07 : Thrash { }
    public class Thrash_08 : Thrash { }
    public class Thrash_09 : Thrash { }
    public class Thrash_10 : Thrash { }
    public class Thrash_11 : Thrash { }
    public class Thrash_12 : Thrash { }
    public class Thrash_13 : Thrash { }
    public class Thrash_14 : Thrash { }
    public class Thrash_15 : Thrash { }
    public class Thrash_16 : Thrash { }
    public class Thrash_17 : Thrash { }
    public class Thrash_18 : Thrash { }
    public class Thrash_19 : Thrash { }
}

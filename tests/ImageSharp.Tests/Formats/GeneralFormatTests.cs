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
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Processors.Quantization;
    using SixLabors.Memory;

    public class GeneralFormatTests : FileTestBase
    {
        [Theory]
        [WithFileCollection(nameof(DefaultFiles), DefaultPixelType)]
        public void ResolutionShouldChange<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
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
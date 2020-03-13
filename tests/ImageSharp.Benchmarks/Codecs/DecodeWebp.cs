// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;

using ImageMagick;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortClr))]
    public class DecodeWebp : BenchmarkBase
    {
        private byte[] webpLossyBytes;
        private byte[] webpLosslessBytes;

        private string TestImageLossyFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImageLossy);
        private string TestImageLosslessFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImageLossless);

        [Params(TestImages.WebP.Lossy.Bike)]
        public string TestImageLossy { get; set; }

        [Params(TestImages.WebP.Lossless.BikeThreeTransforms)]
        public string TestImageLossless { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.webpLossyBytes is null)
            {
                this.webpLossyBytes = File.ReadAllBytes(this.TestImageLossyFullPath);
            }

            if (this.webpLosslessBytes is null)
            {
                this.webpLosslessBytes = File.ReadAllBytes(this.TestImageLosslessFullPath);
            }
        }

        [Benchmark(Description = "Magick Lossy WebP")]
        public int WebpLossyMagick()
        {
            var settings = new MagickReadSettings { Format = MagickFormat.WebP };
            using (var image = new MagickImage(new MemoryStream(this.webpLossyBytes), settings))
            {
                return image.Width;
            }
        }

        [Benchmark(Description = "ImageSharp Lossy Webp")]
        public int WebpLossy()
        {
            using (var memoryStream = new MemoryStream(this.webpLossyBytes))
            {
                using (var image = Image.Load<Rgba32>(memoryStream))
                {
                    return image.Height;
                }
            }
        }

        [Benchmark(Description = "Magick Lossless WebP")]
        public int WebpLosslessMagick()
        {
            var settings = new MagickReadSettings { Format = MagickFormat.WebP };
            using (var image = new MagickImage(new MemoryStream(this.webpLosslessBytes), settings))
            {
                return image.Width;
            }
        }

        [Benchmark(Description = "ImageSharp Lossless Webp")]
        public int WebpLossless()
        {
            using (var memoryStream = new MemoryStream(this.webpLosslessBytes))
            {
                using (var image = Image.Load<Rgba32>(memoryStream))
                {
                    return image.Height;
                }
            }
        }
    }
}

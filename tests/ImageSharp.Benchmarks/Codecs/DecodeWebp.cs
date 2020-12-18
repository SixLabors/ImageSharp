// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;

using ImageMagick;
using SixLabors.ImageSharp.Formats.Experimental.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class DecodeWebp
    {
        private Configuration configuration;

        private byte[] webpLossyBytes;

        private byte[] webpLosslessBytes;

        private string TestImageLossyFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImageLossy);

        private string TestImageLosslessFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImageLossless);

        [Params(TestImages.WebP.Lossy.Earth)]
        public string TestImageLossy { get; set; }

        [Params(TestImages.WebP.Lossless.Earth)]
        public string TestImageLossless { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            this.configuration = Configuration.CreateDefaultInstance();
            new WebpConfigurationModule().Configure(this.configuration);

            this.webpLossyBytes ??= File.ReadAllBytes(this.TestImageLossyFullPath);
            this.webpLosslessBytes ??= File.ReadAllBytes(this.TestImageLosslessFullPath);
        }

        [Benchmark(Description = "Magick Lossy WebP")]
        public int WebpLossyMagick()
        {
            var settings = new MagickReadSettings { Format = MagickFormat.WebP };
            using var image = new MagickImage(new MemoryStream(this.webpLossyBytes), settings);
            return image.Width;
        }

        [Benchmark(Description = "ImageSharp Lossy Webp")]
        public int WebpLossy()
        {
            using var memoryStream = new MemoryStream(this.webpLossyBytes);
            using var image = Image.Load<Rgba32>(this.configuration, memoryStream);
            return image.Height;
        }

        [Benchmark(Description = "Magick Lossless WebP")]
        public int WebpLosslessMagick()
        {
            var settings = new MagickReadSettings { Format = MagickFormat.WebP };
            using var image = new MagickImage(new MemoryStream(this.webpLosslessBytes), settings);
            return image.Width;
        }

        [Benchmark(Description = "ImageSharp Lossless Webp")]
        public int WebpLossless()
        {
            using var memoryStream = new MemoryStream(this.webpLosslessBytes);
            using var image = Image.Load<Rgba32>(this.configuration, memoryStream);
            return image.Height;
        }

        /* Results 15.05.2020
         *  BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18362
            Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
            .NET Core SDK=3.1.202
              [Host]     : .NET Core 3.1.4 (CoreCLR 4.700.20.20201, CoreFX 4.700.20.22101), X64 RyuJIT
              Job-AQFZAV : .NET Framework 4.8 (4.8.4180.0), X64 RyuJIT
              Job-YCDAPQ : .NET Core 2.1.18 (CoreCLR 4.6.28801.04, CoreFX 4.6.28802.05), X64 RyuJIT
              Job-WMTYOZ : .NET Core 3.1.4 (CoreCLR 4.700.20.20201, CoreFX 4.700.20.22101), X64 RyuJIT

            IterationCount=3  LaunchCount=1  WarmupCount=3
            |                     Method |       Runtime |       TestImageLossy |    TestImageLossless |       Mean |     Error |  StdDev |     Gen 0 |     Gen 1 | Gen 2 |    Allocated |
            |--------------------------- |-------------- |--------------------- |--------------------- |-----------:|----------:|--------:|----------:|----------:|------:|-------------:|
            |        'Magick Lossy WebP' |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] |   125.2 ms |   7.93 ms | 0.43 ms |         - |         - |     - |     18.05 KB |
            |    'ImageSharp Lossy Webp' |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] | 1,102.1 ms |  67.88 ms | 3.72 ms | 2000.0000 |         - |     - |  11835.55 KB |
            |     'Magick Lossless WebP' |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] |   183.6 ms |   7.11 ms | 0.39 ms |         - |         - |     - |     18.71 KB |
            | 'ImageSharp Lossless Webp' |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] | 1,820.1 ms |  68.66 ms | 3.76 ms | 4000.0000 | 1000.0000 |     - | 223765.64 KB |
            |        'Magick Lossy WebP' | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   124.7 ms |   1.92 ms | 0.11 ms |         - |         - |     - |     15.97 KB |
            |    'ImageSharp Lossy Webp' | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   739.0 ms |  39.51 ms | 2.17 ms | 2000.0000 |         - |     - |  11802.98 KB |
            |     'Magick Lossless WebP' | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   184.0 ms |  21.65 ms | 1.19 ms |         - |         - |     - |     17.96 KB |
            | 'ImageSharp Lossless Webp' | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   618.3 ms |  16.33 ms | 0.90 ms | 4000.0000 | 1000.0000 |     - | 223699.11 KB |
            |        'Magick Lossy WebP' | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   125.6 ms |  17.51 ms | 0.96 ms |         - |         - |     - |      16.1 KB |
            |    'ImageSharp Lossy Webp' | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   768.4 ms | 114.73 ms | 6.29 ms | 2000.0000 |         - |     - |  11802.89 KB |
            |     'Magick Lossless WebP' | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   183.6 ms |   3.32 ms | 0.18 ms |         - |         - |     - |        17 KB |
            | 'ImageSharp Lossless Webp' | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   621.3 ms |  12.12 ms | 0.66 ms | 4000.0000 | 1000.0000 |     - | 223698.75 KB |
         */
    }
}

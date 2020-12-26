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

        /* Results 26.12.2020
         *  BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18362
            Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
            .NET Core SDK=3.1.202
              [Host]     : .NET Core 3.1.4 (CoreCLR 4.700.20.20201, CoreFX 4.700.20.22101), X64 RyuJIT
              Job-AQFZAV : .NET Framework 4.8 (4.8.4180.0), X64 RyuJIT
              Job-YCDAPQ : .NET Core 2.1.18 (CoreCLR 4.6.28801.04, CoreFX 4.6.28802.05), X64 RyuJIT
              Job-WMTYOZ : .NET Core 3.1.4 (CoreCLR 4.700.20.20201, CoreFX 4.700.20.22101), X64 RyuJIT

            IterationCount=3  LaunchCount=1  WarmupCount=3
            |                     Method |        Job |       Runtime |       TestImageLossy |    TestImageLossless |       Mean |    Error |  StdDev |     Gen 0 |     Gen 1 | Gen 2 |   Allocated |
            |--------------------------- |----------- |-------------- |--------------------- |--------------------- |-----------:|---------:|--------:|----------:|----------:|------:|------------:|
            |        'Magick Lossy WebP' | Job-TNALDZ |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] |   107.1 ms | 47.56 ms | 2.61 ms |         - |         - |     - |    32.05 KB |
            |    'ImageSharp Lossy Webp' | Job-TNALDZ |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] | 1,108.4 ms | 25.90 ms | 1.42 ms |         - |         - |     - |  2779.53 KB |
            |     'Magick Lossless WebP' | Job-TNALDZ |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] |   145.8 ms |  8.97 ms | 0.49 ms |         - |         - |     - |    18.05 KB |
            | 'ImageSharp Lossless Webp' | Job-TNALDZ |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] | 1,662.9 ms |  9.34 ms | 0.51 ms | 4000.0000 | 1000.0000 |     - | 30556.87 KB |
            |        'Magick Lossy WebP' | Job-ATRTFL | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   106.2 ms | 14.80 ms | 0.81 ms |         - |         - |     - |       16 KB |
            |    'ImageSharp Lossy Webp' | Job-ATRTFL | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   743.1 ms |  7.53 ms | 0.41 ms |         - |         - |     - |   2767.8 KB |
            |     'Magick Lossless WebP' | Job-ATRTFL | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   146.7 ms | 25.23 ms | 1.38 ms |         - |         - |     - |    16.76 KB |
            | 'ImageSharp Lossless Webp' | Job-ATRTFL | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   529.2 ms | 64.09 ms | 3.51 ms | 4000.0000 | 1000.0000 |     - | 22859.97 KB |
            |        'Magick Lossy WebP' | Job-TMFWEM | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   106.0 ms |  9.51 ms | 0.52 ms |         - |         - |     - |    15.71 KB |
            |    'ImageSharp Lossy Webp' | Job-TMFWEM | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   765.8 ms | 34.82 ms | 1.91 ms |         - |         - |     - |  2767.79 KB |
            |     'Magick Lossless WebP' | Job-TMFWEM | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   146.0 ms | 25.51 ms | 1.40 ms |         - |         - |     - |    16.02 KB |
            | 'ImageSharp Lossless Webp' | Job-TMFWEM | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |   478.3 ms | 89.70 ms | 4.92 ms | 4000.0000 | 1000.0000 |     - | 22859.61 KB |
         */
    }
}

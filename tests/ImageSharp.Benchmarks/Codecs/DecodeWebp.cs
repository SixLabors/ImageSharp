// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;

using ImageMagick;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [MarkdownExporter]
    [HtmlExporter]
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

        [Benchmark(Description = "Magick Lossy Webp")]
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

        [Benchmark(Description = "Magick Lossless Webp")]
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

        /* Results 17.06.2021
         *  BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18362
            Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
            .NET Core SDK=3.1.202
              [Host]     : .NET Core 3.1.4 (CoreCLR 4.700.20.20201, CoreFX 4.700.20.22101), X64 RyuJIT
              Job-AQFZAV : .NET Framework 4.8 (4.8.4180.0), X64 RyuJIT
              Job-YCDAPQ : .NET Core 2.1.18 (CoreCLR 4.6.28801.04, CoreFX 4.6.28802.05), X64 RyuJIT
              Job-WMTYOZ : .NET Core 3.1.4 (CoreCLR 4.700.20.20201, CoreFX 4.700.20.22101), X64 RyuJIT

            IterationCount=3  LaunchCount=1  WarmupCount=3
            |                     Method |        Job |       Runtime |        TestImageLossy |        TestImageLossless |       Mean |     Error |   StdDev |     Gen 0 |     Gen 1 | Gen 2 |   Allocated |
            |--------------------------- |----------- |-------------- |---------------------- |------------------------- |-----------:|----------:|---------:|----------:|----------:|------:|------------:|
            |        'Magick Lossy Webp' | Job-IERNAB |    .NET 4.7.2 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   105.8 ms |   6.28 ms |  0.34 ms |         - |         - |     - |    17.65 KB |
            |    'ImageSharp Lossy Webp' | Job-IERNAB |    .NET 4.7.2 | WebP/earth_lossy.webp | WebP/earth_lossless.webp | 1,145.0 ms | 110.82 ms |  6.07 ms |         - |         - |     - |  2779.53 KB |
            |     'Magick Lossless Webp' | Job-IERNAB |    .NET 4.7.2 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   145.9 ms |   8.55 ms |  0.47 ms |         - |         - |     - |    18.05 KB |
            | 'ImageSharp Lossless Webp' | Job-IERNAB |    .NET 4.7.2 | WebP/earth_lossy.webp | WebP/earth_lossless.webp | 1,694.1 ms |  55.09 ms |  3.02 ms | 4000.0000 | 1000.0000 |     - | 30556.87 KB |
            |        'Magick Lossy Webp' | Job-IMRAGJ | .NET Core 2.1 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   105.7 ms |   1.89 ms |  0.10 ms |         - |         - |     - |    15.75 KB |
            |    'ImageSharp Lossy Webp' | Job-IMRAGJ | .NET Core 2.1 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   741.6 ms |  21.45 ms |  1.18 ms |         - |         - |     - |  2767.85 KB |
            |     'Magick Lossless Webp' | Job-IMRAGJ | .NET Core 2.1 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   146.1 ms |   9.52 ms |  0.52 ms |         - |         - |     - |    16.54 KB |
            | 'ImageSharp Lossless Webp' | Job-IMRAGJ | .NET Core 2.1 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   522.5 ms |  21.15 ms |  1.16 ms | 4000.0000 | 1000.0000 |     - | 22860.02 KB |
            |        'Magick Lossy Webp' | Job-NAASQX | .NET Core 3.1 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   105.9 ms |   5.34 ms |  0.29 ms |         - |         - |     - |    15.45 KB |
            |    'ImageSharp Lossy Webp' | Job-NAASQX | .NET Core 3.1 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   748.8 ms | 290.47 ms | 15.92 ms |         - |         - |     - |  2767.84 KB |
            |     'Magick Lossless Webp' | Job-NAASQX | .NET Core 3.1 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   146.1 ms |   1.14 ms |  0.06 ms |         - |         - |     - |     15.9 KB |
            | 'ImageSharp Lossless Webp' | Job-NAASQX | .NET Core 3.1 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   480.7 ms |  25.25 ms |  1.38 ms | 4000.0000 | 1000.0000 |     - |  22859.7 KB |
            |        'Magick Lossy Webp' | Job-GLNACU | .NET Core 5.0 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   105.7 ms |   4.71 ms |  0.26 ms |         - |         - |     - |    15.48 KB |
            |    'ImageSharp Lossy Webp' | Job-GLNACU | .NET Core 5.0 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   645.7 ms |  61.00 ms |  3.34 ms |         - |         - |     - |  2768.13 KB |
            |     'Magick Lossless Webp' | Job-GLNACU | .NET Core 5.0 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   146.5 ms |  18.63 ms |  1.02 ms |         - |         - |     - |     15.8 KB |
            | 'ImageSharp Lossless Webp' | Job-GLNACU | .NET Core 5.0 | WebP/earth_lossy.webp | WebP/earth_lossless.webp |   306.7 ms |  32.31 ms |  1.77 ms | 4000.0000 | 1000.0000 |     - | 22860.02 KB |
         */
    }
}

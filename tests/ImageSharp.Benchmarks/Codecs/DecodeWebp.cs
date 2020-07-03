// Copyright (c) Six Labors.
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

        [Params(TestImages.WebP.Lossy.Earth)]
        public string TestImageLossy { get; set; }

        [Params(TestImages.WebP.Lossless.Earth)]
        public string TestImageLossless { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
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
            using var image = Image.Load<Rgba32>(memoryStream);
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
            using var image = Image.Load<Rgba32>(memoryStream);
            return image.Height;
        }

        /* Results 18.03.2020
         * BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18362
        Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
        .NET Core SDK=3.1.200
          [Host]     : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT
          Job-TLYXIR : .NET Framework 4.8 (4.8.4121.0), X64 RyuJIT
          Job-HPKRXU : .NET Core 2.1.16 (CoreCLR 4.6.28516.03, CoreFX 4.6.28516.10), X64 RyuJIT
          Job-OBFQMR : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT
        |                     Method |       Runtime |       TestImageLossy |    TestImageLossless |      Mean |     Error |   StdDev |      Gen 0 |     Gen 1 |     Gen 2 |    Allocated |
        |--------------------------- |-------------- |--------------------- |--------------------- |----------:|----------:|---------:|-----------:|----------:|----------:|-------------:|
        |        'Magick Lossy WebP' |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] |  70.37 ms |  9.234 ms | 0.506 ms |          - |         - |         - |     32.05 KB |
        |    'ImageSharp Lossy Webp' |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] | 211.77 ms |  8.055 ms | 0.442 ms | 19000.0000 |         - |         - |  82297.31 KB |
        |     'Magick Lossless WebP' |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] |  49.35 ms |  1.099 ms | 0.060 ms |          - |         - |         - |     15.32 KB |
        | 'ImageSharp Lossless Webp' |    .NET 4.7.2 | WebP/(...).webp [21] | WebP/(...).webp [24] | 494.34 ms |  5.505 ms | 0.302 ms |  2000.0000 | 1000.0000 | 1000.0000 | 151801.78 KB |
        |        'Magick Lossy WebP' | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |  70.21 ms |  1.440 ms | 0.079 ms |          - |         - |         - |      14.8 KB |
        |    'ImageSharp Lossy Webp' | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] | 142.32 ms |  6.046 ms | 0.331 ms |  9000.0000 |         - |         - |  40610.23 KB |
        |     'Magick Lossless WebP' | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |  49.44 ms |  0.258 ms | 0.014 ms |          - |         - |         - |      14.3 KB |
        | 'ImageSharp Lossless Webp' | .NET Core 2.1 | WebP/(...).webp [21] | WebP/(...).webp [24] | 206.45 ms | 11.093 ms | 0.608 ms |  2666.6667 | 1666.6667 | 1000.0000 | 151758.87 KB |
        |        'Magick Lossy WebP' | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |  69.69 ms |  1.147 ms | 0.063 ms |          - |         - |         - |     14.42 KB |
        |    'ImageSharp Lossy Webp' | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] | 121.72 ms |  2.373 ms | 0.130 ms |  9000.0000 |         - |         - |  40050.06 KB |
        |     'Magick Lossless WebP' | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] |  49.37 ms |  1.865 ms | 0.102 ms |          - |         - |         - |     14.27 KB |
        | 'ImageSharp Lossless Webp' | .NET Core 3.1 | WebP/(...).webp [21] | WebP/(...).webp [24] | 194.03 ms | 37.759 ms | 2.070 ms |  2000.0000 | 1000.0000 | 1000.0000 | 151756.38 KB |
         */
    }
}

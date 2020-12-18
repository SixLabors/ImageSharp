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
    public class EncodeWebp
    {
        private MagickImage webpMagick;
        private Image<Rgba32> webp;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(TestImages.WebP.Peak)]
        public string TestImage { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.webp == null)
            {
                this.webp = Image.Load<Rgba32>(this.TestImageFullPath);
                this.webpMagick = new MagickImage(this.TestImageFullPath);
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.webp.Dispose();
            this.webpMagick.Dispose();
        }

        [Benchmark(Description = "Magick Webp Lossy")]
        public void MagickWebpLossy()
        {
            using var memoryStream = new MemoryStream();
            this.webpMagick.Settings.SetDefine(MagickFormat.WebP, "lossless", false);
            this.webpMagick.Write(memoryStream, MagickFormat.WebP);
        }

        [Benchmark(Description = "ImageSharp Webp Lossy")]
        public void ImageSharpWebpLossy()
        {
            using var memoryStream = new MemoryStream();
            this.webp.Save(memoryStream, new WebpEncoder()
            {
                Lossy = true
            });
        }

        [Benchmark(Baseline = true, Description = "Magick Webp Lossless")]
        public void MagickWebpLossless()
        {
            using var memoryStream = new MemoryStream();
            this.webpMagick.Settings.SetDefine(MagickFormat.WebP, "lossless", true);
            this.webpMagick.Write(memoryStream, MagickFormat.WebP);
        }

        [Benchmark(Description = "ImageSharp Webp Lossless")]
        public void ImageSharpWebpLossless()
        {
            using var memoryStream = new MemoryStream();
            this.webp.Save(memoryStream, new WebpEncoder()
            {
                Lossy = false
            });
        }

        /* Results 14.11.2020
         * Summary *
        BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.630 (2004/?/20H1)
        Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
        .NET Core SDK=5.0.100
          [Host]     : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
          Job-OUUGWL : .NET Framework 4.8 (4.8.4250.0), X64 RyuJIT
          Job-GAIITM : .NET Core 2.1.23 (CoreCLR 4.6.29321.03, CoreFX 4.6.29321.01), X64 RyuJIT
          Job-HWOBSO : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT

        |                     Method |        Job |       Runtime |     TestImage |      Mean |     Error |    StdDev | Ratio | RatioSD |     Gen 0 |    Gen 1 |    Gen 2 |  Allocated |
        |--------------------------- |----------- |-------------- |-------------- |----------:|----------:|----------:|------:|--------:|----------:|---------:|---------:|-----------:|
        |        'Magick Webp Lossy' | Job-MYNMXL |    .NET 4.7.2 | WebP/Peak.png |  1.744 ms | 0.0399 ms | 0.0022 ms |  0.35 |    0.00 |    1.9531 |        - |        - |   13.58 KB |
        |    'ImageSharp Webp Lossy' | Job-MYNMXL |    .NET 4.7.2 | WebP/Peak.png |  5.195 ms | 0.4241 ms | 0.0232 ms |  1.04 |    0.01 |  398.4375 |  93.7500 |        - | 1661.83 KB |
        |     'Magick Webp Lossless' | Job-MYNMXL |    .NET 4.7.2 | WebP/Peak.png |  4.993 ms | 0.5097 ms | 0.0279 ms |  1.00 |    0.00 |    7.8125 |        - |        - |    35.7 KB |
        | 'ImageSharp Webp Lossless' | Job-MYNMXL |    .NET 4.7.2 | WebP/Peak.png | 12.174 ms | 1.2476 ms | 0.0684 ms |  2.44 |    0.02 | 1000.0000 | 984.3750 | 984.3750 | 8197.11 KB |
        |                            |            |               |               |           |           |           |       |         |           |          |          |            |
        |        'Magick Webp Lossy' | Job-MPXHSM | .NET Core 2.1 | WebP/Peak.png |  1.747 ms | 0.0581 ms | 0.0032 ms |  0.35 |    0.00 |    1.9531 |        - |        - |   13.34 KB |
        |    'ImageSharp Webp Lossy' | Job-MPXHSM | .NET Core 2.1 | WebP/Peak.png |  3.527 ms | 0.0972 ms | 0.0053 ms |  0.71 |    0.00 |  402.3438 |  97.6563 |        - | 1656.92 KB |
        |     'Magick Webp Lossless' | Job-MPXHSM | .NET Core 2.1 | WebP/Peak.png |  5.001 ms | 0.4543 ms | 0.0249 ms |  1.00 |    0.00 |    7.8125 |        - |        - |   35.39 KB |
        | 'ImageSharp Webp Lossless' | Job-MPXHSM | .NET Core 2.1 | WebP/Peak.png | 10.704 ms | 0.9844 ms | 0.0540 ms |  2.14 |    0.02 | 1000.0000 | 984.3750 | 984.3750 |  8182.6 KB |
        |                            |            |               |               |           |           |           |       |         |           |          |          |            |
        |        'Magick Webp Lossy' | Job-SYDSGM | .NET Core 3.1 | WebP/Peak.png |  1.742 ms | 0.0279 ms | 0.0015 ms |  0.35 |    0.01 |    1.9531 |        - |        - |   13.31 KB |
        |    'ImageSharp Webp Lossy' | Job-SYDSGM | .NET Core 3.1 | WebP/Peak.png |  3.347 ms | 0.0638 ms | 0.0035 ms |  0.68 |    0.01 |  402.3438 |  97.6563 |        - | 1656.93 KB |
        |     'Magick Webp Lossless' | Job-SYDSGM | .NET Core 3.1 | WebP/Peak.png |  4.954 ms | 1.4131 ms | 0.0775 ms |  1.00 |    0.00 |    7.8125 |        - |        - |   35.35 KB |
        | 'ImageSharp Webp Lossless' | Job-SYDSGM | .NET Core 3.1 | WebP/Peak.png | 10.737 ms | 2.5604 ms | 0.1403 ms |  2.17 |    0.05 | 1000.0000 | 984.3750 | 984.3750 | 8182.49 KB |
        */
    }
}

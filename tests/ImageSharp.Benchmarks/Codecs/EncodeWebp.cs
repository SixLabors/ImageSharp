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

        [Params(TestImages.Png.Bike)] // The bike image will have all 3 transforms as lossless webp.
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

        /* Results 25.12.2020
         * Summary *
        BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.630 (2004/?/20H1)
        Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
        .NET Core SDK=5.0.100
          [Host]     : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
          Job-OUUGWL : .NET Framework 4.8 (4.8.4250.0), X64 RyuJIT
          Job-GAIITM : .NET Core 2.1.23 (CoreCLR 4.6.29321.03, CoreFX 4.6.29321.01), X64 RyuJIT
          Job-HWOBSO : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT

        |                     Method |        Job |       Runtime |    TestImage |      Mean |      Error |    StdDev | Ratio | RatioSD |      Gen 0 |     Gen 1 |     Gen 2 |    Allocated |
        |--------------------------- |----------- |-------------- |------------- |----------:|-----------:|----------:|------:|--------:|-----------:|----------:|----------:|-------------:|
        |        'Magick Webp Lossy' | Job-NTTOHF |    .NET 4.7.2 | Png/Bike.png |  23.89 ms |   3.742 ms |  0.205 ms |  0.14 |    0.00 |          - |         - |         - |     68.19 KB |
        |    'ImageSharp Webp Lossy' | Job-NTTOHF |    .NET 4.7.2 | Png/Bike.png |  72.27 ms |  20.228 ms |  1.109 ms |  0.43 |    0.01 |  6142.8571 |  142.8571 |         - |  26360.05 KB |
        |     'Magick Webp Lossless' | Job-NTTOHF |    .NET 4.7.2 | Png/Bike.png | 167.75 ms |  41.847 ms |  2.294 ms |  1.00 |    0.00 |          - |         - |         - |    520.28 KB |
        | 'ImageSharp Webp Lossless' | Job-NTTOHF |    .NET 4.7.2 | Png/Bike.png | 388.12 ms |  84.867 ms |  4.652 ms |  2.31 |    0.03 | 34000.0000 | 5000.0000 | 2000.0000 |  163174.2 KB |
        |                            |            |               |              |           |            |           |       |         |            |           |           |              |
        |        'Magick Webp Lossy' | Job-RXOYDK | .NET Core 2.1 | Png/Bike.png |  24.00 ms |   7.621 ms |  0.418 ms |  0.14 |    0.00 |          - |         - |         - |     67.67 KB |
        |    'ImageSharp Webp Lossy' | Job-RXOYDK | .NET Core 2.1 | Png/Bike.png |  47.77 ms |   6.498 ms |  0.356 ms |  0.29 |    0.00 |  6272.7273 |  272.7273 |   90.9091 |  26284.65 KB |
        |     'Magick Webp Lossless' | Job-RXOYDK | .NET Core 2.1 | Png/Bike.png | 166.07 ms |  25.133 ms |  1.378 ms |  1.00 |    0.00 |          - |         - |         - |    519.06 KB |
        | 'ImageSharp Webp Lossless' | Job-RXOYDK | .NET Core 2.1 | Png/Bike.png | 356.60 ms | 249.912 ms | 13.699 ms |  2.15 |    0.10 | 34000.0000 | 5000.0000 | 2000.0000 | 162719.59 KB |
        |                            |            |               |              |           |            |           |       |         |            |           |           |              |
        |        'Magick Webp Lossy' | Job-UDPFDM | .NET Core 3.1 | Png/Bike.png |  23.95 ms |   5.531 ms |  0.303 ms |  0.14 |    0.00 |          - |         - |         - |     67.57 KB |
        |    'ImageSharp Webp Lossy' | Job-UDPFDM | .NET Core 3.1 | Png/Bike.png |  44.12 ms |   4.250 ms |  0.233 ms |  0.27 |    0.01 |  6250.0000 |  250.0000 |   83.3333 |  26284.72 KB |
        |     'Magick Webp Lossless' | Job-UDPFDM | .NET Core 3.1 | Png/Bike.png | 165.94 ms |  66.670 ms |  3.654 ms |  1.00 |    0.00 |          - |         - |         - |    523.05 KB |
        | 'ImageSharp Webp Lossless' | Job-UDPFDM | .NET Core 3.1 | Png/Bike.png | 342.97 ms |  92.856 ms |  5.090 ms |  2.07 |    0.05 | 34000.0000 | 5000.0000 | 2000.0000 | 162725.32 KB |
        */
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using ImageMagick;
using ImageMagick.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [MarkdownExporter]
    [HtmlExporter]
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

            var defines = new WebPWriteDefines
            {
                Lossless = false,
                Method = 4,
                AlphaCompression = WebPAlphaCompression.None,
                FilterStrength = 60,
                SnsStrength = 50,
                Pass = 1,

                // 100 means off.
                NearLossless = 100
            };

            this.webpMagick.Quality = 75;
            this.webpMagick.Write(memoryStream, defines);
        }

        [Benchmark(Description = "ImageSharp Webp Lossy")]
        public void ImageSharpWebpLossy()
        {
            using var memoryStream = new MemoryStream();
            this.webp.Save(memoryStream, new WebpEncoder()
            {
                FileFormat = WebpFileFormatType.Lossy,
                Method = WebpEncodingMethod.Level4,
                UseAlphaCompression = false,
                FilterStrength = 60,
                SpatialNoiseShaping = 50,
                EntropyPasses = 1
            });
        }

        [Benchmark(Baseline = true, Description = "Magick Webp Lossless")]
        public void MagickWebpLossless()
        {
            using var memoryStream = new MemoryStream();
            var defines = new WebPWriteDefines
            {
                Lossless = true,
                Method = 4,

                // 100 means off.
                NearLossless = 100
            };

            this.webpMagick.Quality = 75;
            this.webpMagick.Write(memoryStream, defines);
        }

        [Benchmark(Description = "ImageSharp Webp Lossless")]
        public void ImageSharpWebpLossless()
        {
            using var memoryStream = new MemoryStream();
            this.webp.Save(memoryStream, new WebpEncoder()
            {
                FileFormat = WebpFileFormatType.Lossless,
                Method = WebpEncodingMethod.Level4,
                NearLossless = false,

                // This is equal to exact = false in libwebp, which is the default.
                TransparentColorMode = WebpTransparentColorMode.Clear
            });
        }

        /* Results 17.06.2021
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
        |        'Magick Webp Lossy' | Job-RYVNHD |    .NET 4.7.2 | Png/Bike.png |  23.30 ms |   0.869 ms |  0.048 ms |  0.14 |    0.00 |          - |         - |         - |     68.19 KB |
        |    'ImageSharp Webp Lossy' | Job-RYVNHD |    .NET 4.7.2 | Png/Bike.png |  68.22 ms |  16.454 ms |  0.902 ms |  0.42 |    0.01 |  6125.0000 |  125.0000 |         - |  26359.49 KB |
        |     'Magick Webp Lossless' | Job-RYVNHD |    .NET 4.7.2 | Png/Bike.png | 161.96 ms |   9.879 ms |  0.541 ms |  1.00 |    0.00 |          - |         - |         - |    520.28 KB |
        | 'ImageSharp Webp Lossless' | Job-RYVNHD |    .NET 4.7.2 | Png/Bike.png | 370.88 ms |  58.875 ms |  3.227 ms |  2.29 |    0.02 | 34000.0000 | 5000.0000 | 2000.0000 | 163177.15 KB |
        |                            |            |               |              |           |            |           |       |         |            |           |           |              |
        |        'Magick Webp Lossy' | Job-GOZXWU | .NET Core 2.1 | Png/Bike.png |  23.35 ms |   0.428 ms |  0.023 ms |  0.14 |    0.00 |          - |         - |         - |     67.76 KB |
        |    'ImageSharp Webp Lossy' | Job-GOZXWU | .NET Core 2.1 | Png/Bike.png |  43.95 ms |   2.850 ms |  0.156 ms |  0.27 |    0.00 |  6250.0000 |  250.0000 |   83.3333 |  26284.72 KB |
        |     'Magick Webp Lossless' | Job-GOZXWU | .NET Core 2.1 | Png/Bike.png | 161.44 ms |   3.749 ms |  0.206 ms |  1.00 |    0.00 |          - |         - |         - |    519.26 KB |
        | 'ImageSharp Webp Lossless' | Job-GOZXWU | .NET Core 2.1 | Png/Bike.png | 335.78 ms |  78.666 ms |  4.312 ms |  2.08 |    0.03 | 34000.0000 | 5000.0000 | 2000.0000 | 162727.56 KB |
        |                            |            |               |              |           |            |           |       |         |            |           |           |              |
        |        'Magick Webp Lossy' | Job-VRDVKW | .NET Core 3.1 | Png/Bike.png |  23.48 ms |   4.325 ms |  0.237 ms |  0.15 |    0.00 |          - |         - |         - |     67.66 KB |
        |    'ImageSharp Webp Lossy' | Job-VRDVKW | .NET Core 3.1 | Png/Bike.png |  43.29 ms |  16.503 ms |  0.905 ms |  0.27 |    0.01 |  6272.7273 |  272.7273 |   90.9091 |  26284.86 KB |
        |     'Magick Webp Lossless' | Job-VRDVKW | .NET Core 3.1 | Png/Bike.png | 161.81 ms |  10.693 ms |  0.586 ms |  1.00 |    0.00 |          - |         - |         - |    523.25 KB |
        | 'ImageSharp Webp Lossless' | Job-VRDVKW | .NET Core 3.1 | Png/Bike.png | 323.97 ms | 235.468 ms | 12.907 ms |  2.00 |    0.08 | 34000.0000 | 5000.0000 | 2000.0000 | 162724.84 KB |
        |                            |            |               |              |           |            |           |       |         |            |           |           |              |
        |        'Magick Webp Lossy' | Job-ZJRLRB | .NET Core 5.0 | Png/Bike.png |  23.36 ms |   0.448 ms |  0.025 ms |  0.14 |    0.00 |          - |         - |         - |     67.66 KB |
        |    'ImageSharp Webp Lossy' | Job-ZJRLRB | .NET Core 5.0 | Png/Bike.png |  40.11 ms |   2.465 ms |  0.135 ms |  0.25 |    0.00 |  6307.6923 |  230.7692 |   76.9231 |  26284.71 KB |
        |     'Magick Webp Lossless' | Job-ZJRLRB | .NET Core 5.0 | Png/Bike.png | 161.55 ms |   6.662 ms |  0.365 ms |  1.00 |    0.00 |          - |         - |         - |    518.84 KB |
        | 'ImageSharp Webp Lossless' | Job-ZJRLRB | .NET Core 5.0 | Png/Bike.png | 298.73 ms |  17.953 ms |  0.984 ms |  1.85 |    0.01 | 34000.0000 | 5000.0000 | 2000.0000 | 162725.13 KB |
        */
    }
}

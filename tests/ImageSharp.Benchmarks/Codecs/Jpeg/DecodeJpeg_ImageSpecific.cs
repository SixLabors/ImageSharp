// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using CoreSize = SixLabors.Primitives.Size;
using SDImage = System.Drawing.Image;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    /// <summary>
    /// Image-specific Jpeg benchmarks
    /// </summary>
    [Config(typeof(Config.ShortClr))]
    public class DecodeJpeg_ImageSpecific
    {
        public class Config : ManualConfig
        {
            public Config()
            {
                this.Add(MemoryDiagnoser.Default);
            }

            public class ShortClr : Benchmarks.Config
            {
                public ShortClr()
                {
                    this.Add(
                        // Job.Clr.WithLaunchCount(1).WithWarmupCount(2).WithIterationCount(3),
                        Job.Core.WithLaunchCount(1).WithWarmupCount(2).WithIterationCount(3)
                    );
                }
            }
        }

        private byte[] jpegBytes;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(
            TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr,
            TestImages.Jpeg.BenchmarkSuite.BadRstProgressive518_Large444YCbCr,

            // The scaled result for the large image "ExifGetString750Transform_Huge420YCbCr"
            // is almost the same as the result for Jpeg420Exif,
            // which proves that the execution time for the most common YCbCr 420 path scales linearly.
            //
            // TestImages.Jpeg.BenchmarkSuite.ExifGetString750Transform_Huge420YCbCr,

            TestImages.Jpeg.BenchmarkSuite.Jpeg420Exif_MidSizeYCbCr
            )]
        public string TestImage { get; set; }


        [GlobalSetup]
        public void ReadImages()
        {
            if (this.jpegBytes == null)
            {
                this.jpegBytes = File.ReadAllBytes(this.TestImageFullPath);
            }
        }

        [Benchmark(Baseline = true, Description = "Decode Jpeg - System.Drawing")]
        public Size JpegSystemDrawing()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (var image = SDImage.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "Decode Jpeg - ImageSharp")]
        public CoreSize JpegImageSharp()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (var image = Image.Load<Rgba32>(memoryStream, new JpegDecoder { IgnoreMetadata = true }))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }

        // RESULTS (2018 November 4):
        //
        // BenchmarkDotNet=v0.10.14, OS=Windows 10.0.17134
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742191 Hz, Resolution=364.6719 ns, Timer=TSC
        // .NET Core SDK=2.1.403
        //   [Host]     : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT
        // 
        //                          Method |                                   TestImage |       Mean |      Error |    StdDev | Scaled | ScaledSD |     Gen 0 |    Gen 1 |    Gen 2 |   Allocated |
        // ------------------------------- |-------------------------------------------- |-----------:|-----------:|----------:|-------:|---------:|----------:|---------:|---------:|------------:|
        //  'Decode Jpeg - System.Drawing' |                       Jpg/baseline/Lake.jpg |   6.117 ms |  0.3923 ms | 0.0222 ms |   1.00 |     0.00 |   62.5000 |        - |        - |   205.83 KB |
        //      'Decode Jpeg - ImageSharp' |                       Jpg/baseline/Lake.jpg |  18.126 ms |  0.6023 ms | 0.0340 ms |   2.96 |     0.01 |         - |        - |        - |    19.97 KB |
        //                                 |                                             |            |            |           |        |          |           |          |          |             |
        //  'Decode Jpeg - System.Drawing' |                Jpg/baseline/jpeg420exif.jpg |  17.063 ms |  2.6096 ms | 0.1474 ms |   1.00 |     0.00 |  218.7500 |        - |        - |   757.04 KB |
        //      'Decode Jpeg - ImageSharp' |                Jpg/baseline/jpeg420exif.jpg |  41.366 ms |  1.0115 ms | 0.0572 ms |   2.42 |     0.02 |         - |        - |        - |    21.94 KB |
        //                                 |                                             |            |            |           |        |          |           |          |          |             |
        //  'Decode Jpeg - System.Drawing' | Jpg/issues/Issue518-Bad-RST-Progressive.jpg | 428.282 ms | 94.9163 ms | 5.3629 ms |   1.00 |     0.00 | 2375.0000 |        - |        - |  7403.76 KB |
        //      'Decode Jpeg - ImageSharp' | Jpg/issues/Issue518-Bad-RST-Progressive.jpg | 386.698 ms | 33.0065 ms | 1.8649 ms |   0.90 |     0.01 |  125.0000 | 125.0000 | 125.0000 | 35186.97 KB |
        //                                 |                                             |            |            |           |        |          |           |          |          |             |
        //  'Decode Jpeg - System.Drawing' |       Jpg/issues/issue750-exif-tranform.jpg |  95.192 ms |  3.1762 ms | 0.1795 ms |   1.00 |     0.00 | 1750.0000 |        - |        - |  5492.63 KB |
        //      'Decode Jpeg - ImageSharp' |       Jpg/issues/issue750-exif-tranform.jpg | 230.158 ms | 48.8128 ms | 2.7580 ms |   2.42 |     0.02 |  312.5000 | 312.5000 | 312.5000 | 58834.66 KB |

        // RESULTS (2019 April 23):
        //
        //BenchmarkDotNet=v0.11.5, OS=Windows 10.0.17763.437 (1809/October2018Update/Redstone5)
        //Intel Core i7-6600U CPU 2.60GHz (Skylake), 1 CPU, 4 logical and 2 physical cores
        //.NET Core SDK=2.2.202
        //  [Host] : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //  Core   : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //
        //|                         Method |            TestImage |       Mean |      Error |     StdDev | Ratio | RatioSD |     Gen 0 | Gen 1 | Gen 2 |   Allocated |
        //|------------------------------- |--------------------- |-----------:|-----------:|-----------:|------:|--------:|----------:|------:|------:|------------:|
        //| 'Decode Jpeg - System.Drawing' | Jpg/b(...)e.jpg [21] |   6.957 ms |   9.618 ms |  0.5272 ms |  1.00 |    0.00 |   93.7500 |     - |     - |   205.83 KB |
        //|     'Decode Jpeg - ImageSharp' | Jpg/b(...)e.jpg [21] |  18.348 ms |   8.876 ms |  0.4865 ms |  2.65 |    0.23 |         - |     - |     - |    14.49 KB |
        //|                                |                      |            |            |            |       |         |           |       |       |             |
        //| 'Decode Jpeg - System.Drawing' | Jpg/b(...)f.jpg [28] |  18.687 ms |  11.632 ms |  0.6376 ms |  1.00 |    0.00 |  343.7500 |     - |     - |   757.04 KB |
        //|     'Decode Jpeg - ImageSharp' | Jpg/b(...)f.jpg [28] |  41.990 ms |  25.514 ms |  1.3985 ms |  2.25 |    0.10 |         - |     - |     - |    15.48 KB |
        //|                                |                      |            |            |            |       |         |           |       |       |             |
        //| 'Decode Jpeg - System.Drawing' | Jpg/i(...)e.jpg [43] | 477.265 ms | 732.126 ms | 40.1303 ms |  1.00 |    0.00 | 3000.0000 |     - |     - |  7403.76 KB |
        //|     'Decode Jpeg - ImageSharp' | Jpg/i(...)e.jpg [43] | 348.545 ms |  91.480 ms |  5.0143 ms |  0.73 |    0.06 |         - |     - |     - | 35177.21 KB |
    }
}

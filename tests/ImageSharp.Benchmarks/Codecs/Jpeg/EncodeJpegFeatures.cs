// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    /// <summary>
    /// Benchmark for all available encoding features of the Jpeg file type.
    /// </summary>
    /// <remarks>
    /// This benchmark does NOT compare ImageSharp to any other jpeg codecs.
    /// </remarks>
    public class EncodeJpegFeatures
    {
        // Big enough, 4:4:4 chroma sampling
        // No metadata
        private const string TestImage = TestImages.Jpeg.Baseline.Calliphora;

        public static IEnumerable<JpegColorType> ColorSpaceValues =>
            new[] { JpegColorType.Luminance, JpegColorType.Rgb, JpegColorType.YCbCrRatio420, JpegColorType.YCbCrRatio444 };

        [Params(75, 90, 100)]
        public int Quality;

        [ParamsSource(nameof(ColorSpaceValues), Priority = -100)]
        public JpegColorType TargetColorSpace;

        private Image<Rgb24> bmpCore;
        private JpegEncoder encoder;

        private MemoryStream destinationStream;

        [GlobalSetup]
        public void Setup()
        {
            using FileStream imageBinaryStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImage));
            this.bmpCore = Image.Load<Rgb24>(imageBinaryStream);
            this.encoder = new JpegEncoder { Quality = this.Quality, ColorType = this.TargetColorSpace };
            this.destinationStream = new MemoryStream();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.bmpCore.Dispose();
            this.bmpCore = null;

            this.destinationStream.Dispose();
            this.destinationStream = null;
        }

        [Benchmark]
        public void Benchmark()
        {
            this.bmpCore.SaveAsJpeg(this.destinationStream, this.encoder);
            this.destinationStream.Seek(0, SeekOrigin.Begin);
        }
    }
}

/*
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19044
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.3.21202.5
  [Host]     : .NET Core 3.1.21 (CoreCLR 4.700.21.51404, CoreFX 4.700.21.51508), X64 RyuJIT
  DefaultJob : .NET Core 3.1.21 (CoreCLR 4.700.21.51404, CoreFX 4.700.21.51508), X64 RyuJIT


|    Method | TargetColorSpace | Quality |      Mean |     Error |    StdDev |
|---------- |----------------- |-------- |----------:|----------:|----------:|
| Benchmark |        Luminance |      75 |  7.055 ms | 0.1411 ms | 0.3297 ms |
| Benchmark |              Rgb |      75 | 12.139 ms | 0.0645 ms | 0.0538 ms |
| Benchmark |    YCbCrRatio420 |      75 |  6.463 ms | 0.0282 ms | 0.0235 ms |
| Benchmark |    YCbCrRatio444 |      75 |  8.616 ms | 0.0422 ms | 0.0374 ms |
| Benchmark |        Luminance |      90 |  7.011 ms | 0.0361 ms | 0.0301 ms |
| Benchmark |              Rgb |      90 | 13.119 ms | 0.0947 ms | 0.0886 ms |
| Benchmark |    YCbCrRatio420 |      90 |  6.786 ms | 0.0328 ms | 0.0274 ms |
| Benchmark |    YCbCrRatio444 |      90 |  8.672 ms | 0.0772 ms | 0.0722 ms |
| Benchmark |        Luminance |     100 |  9.554 ms | 0.1211 ms | 0.1012 ms |
| Benchmark |              Rgb |     100 | 19.475 ms | 0.1080 ms | 0.0958 ms |
| Benchmark |    YCbCrRatio420 |     100 | 10.146 ms | 0.0585 ms | 0.0519 ms |
| Benchmark |    YCbCrRatio444 |     100 | 15.317 ms | 0.0709 ms | 0.0592 ms |
*/

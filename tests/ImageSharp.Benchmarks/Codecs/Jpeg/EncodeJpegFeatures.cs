// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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

        public static IEnumerable<JpegEncodingColor> ColorSpaceValues => new[]
        {
            JpegEncodingColor.Luminance,
            JpegEncodingColor.Rgb,
            JpegEncodingColor.YCbCrRatio420,
            JpegEncodingColor.YCbCrRatio444,
        };

        [Params(75, 90, 100)]
        public int Quality;

        [ParamsSource(nameof(ColorSpaceValues), Priority = -100)]
        public JpegEncodingColor TargetColorSpace;

        private Image<Rgb24> bmpCore;
        private JpegEncoder encoder;

        private MemoryStream destinationStream;

        [GlobalSetup]
        public void Setup()
        {
            using FileStream imageBinaryStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImage));
            this.bmpCore = Image.Load<Rgb24>(imageBinaryStream);
            this.encoder = new JpegEncoder
            {
                Quality = this.Quality,
                ColorType = this.TargetColorSpace,
                Interleaved = true,
            };
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
.NET SDK=6.0.202
  [Host]     : .NET 6.0.4 (6.0.422.16404), X64 RyuJIT
  DefaultJob : .NET 6.0.4 (6.0.422.16404), X64 RyuJIT


|    Method | TargetColorSpace | Quality |      Mean |     Error |    StdDev |
|---------- |----------------- |-------- |----------:|----------:|----------:|
| Benchmark |        Luminance |      75 |  4.575 ms | 0.0233 ms | 0.0207 ms |
| Benchmark |              Rgb |      75 | 12.477 ms | 0.1051 ms | 0.0932 ms |
| Benchmark |    YCbCrRatio420 |      75 |  6.421 ms | 0.0464 ms | 0.0434 ms |
| Benchmark |    YCbCrRatio444 |      75 |  8.449 ms | 0.1246 ms | 0.1166 ms |
| Benchmark |        Luminance |      90 |  4.863 ms | 0.0120 ms | 0.0106 ms |
| Benchmark |              Rgb |      90 | 13.287 ms | 0.0548 ms | 0.0513 ms |
| Benchmark |    YCbCrRatio420 |      90 |  7.012 ms | 0.0533 ms | 0.0499 ms |
| Benchmark |    YCbCrRatio444 |      90 |  8.916 ms | 0.1285 ms | 0.1202 ms |
| Benchmark |        Luminance |     100 |  6.665 ms | 0.0136 ms | 0.0113 ms |
| Benchmark |              Rgb |     100 | 19.734 ms | 0.0477 ms | 0.0446 ms |
| Benchmark |    YCbCrRatio420 |     100 | 10.541 ms | 0.0925 ms | 0.0865 ms |
| Benchmark |    YCbCrRatio444 |     100 | 15.587 ms | 0.1695 ms | 0.1586 ms |
*/

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    public class DecodeJpeg
    {
        private JpegDecoder decoder;

        private MemoryStream preloadedImageStream;

        private void GenericSetup(string imageSubpath)
        {
            this.decoder = new JpegDecoder();
            byte[] bytes = File.ReadAllBytes(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, imageSubpath));
            this.preloadedImageStream = new MemoryStream(bytes);
        }

        private void GenericBechmark()
        {
            this.preloadedImageStream.Position = 0;
            using Image img = this.decoder.Decode(Configuration.Default, this.preloadedImageStream);
        }

        [GlobalSetup(Target = nameof(JpegBaselineInterleaved444))]
        public void SetupBaselineInterleaved444() =>
            this.GenericSetup(TestImages.Jpeg.Baseline.Winter444_Interleaved);

        [GlobalSetup(Target = nameof(JpegBaselineInterleaved420))]
        public void SetupBaselineInterleaved420() =>
            this.GenericSetup(TestImages.Jpeg.Baseline.Hiyamugi);

        [GlobalSetup(Target = nameof(JpegBaseline400))]
        public void SetupBaselineSingleComponent() =>
            this.GenericSetup(TestImages.Jpeg.Baseline.Jpeg400);

        [GlobalSetup(Target = nameof(JpegProgressiveNonInterleaved420))]
        public void SetupProgressiveNoninterleaved420() =>
            this.GenericSetup(TestImages.Jpeg.Progressive.Winter420_NonInterleaved);

        [GlobalCleanup]
        public void Cleanup()
        {
            this.preloadedImageStream.Dispose();
            this.preloadedImageStream = null;
        }

        [Benchmark(Description = "Baseline 4:4:4 Interleaved")]
        public void JpegBaselineInterleaved444() => this.GenericBechmark();

        [Benchmark(Description = "Baseline 4:2:0 Interleaved")]
        public void JpegBaselineInterleaved420() => this.GenericBechmark();

        [Benchmark(Description = "Baseline 4:0:0 (grayscale)")]
        public void JpegBaseline400() => this.GenericBechmark();

        [Benchmark(Description = "Progressive 4:2:0 Non-Interleaved")]
        public void JpegProgressiveNonInterleaved420() => this.GenericBechmark();
    }
}


/*
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19042.1288 (20H2/October2020Update)
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.3.21202.5
  [Host]     : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT
  DefaultJob : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT

|                              Method |      Mean |     Error |    StdDev |
|------------------------------------ |----------:|----------:|----------:|
|        'Baseline 4:4:4 Interleaved' | 12.710 ms | 0.1120 ms | 0.0990 ms |
|        'Baseline 4:2:0 Interleaved' |  8.855 ms | 0.1447 ms | 0.1353 ms |
|        'Baseline 4:0:0 (grayscale)' |  1.660 ms | 0.0106 ms | 0.0088 ms |
| 'Progressive 4:2:0 Non-Interleaved' | 14.138 ms | 0.2797 ms | 0.3330 ms |
*/

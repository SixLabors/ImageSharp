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

        [GlobalSetup(Target = nameof(JpegProgressiveNonInterleaved420))]
        public void SetupProgressiveNoninterleaved420() =>
            this.GenericSetup(TestImages.Jpeg.Progressive.Winter420_NonInterleaved);

        [GlobalCleanup(Targets = new[]
        {
            nameof(JpegProgressiveNonInterleaved420),
            nameof(JpegBaselineInterleaved444)
        })]
        public void Cleanup()
        {
            this.preloadedImageStream.Dispose();
            this.preloadedImageStream = null;
        }

        [Benchmark(Description = "Baseline Interleaved 4:4:4")]
        public void JpegBaselineInterleaved444() => this.GenericBechmark();

        [Benchmark(Description = "Progressive Non-Interleaved 4:2:0")]
        public void JpegProgressiveNonInterleaved420() => this.GenericBechmark();
    }
}


/*
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19042.1288 (20H2/October2020Update)
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.3.21202.5
  [Host]     : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT
  DefaultJob : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT

MASTER
|                              Method |     Mean |    Error |   StdDev |
|------------------------------------ |---------:|---------:|---------:|
|        'Baseline Interleaved 4:4:4' | 12.71 ms | 0.112 ms | 0.099 ms |
| 'Progressive Non-Interleaved 4:2:0' | 13.89 ms | 0.087 ms | 0.073 ms |

AAN IDCT + Fused zigzag/transpose
|                              Method |     Mean |    Error |   StdDev |
|------------------------------------ |---------:|---------:|---------:|
|        'Baseline Interleaved 4:4:4' | 11.49 ms | 0.105 ms | 0.093 ms |
| 'Progressive Non-Interleaved 4:2:0' | 13.46 ms | 0.060 ms | 0.050 ms |
*/

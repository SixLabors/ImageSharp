// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

[Config(typeof(Config.HwIntrinsics_SSE_AVX))]
public class DecodeJpeg
{
    private JpegDecoder decoder;

    private MemoryStream preloadedImageStream;

    private void GenericSetup(string imageSubpath)
    {
        this.decoder = JpegDecoder.Instance;
        byte[] bytes = File.ReadAllBytes(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, imageSubpath));
        this.preloadedImageStream = new MemoryStream(bytes);
    }

    private void GenericBenchmark()
    {
        this.preloadedImageStream.Position = 0;
        using Image img = this.decoder.Decode(DecoderOptions.Default, this.preloadedImageStream);
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
    public void JpegBaselineInterleaved444() => this.GenericBenchmark();

    [Benchmark(Description = "Baseline 4:2:0 Interleaved")]
    public void JpegBaselineInterleaved420() => this.GenericBenchmark();

    [Benchmark(Description = "Baseline 4:0:0 (grayscale)")]
    public void JpegBaseline400() => this.GenericBenchmark();

    [Benchmark(Description = "Progressive 4:2:0 Non-Interleaved")]
    public void JpegProgressiveNonInterleaved420() => this.GenericBenchmark();
}

/*
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19042.1348 (20H2/October2020Update)
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.3.21202.5
[Host]     : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT
DefaultJob : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT


|                              Method |      Mean |     Error |    StdDev |
|------------------------------------ |----------:|----------:|----------:|
|        'Baseline 4:4:4 Interleaved' | 11.127 ms | 0.0659 ms | 0.0550 ms |
|        'Baseline 4:2:0 Interleaved' |  8.458 ms | 0.0289 ms | 0.0256 ms |
|        'Baseline 4:0:0 (grayscale)' |  1.550 ms | 0.0050 ms | 0.0044 ms |
| 'Progressive 4:2:0 Non-Interleaved' | 13.220 ms | 0.0449 ms | 0.0398 ms |


FRESH BENCHMARKS FOR NEW SPECTRAL CONVERSION SETUP

BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19044
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.3.21202.5
[Host]     : .NET Core 3.1.21 (CoreCLR 4.700.21.51404, CoreFX 4.700.21.51508), X64 RyuJIT
DefaultJob : .NET Core 3.1.21 (CoreCLR 4.700.21.51404, CoreFX 4.700.21.51508), X64 RyuJIT


|                              Method |      Mean |     Error |    StdDev |
|------------------------------------ |----------:|----------:|----------:|
|        'Baseline 4:4:4 Interleaved' | 10.734 ms | 0.0287 ms | 0.0254 ms |
|        'Baseline 4:2:0 Interleaved' |  8.517 ms | 0.0401 ms | 0.0356 ms |
|        'Baseline 4:0:0 (grayscale)' |  1.442 ms | 0.0051 ms | 0.0045 ms |
| 'Progressive 4:2:0 Non-Interleaved' | 12.740 ms | 0.0832 ms | 0.0730 ms |
*/

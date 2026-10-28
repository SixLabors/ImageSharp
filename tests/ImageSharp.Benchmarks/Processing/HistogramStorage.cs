// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Effects;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Processing;

/* Measured 2026-10-19: .NET 10.0.13, AMD Ryzen AI MAX+ 395, DOTNET_TieredCompilation=0, default configuration.

| Method             | FileName                              | Size |         Mean |       Error |      StdDev |      Op/s |  Allocated |
| ------------------ | ------------------------------------- | ---- | -----------: | ----------: | ----------: | --------: | ---------: |
| GlobalEqualization | Exr/Calliphora_float_uncompressed.exr | 512  |     551.5 μs |    41.52 μs |    21.72 μs | 1,813.319 |  503.76 KB |
| OilPainting        | Exr/Calliphora_float_uncompressed.exr | 512  |  21,611.0 μs | 2,472.37 μs | 1,635.32 μs |    46.273 | 1037.29 KB |
| GlobalEqualization | Exr/Calliphora_float_uncompressed.exr | 1024 |   1,941.6 μs |   196.74 μs |   117.08 μs |   515.030 |  642.66 KB |
| OilPainting        | Exr/Calliphora_float_uncompressed.exr | 1024 |  80,939.6 μs | 3,397.54 μs | 2,247.27 μs |    12.355 | 1990.48 KB |
| GlobalEqualization | Png/CalliphoraPartial.png             | 512  |     806.4 μs |   106.41 μs |    55.65 μs | 1,240.060 |  545.38 KB |
| OilPainting        | Png/CalliphoraPartial.png             | 512  |  35,435.6 μs | 3,542.11 μs | 2,107.85 μs |    28.220 | 1668.02 KB |
| GlobalEqualization | Png/CalliphoraPartial.png             | 1024 |   1,570.5 μs |   242.65 μs |   160.50 μs |   636.748 |  881.73 KB |
| OilPainting        | Png/CalliphoraPartial.png             | 1024 | 121,949.0 μs | 4,561.15 μs | 2,714.27 μs |     8.200 | 2053.75 KB |
*/

/// <summary>
/// Measures global equalization and oil painting with extended-range samples and the default configuration.
/// </summary>
[Config(typeof(Config.Short))]
[OperationsPerSecond]
public class HistogramStorage
{
    private Image<RgbaVector> source;
    private Image<RgbaVector> working;

    /// <summary>
    /// Gets or sets the existing input photograph.
    /// </summary>
    [Params(TestImages.Exr.UncompressedFloatRgb, TestImages.Png.CalliphoraPartial)]
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the square image size used to measure scaling.
    /// </summary>
    [Params(512, 1024)]
    public int Size { get; set; }

    /// <summary>
    /// Loads and resizes each photograph outside the measured operation.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source = Image.Load<RgbaVector>(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.FileName));
        this.source.Mutate(x => x.Resize(this.Size, this.Size));
    }

    /// <summary>
    /// Restores input pixels before each measured operation.
    /// </summary>
    [IterationSetup]
    public void Reset() => this.working = this.source.Clone();

    /// <summary>
    /// Disposes the processed frame after measurement.
    /// </summary>
    [IterationCleanup]
    public void Release() => this.working.Dispose();

    /// <summary>
    /// Disposes the immutable input photograph.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.source.Dispose();

    /// <summary>
    /// Measures global histogram equalization through the existing processor.
    /// </summary>
    [Benchmark]
    public void GlobalEqualization()
    {
        using IImageProcessor<RgbaVector> processor = new GlobalHistogramEqualizationProcessor<RgbaVector>(
            this.working.Configuration, 256, false, 350, this.working, this.working.Bounds);

        processor.Execute();
    }

    /// <summary>
    /// Measures oil painting through the existing processor.
    /// </summary>
    [Benchmark]
    public void OilPainting()
    {
        // The 33-pixel brush exercises 17-sample rows, including a SIMD remainder.
        using IImageProcessor<RgbaVector> processor = new OilPaintingProcessor<RgbaVector>(
            this.working.Configuration, new OilPaintingProcessor(128, 33), this.working, this.working.Bounds);

        processor.Execute();
    }
}

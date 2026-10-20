// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Processing;

/* Measured 2026-10-19: .NET 10.0.13, AMD Ryzen AI MAX+ 395, DOTNET_TieredCompilation=0, default configuration.

| Method        | FileName                              | ConcurrentImages |     Mean |    Error |   StdDev |  Op/s |     Gen0 |     Gen1 |     Gen2 | Allocated |
| ------------- | ------------------------------------- | ---------------- | -------: | -------: | -------: | ----: | -------: | -------: | -------: | --------: |
| SlidingWindow | Exr/Calliphora_float_uncompressed.exr | 1                | 13.33 ms | 0.602 ms | 0.315 ms | 75.03 |        - |        - |        - | 200.96 KB |
| SlidingWindow | Exr/Calliphora_float_uncompressed.exr | 4                | 11.92 ms | 0.151 ms | 0.090 ms | 83.87 |  62.5000 |  62.5000 |  62.5000 | 258.14 KB |
| SlidingWindow | Exr/Calliphora_float_uncompressed.exr | 16               | 12.38 ms | 0.222 ms | 0.132 ms | 80.79 | 250.0000 | 250.0000 | 250.0000 | 206.25 KB |
| SlidingWindow | Png/CalliphoraPartial.png             | 1                | 23.99 ms | 0.704 ms | 0.465 ms | 41.68 |        - |        - |        - | 207.68 KB |
| SlidingWindow | Png/CalliphoraPartial.png             | 4                | 18.52 ms | 0.321 ms | 0.191 ms | 53.99 |  62.5000 |  62.5000 |  62.5000 | 267.17 KB |
| SlidingWindow | Png/CalliphoraPartial.png             | 16               | 19.12 ms | 0.268 ms | 0.159 ms | 52.31 | 125.0000 | 125.0000 | 125.0000 | 251.22 KB |
*/

/// <summary>
/// Measures sliding-window throughput when independent images share the memory pools.
/// </summary>
[Config(typeof(Config.Short))]
[OperationsPerSecond]
public class HistogramConcurrentProcessing
{
    // Each invocation processes sixteen images. OperationsPerInvoke divides the measured time and managed allocations by sixteen.
    // Images can reuse allocator buffers within the batch. The allocation column is therefore a batch average, not a standalone-image allocation.
    private const int BatchSize = 16;
    private Image<RgbaVector> source;
    private Image<RgbaVector>[] working;
    private ParallelOptions parallelOptions;
    private Action<int> pagedAction;

    /// <summary>
    /// Gets or sets the real source photograph, decoded before measurement.
    /// </summary>
    [Params(TestImages.Exr.UncompressedFloatRgb, TestImages.Png.CalliphoraPartial)]
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of images processed concurrently.
    /// </summary>
    [Params(1, 4, 16)]
    public int ConcurrentImages { get; set; }

    /// <summary>
    /// Prepares the source, scheduling options and delegates outside measurement.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source = Image.Load<RgbaVector>(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.FileName));
        this.source.Mutate(x => x.Resize(1024, 1024));
        this.working = new Image<RgbaVector>[BatchSize];
        this.parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = this.ConcurrentImages };
        this.pagedAction = this.ProcessPaged;
    }

    /// <summary>
    /// Restores the same sixteen input images outside measurement.
    /// </summary>
    [IterationSetup]
    public void Reset()
    {
        for (int i = 0; i < this.working.Length; i++)
        {
            this.working[i] = this.source.Clone();
        }
    }

    /// <summary>
    /// Disposes the batch results outside measurement.
    /// </summary>
    [IterationCleanup]
    public void Release()
    {
        foreach (Image<RgbaVector> image in this.working)
        {
            image.Dispose();
        }
    }

    /// <summary>
    /// Returns the source image storage after all measurements.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.source.Dispose();

    /// <summary>
    /// Processes the same batch with histogram pages allocated as needed.
    /// </summary>
    [Benchmark(OperationsPerInvoke = BatchSize)]
    public void SlidingWindow() => Parallel.For(0, BatchSize, this.parallelOptions, this.pagedAction);

    /// <summary>
    /// Processes one independent image using bins allocated as needed.
    /// </summary>
    /// <param name="index">The image index within the batch.</param>
    private void ProcessPaged(int index)
    {
        Image<RgbaVector> image = this.working[index];

        // Use the same 64-pixel window for each independent image.
        using IImageProcessor<RgbaVector> processor = new AdaptiveHistogramEqualizationSlidingWindowProcessor<RgbaVector>(
            image.Configuration, 256, false, 350, image.Width / 64, image, image.Bounds);

        processor.Execute();
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.LoadResizeSave;

// See README.md for instructions about initialization.
[MemoryDiagnoser]
[ShortRunJob]
public class LoadResizeSaveStressBenchmarks
{
    private LoadResizeSaveStressRunner runner;

    // private const JpegKind Filter = JpegKind.Progressive;
    private const JpegKind Filter = JpegKind.Any;

    [GlobalSetup]
    public void Setup()
    {
        this.runner = new LoadResizeSaveStressRunner
        {
            ImageCount = Environment.ProcessorCount,
            Filter = Filter
        };
        Console.WriteLine($"ImageCount: {this.runner.ImageCount} Filter: {Filter}");
        this.runner.Init();
    }

    private void ForEachImage(Action<string> action, int maxDegreeOfParallelism)
    {
        this.runner.MaxDegreeOfParallelism = maxDegreeOfParallelism;
        this.runner.ForEachImageParallel(action);
    }

    public int[] ParallelismValues { get; } =
    {
        // Environment.ProcessorCount,
        // Environment.ProcessorCount / 2,
        // Environment.ProcessorCount / 4,
        1
    };

    [Benchmark]
    [ArgumentsSource(nameof(ParallelismValues))]
    public void SystemDrawing(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.SystemDrawingResize, maxDegreeOfParallelism);

    [Benchmark(Baseline = true)]
    [ArgumentsSource(nameof(ParallelismValues))]
    public void ImageSharp(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.ImageSharpResize, maxDegreeOfParallelism);

    [Benchmark]
    [ArgumentsSource(nameof(ParallelismValues))]
    public void Magick(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.MagickResize, maxDegreeOfParallelism);

    [Benchmark]
    [ArgumentsSource(nameof(ParallelismValues))]
    public void MagicScaler(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.MagicScalerResize, maxDegreeOfParallelism);

    [Benchmark]
    [ArgumentsSource(nameof(ParallelismValues))]
    public void SkiaBitmap(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.SkiaBitmapResize, maxDegreeOfParallelism);

    [Benchmark]
    [ArgumentsSource(nameof(ParallelismValues))]
    public void SkiaBitmapDecodeToTargetSize(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.SkiaBitmapDecodeToTargetSize, maxDegreeOfParallelism);

    [Benchmark]
    [ArgumentsSource(nameof(ParallelismValues))]
    public void NetVips(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.NetVipsResize, maxDegreeOfParallelism);
}

/*
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19044
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.300
[Host]   : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT
ShortRun : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|                       Method | maxDegreeOfParallelism |       Mean |       Error |    StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
|----------------------------- |----------------------- |-----------:|------------:|----------:|------:|--------:|------:|------:|------:|----------:|
|                SystemDrawing |                      1 | 3,624.2 ms |   721.39 ms |  39.54 ms |  3.30 |    0.04 |     - |     - |     - |     12 KB |
|                   ImageSharp |                      1 | 1,098.4 ms |    45.64 ms |   2.50 ms |  1.00 |    0.00 |     - |     - |     - |    717 KB |
|                       Magick |                      1 | 4,089.8 ms |   905.06 ms |  49.61 ms |  3.72 |    0.04 |     - |     - |     - |     43 KB |
|                  MagicScaler |                      1 |   888.0 ms |   168.33 ms |   9.23 ms |  0.81 |    0.01 |     - |     - |     - |    105 KB |
|                   SkiaBitmap |                      1 | 2,934.4 ms | 2,023.43 ms | 110.91 ms |  2.67 |    0.10 |     - |     - |     - |     43 KB |
| SkiaBitmapDecodeToTargetSize |                      1 |   892.3 ms |   115.54 ms |   6.33 ms |  0.81 |    0.01 |     - |     - |     - |     48 KB |
|                      NetVips |                      1 |   806.8 ms |    86.23 ms |   4.73 ms |  0.73 |    0.01 |     - |     - |     - |     42 KB |
*/

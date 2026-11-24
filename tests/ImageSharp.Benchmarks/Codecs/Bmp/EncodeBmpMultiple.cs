// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Bmp;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class EncodeBmpMultiple : MultiImageBenchmarkBase.WithImagesPreloaded
{
    protected override IEnumerable<string> InputImageSubfoldersOrFiles => ["Bmp/", "Jpg/baseline"];

    [Benchmark(Description = "EncodeBmpMultiple - ImageSharp")]
    public void EncodeBmpImageSharp()
        => this.ForEachImageSharpImage((img, ms) =>
        {
            img.Save(ms, new BmpEncoder());
            return null;
        });

    [Benchmark(Baseline = true, Description = "EncodeBmpMultiple - System.Drawing")]
    public void EncodeBmpSystemDrawing()
        => this.ForEachSystemDrawingImage((img, ms) =>
        {
            img.Save(ms, ImageFormat.Bmp);
            return null;
        });

    /* Results 24.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.10075)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100
      [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                               | InputCategory   | Mean      | Error     | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
    |------------------------------------- |---------------- |----------:|----------:|---------:|------:|--------:|----------:|------------:|
    | 'EncodeBmpMultiple - ImageSharp'     | AllImages       | 195.85 ms | 30.971 ms | 1.698 ms |  1.18 |    0.01 | 140.24 KB |        0.50 |
    | 'EncodeBmpMultiple - System.Drawing' | AllImages       | 165.76 ms | 12.498 ms | 0.685 ms |  1.00 |    0.01 | 278.28 KB |        1.00 |
    |                                      |                 |           |           |          |       |         |           |             |
    | 'EncodeBmpMultiple - ImageSharp'     | SmallImagesOnly | 119.31 ms |  2.587 ms | 0.142 ms |  3.50 |    0.03 | 122.95 KB |        0.53 |
    | 'EncodeBmpMultiple - System.Drawing' | SmallImagesOnly |  34.12 ms |  6.112 ms | 0.335 ms |  1.00 |    0.01 | 232.13 KB |        1.00 |
    |                                      |                 |           |           |          |       |         |           |             |
    | 'EncodeBmpMultiple - ImageSharp'     | LargeImagesOnly |  79.45 ms | 51.830 ms | 2.841 ms |  0.60 |    0.02 |  17.78 KB |        0.38 |
    | 'EncodeBmpMultiple - System.Drawing' | LargeImagesOnly | 132.83 ms | 14.372 ms | 0.788 ms |  1.00 |    0.01 |  46.94 KB |        1.00 |

    Allocated reports managed memory only.
    Inputs: Bmp/ and Jpg/baseline, without the files that either library cannot load.
    */
}

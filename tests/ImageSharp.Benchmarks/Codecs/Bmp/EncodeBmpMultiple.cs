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

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                               | InputCategory   | Mean      | Error     | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
    |------------------------------------- |---------------- |----------:|----------:|---------:|------:|--------:|----------:|------------:|
    | 'EncodeBmpMultiple - ImageSharp'     | AllImages       | 225.73 ms | 37.067 ms | 2.032 ms |  1.33 |    0.01 | 789.57 KB |        2.99 |
    | 'EncodeBmpMultiple - System.Drawing' | AllImages       | 169.86 ms |  8.830 ms | 0.484 ms |  1.00 |    0.00 | 264.47 KB |        1.00 |
    |                                      |                 |           |           |          |       |         |           |             |
    | 'EncodeBmpMultiple - ImageSharp'     | SmallImagesOnly | 128.19 ms |  8.559 ms | 0.469 ms |  3.63 |    0.04 |  608.8 KB |        2.81 |
    | 'EncodeBmpMultiple - System.Drawing' | SmallImagesOnly |  35.30 ms |  7.651 ms | 0.419 ms |  1.00 |    0.01 | 216.63 KB |        1.00 |
    |                                      |                 |           |           |          |       |         |           |             |
    | 'EncodeBmpMultiple - ImageSharp'     | LargeImagesOnly |  78.38 ms | 32.832 ms | 1.800 ms |  0.59 |    0.01 | 282.08 KB |        6.01 |
    | 'EncodeBmpMultiple - System.Drawing' | LargeImagesOnly | 131.90 ms | 10.038 ms | 0.550 ms |  1.00 |    0.01 |  46.94 KB |        1.00 |

    Allocated reports managed memory only.
    Inputs: Bmp/ and Jpg/baseline, without the files that either library cannot load.
    */
}

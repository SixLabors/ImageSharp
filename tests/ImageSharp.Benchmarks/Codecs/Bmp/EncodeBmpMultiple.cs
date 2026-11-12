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

    | Method                               | InputCategory   | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
    |------------------------------------- |---------------- |----------:|----------:|---------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
    | 'EncodeBmpMultiple - ImageSharp'     | AllImages       | 214.40 ms | 53.167 ms | 2.914 ms |  1.24 |    0.02 |        - |        - |        - |  66.06 MB |        2.10 |
    | 'EncodeBmpMultiple - System.Drawing' | AllImages       | 172.48 ms | 21.767 ms | 1.193 ms |  1.00 |    0.01 |        - |        - |        - |  31.46 MB |        1.00 |
    |                                      |                 |           |           |          |       |         |          |          |          |           |             |
    | 'EncodeBmpMultiple - ImageSharp'     | SmallImagesOnly | 130.17 ms | 51.876 ms | 2.844 ms |  3.63 |    0.07 |        - |        - |        - |   4.59 MB |        0.79 |
    | 'EncodeBmpMultiple - System.Drawing' | SmallImagesOnly |  35.83 ms |  4.061 ms | 0.223 ms |  1.00 |    0.01 |        - |        - |        - |   5.78 MB |        1.00 |
    |                                      |                 |           |           |          |       |         |          |          |          |           |             |
    | 'EncodeBmpMultiple - ImageSharp'     | LargeImagesOnly |  85.64 ms | 29.914 ms | 1.640 ms |  0.59 |    0.01 | 833.3333 | 833.3333 | 833.3333 |  91.89 MB |        3.20 |
    | 'EncodeBmpMultiple - System.Drawing' | LargeImagesOnly | 145.74 ms | 37.234 ms | 2.041 ms |  1.00 |    0.02 |        - |        - |        - |  28.76 MB |        1.00 |

    Allocated reports managed memory only.
    Inputs: Bmp/ and Jpg/baseline, without the files that either library cannot load.
    */
}

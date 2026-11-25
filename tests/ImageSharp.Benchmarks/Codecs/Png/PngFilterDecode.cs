// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Filters;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Png;

/// <summary>
/// Measures the PNG filter decode of one scanline for the filters that read the left pixel.
/// </summary>
[Config(typeof(Config.Short))]
public class PngFilterDecode
{
    /// <summary>
    /// The byte count of one scanline: a 1920-pixel RGBA row. It is a multiple of each pixel size below.
    /// </summary>
    private const int Count = 7680;

    private byte[] scanline;
    private byte[] previousScanline;

    /// <summary>
    /// Gets or sets the filter of each invocation.
    /// </summary>
    [Params(PngFilterMethod.Sub, PngFilterMethod.Average, PngFilterMethod.Paeth)]
    public PngFilterMethod Filter { get; set; }

    /// <summary>
    /// Gets or sets the byte count of one pixel: each pixel width of the 8-bit and 16-bit PNG layouts.
    /// </summary>
    [Params(1, 2, 3, 4, 6, 8)]
    public int BytesPerPixel { get; set; }

    /// <summary>
    /// Creates deterministic inputs. The first byte of each scanline is the filter type.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.scanline = new byte[Count + 1];
        this.previousScanline = new byte[Count + 1];

        Random random = new(12345678);
        random.NextBytes(this.scanline);
        random.NextBytes(this.previousScanline);
    }

    /// <summary>
    /// Decodes the scanline in place. The cost of each filter does not depend on the byte values, so each
    /// invocation decodes the output of the one before it.
    /// </summary>
    [Benchmark]
    public void Decode()
    {
        switch (this.Filter)
        {
            case PngFilterMethod.Sub:
                PngFilterDecoder.Decode<SubFilterOperator>(this.scanline, this.previousScanline, this.BytesPerPixel);
                break;
            case PngFilterMethod.Average:
                PngFilterDecoder.Decode<AverageFilterOperator>(this.scanline, this.previousScanline, this.BytesPerPixel);
                break;
            default:
                PngFilterDecoder.Decode<PaethFilterOperator>(this.scanline, this.previousScanline, this.BytesPerPixel);
                break;
        }
    }

    /* Results 25.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.10075)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100
      [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method | Filter  | BytesPerPixel | Mean        | Error       | StdDev    | Allocated |
    |------- |-------- |-------------- |------------:|------------:|----------:|----------:|
    | Decode | Sub     | 1             |  3,754.7 ns |   228.34 ns |  12.52 ns |         - |
    | Decode | Sub     | 2             |  2,016.7 ns |   795.54 ns |  43.61 ns |         - |
    | Decode | Sub     | 3             |  1,410.0 ns |   374.67 ns |  20.54 ns |         - |
    | Decode | Sub     | 4             |    690.5 ns |    79.69 ns |   4.37 ns |         - |
    | Decode | Sub     | 6             |    495.5 ns |     7.86 ns |   0.43 ns |         - |
    | Decode | Sub     | 8             |    334.1 ns |    66.87 ns |   3.67 ns |         - |
    | Decode | Average | 1             | 10,659.9 ns |   302.49 ns |  16.58 ns |         - |
    | Decode | Average | 2             |  5,336.4 ns |    97.01 ns |   5.32 ns |         - |
    | Decode | Average | 3             |  3,624.7 ns |    92.44 ns |   5.07 ns |         - |
    | Decode | Average | 4             |  2,355.1 ns |    59.75 ns |   3.28 ns |         - |
    | Decode | Average | 6             |  1,627.9 ns |    35.92 ns |   1.97 ns |         - |
    | Decode | Average | 8             |  1,161.0 ns |     8.50 ns |   0.47 ns |         - |
    | Decode | Paeth   | 1             | 26,610.1 ns | 1,935.68 ns | 106.10 ns |         - |
    | Decode | Paeth   | 2             | 13,355.1 ns |   232.15 ns |  12.72 ns |         - |
    | Decode | Paeth   | 3             |  8,948.7 ns |   104.31 ns |   5.72 ns |         - |
    | Decode | Paeth   | 4             |  6,647.6 ns | 2,808.59 ns | 153.95 ns |         - |
    | Decode | Paeth   | 6             |  4,405.2 ns |   123.38 ns |   6.76 ns |         - |
    | Decode | Paeth   | 8             |  3,257.8 ns |    82.18 ns |   4.50 ns |         - |

    Allocated reports managed memory only.
    */
}

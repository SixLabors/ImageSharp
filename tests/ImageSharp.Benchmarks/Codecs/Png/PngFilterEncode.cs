// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Filters;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Png;

/// <summary>
/// Measures the shared PNG filter map/reduce traversal.
/// </summary>
[Config(typeof(Config.Short))]
public class PngFilterEncode
{
    private const int BytesPerPixel = 4;

    private byte[] scanline;
    private byte[] previousScanline;
    private byte[] result;

    /// <summary>
    /// Gets or sets the filter evaluated by each invocation.
    /// </summary>
    [Params(PngFilterMethod.Sub, PngFilterMethod.Up, PngFilterMethod.Average, PngFilterMethod.Paeth)]
    public PngFilterMethod Filter { get; set; }

    /// <summary>
    /// Gets or sets the number of scanline bytes.
    /// </summary>
    [Params(64, 1024, 16384)]
    public int Count { get; set; }

    /// <summary>
    /// Creates deterministic non-uniform inputs and a result buffer.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.scanline = new byte[this.Count];
        this.previousScanline = new byte[this.Count];
        this.result = new byte[this.Count + 1];

        Random random = new(12345678);
        random.NextBytes(this.scanline);
        random.NextBytes(this.previousScanline);
    }

    /// <summary>
    /// Executes the shared operator-driven map/reduce traversal.
    /// </summary>
    /// <returns>The filter variance sum.</returns>
    [Benchmark]
    public int Encode()
        => this.Filter switch
        {
            PngFilterMethod.Sub => this.EncodeSub(),
            PngFilterMethod.Up => this.EncodeUp(),
            PngFilterMethod.Average => this.EncodeAverage(),
            PngFilterMethod.Paeth => this.EncodePaeth(),
            _ => throw new InvalidOperationException()
        };

    /// <summary>
    /// Executes the current Sub encoder.
    /// </summary>
    private int EncodeSub()
    {
        PngFilterEncoder.Encode<SubFilterOperator>(this.scanline, this.scanline, this.result, BytesPerPixel, out int sum);
        return sum;
    }

    /// <summary>
    /// Executes the current Up encoder.
    /// </summary>
    private int EncodeUp()
    {
        PngFilterEncoder.Encode<UpFilterOperator>(this.scanline, this.previousScanline, this.result, 0, out int sum);
        return sum;
    }

    /// <summary>
    /// Executes the current Average encoder.
    /// </summary>
    private int EncodeAverage()
    {
        PngFilterEncoder.Encode<AverageFilterOperator>(this.scanline, this.previousScanline, this.result, BytesPerPixel, out int sum);
        return sum;
    }

    /// <summary>
    /// Executes the current Paeth encoder.
    /// </summary>
    private int EncodePaeth()
    {
        PngFilterEncoder.Encode<PaethFilterOperator>(this.scanline, this.previousScanline, this.result, BytesPerPixel, out int sum);
        return sum;
    }

    /* Results 25.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.10075)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100
      [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method | Filter  | Count | Mean       | Error       | StdDev     | Allocated |
    |------- |-------- |------ |-----------:|------------:|-----------:|----------:|
    | Encode | Sub     | 64    |  13.104 ns |   1.9051 ns |  0.1044 ns |         - |
    | Encode | Sub     | 1024  |  19.778 ns |   1.2530 ns |  0.0687 ns |         - |
    | Encode | Sub     | 16384 | 202.259 ns |  41.9293 ns |  2.2983 ns |         - |
    | Encode | Up      | 64    |   6.898 ns |   0.0961 ns |  0.0053 ns |         - |
    | Encode | Up      | 1024  |  13.513 ns |   0.0921 ns |  0.0050 ns |         - |
    | Encode | Up      | 16384 | 204.063 ns |  58.2902 ns |  3.1951 ns |         - |
    | Encode | Average | 64    |  14.307 ns |   5.8130 ns |  0.3186 ns |         - |
    | Encode | Average | 1024  |  24.503 ns |   0.6213 ns |  0.0341 ns |         - |
    | Encode | Average | 16384 | 301.720 ns | 104.0496 ns |  5.7033 ns |         - |
    | Encode | Paeth   | 64    |  27.742 ns |   7.5759 ns |  0.4153 ns |         - |
    | Encode | Paeth   | 1024  |  56.536 ns |  18.8673 ns |  1.0342 ns |         - |
    | Encode | Paeth   | 16384 | 606.692 ns | 551.3516 ns | 30.2214 ns |         - |

    Allocated reports managed memory only.
    */
}

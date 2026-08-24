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
        SubFilter.Encode(this.scanline, this.result, BytesPerPixel, out int sum);
        return sum;
    }

    /// <summary>
    /// Executes the current Up encoder.
    /// </summary>
    private int EncodeUp()
    {
        UpFilter.Encode(this.scanline, this.previousScanline, this.result, out int sum);
        return sum;
    }

    /// <summary>
    /// Executes the current Average encoder.
    /// </summary>
    private int EncodeAverage()
    {
        AverageFilter.Encode(this.scanline, this.previousScanline, this.result, BytesPerPixel, out int sum);
        return sum;
    }

    /// <summary>
    /// Executes the current Paeth encoder.
    /// </summary>
    private int EncodePaeth()
    {
        PaethFilter.Encode(this.scanline, this.previousScanline, this.result, BytesPerPixel, out int sum);
        return sum;
    }
}

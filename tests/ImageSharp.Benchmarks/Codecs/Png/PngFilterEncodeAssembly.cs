// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png.Filters;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Png;

/// <summary>
/// Exposes every normalized PNG filter for assembly inspection.
/// </summary>
[Config(typeof(Config.Analysis))]
public class PngFilterEncodeAssembly
{
    private const int BytesPerPixel = 4;
    private const int Count = 180;

    private byte[] scanline;
    private byte[] previousScanline;
    private byte[] result;

    /// <summary>
    /// Creates inputs whose suffix exercises 512-, 256-, and 128-bit register widths.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.scanline = new byte[Count];
        this.previousScanline = new byte[Count];
        this.result = new byte[Count + 1];

        Random random = new(12345678);
        random.NextBytes(this.scanline);
        random.NextBytes(this.previousScanline);
    }

    /// <summary>
    /// Executes the normalized Sub encoder.
    /// </summary>
    [Benchmark]
    public void Sub()
        => SubFilter.Encode(this.scanline, this.result, BytesPerPixel, out _);

    /// <summary>
    /// Executes the normalized Up encoder.
    /// </summary>
    [Benchmark]
    public void Up()
        => UpFilter.Encode(this.scanline, this.previousScanline, this.result, out _);

    /// <summary>
    /// Executes the normalized Average encoder.
    /// </summary>
    [Benchmark]
    public void Average()
        => AverageFilter.Encode(this.scanline, this.previousScanline, this.result, BytesPerPixel, out _);

    /// <summary>
    /// Executes the normalized Paeth encoder.
    /// </summary>
    [Benchmark]
    public void Paeth()
        => PaethFilter.Encode(this.scanline, this.previousScanline, this.result, BytesPerPixel, out _);
}

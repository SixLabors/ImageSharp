// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png.Filters;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Png;

/// <summary>
/// Exposes every normalized PNG filter and retained baseline for assembly comparison.
/// </summary>
[Config(typeof(Config.Analysis))]
public class PngFilterEncodeAssembly
{
    private const int BytesPerPixel = 4;
    private const int Count = 180;

    private byte[] scanline;
    private byte[] previousScanline;
    private byte[] currentResult;
    private byte[] baselineResult;

    /// <summary>
    /// Creates inputs whose suffix exercises 512-, 256-, and 128-bit register widths.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.scanline = new byte[Count];
        this.previousScanline = new byte[Count];
        this.currentResult = new byte[Count + 1];
        this.baselineResult = new byte[Count + 1];

        Random random = new(12345678);
        random.NextBytes(this.scanline);
        random.NextBytes(this.previousScanline);
    }

    /// <summary>
    /// Executes the normalized Sub encoder.
    /// </summary>
    [Benchmark]
    public void Sub()
        => SubFilter.Encode(this.scanline, this.currentResult, BytesPerPixel, out _);

    /// <summary>
    /// Executes the normalized Up encoder.
    /// </summary>
    [Benchmark]
    public void Up()
        => UpFilter.Encode(this.scanline, this.previousScanline, this.currentResult, out _);

    /// <summary>
    /// Executes the normalized Average encoder.
    /// </summary>
    [Benchmark]
    public void Average()
        => AverageFilter.Encode(this.scanline, this.previousScanline, this.currentResult, BytesPerPixel, out _);

    /// <summary>
    /// Executes the normalized Paeth encoder.
    /// </summary>
    [Benchmark]
    public void Paeth()
        => PaethFilter.Encode(this.scanline, this.previousScanline, this.currentResult, BytesPerPixel, out _);

    /// <summary>
    /// Executes the retained Sub encoder.
    /// </summary>
    [Benchmark]
    public void BaselineSub()
        => PngFilterEncodeBaseline.EncodeSub(this.scanline, this.baselineResult, BytesPerPixel, out _);

    /// <summary>
    /// Executes the retained Up encoder.
    /// </summary>
    [Benchmark]
    public void BaselineUp()
        => PngFilterEncodeBaseline.EncodeUp(this.scanline, this.previousScanline, this.baselineResult, out _);

    /// <summary>
    /// Executes the retained Average encoder.
    /// </summary>
    [Benchmark]
    public void BaselineAverage()
        => PngFilterEncodeBaseline.EncodeAverage(
            this.scanline,
            this.previousScanline,
            this.baselineResult,
            BytesPerPixel,
            out _);

    /// <summary>
    /// Executes the retained Paeth encoder.
    /// </summary>
    [Benchmark]
    public void BaselinePaeth()
        => PngFilterEncodeBaseline.EncodePaeth(
            this.scanline,
            this.previousScanline,
            this.baselineResult,
            BytesPerPixel,
            out _);
}

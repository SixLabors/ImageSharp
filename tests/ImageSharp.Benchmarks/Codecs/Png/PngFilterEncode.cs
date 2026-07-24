// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Filters;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Png;

/// <summary>
/// Compares the shared PNG map/reduce traversal with the filter-specific traversals it replaces.
/// </summary>
[Config(typeof(Config.Short))]
public class PngFilterEncode
{
    private const int BytesPerPixel = 4;

    private byte[] scanline;
    private byte[] previousScanline;
    private byte[] currentResult;
    private byte[] baselineResult;

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
    /// Creates deterministic non-uniform inputs and independent result buffers.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.scanline = new byte[this.Count];
        this.previousScanline = new byte[this.Count];
        this.currentResult = new byte[this.Count + 1];
        this.baselineResult = new byte[this.Count + 1];

        Random random = new(12345678);
        random.NextBytes(this.scanline);
        random.NextBytes(this.previousScanline);
    }

    /// <summary>
    /// Executes the operator-driven map/reduce traversal.
    /// </summary>
    /// <returns>The filter variance sum.</returns>
    [Benchmark]
    public int Current()
        => this.Filter switch
        {
            PngFilterMethod.Sub => this.EncodeSubCurrent(),
            PngFilterMethod.Up => this.EncodeUpCurrent(),
            PngFilterMethod.Average => this.EncodeAverageCurrent(),
            PngFilterMethod.Paeth => this.EncodePaethCurrent(),
            _ => throw new InvalidOperationException()
        };

    /// <summary>
    /// Executes the filter-specific traversal being replaced.
    /// </summary>
    /// <returns>The filter variance sum.</returns>
    [Benchmark(Baseline = true)]
    public int Baseline()
    {
        int sum;

        switch (this.Filter)
        {
            case PngFilterMethod.Sub:
                PngFilterEncodeBaseline.EncodeSub(
                    this.scanline,
                    this.baselineResult,
                    BytesPerPixel,
                    out sum);

                break;

            case PngFilterMethod.Up:
                PngFilterEncodeBaseline.EncodeUp(
                    this.scanline,
                    this.previousScanline,
                    this.baselineResult,
                    out sum);

                break;

            case PngFilterMethod.Average:
                PngFilterEncodeBaseline.EncodeAverage(
                    this.scanline,
                    this.previousScanline,
                    this.baselineResult,
                    BytesPerPixel,
                    out sum);

                break;

            case PngFilterMethod.Paeth:
                PngFilterEncodeBaseline.EncodePaeth(
                    this.scanline,
                    this.previousScanline,
                    this.baselineResult,
                    BytesPerPixel,
                    out sum);

                break;

            default:
                throw new InvalidOperationException();
        }

        return sum;
    }

    /// <summary>
    /// Executes the current Sub encoder.
    /// </summary>
    private int EncodeSubCurrent()
    {
        SubFilter.Encode(this.scanline, this.currentResult, BytesPerPixel, out int sum);
        return sum;
    }

    /// <summary>
    /// Executes the current Up encoder.
    /// </summary>
    private int EncodeUpCurrent()
    {
        UpFilter.Encode(this.scanline, this.previousScanline, this.currentResult, out int sum);
        return sum;
    }

    /// <summary>
    /// Executes the current Average encoder.
    /// </summary>
    private int EncodeAverageCurrent()
    {
        AverageFilter.Encode(this.scanline, this.previousScanline, this.currentResult, BytesPerPixel, out int sum);
        return sum;
    }

    /// <summary>
    /// Executes the current Paeth encoder.
    /// </summary>
    private int EncodePaethCurrent()
    {
        PaethFilter.Encode(this.scanline, this.previousScanline, this.currentResult, BytesPerPixel, out int sum);
        return sum;
    }
}

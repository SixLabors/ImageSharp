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
    /// Gets or sets the byte count of one pixel: 8-bit RGB, 8-bit RGBA and 16-bit RGBA.
    /// </summary>
    [Params(3, 4, 8)]
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
}

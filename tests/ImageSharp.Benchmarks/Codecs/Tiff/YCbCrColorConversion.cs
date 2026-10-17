// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Tiff;

/// <summary>
/// Compares scalar and bulk conversion of the same packed TIFF YCbCr row.
/// </summary>
[Config(typeof(Config.Short))]
public class YCbCrColorConversion
{
    // TIFF maps Y references 0 and 255 to black and white. Chroma reference 128 is the zero difference.
    // BT.601 supplies the red, green, and blue luma weights 0.299, 0.587, and 0.114.
    private readonly TiffYCbCrOperator colorOperator = new(
        [new(0, 1), new(255, 1), new(128, 1), new(255, 1), new(128, 1), new(255, 1)],
        [new(299, 1000), new(587, 1000), new(114, 1000)]);

    private byte[] source = [];

    private Rgba32[] destination = [];

    /// <summary>
    /// Gets or sets the number of pixels converted per invocation.
    /// </summary>
    [Params(128, 1024)]
    public int Length { get; set; }

    /// <summary>
    /// Initializes equal source and destination buffers outside the measured operations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source = new byte[this.Length * 3];
        this.destination = new Rgba32[this.Length];

        // TIFF stores interleaved Y, Cb, Cr bytes; a fixed seed gives both paths the same row.
        Random random = new(42);
        random.NextBytes(this.source);
    }

    /// <summary>
    /// Converts each source triple through the scalar API.
    /// </summary>
    /// <returns>The final RGB pixel, keeping conversion writes observable.</returns>
    [Benchmark(Baseline = true)]
    public Rgba32 Scalar()
    {
        for (int i = 0; i < this.destination.Length; i++)
        {
            int offset = i * 3;
            this.colorOperator.ConvertByte(this.source[offset], this.source[offset + 1], this.source[offset + 2], out byte red, out byte green, out byte blue);
            this.destination[i] = new Rgba32(red, green, blue);
        }

        return this.destination[^1];
    }

    /// <summary>
    /// Converts the same source triples through the packed byte row API.
    /// </summary>
    /// <returns>The final RGB pixel, keeping conversion writes observable.</returns>
    [Benchmark]
    public Rgba32 Bulk()
    {
        TiffYCbCr8ColorConverter.Convert(this.source, [], [], this.destination, new TiffYCbCr8Source(planar: false), this.colorOperator);

        return this.destination[^1];
    }
}

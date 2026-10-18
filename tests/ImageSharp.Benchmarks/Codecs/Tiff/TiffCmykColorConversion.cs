// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.Readers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Tiff;

/// <summary>
/// Compares scalar and bulk decoding of the same packed CMYK TIFF row.
/// </summary>
[Config(typeof(Config.Short))]
public class TiffCmykColorConversion
{
    private const int Length = 1024;

    private readonly Image<RgbaVector> destination = new(Length, 1);
    private readonly byte[] packed = new byte[Length * 4];
    private readonly ColorProfileConverter scalarConverter = new();
    private readonly CmykTiffColor<RgbaVector> decoder = new(Configuration.Default, new(), Configuration.Default.MemoryAllocator);

    /// <summary>
    /// Initializes packed CMYK component codes outside measured invocations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // The fixed seed reproduces one packed CMYK row for both benchmark methods.
        Random random = new(42);
        random.NextBytes(this.packed);

        // Black and white endpoints share the same packed input in both conversion paths.
        this.packed.AsSpan(0, 4).Clear();
        this.packed.AsSpan(4, 4).Fill(byte.MaxValue);
    }

    /// <summary>
    /// Releases the destination image after benchmarking.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.destination.Dispose();

    /// <summary>
    /// Measures per-pixel CMYK normalization, color conversion, and RgbaVector packing.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    public RgbaVector Scalar()
    {
        Span<RgbaVector> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            int offset = i * 4;

            // TIFF stores four unsigned byte codes; the color converter consumes normalized CMYK components.
            Cmyk cmyk = new(this.packed[offset] / 255F, this.packed[offset + 1] / 255F, this.packed[offset + 2] / 255F, this.packed[offset + 3] / 255F);
            Rgb rgb = this.scalarConverter.Convert<Cmyk, Rgb>(in cmyk);
            row[i] = RgbaVector.FromUnassociatedScaledVector4(new Vector4(rgb.R, rgb.G, rgb.B, 1F));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk CMYK TIFF row decoding and pixel packing.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    public RgbaVector Bulk()
    {
        this.decoder.Decode(this.packed, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }
}

/* Measured 2026-10-18

| Method |      Mean |     Error |    StdDev | Ratio |   Gen0 | Allocated | Alloc Ratio |
| ------ | --------: | --------: | --------: | ----: | -----: | --------: | ----------: |
| Scalar | 48.076 μs | 0.7953 μs | 0.0436 μs |  1.00 |      - |         - |          NA |
| Bulk   |  5.226 μs | 0.0839 μs | 0.0046 μs |  0.11 | 0.0229 |     432 B |          NA |
*/

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Tiff;

/// <summary>
/// Compares scalar and bulk decoding of packed TIFF Lab rows.
/// </summary>
[Config(typeof(Config.Short))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TiffLabColorConversion
{
    private const int Length = 1024;

    // TIFF maps lightness into 0–100 and signed 16-bit chroma uses eight fractional bits.
    private const float Inv255 = 1F / 255F;
    private const float Inv65535 = 1F / 65535F;
    private const float Inv256 = 1F / 256F;

    private readonly ColorProfileConverter scalarConverter = new();
    private readonly Image<Rgba32> destination = new(Length, 1);
    private readonly byte[] packed8 = new byte[Length * 3];
    private readonly byte[] packed16 = new byte[Length * 6];
    private readonly TiffLabColorDecoder<Rgba32, TiffLab8Source> packed8Decoder = new(
        Configuration.Default,
        new TiffLab8Source(false),
        new(),
        Configuration.Default.MemoryAllocator);

    private readonly TiffLabColorDecoder<Rgba32, TiffLab16Source> packed16Decoder = new(
        Configuration.Default,
        new TiffLab16Source(isBigEndian: true, planar: false),
        new(),
        Configuration.Default.MemoryAllocator);

    /// <summary>
    /// Initializes representative packed Lab codes outside timed invocations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // A fixed seed gives both conversion paths the same reproducible packed samples.
        Random random = new(42);
        random.NextBytes(this.packed8);

        for (int i = 0; i < Length; i++)
        {
            // Signed chroma uses two's-complement 16-bit codes with eight fractional bits.
            ushort lightness = (ushort)random.Next(ushort.MaxValue + 1);
            short chromaA = unchecked((short)random.Next(ushort.MaxValue + 1));
            short chromaB = unchecked((short)random.Next(ushort.MaxValue + 1));
            BinaryPrimitives.WriteUInt16BigEndian(this.packed16.AsSpan(i * 6), lightness);
            BinaryPrimitives.WriteUInt16BigEndian(this.packed16.AsSpan((i * 6) + 2), unchecked((ushort)chromaA));
            BinaryPrimitives.WriteUInt16BigEndian(this.packed16.AsSpan((i * 6) + 4), unchecked((ushort)chromaB));
        }
    }

    /// <summary>
    /// Releases the shared destination image after benchmarking.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.destination.Dispose();

    /// <summary>
    /// Measures per-pixel Lab8 normalization, conversion, and pixel packing.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PackedLab8")]
    public Rgba32 ScalarPacked8()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            int offset = i * 3;
            CieLab lab = new(this.packed8[offset] * 100F * Inv255, (sbyte)this.packed8[offset + 1], (sbyte)this.packed8[offset + 2]);
            Rgb rgb = this.scalarConverter.Convert<CieLab, Rgb>(in lab);
            row[i] = Rgba32.FromUnassociatedScaledVector4(new Vector4(rgb.R, rgb.G, rgb.B, 1F));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk packed Lab8 decoding and pixel packing.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("PackedLab8")]
    public Rgba32 BulkPacked8()
    {
        this.packed8Decoder.Decode(this.packed8, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures per-pixel Lab16 normalization, conversion, and pixel packing.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PackedLab16")]
    public Rgba32 ScalarPacked16()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            int offset = i * 6;
            ushort lightness = BinaryPrimitives.ReadUInt16BigEndian(this.packed16.AsSpan(offset));
            short chromaA = unchecked((short)BinaryPrimitives.ReadUInt16BigEndian(this.packed16.AsSpan(offset + 2)));
            short chromaB = unchecked((short)BinaryPrimitives.ReadUInt16BigEndian(this.packed16.AsSpan(offset + 4)));
            CieLab lab = new(lightness * 100F * Inv65535, chromaA * Inv256, chromaB * Inv256);
            Rgb rgb = this.scalarConverter.Convert<CieLab, Rgb>(in lab);
            row[i] = Rgba32.FromUnassociatedScaledVector4(new Vector4(rgb.R, rgb.G, rgb.B, 1F));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk packed Lab16 decoding and pixel packing.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("PackedLab16")]
    public Rgba32 BulkPacked16()
    {
        this.packed16Decoder.Decode(this.packed16, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }
}

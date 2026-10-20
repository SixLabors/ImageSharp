// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Readers;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Tiff;

/// <summary>
/// Compares scalar and bulk TIFF integer row decoding for Rgba32 output.
/// </summary>
[Config(typeof(Config.Short))]
[DisassemblyDiagnoser(maxDepth: 5)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TiffIntegerColorConversion
{
    private const int Length = 1024;

    private readonly WhiteIsZero8TiffColor<Rgba32> white8Reader = new(Configuration.Default);
    private readonly TiffBitGrayscaleDecoder<Rgba32> gray4Reader = new(Configuration.Default, 4, whiteIsZero: false);
    private readonly TiffNibbleColorDecoder<Rgba32, TiffRgb444Source, TiffNibbleOperator> rgb444Reader = new(
        Configuration.Default,
        default,
        new TiffNibbleOperator(hasAlpha: false, associated: false));

    private readonly TiffNibbleColorDecoder<Rgba32, TiffRgba4444Source, TiffNibbleOperator> rgba4444Reader = new(
        Configuration.Default,
        default,
        new TiffNibbleOperator(hasAlpha: true, associated: false));

    private readonly TiffGrayL16Reader<Rgba32> white16BigReader = new(Configuration.Default, isBigEndian: true, whiteIsZero: true);
    private readonly TiffGrayL16Reader<Rgba32> white16LittleReader = new(Configuration.Default, isBigEndian: false, whiteIsZero: true);
    private readonly TiffGrayL16Reader<Rgba32> black16BigReader = new(Configuration.Default, isBigEndian: true, whiteIsZero: false);
    private readonly TiffRgba64BigEndianDecoder<Rgba32> rgba16BigReader = new(Configuration.Default, associated: false);
    private readonly TiffRgba64BigEndianDecoder<Rgba32> rgba16BigAssociatedReader = new(Configuration.Default, associated: true);
    private readonly TiffRgba64LittleEndianDecoder<Rgba32> rgba16LittleAssociatedReader = new(Configuration.Default, associated: true);

    private readonly Image<Rgba32> destination = new(Length, 1);
    private readonly byte[] grayBigInput = new byte[Length * 2];
    private readonly byte[] grayLittleInput = new byte[Length * 2];
    private readonly byte[] rgbaBigInput = new byte[Length * 8];
    private readonly byte[] rgbaLittleInput = new byte[Length * 8];

    /// <summary>
    /// Initializes equivalent byte-order variants outside the measured methods.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // The fixed seed gives both byte orders the same 16-bit channel values for each method pair.
        Random random = new(42);

        for (int i = 0; i < Length; i++)
        {
            // Write the same channel values in both byte orders so each scalar and bulk pair decodes identical samples.
            ushort gray = (ushort)random.Next(ushort.MaxValue + 1);
            ushort red = (ushort)random.Next(ushort.MaxValue + 1);
            ushort green = (ushort)random.Next(ushort.MaxValue + 1);
            ushort blue = (ushort)random.Next(ushort.MaxValue + 1);
            ushort alpha = i == 0 ? (ushort)0 : (ushort)random.Next(ushort.MaxValue + 1);

            BinaryPrimitives.WriteUInt16BigEndian(this.grayBigInput.AsSpan(i * 2), gray);
            BinaryPrimitives.WriteUInt16LittleEndian(this.grayLittleInput.AsSpan(i * 2), gray);
            BinaryPrimitives.WriteUInt16BigEndian(this.rgbaBigInput.AsSpan(i * 8), red);
            BinaryPrimitives.WriteUInt16BigEndian(this.rgbaBigInput.AsSpan((i * 8) + 2), green);
            BinaryPrimitives.WriteUInt16BigEndian(this.rgbaBigInput.AsSpan((i * 8) + 4), blue);
            BinaryPrimitives.WriteUInt16BigEndian(this.rgbaBigInput.AsSpan((i * 8) + 6), alpha);
            BinaryPrimitives.WriteUInt16LittleEndian(this.rgbaLittleInput.AsSpan(i * 8), red);
            BinaryPrimitives.WriteUInt16LittleEndian(this.rgbaLittleInput.AsSpan((i * 8) + 2), green);
            BinaryPrimitives.WriteUInt16LittleEndian(this.rgbaLittleInput.AsSpan((i * 8) + 4), blue);
            BinaryPrimitives.WriteUInt16LittleEndian(this.rgbaLittleInput.AsSpan((i * 8) + 6), alpha);
        }
    }

    /// <summary>
    /// Releases the associated reader's conversion row and destination image after benchmarking.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.rgba16LittleAssociatedReader.Dispose();
        this.destination.Dispose();
    }

    /// <summary>
    /// Measures scalar eight-bit WhiteIsZero decoding.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("White8")]
    public Rgba32 White8Scalar()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            row[i] = Rgba32.FromL8(new L8((byte)(255 - this.grayBigInput[i])));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures shared SIMD byte inversion and bulk pixel packing.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("White8")]
    public Rgba32 White8Bulk()
    {
        // Both methods use the same first 1024 source bytes; the remaining bytes belong to the 16-bit cases.
        this.white8Reader.Decode(this.grayBigInput, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar four-bit grayscale decoding.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Gray4")]
    public Rgba32 Gray4Scalar()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        BitReader reader = new(this.grayBigInput);

        for (int i = 0; i < Length; i++)
        {
            // Repeating a four-bit code in both nibbles gives its exact eight-bit gray intensity.
            row[i] = Rgba32.FromL8(new L8((byte)(reader.ReadBits(4) * 17)));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures packed grayscale extraction and shared SIMD normalization.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("Gray4")]
    public Rgba32 Gray4Bulk()
    {
        this.gray4Reader.Decode(this.grayBigInput, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar four-bit RGB decoding.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rgb444")]
    public Rgba32 Rgb444Scalar()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        BitReader reader = new(this.grayBigInput);

        for (int i = 0; i < Length; i++)
        {
            // TIFF stores R, G, and B nibbles in order. Normalize before the destination pixel packs the result.
            Vector4 value = new(reader.ReadBits(4) / 15F, reader.ReadBits(4) / 15F, reader.ReadBits(4) / 15F, 1F);
            row[i] = Rgba32.FromUnassociatedScaledVector4(value);
        }

        return row[^1];
    }

    /// <summary>
    /// Measures four-bit RGB decoding through the shared SIMD traversal.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("Rgb444")]
    public Rgba32 Rgb444Bulk()
    {
        this.rgb444Reader.Decode(this.grayBigInput, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar four-bit straight RGBA decoding.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rgba4444")]
    public Rgba32 Rgba4444Scalar()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        BitReader reader = new(this.grayBigInput);

        for (int i = 0; i < Length; i++)
        {
            Vector4 value = new(reader.ReadBits(4) / 15F, reader.ReadBits(4) / 15F, reader.ReadBits(4) / 15F, reader.ReadBits(4) / 15F);
            row[i] = Rgba32.FromUnassociatedScaledVector4(value);
        }

        return row[^1];
    }

    /// <summary>
    /// Measures four-bit straight RGBA decoding through the shared SIMD traversal.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("Rgba4444")]
    public Rgba32 Rgba4444Bulk()
    {
        this.rgba4444Reader.Decode(this.grayBigInput, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar big-endian white-is-zero 16-bit decoding.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("White16BigEndian")]
    public Rgba32 White16BigEndianScalar()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        int offset = 0;

        for (int i = 0; i < Length; i++)
        {
            ushort intensity = (ushort)(ushort.MaxValue - BinaryPrimitives.ReadUInt16BigEndian(this.grayBigInput.AsSpan(offset, 2)));
            offset += 2;
            row[i] = Rgba32.FromL16(new L16(intensity));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk big-endian white-is-zero 16-bit decoding.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("White16BigEndian")]
    public Rgba32 White16BigEndianBulk()
    {
        this.white16BigReader.Decode(this.grayBigInput, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar little-endian white-is-zero 16-bit decoding.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("White16LittleEndian")]
    public Rgba32 White16LittleEndianScalar()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        int offset = 0;

        for (int i = 0; i < Length; i++)
        {
            ushort intensity = (ushort)(ushort.MaxValue - BinaryPrimitives.ReadUInt16LittleEndian(this.grayLittleInput.AsSpan(offset, 2)));
            offset += 2;
            row[i] = Rgba32.FromL16(new L16(intensity));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk little-endian white-is-zero 16-bit decoding.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("White16LittleEndian")]
    public Rgba32 White16LittleEndianBulk()
    {
        this.white16LittleReader.Decode(this.grayLittleInput, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar big-endian black-is-zero 16-bit decoding.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Black16BigEndian")]
    public Rgba32 Black16BigEndianScalar()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        int offset = 0;

        for (int i = 0; i < Length; i++)
        {
            ushort intensity = BinaryPrimitives.ReadUInt16BigEndian(this.grayBigInput.AsSpan(offset, 2));
            offset += 2;
            row[i] = Rgba32.FromL16(new L16(intensity));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk big-endian black-is-zero 16-bit decoding.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("Black16BigEndian")]
    public Rgba32 Black16BigEndianBulk()
    {
        this.black16BigReader.Decode(this.grayBigInput, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar big-endian straight-alpha RGBA decoding with 16 bits per channel.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rgba16BigEndianStraight")]
    public Rgba32 Rgba16BigEndianStraightScalar()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            int offset = i * 8;
            ushort r = BinaryPrimitives.ReadUInt16BigEndian(this.rgbaBigInput.AsSpan(offset, 2));
            ushort g = BinaryPrimitives.ReadUInt16BigEndian(this.rgbaBigInput.AsSpan(offset + 2, 2));
            ushort b = BinaryPrimitives.ReadUInt16BigEndian(this.rgbaBigInput.AsSpan(offset + 4, 2));
            ushort a = BinaryPrimitives.ReadUInt16BigEndian(this.rgbaBigInput.AsSpan(offset + 6, 2));
            row[i] = Rgba32.FromRgba64(new Rgba64(r, g, b, a));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk big-endian straight-alpha RGBA decoding with 16 bits per channel.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("Rgba16BigEndianStraight")]
    public Rgba32 Rgba16BigEndianStraightBulk()
    {
        this.rgba16BigReader.Decode(this.rgbaBigInput, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar big-endian associated-alpha RGBA decoding with 16 bits per channel.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rgba16BigEndianAssociated")]
    public Rgba32 Rgba16BigEndianAssociatedScalar()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            int offset = i * 8;
            ushort r = BinaryPrimitives.ReadUInt16BigEndian(this.rgbaBigInput.AsSpan(offset, 2));
            ushort g = BinaryPrimitives.ReadUInt16BigEndian(this.rgbaBigInput.AsSpan(offset + 2, 2));
            ushort b = BinaryPrimitives.ReadUInt16BigEndian(this.rgbaBigInput.AsSpan(offset + 4, 2));
            ushort a = BinaryPrimitives.ReadUInt16BigEndian(this.rgbaBigInput.AsSpan(offset + 6, 2));
            row[i] = TiffUtilities.ColorFromRgba64Premultiplied<Rgba32>(r, g, b, a);
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk big-endian associated-alpha RGBA decoding with 16 bits per channel.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("Rgba16BigEndianAssociated")]
    public Rgba32 Rgba16BigEndianAssociatedBulk()
    {
        this.rgba16BigAssociatedReader.Decode(this.rgbaBigInput, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar little-endian associated-alpha RGBA decoding with 16 bits per channel.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rgba16LittleEndianAssociated")]
    public Rgba32 Rgba16LittleEndianAssociatedScalar()
    {
        Span<Rgba32> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            int offset = i * 8;
            ushort r = BinaryPrimitives.ReadUInt16LittleEndian(this.rgbaLittleInput.AsSpan(offset, 2));
            ushort g = BinaryPrimitives.ReadUInt16LittleEndian(this.rgbaLittleInput.AsSpan(offset + 2, 2));
            ushort b = BinaryPrimitives.ReadUInt16LittleEndian(this.rgbaLittleInput.AsSpan(offset + 4, 2));
            ushort a = BinaryPrimitives.ReadUInt16LittleEndian(this.rgbaLittleInput.AsSpan(offset + 6, 2));

            // The little-endian bulk decoder retains RGB when alpha is zero; the TIFF utility would clear it.
            row[i] = Rgba32.FromAssociatedScaledVector4(new Rgba64(r, g, b, a).ToScaledVector4());
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk little-endian associated-alpha RGBA decoding with 16 bits per channel.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("Rgba16LittleEndianAssociated")]
    public Rgba32 Rgba16LittleEndianAssociatedBulk()
    {
        this.rgba16LittleAssociatedReader.Decode(this.rgbaLittleInput, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }
}

/* Measured 2026-10-18

| Method                             | Categories                   |       Mean |     Error |    StdDev | Ratio | RatioSD |   Gen0 | Code Size | Allocated | Alloc Ratio |
| ---------------------------------- | ---------------------------- | ---------: | --------: | --------: | ----: | ------: | -----: | --------: | --------: | ----------: |
| Black16BigEndianScalar             | Black16BigEndian             | 1,126.6 ns | 103.90 ns |  61.83 ns |  1.00 |    0.07 |      - |   1,107 B |         - |          NA |
| Black16BigEndianBulk               | Black16BigEndian             |   877.1 ns |  16.35 ns |   8.55 ns |  0.78 |    0.04 |      - |   4,452 B |         - |          NA |
|                                    |                              |            |           |           |       |         |        |           |           |             |
| Gray4Scalar                        | Gray4                        | 2,045.2 ns |  58.31 ns |  38.57 ns |  1.00 |    0.03 |      - |   1,612 B |         - |          NA |
| Gray4Bulk                          | Gray4                        | 1,247.4 ns |  31.64 ns |  20.93 ns |  0.61 |    0.01 |      - |   6,747 B |         - |          NA |
|                                    |                              |            |           |           |       |         |        |           |           |             |
| Rgb444Scalar                       | Rgb444                       | 6,907.4 ns | 177.18 ns | 105.44 ns |  1.00 |    0.02 |      - |   3,053 B |         - |          NA |
| Rgb444Bulk                         | Rgb444                       | 1,448.5 ns | 185.48 ns | 122.69 ns |  0.21 |    0.02 |      - |   5,656 B |         - |          NA |
|                                    |                              |            |           |           |       |         |        |           |           |             |
| Rgba16BigEndianAssociatedScalar    | Rgba16BigEndianAssociated    | 3,775.9 ns |  89.21 ns |  53.09 ns |  1.00 |    0.02 |      - |   1,536 B |         - |          NA |
| Rgba16BigEndianAssociatedBulk      | Rgba16BigEndianAssociated    | 1,247.6 ns | 114.85 ns |  75.97 ns |  0.33 |    0.02 |      - |   7,240 B |         - |          NA |
|                                    |                              |            |           |           |       |         |        |           |           |             |
| Rgba16BigEndianStraightScalar      | Rgba16BigEndianStraight      | 2,591.8 ns |  71.47 ns |  37.38 ns |  1.00 |    0.02 |      - |   1,585 B |         - |          NA |
| Rgba16BigEndianStraightBulk        | Rgba16BigEndianStraight      | 1,708.2 ns |  48.37 ns |  28.78 ns |  0.66 |    0.01 |      - |   6,365 B |         - |          NA |
|                                    |                              |            |           |           |       |         |        |           |           |             |
| Rgba16LittleEndianAssociatedScalar | Rgba16LittleEndianAssociated | 3,609.8 ns | 183.68 ns | 109.31 ns |  1.00 |    0.04 |      - |   1,814 B |         - |          NA |
| Rgba16LittleEndianAssociatedBulk   | Rgba16LittleEndianAssociated |   520.4 ns |  10.02 ns |   6.63 ns |  0.14 |    0.00 | 0.0086 |   8,544 B |     144 B |          NA |
|                                    |                              |            |           |           |       |         |        |           |           |             |
| Rgba4444Scalar                     | Rgba4444                     | 8,433.2 ns | 131.80 ns |  87.18 ns |  1.00 |    0.01 |      - |   3,511 B |         - |          NA |
| Rgba4444Bulk                       | Rgba4444                     | 1,221.7 ns |  31.25 ns |  18.60 ns |  0.14 |    0.00 |      - |   5,257 B |         - |          NA |
|                                    |                              |            |           |           |       |         |        |           |           |             |
| White16BigEndianScalar             | White16BigEndian             | 1,238.9 ns |  26.11 ns |  15.54 ns |  1.00 |    0.02 |      - |   1,114 B |         - |          NA |
| White16BigEndianBulk               | White16BigEndian             |   964.1 ns |  98.03 ns |  64.84 ns |  0.78 |    0.05 |      - |   4,450 B |         - |          NA |
|                                    |                              |            |           |           |       |         |        |           |           |             |
| White16LittleEndianScalar          | White16LittleEndian          | 1,123.2 ns |  27.60 ns |  16.42 ns |  1.00 |    0.02 |      - |   1,109 B |         - |          NA |
| White16LittleEndianBulk            | White16LittleEndian          |   865.5 ns |  12.88 ns |   7.67 ns |  0.77 |    0.01 |      - |   4,919 B |         - |          NA |
|                                    |                              |            |           |           |       |         |        |           |           |             |
| White8Scalar                       | White8                       |   621.8 ns |  17.91 ns |  11.85 ns |  1.00 |    0.03 |      - |   1,129 B |         - |          NA |
| White8Bulk                         | White8                       |   518.3 ns |  10.19 ns |   6.74 ns |  0.83 |    0.02 |      - |   1,255 B |         - |          NA |
*/

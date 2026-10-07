// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
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

    private readonly WhiteIsZero16TiffColor<Rgba32> white16BigReader = new(Configuration.Default, isBigEndian: true);
    private readonly WhiteIsZero16TiffColor<Rgba32> white16LittleReader = new(Configuration.Default, isBigEndian: false);
    private readonly BlackIsZero16TiffColor<Rgba32> black16BigReader = new(Configuration.Default, isBigEndian: true);
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
    /// Releases the shared destination image after benchmarking.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.destination.Dispose();

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

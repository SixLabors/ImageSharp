// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Tiff;

/// <summary>
/// Compares scalar and bulk decoding of packed TIFF RGB and RGBA rows with 24 or 32 bits per component.
/// </summary>
[Config(typeof(Config.Short))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TiffPackedHighDepthColorConversion
{
    private const int Length = 1024;

    // Integer sample maxima map to full channel intensity in the scalar and bulk pixel paths.
    private const float Scale24 = 1F / 0xFFFFFF;
    private const float Scale32 = 1F / uint.MaxValue;

    private readonly Image<RgbaVector> destination = new(Length, 1);
    private readonly byte[] rgb24 = new byte[Length * 9];
    private readonly byte[] rgba24 = new byte[Length * 12];
    private readonly byte[] rgb32 = new byte[Length * 12];
    private readonly byte[] rgba32 = new byte[Length * 16];
    private readonly TiffColorDecoder<RgbaVector, TiffRgb24Source, TiffRgbOperator> rgb24Decoder = new(Configuration.Default, new TiffRgb24Source(isBigEndian: true, planar: false), new TiffRgbOperator(Scale24));
    private readonly TiffColorDecoder<RgbaVector, TiffRgba24Source, TiffRgbaOperator> rgba24Decoder = new(Configuration.Default, new TiffRgba24Source(isBigEndian: true, planar: false), new TiffRgbaOperator(Scale24, associated: false));
    private readonly TiffColorDecoder<RgbaVector, TiffRgb32Source, TiffRgbOperator> rgb32Decoder = new(Configuration.Default, new TiffRgb32Source(isBigEndian: true, planar: false), new TiffRgbOperator(Scale32));
    private readonly TiffRgba32PackedDecoder<RgbaVector> rgba32Decoder = new(Configuration.Default, isBigEndian: true, associated: false);

    /// <summary>
    /// Initializes representative packed sample rows outside measured invocations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // A fixed seed gives scalar and bulk decoding the same channel values.
        Random random = new(42);

        for (int i = 0; i < Length; i++)
        {
            uint red = (uint)random.NextInt64(0, (long)uint.MaxValue + 1);
            uint green = (uint)random.NextInt64(0, (long)uint.MaxValue + 1);
            uint blue = (uint)random.NextInt64(0, (long)uint.MaxValue + 1);
            uint alpha = (uint)random.NextInt64(0, (long)uint.MaxValue + 1);
            int rgb24Offset = i * 9;
            int rgba24Offset = i * 12;
            int rgb32Offset = i * 12;
            int rgba32Offset = i * 16;

            // Three-byte TIFF samples retain the low 24 bits; RGB and straight-alpha RGBA share those codes.
            WriteUInt24BigEndian(this.rgb24.AsSpan(rgb24Offset), red & 0xFFFFFFU);
            WriteUInt24BigEndian(this.rgb24.AsSpan(rgb24Offset + 3), green & 0xFFFFFFU);
            WriteUInt24BigEndian(this.rgb24.AsSpan(rgb24Offset + 6), blue & 0xFFFFFFU);
            WriteUInt24BigEndian(this.rgba24.AsSpan(rgba24Offset), red & 0xFFFFFFU);
            WriteUInt24BigEndian(this.rgba24.AsSpan(rgba24Offset + 3), green & 0xFFFFFFU);
            WriteUInt24BigEndian(this.rgba24.AsSpan(rgba24Offset + 6), blue & 0xFFFFFFU);
            WriteUInt24BigEndian(this.rgba24.AsSpan(rgba24Offset + 9), alpha & 0xFFFFFFU);
            BinaryPrimitives.WriteUInt32BigEndian(this.rgb32.AsSpan(rgb32Offset), red);
            BinaryPrimitives.WriteUInt32BigEndian(this.rgb32.AsSpan(rgb32Offset + 4), green);
            BinaryPrimitives.WriteUInt32BigEndian(this.rgb32.AsSpan(rgb32Offset + 8), blue);
            BinaryPrimitives.WriteUInt32BigEndian(this.rgba32.AsSpan(rgba32Offset), red);
            BinaryPrimitives.WriteUInt32BigEndian(this.rgba32.AsSpan(rgba32Offset + 4), green);
            BinaryPrimitives.WriteUInt32BigEndian(this.rgba32.AsSpan(rgba32Offset + 8), blue);
            BinaryPrimitives.WriteUInt32BigEndian(this.rgba32.AsSpan(rgba32Offset + 12), alpha);
        }
    }

    /// <summary>
    /// Releases the destination image after benchmarking.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.destination.Dispose();

    /// <summary>
    /// Measures scalar decoding of RGB with 24 bits per component.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RGB24")]
    public RgbaVector ScalarRgb24()
    {
        Span<RgbaVector> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            int offset = i * 9;
            uint red = TiffUtilities.ConvertToUInt24BigEndian(this.rgb24.AsSpan(offset));
            uint green = TiffUtilities.ConvertToUInt24BigEndian(this.rgb24.AsSpan(offset + 3));
            uint blue = TiffUtilities.ConvertToUInt24BigEndian(this.rgb24.AsSpan(offset + 6));
            row[i] = RgbaVector.FromUnassociatedScaledVector4(new Vector4(red * Scale24, green * Scale24, blue * Scale24, 1F));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk decoding of RGB with 24 bits per component.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("RGB24")]
    public RgbaVector BulkRgb24()
    {
        this.rgb24Decoder.Decode(this.rgb24, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar decoding of straight-alpha RGBA with 24 bits per component.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RGBA24")]
    public RgbaVector ScalarRgba24()
    {
        Span<RgbaVector> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            int offset = i * 12;
            uint red = TiffUtilities.ConvertToUInt24BigEndian(this.rgba24.AsSpan(offset));
            uint green = TiffUtilities.ConvertToUInt24BigEndian(this.rgba24.AsSpan(offset + 3));
            uint blue = TiffUtilities.ConvertToUInt24BigEndian(this.rgba24.AsSpan(offset + 6));
            uint alpha = TiffUtilities.ConvertToUInt24BigEndian(this.rgba24.AsSpan(offset + 9));
            row[i] = RgbaVector.FromUnassociatedScaledVector4(new Vector4(red * Scale24, green * Scale24, blue * Scale24, alpha * Scale24));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk decoding of straight-alpha RGBA with 24 bits per component.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("RGBA24")]
    public RgbaVector BulkRgba24()
    {
        this.rgba24Decoder.Decode(this.rgba24, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar decoding of RGB with 32 bits per component.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RGB32")]
    public RgbaVector ScalarRgb32()
    {
        Span<RgbaVector> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            int offset = i * 12;
            uint red = BinaryPrimitives.ReadUInt32BigEndian(this.rgb32.AsSpan(offset));
            uint green = BinaryPrimitives.ReadUInt32BigEndian(this.rgb32.AsSpan(offset + 4));
            uint blue = BinaryPrimitives.ReadUInt32BigEndian(this.rgb32.AsSpan(offset + 8));
            row[i] = RgbaVector.FromUnassociatedScaledVector4(new Vector4(red * Scale32, green * Scale32, blue * Scale32, 1F));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk decoding of RGB with 32 bits per component.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("RGB32")]
    public RgbaVector BulkRgb32()
    {
        this.rgb32Decoder.Decode(this.rgb32, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar decoding of straight-alpha RGBA with 32 bits per component.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RGBA32")]
    public RgbaVector ScalarRgba32()
    {
        Span<RgbaVector> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            int offset = i * 16;
            uint red = BinaryPrimitives.ReadUInt32BigEndian(this.rgba32.AsSpan(offset));
            uint green = BinaryPrimitives.ReadUInt32BigEndian(this.rgba32.AsSpan(offset + 4));
            uint blue = BinaryPrimitives.ReadUInt32BigEndian(this.rgba32.AsSpan(offset + 8));
            uint alpha = BinaryPrimitives.ReadUInt32BigEndian(this.rgba32.AsSpan(offset + 12));
            row[i] = RgbaVector.FromUnassociatedScaledVector4(new Vector4(red * Scale32, green * Scale32, blue * Scale32, alpha * Scale32));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk decoding of straight-alpha RGBA with 32 bits per component.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("RGBA32")]
    public RgbaVector BulkRgba32()
    {
        this.rgba32Decoder.Decode(this.rgba32, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Stores a 24-bit sample in TIFF big-endian byte order.
    /// </summary>
    /// <param name="destination">The three-byte output sample.</param>
    /// <param name="value">The sample code.</param>
    private static void WriteUInt24BigEndian(Span<byte> destination, uint value)
    {
        // Each sample has exactly three bytes, including the last component at the row boundary.
        destination[0] = (byte)(value >> 16);
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)value;
    }
}

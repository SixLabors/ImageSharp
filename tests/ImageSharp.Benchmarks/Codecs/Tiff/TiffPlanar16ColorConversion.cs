// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Readers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Tiff;

/// <summary>
/// Compares scalar and bulk decoding of 16-bit RGB and RGBA planes.
/// </summary>
[Config(typeof(Config.Short))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TiffPlanar16ColorConversion
{
    private const int Length = 1024;

    private TiffRgb48PlanarDecoder<RgbaVector> rgbReader;
    private TiffRgba64PlanarDecoder<RgbaVector> rgbaReader;
    private TiffRgba64PlanarDecoder<RgbaVector> associatedReader;

    private readonly Image<RgbaVector> rgbDestination = new(Length, 1);
    private readonly Image<RgbaVector> rgbaDestination = new(Length, 1);
    private IMemoryOwner<byte>[] rgbPlanes = [];
    private IMemoryOwner<byte>[] rgbaPlanes = [];

    /// <summary>
    /// Gets or sets whether component words use big-endian byte order.
    /// </summary>
    [Params(false, true)]
    public bool IsBigEndian { get; set; }

    /// <summary>
    /// Initializes identical encoded plane bytes outside the measured methods.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        bool reverseEndianness = this.IsBigEndian == BitConverter.IsLittleEndian;
        this.rgbReader = new(Configuration.Default, this.IsBigEndian);
        this.rgbaReader = new(Configuration.Default, this.IsBigEndian, associated: false);
        this.associatedReader = new(Configuration.Default, this.IsBigEndian, associated: true);
        this.rgbPlanes = new IMemoryOwner<byte>[3];
        this.rgbaPlanes = new IMemoryOwner<byte>[4];
        Random random = new(42);

        for (int channel = 0; channel < this.rgbaPlanes.Length; channel++)
        {
            IMemoryOwner<byte> plane = Configuration.Default.MemoryAllocator.Allocate<byte>(Length * sizeof(ushort));
            this.rgbaPlanes[channel] = plane;
            if (channel < this.rgbPlanes.Length)
            {
                this.rgbPlanes[channel] = plane;
            }

            // Both byte orders contain the same ushort codes. Only their stored byte order differs.
            random.NextBytes(plane.GetSpan());

            if (reverseEndianness)
            {
                Span<ushort> words = MemoryMarshal.Cast<byte, ushort>(plane.GetSpan());
                BinaryPrimitives.ReverseEndianness(words, words);
            }
        }
    }

    /// <summary>
    /// Releases the benchmark planes and destinations.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (IMemoryOwner<byte> plane in this.rgbaPlanes)
        {
            plane.Dispose();
        }

        this.rgbDestination.Dispose();
        this.rgbaDestination.Dispose();
    }

    /// <summary>
    /// Measures scalar decoding and packing of 16-bit RGB planes.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RGB16")]
    public RgbaVector RgbScalar()
    {
        ReadOnlySpan<ushort> red = MemoryMarshal.Cast<byte, ushort>(this.rgbPlanes[0].GetSpan());
        ReadOnlySpan<ushort> green = MemoryMarshal.Cast<byte, ushort>(this.rgbPlanes[1].GetSpan());
        ReadOnlySpan<ushort> blue = MemoryMarshal.Cast<byte, ushort>(this.rgbPlanes[2].GetSpan());
        Span<RgbaVector> row = this.rgbDestination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        // Select byte order once per row so the scalar baseline measures pixel conversion without a reader call per pixel.
        if (this.IsBigEndian == BitConverter.IsLittleEndian)
        {
            for (int x = 0; x < Length; x++)
            {
                row[x] = RgbaVector.FromRgb48(
                    new Rgb48(
                        BinaryPrimitives.ReverseEndianness(red[x]),
                        BinaryPrimitives.ReverseEndianness(green[x]),
                        BinaryPrimitives.ReverseEndianness(blue[x])));
            }
        }
        else
        {
            for (int x = 0; x < Length; x++)
            {
                row[x] = RgbaVector.FromRgb48(new Rgb48(red[x], green[x], blue[x]));
            }
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk decoding and packing of 16-bit RGB planes.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("RGB16")]
    public RgbaVector RgbBulk()
    {
        this.rgbReader.Decode(this.rgbPlanes, this.rgbDestination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.rgbDestination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar decoding and packing of 16-bit RGBA planes.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RGBA16")]
    public RgbaVector RgbaScalar()
    {
        ReadOnlySpan<ushort> red = MemoryMarshal.Cast<byte, ushort>(this.rgbaPlanes[0].GetSpan());
        ReadOnlySpan<ushort> green = MemoryMarshal.Cast<byte, ushort>(this.rgbaPlanes[1].GetSpan());
        ReadOnlySpan<ushort> blue = MemoryMarshal.Cast<byte, ushort>(this.rgbaPlanes[2].GetSpan());
        ReadOnlySpan<ushort> alpha = MemoryMarshal.Cast<byte, ushort>(this.rgbaPlanes[3].GetSpan());
        Span<RgbaVector> row = this.rgbaDestination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        // Select byte order once per row so the scalar baseline measures pixel conversion without a reader call per pixel.
        if (this.IsBigEndian == BitConverter.IsLittleEndian)
        {
            for (int x = 0; x < Length; x++)
            {
                row[x] = RgbaVector.FromRgba64(
                    new Rgba64(
                        BinaryPrimitives.ReverseEndianness(red[x]),
                        BinaryPrimitives.ReverseEndianness(green[x]),
                        BinaryPrimitives.ReverseEndianness(blue[x]),
                        BinaryPrimitives.ReverseEndianness(alpha[x])));
            }
        }
        else
        {
            for (int x = 0; x < Length; x++)
            {
                row[x] = RgbaVector.FromRgba64(new Rgba64(red[x], green[x], blue[x], alpha[x]));
            }
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk decoding and packing of 16-bit RGBA planes.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("RGBA16")]
    public RgbaVector RgbaBulk()
    {
        this.rgbaReader.Decode(this.rgbaPlanes, this.rgbaDestination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.rgbaDestination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar decoding and unassociation of sixteen-bit RGBA planes.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RGBA16Associated")]
    public RgbaVector RgbaAssociatedScalar()
    {
        ReadOnlySpan<ushort> red = MemoryMarshal.Cast<byte, ushort>(this.rgbaPlanes[0].GetSpan());
        ReadOnlySpan<ushort> green = MemoryMarshal.Cast<byte, ushort>(this.rgbaPlanes[1].GetSpan());
        ReadOnlySpan<ushort> blue = MemoryMarshal.Cast<byte, ushort>(this.rgbaPlanes[2].GetSpan());
        ReadOnlySpan<ushort> alpha = MemoryMarshal.Cast<byte, ushort>(this.rgbaPlanes[3].GetSpan());
        Span<RgbaVector> row = this.rgbaDestination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        TiffRgba64PlanarOperator colorOperator = default;

        // Select byte order once per row so the scalar baseline measures pixel conversion without a reader call per pixel.
        if (this.IsBigEndian == BitConverter.IsLittleEndian)
        {
            for (int x = 0; x < Length; x++)
            {
                ushort r = BinaryPrimitives.ReverseEndianness(red[x]);
                ushort g = BinaryPrimitives.ReverseEndianness(green[x]);
                ushort b = BinaryPrimitives.ReverseEndianness(blue[x]);
                ushort a = BinaryPrimitives.ReverseEndianness(alpha[x]);
                colorOperator.Convert(ref r, ref g, ref b, ref a);
                row[x] = RgbaVector.FromAssociatedScaledVector4(new Rgba64(r, g, b, a).ToScaledVector4());
            }
        }
        else
        {
            for (int x = 0; x < Length; x++)
            {
                ushort r = red[x];
                ushort g = green[x];
                ushort b = blue[x];
                ushort a = alpha[x];
                colorOperator.Convert(ref r, ref g, ref b, ref a);
                row[x] = RgbaVector.FromAssociatedScaledVector4(new Rgba64(r, g, b, a).ToScaledVector4());
            }
        }

        return row[^1];
    }

    /// <summary>
    /// Measures the same associated sixteen-bit planes through the TIFF bulk reader.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("RGBA16Associated")]
    public RgbaVector RgbaAssociatedBulk()
    {
        this.associatedReader.Decode(this.rgbaPlanes, this.rgbaDestination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.rgbaDestination[Length - 1, 0];
    }
}

/* Measured 2026-10-18

| Method               | Categories       | IsBigEndian |       Mean |       Error |    StdDev | Ratio | RatioSD | Allocated | Alloc Ratio |
| -------------------- | ---------------- | ----------- | ---------: | ----------: | --------: | ----: | ------: | --------: | ----------: |
| RgbScalar            | RGB16            | False       | 1,528.5 ns |   279.25 ns |  15.31 ns |  1.00 |    0.01 |         - |          NA |
| RgbBulk              | RGB16            | False       |   704.1 ns |   333.98 ns |  18.31 ns |  0.46 |    0.01 |         - |          NA |
|                      |                  |             |            |             |           |       |         |           |             |
| RgbScalar            | RGB16            | True        | 1,548.3 ns |    67.98 ns |   3.73 ns |  1.00 |    0.00 |         - |          NA |
| RgbBulk              | RGB16            | True        |   768.9 ns |   666.22 ns |  36.52 ns |  0.50 |    0.02 |         - |          NA |
|                      |                  |             |            |             |           |       |         |           |             |
| RgbaScalar           | RGBA16           | False       |   981.3 ns |   497.00 ns |  27.24 ns |  1.00 |    0.03 |         - |          NA |
| RgbaBulk             | RGBA16           | False       |   531.4 ns |   694.20 ns |  38.05 ns |  0.54 |    0.04 |         - |          NA |
|                      |                  |             |            |             |           |       |         |           |             |
| RgbaScalar           | RGBA16           | True        | 1,257.1 ns | 1,673.53 ns |  91.73 ns |  1.00 |    0.09 |         - |          NA |
| RgbaBulk             | RGBA16           | True        |   512.9 ns |   117.84 ns |   6.46 ns |  0.41 |    0.03 |         - |          NA |
|                      |                  |             |            |             |           |       |         |           |             |
| RgbaAssociatedScalar | RGBA16Associated | False       | 1,615.6 ns | 1,132.88 ns |  62.10 ns |  1.00 |    0.05 |         - |          NA |
| RgbaAssociatedBulk   | RGBA16Associated | False       |   979.2 ns |   534.25 ns |  29.28 ns |  0.61 |    0.03 |         - |          NA |
|                      |                  |             |            |             |           |       |         |           |             |
| RgbaAssociatedScalar | RGBA16Associated | True        | 1,917.0 ns | 2,682.04 ns | 147.01 ns |  1.00 |    0.10 |         - |          NA |
| RgbaAssociatedBulk   | RGBA16Associated | True        |   924.0 ns |   503.26 ns |  27.59 ns |  0.48 |    0.04 |         - |          NA |
*/

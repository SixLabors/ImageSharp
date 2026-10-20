// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Readers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Tiff;

/// <summary>
/// Compares scalar and bulk conversion for planar high-depth, planar Lab, and floating-point TIFF layouts.
/// </summary>
[Config(typeof(Config.Short))]
public class TiffPlanarAndFloatColorConversion
{
    private const int Length = 1024;
    private const float Scale24 = 1F / 0xFFFFFF;
    private const float Scale32 = 1F / uint.MaxValue;

    private readonly Image<RgbaVector> destination = new(Length, 1);
    private readonly ColorProfileConverter scalarConverter = new();
    private IMemoryOwner<byte>[] planes = [];
    private byte[] packed = [];
    private bool isPacked;
    private TiffBaseColorDecoder<RgbaVector> packedDecoder;
    private TiffBasePlanarColorDecoder<RgbaVector> planarDecoder;

    /// <summary>
    /// Identifies the stored component layout and alpha representation.
    /// </summary>
    public enum SampleLayout
    {
        /// <summary>
        /// Packed floating-point RGB samples.
        /// </summary>
        FloatRgb32,

        /// <summary>
        /// Packed straight floating-point RGBA samples.
        /// </summary>
        FloatRgba32,

        /// <summary>
        /// Packed associated floating-point RGBA samples.
        /// </summary>
        FloatRgba32Associated,

        /// <summary>
        /// Separate RGB planes with 24-bit unsigned samples.
        /// </summary>
        PlanarRgb24,

        /// <summary>
        /// Separate straight RGBA planes with 24-bit unsigned samples.
        /// </summary>
        PlanarRgba24,

        /// <summary>
        /// Separate associated RGBA planes with 24-bit unsigned samples.
        /// </summary>
        PlanarRgba24Associated,

        /// <summary>
        /// Separate RGB planes with 32-bit unsigned samples.
        /// </summary>
        PlanarRgb32,

        /// <summary>
        /// Separate straight RGBA planes with 32-bit unsigned samples.
        /// </summary>
        PlanarRgba32,

        /// <summary>
        /// Separate associated RGBA planes with 32-bit unsigned samples.
        /// </summary>
        PlanarRgba32Associated,

        /// <summary>
        /// Separate lightness and signed chroma planes with eight-bit samples.
        /// </summary>
        PlanarLab8,

        /// <summary>
        /// Separate lightness and signed chroma planes with sixteen-bit samples.
        /// </summary>
        PlanarLab16,
    }

    /// <summary>
    /// Gets or sets the TIFF component layout.
    /// </summary>
    [ParamsAllValues]
    public SampleLayout Layout { get; set; }

    /// <summary>
    /// Prepares equivalent big-endian component data and configures the actual TIFF reader outside measurement.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.isPacked = this.Layout is SampleLayout.FloatRgb32 or SampleLayout.FloatRgba32 or SampleLayout.FloatRgba32Associated;
        int channels = this.Layout switch
        {
            SampleLayout.FloatRgb32 or SampleLayout.PlanarRgb24 or SampleLayout.PlanarRgb32 or SampleLayout.PlanarLab8 or SampleLayout.PlanarLab16 => 3,
            _ => 4
        };
        int sampleBytes = this.Layout switch
        {
            SampleLayout.PlanarLab8 => 1,
            SampleLayout.PlanarLab16 => 2,
            SampleLayout.PlanarRgb24 or SampleLayout.PlanarRgba24 or SampleLayout.PlanarRgba24Associated => 3,
            _ => 4
        };

        this.planes = new IMemoryOwner<byte>[channels];
        Random random = new(42);

        for (int channel = 0; channel < channels; channel++)
        {
            this.planes[channel] = Configuration.Default.MemoryAllocator.Allocate<byte>(Length * sampleBytes);
            Span<byte> plane = this.planes[channel].GetSpan();
            random.NextBytes(plane);

            if (this.isPacked)
            {
                // Floating RGB includes negative and greater-than-one samples. Alpha stays in its normal opacity range.
                for (int i = 0; i < Length; i++)
                {
                    float value = channel == 3 ? random.NextSingle() : (random.NextSingle() * 2F) - .5F;
                    BinaryPrimitives.WriteSingleBigEndian(plane.Slice(i * sizeof(float), sizeof(float)), value);
                }
            }
        }

        if (this.isPacked)
        {
            this.packed = new byte[Length * channels * sampleBytes];

            for (int i = 0; i < Length; i++)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    // Interleave the same encoded words used by the scalar source reader.
                    this.planes[channel].GetSpan().Slice(i * sampleBytes, sampleBytes).CopyTo(
                        this.packed.AsSpan(((i * channels) + channel) * sampleBytes, sampleBytes));
                }
            }
        }

        switch (this.Layout)
        {
            case SampleLayout.FloatRgb32:
                this.packedDecoder = new TiffColorDecoder<RgbaVector, TiffFloat32Source, TiffRgbOperator>(
                    Configuration.Default,
                    new(true, false),
                    new(1F));

                break;
            case SampleLayout.FloatRgba32:
                this.packedDecoder = new TiffColorDecoder<RgbaVector, TiffFloat32Source, TiffRgbaOperator>(
                    Configuration.Default,
                    new(true, true),
                    new(1F, associated: false));

                break;
            case SampleLayout.FloatRgba32Associated:
                this.packedDecoder = new TiffColorDecoder<RgbaVector, TiffFloat32Source, TiffRgbaOperator>(
                    Configuration.Default,
                    new(true, true),
                    new(1F, associated: true));

                break;
            case SampleLayout.PlanarRgb24:
                this.planarDecoder = new TiffPlanarColorDecoder<RgbaVector, TiffRgb24Source, TiffRgbOperator>(
                    Configuration.Default,
                    new(true, true),
                    new(Scale24));

                break;
            case SampleLayout.PlanarRgba24:
                this.planarDecoder = new TiffPlanarColorDecoder<RgbaVector, TiffRgba24Source, TiffRgbaOperator>(
                    Configuration.Default,
                    new(true, true),
                    new(Scale24, associated: false));

                break;
            case SampleLayout.PlanarRgba24Associated:
                this.planarDecoder = new TiffPlanarColorDecoder<RgbaVector, TiffRgba24Source, TiffRgbaOperator>(
                    Configuration.Default,
                    new(true, true),
                    new(Scale24, associated: true));

                break;
            case SampleLayout.PlanarRgb32:
                this.planarDecoder = new TiffPlanarColorDecoder<RgbaVector, TiffRgb32Source, TiffRgbOperator>(
                    Configuration.Default,
                    new(true, true),
                    new(Scale32));

                break;
            case SampleLayout.PlanarRgba32:
                this.planarDecoder = new TiffPlanarColorDecoder<RgbaVector, TiffRgba32Source, TiffRgbaOperator>(
                    Configuration.Default,
                    new(true, true),
                    new(Scale32, associated: false));

                break;
            case SampleLayout.PlanarRgba32Associated:
                this.planarDecoder = new TiffPlanarColorDecoder<RgbaVector, TiffRgba32Source, TiffRgbaOperator>(
                    Configuration.Default,
                    new(true, true),
                    new(Scale32, associated: true));

                break;
            case SampleLayout.PlanarLab8:
                this.planarDecoder = new TiffLabPlanarColorDecoder<RgbaVector, TiffLab8Source>(
                    Configuration.Default,
                    new(true),
                    new(),
                    Configuration.Default.MemoryAllocator);

                break;
            case SampleLayout.PlanarLab16:
                this.planarDecoder = new TiffLabPlanarColorDecoder<RgbaVector, TiffLab16Source>(
                    Configuration.Default,
                    new(true, true),
                    new(),
                    Configuration.Default.MemoryAllocator);

                break;
        }
    }

    /// <summary>
    /// Releases the readers' conversion rows, encoded component planes, and destination.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.packedDecoder?.Dispose();
        this.planarDecoder?.Dispose();

        foreach (IMemoryOwner<byte> plane in this.planes)
        {
            plane.Dispose();
        }

        this.destination.Dispose();
    }

    /// <summary>
    /// Reads and converts the row through the existing scalar source and color APIs.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark(Baseline = true)]
    public RgbaVector Scalar() => this.Layout switch
    {
        SampleLayout.FloatRgb32 => this.ConvertScalar<TiffFloat32Source, TiffRgbOperator>(new(true, false), new(1F)),
        SampleLayout.FloatRgba32 => this.ConvertScalar<TiffFloat32Source, TiffRgbaOperator>(new(true, true), new(1F, associated: false)),
        SampleLayout.FloatRgba32Associated => this.ConvertScalar<TiffFloat32Source, TiffRgbaOperator>(new(true, true), new(1F, associated: true)),
        SampleLayout.PlanarRgb24 => this.ConvertScalar<TiffRgb24Source, TiffRgbOperator>(new(true, true), new(Scale24)),
        SampleLayout.PlanarRgba24 => this.ConvertScalar<TiffRgba24Source, TiffRgbaOperator>(new(true, true), new(Scale24, associated: false)),
        SampleLayout.PlanarRgba24Associated => this.ConvertScalar<TiffRgba24Source, TiffRgbaOperator>(new(true, true), new(Scale24, associated: true)),
        SampleLayout.PlanarRgb32 => this.ConvertScalar<TiffRgb32Source, TiffRgbOperator>(new(true, true), new(Scale32)),
        SampleLayout.PlanarRgba32 => this.ConvertScalar<TiffRgba32Source, TiffRgbaOperator>(new(true, true), new(Scale32, associated: false)),
        SampleLayout.PlanarRgba32Associated => this.ConvertScalar<TiffRgba32Source, TiffRgbaOperator>(new(true, true), new(Scale32, associated: true)),
        SampleLayout.PlanarLab8 => this.ConvertLabScalar<TiffLab8Source>(new(true)),
        SampleLayout.PlanarLab16 => this.ConvertLabScalar<TiffLab16Source>(new(true, true)),
        _ => throw new ArgumentOutOfRangeException(nameof(this.Layout))
    };

    /// <summary>
    /// Decodes the same component bytes through the actual TIFF bulk reader.
    /// </summary>
    /// <returns>The final converted pixel.</returns>
    [Benchmark]
    public RgbaVector Bulk()
    {
        if (this.isPacked)
        {
            this.packedDecoder.Decode(this.packed, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        }
        else
        {
            this.planarDecoder.Decode(this.planes, this.destination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        }

        return this.destination[Length - 1, 0];
    }

    /// <summary>
    /// Runs the scalar component reader and photometric operator with the same alpha packing as the bulk decoder.
    /// </summary>
    /// <typeparam name="TSource">The stored component layout.</typeparam>
    /// <typeparam name="TOperator">The photometric calculation.</typeparam>
    /// <param name="source">The scalar component reader.</param>
    /// <param name="colorOperator">The photometric calculation.</param>
    /// <returns>The final converted pixel.</returns>
    private RgbaVector ConvertScalar<TSource, TOperator>(TSource source, TOperator colorOperator)
        where TSource : struct, ITiffColorSource<float>
        where TOperator : struct, ITiffColorOperator<float>
    {
        ReadOnlySpan<byte> first = this.isPacked ? this.packed : this.planes[0].GetSpan();
        ReadOnlySpan<byte> second = this.isPacked ? [] : this.planes[1].GetSpan();
        ReadOnlySpan<byte> third = this.isPacked ? [] : this.planes[2].GetSpan();
        ReadOnlySpan<byte> fourth = !this.isPacked && this.planes.Length == 4 ? this.planes[3].GetSpan() : [];
        Span<RgbaVector> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        // Alpha interpretation is invariant for the complete row. Keep its decision outside the scalar loops.
        if (colorOperator.AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            for (int i = 0; i < Length; i++)
            {
                source.Read(first, second, third, fourth, i, out float r, out float g, out float b, out float a);
                colorOperator.Convert(ref r, ref g, ref b, ref a);
                row[i] = RgbaVector.FromAssociatedScaledVector4(new Vector4(r, g, b, a));
            }
        }
        else
        {
            for (int i = 0; i < Length; i++)
            {
                source.Read(first, second, third, fourth, i, out float r, out float g, out float b, out float a);
                colorOperator.Convert(ref r, ref g, ref b, ref a);
                row[i] = RgbaVector.FromUnassociatedScaledVector4(new Vector4(r, g, b, a));
            }
        }

        return row[^1];
    }

    /// <summary>
    /// Converts planar Lab samples through the scalar profile converter before packing floating RGB.
    /// </summary>
    /// <typeparam name="TSource">The Lab component layout.</typeparam>
    /// <param name="source">The scalar Lab reader.</param>
    /// <returns>The final converted pixel.</returns>
    private RgbaVector ConvertLabScalar<TSource>(TSource source)
        where TSource : struct, ITiffColorSource<float>
    {
        ReadOnlySpan<byte> lightness = this.planes[0].GetSpan();
        ReadOnlySpan<byte> chromaA = this.planes[1].GetSpan();
        ReadOnlySpan<byte> chromaB = this.planes[2].GetSpan();
        Span<RgbaVector> row = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int i = 0; i < Length; i++)
        {
            source.Read(lightness, chromaA, chromaB, [], i, out float l, out float a, out float b, out _);
            CieLab lab = new(l, a, b);
            Rgb rgb = this.scalarConverter.Convert<CieLab, Rgb>(in lab);
            row[i] = RgbaVector.FromUnassociatedScaledVector4(new Vector4(rgb.ToScaledVector3(), 1F));
        }

        return row[^1];
    }
}

/* Measured 2026-10-18

| Method | Layout                 |        Mean |      Error |    StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
| ------ | ---------------------- | ----------: | ---------: | --------: | ----: | ------: | -----: | --------: | ----------: |
| Scalar | FloatRgb32             |  1,250.7 ns |   654.2 ns |  35.86 ns |  1.00 |    0.04 |      - |         - |          NA |
| Bulk   | FloatRgb32             |    684.3 ns |   107.2 ns |   5.88 ns |  0.55 |    0.01 |      - |         - |          NA |
|        |                        |             |            |           |       |         |        |           |             |
| Scalar | FloatRgba32            |  1,652.0 ns |   200.2 ns |  10.98 ns |  1.00 |    0.01 |      - |         - |          NA |
| Bulk   | FloatRgba32            |    726.7 ns |   610.0 ns |  33.44 ns |  0.44 |    0.02 |      - |         - |          NA |
|        |                        |             |            |           |       |         |        |           |             |
| Scalar | FloatRgba32Associated  |  2,116.4 ns | 2,555.2 ns | 140.06 ns |  1.00 |    0.08 |      - |         - |          NA |
| Bulk   | FloatRgba32Associated  |  1,868.6 ns | 1,005.5 ns |  55.11 ns |  0.89 |    0.06 |      - |         - |          NA |
|        |                        |             |            |           |       |         |        |           |             |
| Scalar | PlanarRgb24            |  3,027.4 ns |   211.7 ns |  11.60 ns |  1.00 |    0.00 |      - |         - |          NA |
| Bulk   | PlanarRgb24            |  2,141.1 ns |   151.3 ns |   8.29 ns |  0.71 |    0.00 |      - |         - |          NA |
|        |                        |             |            |           |       |         |        |           |             |
| Scalar | PlanarRgba24           |  4,138.5 ns | 7,656.5 ns | 419.68 ns |  1.01 |    0.12 |      - |         - |          NA |
| Bulk   | PlanarRgba24           |  3,352.4 ns |   917.1 ns |  50.27 ns |  0.82 |    0.07 |      - |         - |          NA |
|        |                        |             |            |           |       |         |        |           |             |
| Scalar | PlanarRgba24Associated |  5,084.0 ns | 5,775.1 ns | 316.55 ns |  1.00 |    0.08 |      - |         - |          NA |
| Bulk   | PlanarRgba24Associated |  3,272.5 ns | 8,502.5 ns | 466.05 ns |  0.65 |    0.09 |      - |         - |          NA |
|        |                        |             |            |           |       |         |        |           |             |
| Scalar | PlanarRgb32            |  2,488.9 ns | 5,962.2 ns | 326.81 ns |  1.01 |    0.16 |      - |         - |          NA |
| Bulk   | PlanarRgb32            |  2,114.1 ns |   661.0 ns |  36.23 ns |  0.86 |    0.09 |      - |         - |          NA |
|        |                        |             |            |           |       |         |        |           |             |
| Scalar | PlanarRgba32           |  2,694.9 ns |   300.9 ns |  16.49 ns |  1.00 |    0.01 |      - |         - |          NA |
| Bulk   | PlanarRgba32           |  2,768.6 ns | 1,189.7 ns |  65.21 ns |  1.03 |    0.02 |      - |         - |          NA |
|        |                        |             |            |           |       |         |        |           |             |
| Scalar | PlanarRgba32Associated |  3,359.7 ns | 5,096.2 ns | 279.34 ns |  1.00 |    0.10 |      - |         - |          NA |
| Bulk   | PlanarRgba32Associated |  3,275.9 ns |   391.2 ns |  21.44 ns |  0.98 |    0.07 |      - |         - |          NA |
|        |                        |             |            |           |       |         |        |           |             |
| Scalar | PlanarLab8             | 50,799.6 ns | 4,921.6 ns | 269.77 ns |  1.00 |    0.01 |      - |         - |          NA |
| Bulk   | PlanarLab8             |  4,462.9 ns | 2,151.7 ns | 117.94 ns |  0.09 |    0.00 | 0.0153 |     288 B |          NA |
|        |                        |             |            |           |       |         |        |           |             |
| Scalar | PlanarLab16            | 51,345.1 ns | 1,175.6 ns |  64.44 ns |  1.00 |    0.00 |      - |         - |          NA |
| Bulk   | PlanarLab16            |  4,701.7 ns | 1,384.4 ns |  75.89 ns |  0.09 |    0.00 | 0.0153 |     288 B |          NA |
*/

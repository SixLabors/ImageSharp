// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

/// <summary>
/// Compares single-pixel extraction with the shared bulk conversion into RgbaVector for every named source format.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
[Config(typeof(Config.Short))]
[GenericTypeArguments(typeof(Argb32))]
[GenericTypeArguments(typeof(Abgr32))]
[GenericTypeArguments(typeof(Bgr24))]
[GenericTypeArguments(typeof(Bgra32))]
[GenericTypeArguments(typeof(L8))]
[GenericTypeArguments(typeof(L16))]
[GenericTypeArguments(typeof(La16))]
[GenericTypeArguments(typeof(La32))]
[GenericTypeArguments(typeof(Rgb24))]
[GenericTypeArguments(typeof(Rgba32))]
[GenericTypeArguments(typeof(Rgb48))]
[GenericTypeArguments(typeof(Rgba64))]
[GenericTypeArguments(typeof(Bgra5551))]
[GenericTypeArguments(typeof(Argb32P))]
[GenericTypeArguments(typeof(Abgr32P))]
[GenericTypeArguments(typeof(Bgra32P))]
[GenericTypeArguments(typeof(Rgba32P))]
public class ToRgbaVector<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private TPixel[] source = [];
    private RgbaVector[] destination = [];

    /// <summary>
    /// Gets or sets the number of converted pixels.
    /// </summary>
    [Params(64, 1024)]
    public int Count { get; set; }

    /// <summary>
    /// Creates reproducible straight colors and lets each source pixel format apply its own packing and alpha representation.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source = new TPixel[this.Count];
        this.destination = new RgbaVector[this.Count];
        Random random = new(42);

        for (int i = 0; i < this.source.Length; i++)
        {
            Vector4 color = new(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle());
            this.source[i] = TPixel.FromUnassociatedScaledVector4(color);
        }
    }

    /// <summary>
    /// Extracts each source pixel through the single-pixel contract.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark(Baseline = true)]
    public RgbaVector Scalar()
    {
        // Match the named single-pixel methods: associated channels normalize before unassociation.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            for (int i = 0; i < this.source.Length; i++)
            {
                this.destination[i] = RgbaVector.FromAssociatedScaledVector4(this.source[i].ToAssociatedScaledVector4());
            }
        }
        else
        {
            for (int i = 0; i < this.source.Length; i++)
            {
                this.destination[i] = RgbaVector.FromUnassociatedScaledVector4(this.source[i].ToUnassociatedScaledVector4());
            }
        }

        return this.destination[^1];
    }

    /// <summary>
    /// Converts the same pixels directly into their final floating storage through the source bulk API.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark]
    public RgbaVector Bulk()
    {
        PixelOperations<RgbaVector>.Instance.From(Configuration.Default, this.source, this.destination);
        return this.destination[^1];
    }
}

/* Measured 2026-10-18

| Source   | Method | Count |        Mean |     Error |    StdDev | Ratio | RatioSD | Allocated | Alloc Ratio |
| -------- | ------ | ----- | ----------: | --------: | --------: | ----: | ------: | --------: | ----------: |
| Abgr32   | Scalar | 1024  |  1,239.6 ns |  172.3 ns |   9.44 ns |  1.00 |         |         - |          NA |
| Abgr32   | Bulk   | 1024  |    170.3 ns |  131.8 ns |   7.22 ns |  0.14 |         |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Abgr32P  | Scalar | 1024  |  1,531.6 ns |  35.46 ns |   1.94 ns |  1.00 |         |         - |          NA |
| Abgr32P  | Bulk   | 1024  |    606.4 ns |  52.05 ns |   2.85 ns |  0.40 |         |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Argb32   | Scalar | 1024  |  1,088.4 ns |  95.93 ns |   5.26 ns |  1.00 |         |         - |          NA |
| Argb32   | Bulk   | 1024  |    157.6 ns |   3.16 ns |   0.17 ns |  0.14 |         |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Argb32P  | Scalar | 1024  |  1,522.2 ns | 173.40 ns |   9.50 ns |  1.00 |         |         - |          NA |
| Argb32P  | Bulk   | 1024  |    717.2 ns |  55.54 ns |   3.04 ns |  0.47 |         |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Bgr24    | Scalar | 1024  |  1,036.5 ns |  16.25 ns |   0.89 ns |  1.00 |         |         - |          NA |
| Bgr24    | Bulk   | 1024  |    202.7 ns | 173.44 ns |   9.51 ns |  0.20 |         |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Bgra32   | Scalar | 1024  |  1,171.3 ns |  537.2 ns |  29.45 ns |  1.00 |    0.03 |         - |          NA |
| Bgra32   | Bulk   | 1024  |    174.9 ns |  208.3 ns |  11.42 ns |  0.15 |    0.01 |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Bgra32P  | Scalar | 1024  |  1,517.9 ns |  50.19 ns |   2.75 ns |  1.00 |         |         - |          NA |
| Bgra32P  | Bulk   | 1024  |    703.3 ns |  26.69 ns |   1.46 ns |  0.46 |         |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Bgra5551 | Scalar | 1024  |    1.524 μs | 0.0347 μs | 0.0019 μs |  1.00 |         |         - |          NA |
| Bgra5551 | Bulk   | 1024  |    1.507 μs | 0.0562 μs | 0.0031 μs |  0.99 |         |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| L16      | Scalar | 1024  |   891.74 ns | 209.33 ns | 11.474 ns |  1.00 |    0.02 |         - |          NA |
| L16      | Bulk   | 1024  |    88.38 ns |  35.53 ns |  1.948 ns |  0.10 |    0.00 |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| L8       | Scalar | 1024  |    832.1 ns |  419.5 ns |  22.99 ns |  1.00 |    0.03 |         - |          NA |
| L8       | Bulk   | 1024  |    606.9 ns |  406.7 ns |  22.29 ns |  0.73 |    0.03 |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| La16     | Scalar | 1024  |    1.179 μs | 2.1467 μs | 0.1177 μs |  1.01 |    0.12 |         - |          NA |
| La16     | Bulk   | 1024  |    1.032 μs | 0.2616 μs | 0.0143 μs |  0.88 |    0.07 |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| La32     | Scalar | 1024  |    1.134 μs | 0.2237 μs | 0.0123 μs |  1.00 |         |         - |          NA |
| La32     | Bulk   | 1024  |    1.015 μs | 0.0895 μs | 0.0049 μs |  0.89 |         |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Rgb24    | Scalar | 1024  |  1,072.7 ns | 220.78 ns |  12.10 ns |  1.00 |         |         - |          NA |
| Rgb24    | Bulk   | 1024  |    179.9 ns |  96.50 ns |   5.29 ns |  0.17 |         |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Rgb48    | Scalar | 1024  |  1,574.3 ns |  366.5 ns |  20.09 ns |  1.00 |    0.02 |         - |          NA |
| Rgb48    | Bulk   | 1024  |    235.6 ns |  142.7 ns |   7.82 ns |  0.15 |    0.00 |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Rgba32   | Scalar | 1024  | 1,172.07 ns | 532.37 ns | 29.181 ns |  1.00 |    0.03 |         - |          NA |
| Rgba32   | Bulk   | 1024  |    94.51 ns | 153.48 ns |  8.413 ns |  0.08 |    0.01 |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Rgba32P  | Scalar | 1024  |  1,522.1 ns | 127.25 ns |   6.97 ns |  1.00 |         |         - |          NA |
| Rgba32P  | Bulk   | 1024  |    214.6 ns |   4.00 ns |   0.22 ns |  0.14 |         |         - |          NA |
|          |        |       |             |           |           |       |         |           |             |
| Rgba64   | Scalar | 1024  |  1,200.3 ns | 297.93 ns |  16.33 ns |  1.00 |    0.02 |         - |          NA |
| Rgba64   | Bulk   | 1024  |    136.8 ns |  54.06 ns |   2.96 ns |  0.11 |    0.00 |         - |          NA |
*/

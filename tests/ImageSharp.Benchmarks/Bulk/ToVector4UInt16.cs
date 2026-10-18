// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

/// <summary>
/// Compares scalar and bulk sixteen-bit extraction in both alpha representations.
/// </summary>
/// <typeparam name="TPixel">The unsigned sixteen-bit source pixel format.</typeparam>
[Config(typeof(Config.Short))]
[GenericTypeArguments(typeof(Rgb48))]
[GenericTypeArguments(typeof(Rgba64))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ToVector4UInt16<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private TPixel[] source = [];
    private Vector4[] destination = [];

    /// <summary>
    /// Gets or sets the number of converted pixels.
    /// </summary>
    [Params(64, 1024)]
    public int Count { get; set; }

    /// <summary>
    /// Creates reproducible full-precision pixels outside the timed methods.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source = new TPixel[this.Count];
        this.destination = new Vector4[this.Count];
        Random random = new(42);

        for (int i = 0; i < this.source.Length; i++)
        {
            Vector4 color = new(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle());
            this.source[i] = TPixel.FromUnassociatedScaledVector4(color);
        }
    }

    /// <summary>
    /// Extracts straight scaled components one pixel at a time.
    /// </summary>
    /// <returns>The last vector.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Unassociated")]
    public Vector4 UnassociatedScalar()
    {
        for (int i = 0; i < this.source.Length; i++)
        {
            this.destination[i] = this.source[i].ToUnassociatedScaledVector4();
        }

        return this.destination[^1];
    }

    /// <summary>
    /// Extracts the same straight components with the shared bulk API.
    /// </summary>
    /// <returns>The last vector.</returns>
    [Benchmark]
    [BenchmarkCategory("Unassociated")]
    public Vector4 UnassociatedBulk()
    {
        PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(Configuration.Default, this.source, this.destination);
        return this.destination[^1];
    }

    /// <summary>
    /// Extracts associated scaled components one pixel at a time.
    /// </summary>
    /// <returns>The last vector.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Associated")]
    public Vector4 AssociatedScalar()
    {
        for (int i = 0; i < this.source.Length; i++)
        {
            this.destination[i] = this.source[i].ToAssociatedScaledVector4();
        }

        return this.destination[^1];
    }

    /// <summary>
    /// Extracts the same associated components with the shared bulk API.
    /// </summary>
    /// <returns>The last vector.</returns>
    [Benchmark]
    [BenchmarkCategory("Associated")]
    public Vector4 AssociatedBulk()
    {
        PixelOperations<TPixel>.Instance.ToAssociatedScaledVector4(Configuration.Default, this.source, this.destination);
        return this.destination[^1];
    }
}

/* Measured 2026-10-18

| Source | Method             | Categories   | Count |       Mean |     Error |   StdDev | Ratio | RatioSD | Allocated | Alloc Ratio |
| ------ | ------------------ | ------------ | ----- | ---------: | --------: | -------: | ----: | ------: | --------: | ----------: |
| Rgb48  | AssociatedScalar   | Associated   | 1024  | 1,503.5 ns |  43.11 ns |  2.36 ns |  1.00 |         |         - |          NA |
| Rgb48  | AssociatedBulk     | Associated   | 1024  |   237.6 ns |  42.55 ns |  2.33 ns |  0.16 |         |         - |          NA |
|        |                    |              |       |            |           |          |       |         |           |             |
| Rgb48  | UnassociatedScalar | Unassociated | 1024  | 1,523.3 ns |   6.00 ns |  0.33 ns |  1.00 |         |         - |          NA |
| Rgb48  | UnassociatedBulk   | Unassociated | 1024  |   232.7 ns |  37.59 ns |  2.06 ns |  0.15 |         |         - |          NA |
|        |                    |              |       |            |           |          |       |         |           |             |
| Rgba64 | AssociatedScalar   | Associated   | 1024  | 1,055.1 ns | 322.15 ns | 17.66 ns |  1.00 |    0.02 |         - |          NA |
| Rgba64 | AssociatedBulk     | Associated   | 1024  |   235.3 ns | 278.19 ns | 15.25 ns |  0.22 |    0.01 |         - |          NA |
|        |                    |              |       |            |           |          |       |         |           |             |
| Rgba64 | UnassociatedScalar | Unassociated | 1024  |   978.6 ns | 320.11 ns | 17.55 ns |  1.00 |    0.02 |         - |          NA |
| Rgba64 | UnassociatedBulk   | Unassociated | 1024  |   131.8 ns |  14.38 ns |  0.79 ns |  0.13 |    0.00 |         - |          NA |
*/

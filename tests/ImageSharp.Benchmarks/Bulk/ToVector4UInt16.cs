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
[GenericTypeArguments(typeof(L16))]
[GenericTypeArguments(typeof(La32))]
[GenericTypeArguments(typeof(Rg32))]
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

| Source | Method             | Categories   | Count |         Mean |       Error |     StdDev | Ratio | RatioSD | Allocated | Alloc Ratio |
| ------ | ------------------ | ------------ | ----- | -----------: | ----------: | ---------: | ----: | ------: | --------: | ----------: |
| L16    | AssociatedScalar   | Associated   | 64    |     50.67 ns |   20.827 ns |   1.142 ns |  1.00 |    0.03 |         - |          NA |
| L16    | AssociatedBulk     | Associated   | 64    |     11.02 ns |    0.738 ns |   0.040 ns |  0.22 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| L16    | AssociatedScalar   | Associated   | 1024  |    835.27 ns |  379.926 ns |  20.825 ns |  1.00 |    0.03 |         - |          NA |
| L16    | AssociatedBulk     | Associated   | 1024  |     93.78 ns |   50.435 ns |   2.765 ns |  0.11 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| L16    | UnassociatedScalar | Unassociated | 64    |     50.44 ns |    9.207 ns |   0.505 ns |  1.00 |    0.01 |         - |          NA |
| L16    | UnassociatedBulk   | Unassociated | 64    |     10.94 ns |    0.509 ns |   0.028 ns |  0.22 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| L16    | UnassociatedScalar | Unassociated | 1024  |    841.55 ns |  442.277 ns |  24.243 ns |  1.00 |    0.04 |         - |          NA |
| L16    | UnassociatedBulk   | Unassociated | 1024  |     91.87 ns |   12.257 ns |   0.672 ns |  0.11 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| La32   | AssociatedScalar   | Associated   | 64    |     75.21 ns |   69.735 ns |   3.822 ns |  1.00 |    0.06 |         - |          NA |
| La32   | AssociatedBulk     | Associated   | 64    |     19.68 ns |    2.169 ns |   0.119 ns |  0.26 |    0.01 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| La32   | AssociatedScalar   | Associated   | 1024  |  1,206.64 ns |  171.099 ns |   9.379 ns |  1.00 |    0.01 |         - |          NA |
| La32   | AssociatedBulk     | Associated   | 1024  |    188.48 ns |   48.467 ns |   2.657 ns |  0.16 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| La32   | UnassociatedScalar | Unassociated | 64    |     63.50 ns |   10.803 ns |   0.592 ns |  1.00 |    0.01 |         - |          NA |
| La32   | UnassociatedBulk   | Unassociated | 64    |     10.90 ns |   11.663 ns |   0.639 ns |  0.17 |    0.01 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| La32   | UnassociatedScalar | Unassociated | 1024  |  1,030.27 ns |  159.927 ns |   8.766 ns |  1.00 |    0.01 |         - |          NA |
| La32   | UnassociatedBulk   | Unassociated | 1024  |    101.83 ns |   85.635 ns |   4.694 ns |  0.10 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rg32   | AssociatedScalar   | Associated   | 64    |    42.813 ns |  16.2429 ns |  0.8903 ns |  1.00 |    0.03 |         - |          NA |
| Rg32   | AssociatedBulk     | Associated   | 64    |    14.129 ns |   3.7253 ns |  0.2042 ns |  0.33 |    0.01 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rg32   | AssociatedScalar   | Associated   | 1024  |   680.702 ns | 353.7679 ns | 19.3912 ns |  1.00 |    0.04 |         - |          NA |
| Rg32   | AssociatedBulk     | Associated   | 1024  |   100.810 ns |  10.8027 ns |  0.5921 ns |  0.15 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rg32   | UnassociatedScalar | Unassociated | 64    |    43.220 ns |  36.5518 ns |  2.0035 ns |  1.00 |    0.06 |         - |          NA |
| Rg32   | UnassociatedBulk   | Unassociated | 64    |     9.919 ns |   0.8220 ns |  0.0451 ns |  0.23 |    0.01 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rg32   | UnassociatedScalar | Unassociated | 1024  |   721.544 ns | 349.2846 ns | 19.1455 ns |  1.00 |    0.03 |         - |          NA |
| Rg32   | UnassociatedBulk   | Unassociated | 1024  |   110.353 ns |  32.5550 ns |  1.7844 ns |  0.15 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rgb48  | AssociatedScalar   | Associated   | 64    |     96.59 ns |   32.076 ns |   1.758 ns |  1.00 |    0.02 |         - |          NA |
| Rgb48  | AssociatedBulk     | Associated   | 64    |     22.04 ns |   53.588 ns |   2.937 ns |  0.23 |    0.03 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rgb48  | AssociatedScalar   | Associated   | 1024  |  1,563.97 ns |  485.675 ns |  26.621 ns |  1.00 |    0.02 |         - |          NA |
| Rgb48  | AssociatedBulk     | Associated   | 1024  |    247.89 ns |  150.670 ns |   8.259 ns |  0.16 |    0.01 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rgb48  | UnassociatedScalar | Unassociated | 64    |     94.17 ns |    2.547 ns |   0.140 ns |  1.00 |    0.00 |         - |          NA |
| Rgb48  | UnassociatedBulk   | Unassociated | 64    |     16.20 ns |    4.138 ns |   0.227 ns |  0.17 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rgb48  | UnassociatedScalar | Unassociated | 1024  |  1,517.66 ns |   31.153 ns |   1.708 ns |  1.00 |    0.00 |         - |          NA |
| Rgb48  | UnassociatedBulk   | Unassociated | 1024  |    233.85 ns |   51.750 ns |   2.837 ns |  0.15 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rgba64 | AssociatedScalar   | Associated   | 64    |    65.140 ns |   38.345 ns |  2.1018 ns |  1.00 |    0.04 |         - |          NA |
| Rgba64 | AssociatedBulk     | Associated   | 64    |    20.286 ns |    1.099 ns |  0.0603 ns |  0.31 |    0.01 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rgba64 | AssociatedScalar   | Associated   | 1024  | 1,017.931 ns |  614.304 ns | 33.6721 ns |  1.00 |    0.04 |         - |          NA |
| Rgba64 | AssociatedBulk     | Associated   | 1024  |   223.050 ns |   18.426 ns |  1.0100 ns |  0.22 |    0.01 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rgba64 | UnassociatedScalar | Unassociated | 64    |    60.407 ns |   31.996 ns |  1.7538 ns |  1.00 |    0.04 |         - |          NA |
| Rgba64 | UnassociatedBulk   | Unassociated | 64    |     9.218 ns |    2.579 ns |  0.1414 ns |  0.15 |    0.00 |         - |          NA |
|        |                    |              |       |              |             |            |       |         |           |             |
| Rgba64 | UnassociatedScalar | Unassociated | 1024  |   965.061 ns |  506.989 ns | 27.7898 ns |  1.00 |    0.04 |         - |          NA |
| Rgba64 | UnassociatedBulk   | Unassociated | 1024  |   133.177 ns |   57.225 ns |  3.1367 ns |  0.14 |    0.00 |         - |          NA |
*/

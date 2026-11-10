// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks;

/// <summary>
/// Measures direct SIMD sRGB companding and compares it with the scalar curve formulas.
/// </summary>
[Config(typeof(Config.Short))]
public abstract class CompandingComparison
{
    // Match the direct-formula accuracy bound documented and checked by CompandingTests.
    // That test suite has a separate, tighter test of the agreement between scalar and SIMD.
    private const float DirectApproximationTolerance = 5E-4F;

    private Vector4[] source;
    private Vector4[] destination;

    /// <summary>
    /// Gets or sets the number of RGBA colors in a row or larger batch.
    /// </summary>
    [Params(256, 65536)]
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets whether to encode linear RGB instead of decoding sRGB.
    /// </summary>
    [Params(false, true)]
    public bool Compress { get; set; }

    /// <summary>
    /// Gets whether the input includes negative RGB and RGB above reference white.
    /// </summary>
    protected abstract bool HasHdrValues { get; }

    /// <summary>
    /// Creates repeatable inputs, warms the existing tables and checks the SIMD candidate before timing it.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Match the existing companding tests' seeded input generation. HDR RGB spans -0.25 to 16,
        // where one is reference white. Alpha stays in its original range and is never companded.
        Random random = new(42);
        this.source = random.GenerateRandomVectorArray(this.Count, 0F, 1F);
        this.destination = new Vector4[this.Count];

        if (this.HasHdrValues)
        {
            for (int i = 0; i < this.source.Length; i++)
            {
                Vector4 value = this.source[i];
                this.source[i] = new Vector4((value.X * 16.25F) - 0.25F, (value.Y * 16.25F) - 0.25F, (value.Z * 16.25F) - 0.25F, value.W);
            }
        }

        // Measured methods include the same input copy,
        // because repeated in-place companding would otherwise change the input distribution.
        this.Simd();

        for (int i = 0; i < this.source.Length; i++)
        {
            Vector4 input = this.source[i];
            Vector4 expected = this.Compress
                ? new Vector4(CompressComponent(input.X), CompressComponent(input.Y), CompressComponent(input.Z), input.W)
                : new Vector4(ExpandComponent(input.X), ExpandComponent(input.Y), ExpandComponent(input.Z), input.W);
            Vector4 actual = this.destination[i];

            // Apply the established direct-formula bound relative to max(1, abs(expected)).
            // The absolute floor near zero also covers negative and small HDR inputs.
            Vector4 error = Vector4.Abs(actual - expected);
            Vector4 limit = Vector4.Max(Vector4.Abs(expected), Vector4.One) * DirectApproximationTolerance;
            if (error.X > limit.X || error.Y > limit.Y || error.Z > limit.Z || actual.W != input.W)
            {
                throw new InvalidOperationException($"SIMD companding differs at color {i}: expected {expected}, actual {actual}.");
            }
        }
    }

    /// <summary>
    /// Applies direct SIMD companding to the same input on every invocation.
    /// </summary>
    [Benchmark]
    public void Simd()
    {
        this.CopySource();
        if (this.Compress)
        {
            SRgbCompanding.Compress(this.destination);
        }
        else
        {
            SRgbCompanding.Expand(this.destination);
        }
    }

    /// <summary>
    /// Restores the input without allocating during measurement.
    /// </summary>
    protected void CopySource() => this.source.AsSpan().CopyTo(this.destination);

    /// <summary>
    /// Computes the scalar IEC sRGB expansion baseline without clipping.
    /// </summary>
    /// <param name="value">The signed input component.</param>
    /// <returns>The linear component.</returns>
    private static float ExpandComponent(float value)
    {
        float magnitude = MathF.Abs(value);
        float result = magnitude <= 0.04045F ? magnitude / 12.92F : MathF.Pow((magnitude + 0.055F) / 1.055F, 2.4F);
        return MathF.CopySign(result, value);
    }

    /// <summary>
    /// Computes the scalar IEC sRGB compression baseline without clipping.
    /// </summary>
    /// <param name="value">The signed linear component.</param>
    /// <returns>The nonlinear component.</returns>
    private static float CompressComponent(float value)
    {
        float magnitude = MathF.Abs(value);
        float result = magnitude <= 0.04045F / 12.92F ? magnitude * 12.92F : (1.055F * MathF.Pow(magnitude, 1F / 2.4F)) - 0.055F;
        return MathF.CopySign(result, value);
    }

    /// <summary>
    /// Measures direct SIMD on normalized input.
    /// </summary>
    public class Sdr : CompandingComparison
    {
        /// <inheritdoc/>
        protected override bool HasHdrValues => false;
    }

    // Measured 10.11.2026. AMD RYZEN AI MAX+ 395, .NET 10.0.14, X64 RyuJIT x86-64-v4.
    // Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1  WarmupCount=3
    //
    // Sdr
    // | Method | Count | Compress | Mean         | Error        | StdDev      | Allocated |
    // |------- |------ |--------- |-------------:|-------------:|------------:|----------:|
    // | Simd   | 256   | False    |     416.6 ns |     11.53 ns |     0.63 ns |         - |
    // | Simd   | 256   | True     |     395.3 ns |     30.18 ns |     1.65 ns |         - |
    // | Simd   | 65536 | False    | 117,395.4 ns | 42,704.13 ns | 2,340.76 ns |         - |
    // | Simd   | 65536 | True     | 114,195.8 ns | 22,651.89 ns | 1,241.63 ns |         - |
    //
    // Hdr
    // | Method | Count | Compress | Mean           | Error         | StdDev       | Ratio | Allocated | Alloc Ratio |
    // |------- |------ |--------- |---------------:|--------------:|-------------:|------:|----------:|------------:|
    // | Simd   | 256   | False    |       438.5 ns |     322.80 ns |     17.69 ns |  0.10 |         - |          NA |
    // | Scalar | 256   | False    |     4,417.1 ns |     750.46 ns |     41.14 ns |  1.00 |         - |          NA |
    // |        |       |          |                |               |              |       |           |             |
    // | Simd   | 256   | True     |       395.4 ns |      15.45 ns |      0.85 ns |  0.10 |         - |          NA |
    // | Scalar | 256   | True     |     4,079.1 ns |     856.95 ns |     46.97 ns |  1.00 |         - |          NA |
    // |        |       |          |                |               |              |       |           |             |
    // | Simd   | 65536 | False    |   113,980.1 ns |  15,382.86 ns |    843.19 ns |  0.10 |         - |          NA |
    // | Scalar | 65536 | False    | 1,199,745.6 ns | 191,714.60 ns | 10,508.52 ns |  1.00 |         - |          NA |
    // |        |       |          |                |               |              |       |           |             |
    // | Simd   | 65536 | True     |   109,456.0 ns |   7,383.75 ns |    404.73 ns |  0.10 |         - |          NA |
    // | Scalar | 65536 | True     | 1,087,719.8 ns |  41,850.90 ns |  2,293.99 ns |  1.00 |         - |          NA |

    /// <summary>
    /// Compares scalar and SIMD companding on signed HDR input.
    /// </summary>
    public class Hdr : CompandingComparison
    {
        /// <inheritdoc/>
        protected override bool HasHdrValues => true;

        /// <summary>
        /// Applies the scalar formula without clamping RGB or changing alpha.
        /// </summary>
        [Benchmark(Baseline = true)]
        public void Scalar()
        {
            this.CopySource();
            if (this.Compress)
            {
                for (int i = 0; i < this.destination.Length; i++)
                {
                    Vector4 value = this.destination[i];
                    this.destination[i] = new Vector4(CompressComponent(value.X), CompressComponent(value.Y), CompressComponent(value.Z), value.W);
                }
            }
            else
            {
                for (int i = 0; i < this.destination.Length; i++)
                {
                    Vector4 value = this.destination[i];
                    this.destination[i] = new Vector4(ExpandComponent(value.X), ExpandComponent(value.Y), ExpandComponent(value.Z), value.W);
                }
            }
        }
    }
}

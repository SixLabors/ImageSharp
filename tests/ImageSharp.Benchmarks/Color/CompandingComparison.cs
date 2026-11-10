// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks;

/// <summary>
/// Measures direct SIMD sRGB companding and checks it against the scalar curve formulas.
/// </summary>
[Config(typeof(Config.Short))]
public abstract class CompandingComparison
{
    // Match the direct-formula accuracy bound documented and checked by CompandingTests.
    // Scalar/SIMD agreement has a separate, tighter check in that test suite.
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

    // Measured 2026-10-15.
    // | Method | Count | Compress | Mean         | Error       | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
    // |------- |------ |--------- |-------------:|------------:|-----------:|------:|--------:|----------:|------------:|
    // | Simd   | 256   | False    |     1.335 us |   0.0239 us |  0.0062 us |  0.30 |    0.00 |         - |          NA |
    // | Scalar | 256   | False    |     4.410 us |   0.2177 us |  0.0565 us |  1.00 |    0.02 |         - |          NA |
    // |        |       |          |              |             |            |       |         |           |             |
    // | Simd   | 256   | True     |     2.123 us |   0.0187 us |  0.0049 us |  0.50 |    0.01 |         - |          NA |
    // | Scalar | 256   | True     |     4.243 us |   0.4756 us |  0.1235 us |  1.00 |    0.04 |         - |          NA |
    // |        |       |          |              |             |            |       |         |           |             |
    // | Simd   | 65536 | False    |   223.890 us |  33.2439 us |  8.6333 us |  0.18 |    0.01 |         - |          NA |
    // | Scalar | 65536 | False    | 1,261.450 us | 335.0455 us | 51.8487 us |  1.00 |    0.05 |         - |          NA |
    // |        |       |          |              |             |            |       |         |           |             |
    // | Simd   | 65536 | True     |   240.204 us |  87.9851 us | 22.8495 us |  0.21 |    0.02 |         - |          NA |
    // | Scalar | 65536 | True     | 1,127.392 us | 158.7023 us | 41.2145 us |  1.00 |    0.05 |         - |          NA |

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

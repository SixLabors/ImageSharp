// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures the flow field upscale of the last 1080p pyramid level at each available intrinsic tier, against the
/// per-entry tap sums that it replaces.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1FlowFieldBenchmarks
{
    /// <summary>
    /// The entries across the level that doubles to the finest 1080p flow field.
    /// </summary>
    private const int Width = 120;

    /// <summary>
    /// The entries down the level that doubles to the finest 1080p flow field.
    /// </summary>
    private const int Height = 67;

    private const int Border = Av1FlowField.BorderOuter;

    private const int Stride = (2 * Width) + (2 * Border);

    private static readonly double[] LowerPhase = [-3 / 128.0, 29 / 128.0, 111 / 128.0, -9 / 128.0];

    private static readonly double[] UpperPhase = [-9 / 128.0, 111 / 128.0, 29 / 128.0, -3 / 128.0];

    private readonly double[] component = new double[Stride * ((2 * Height) + (2 * Border))];

    private readonly double[] scratch = new double[Stride * (Height + (2 * Border))];

    /// <summary>
    /// Populates a deterministic smooth field outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < this.component.Length; i++)
        {
            this.component[i] = Math.Sin(i * 0.01) * 3.5;
        }

        for (int i = 0; i < this.scratch.Length; i++)
        {
            this.scratch[i] = Math.Cos(i * 0.013) * 2.5;
        }
    }

    /// <summary>
    /// Measures both passes.
    /// </summary>
    /// <returns>The final entry, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Upscale")]
    public double Upscale()
    {
        int origin = (Border * Stride) + Border;
        for (int row = 0; row < Height; row++)
        {
            Av1FlowField.UpscaleRow(
                this.component.AsSpan(origin + (row * Stride) - Border, Width + (2 * Border)),
                this.scratch.AsSpan(origin + (row * Stride), 2 * Width));
        }

        for (int row = 0; row < Height; row++)
        {
            int output = origin + (2 * row * Stride);
            Av1FlowField.UpscaleColumns(
                this.scratch.AsSpan(origin + ((row - Border) * Stride)),
                Stride,
                this.component.AsSpan(output, 2 * Width),
                this.component.AsSpan(output + Stride, 2 * Width));
        }

        return this.component[origin];
    }

    /// <summary>
    /// Measures the per-entry tap sums that <see cref="Upscale"/> replaced.
    /// </summary>
    /// <returns>The final entry, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Upscale")]
    public double UpscaleScalar()
    {
        int origin = (Border * Stride) + Border;
        for (int row = 0; row < Height; row++)
        {
            int input = origin + (row * Stride);
            for (int column = 0; column < Width; column++)
            {
                double left = 0;
                double right = 0;
                for (int tap = 0; tap < 4; tap++)
                {
                    left += this.component[input + column + tap - 2] * LowerPhase[tap];
                    right += this.component[input + column + tap - 1] * UpperPhase[tap];
                }

                this.scratch[input + (2 * column)] = 2.0 * left;
                this.scratch[input + (2 * column) + 1] = 2.0 * right;
            }
        }

        for (int row = 0; row < Height; row++)
        {
            int output = origin + (2 * row * Stride);
            for (int column = 0; column < 2 * Width; column++)
            {
                double top = 0;
                double bottom = 0;
                for (int tap = 0; tap < 4; tap++)
                {
                    int input = origin + ((row + tap - 2) * Stride) + column;
                    top += this.scratch[input] * LowerPhase[tap];
                    bottom += this.scratch[input + Stride] * UpperPhase[tap];
                }

                this.component[output + column] = top;
                this.component[output + Stride + column] = bottom;
            }
        }

        return this.component[origin];
    }

    /// <summary>
    /// Configures production-process measurements for the default, AVX2, Vector128, and scalar paths.
    /// </summary>
    public sealed class Configuration : ManualConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        public Configuration()
        {
            this.AddJob(
                Job.ShortRun
                    .WithId("Hardware")
                    .AsBaseline());

            this.AddJob(
                Job.ShortRun
                    .WithId("Avx2")
                    .WithEnvironmentVariable("DOTNET_EnableAVX512", "0"));

            this.AddJob(
                Job.ShortRun
                    .WithId("Vector128")
                    .WithEnvironmentVariable("DOTNET_EnableAVX512", "0")
                    .WithEnvironmentVariable("DOTNET_EnableAVX2", "0"));

            this.AddJob(
                Job.ShortRun
                    .WithId("Scalar")
                    .WithEnvironmentVariable("DOTNET_EnableHWIntrinsic", "0"));
        }
    }
}

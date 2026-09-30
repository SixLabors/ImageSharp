// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures the loop-restoration search statistics of one 128x64 eight-bit unit at each available intrinsic tier.
/// The scalar job runs the scalar operator overloads that the vector tiers replace.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1LoopRestorationStatisticsBenchmarks
{
    private const int Width = 128;
    private const int Height = 64;
    private const int Stride = Width + 8;

    private readonly byte[] original = new byte[Stride * (Height + 8)];
    private readonly byte[] reconstructed = new byte[Stride * (Height + 8)];
    private readonly int[] first = new int[Width];
    private readonly int[] second = new int[Width];
    private readonly long[] moments = new long[5];

    /// <summary>
    /// Populates deterministic samples and filtered values outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < this.original.Length; i++)
        {
            this.original[i] = (byte)((i * 7) + (i / Stride));
            this.reconstructed[i] = (byte)((i * 7) + (i / Stride) + ((i * 13) % 5) - 2);
        }

        for (int i = 0; i < Width; i++)
        {
            this.first[i] = (this.reconstructed[i] << 4) + ((i * 37) % 200) - 100;
            this.second[i] = (this.reconstructed[i] << 4) + ((i * 53) % 160) - 80;
        }
    }

    /// <summary>
    /// Measures the 49 correlations of one seven-tap Wiener window position against every other position.
    /// </summary>
    /// <returns>The final correlation, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Correlation")]
    public long Correlation()
    {
        long total = 0;
        for (int tap = 0; tap < 49; tap++)
        {
            int offset = ((tap % 7) * Stride) + (tap / 7);
            total += Av1LoopRestorationEncoder.MeasureCorrelation<byte>(
                this.reconstructed, Stride, this.reconstructed.AsSpan(offset), Stride, Width, Height, 120, 1);
        }

        return total;
    }

    /// <summary>
    /// Measures the projection moments and error of every row of the unit.
    /// </summary>
    /// <returns>The total error, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Projection")]
    public long Projection()
    {
        long total = 0;
        for (int row = 0; row < Height; row++)
        {
            total += Av1LoopRestorationEncoder.MeasureProjectionRow<byte>(
                this.original.AsSpan(row * Stride, Width),
                this.reconstructed.AsSpan(row * Stride, Width),
                this.first,
                this.second,
                true,
                true,
                -40,
                100,
                this.moments);
        }

        return total;
    }

    /// <summary>
    /// Measures the sample moments of the unit.
    /// </summary>
    /// <returns>The sample sum, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Moments")]
    public long Moments()
        => Av1LoopRestorationEncoder.MeasureSampleMoments(
            new Av1PlaneRegion<byte>(this.original, Stride, new Rectangle(0, 0, Width, Height)), out _);

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

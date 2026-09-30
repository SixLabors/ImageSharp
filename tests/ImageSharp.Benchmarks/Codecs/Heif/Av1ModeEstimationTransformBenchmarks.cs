// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures the intra mode-estimation transforms over one 64-sample row of blocks at each available intrinsic tier.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1ModeEstimationTransformBenchmarks
{
    /// <summary>
    /// The width of the residual row, one 64x64 superblock.
    /// </summary>
    private const int Width = 64;

    /// <summary>
    /// The number of times each row is transformed per measurement.
    /// </summary>
    private const int Repetitions = 256;

    /// <summary>
    /// The residual samples.
    /// </summary>
    private readonly short[] residual = new short[Width * 16];

    /// <summary>
    /// The transformed coefficients of one row of blocks.
    /// </summary>
    private readonly int[] coefficients = new int[Width * 16];

    /// <summary>
    /// The transform scratch.
    /// </summary>
    private readonly int[] workspace = new int[Av1TransformWorkspace.MaximumLength];

    /// <summary>
    /// Populates deterministic residuals outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < this.residual.Length; i++)
        {
            this.residual[i] = (short)(((i * 7919) % 511) - 255);
        }
    }

    /// <summary>
    /// Measures sixteen 4x4 DCTs per row.
    /// </summary>
    /// <returns>The final coefficient, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("4x4")]
    public int Dct4x4() => this.Transform(4);

    /// <summary>
    /// Measures eight 8x8 Hadamard transforms per row.
    /// </summary>
    /// <returns>The final coefficient, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("8x8")]
    public int Hadamard8x8() => this.Transform(8);

    /// <summary>
    /// Measures four 16x16 Hadamard transforms per row.
    /// </summary>
    /// <returns>The final coefficient, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("16x16")]
    public int Hadamard16x16() => this.Transform(16);

    /// <summary>
    /// Measures Hadamard costs of 32x32 blocks, which exercise the 4x4 transform and quadrant combines.
    /// </summary>
    /// <returns>The final cost, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Cost32x32")]
    public long HadamardCost32x32()
    {
        long cost = 0;
        for (int repetition = 0; repetition < Repetitions / 4; repetition++)
        {
            cost += Av1ForwardTransformer.GetHadamardCost(this.residual, Width, 16, false, this.coefficients, this.workspace);
        }

        return cost;
    }

    /// <summary>
    /// Transforms one row of blocks repeatedly.
    /// </summary>
    /// <param name="size">The square transform width.</param>
    /// <returns>The final coefficient.</returns>
    private int Transform(int size)
    {
        for (int repetition = 0; repetition < Repetitions; repetition++)
        {
            Av1ForwardTransformer.TransformRowForModeEstimation(this.residual, Width, size, Width / size, this.coefficients, this.workspace, false);
        }

        return this.coefficients[^1];
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

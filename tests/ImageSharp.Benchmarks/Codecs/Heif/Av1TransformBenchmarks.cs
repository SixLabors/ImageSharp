// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures the production AV1 forward-transform pipeline at the runtime's selected vector width.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1TransformBenchmarks
{
    private readonly short[] spatial = new short[64 * 64];
    private readonly int[] coefficients = new int[32 * 32];
    private readonly int[] workspace = new int[Av1TransformWorkspace.MaximumLength];

    /// <summary>
    /// Gets or sets the coded sample bit depth used by the transform.
    /// </summary>
    [Params(8, 12)]
    public int BitDepth { get; set; }

    /// <summary>
    /// Initializes deterministic residual data outside the measured operations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int index = 0; index < this.spatial.Length; index++)
        {
            this.spatial[index] = (short)(((index * 73) % 511) - 255);
        }
    }

    /// <summary>
    /// Measures an eight-by-eight forward DCT block.
    /// </summary>
    /// <returns>The last coefficient written by the transform.</returns>
    [Benchmark]
    [BenchmarkCategory("Forward8x8")]
    public int Forward8x8()
    {
        Av1ForwardTransformer.Transform2d(
            this.spatial,
            this.coefficients,
            8,
            Av1TransformType.DctDct,
            Av1TransformSize.Size8x8,
            this.BitDepth,
            this.workspace);

        return this.coefficients[63];
    }

    /// <summary>
    /// Measures a thirty-two-by-thirty-two forward DCT block.
    /// </summary>
    /// <returns>The last coefficient written by the transform.</returns>
    [Benchmark]
    [BenchmarkCategory("Forward32x32")]
    public int Forward32x32()
    {
        Av1ForwardTransformer.Transform2d(
            this.spatial,
            this.coefficients,
            32,
            Av1TransformType.DctDct,
            Av1TransformSize.Size32x32,
            this.BitDepth,
            this.workspace);

        return this.coefficients[^1];
    }

    /// <summary>
    /// Measures the packed-to-expanded boundary of a thirty-two-by-sixty-four forward DCT block.
    /// </summary>
    /// <returns>The last coded coefficient written by the transform.</returns>
    [Benchmark]
    [BenchmarkCategory("Forward32x64")]
    public int Forward32x64()
    {
        Av1ForwardTransformer.Transform2d(
            this.spatial,
            this.coefficients,
            32,
            Av1TransformType.DctDct,
            Av1TransformSize.Size32x64,
            this.BitDepth,
            this.workspace);

        return this.coefficients[^1];
    }

    /// <summary>
    /// Measures a sixty-four-by-sixty-four forward DCT block with the normative coefficient truncation.
    /// </summary>
    /// <returns>The last coded coefficient written by the transform.</returns>
    [Benchmark]
    [BenchmarkCategory("Forward64x64")]
    public int Forward64x64()
    {
        Av1ForwardTransformer.Transform2d(
            this.spatial,
            this.coefficients,
            64,
            Av1TransformType.DctDct,
            Av1TransformSize.Size64x64,
            this.BitDepth,
            this.workspace);

        return this.coefficients[^1];
    }

    /// <summary>
    /// Configures separate production-process measurements for preferred 256-bit and 512-bit vectors.
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
                    .WithId("Vector256")
                    .WithEnvironmentVariable("DOTNET_PreferredVectorBitWidth", "256")
                    .WithEnvironmentVariable("COMPlus_PreferredVectorBitWidth", "256")
                    .AsBaseline());

            this.AddJob(
                Job.ShortRun
                    .WithId("Vector512")
                    .WithEnvironmentVariable("DOTNET_PreferredVectorBitWidth", "512")
                    .WithEnvironmentVariable("COMPlus_PreferredVectorBitWidth", "512"));
        }
    }
}

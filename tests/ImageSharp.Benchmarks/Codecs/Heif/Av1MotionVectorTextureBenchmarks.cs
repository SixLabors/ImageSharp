// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Compares the source texture measure of the motion vector statistics with the reference's scalar loop.
/// Reference: the texture loops of collect_mv_stats_b().
/// </summary>
[Config(typeof(Configuration))]
public class Av1MotionVectorTextureBenchmarks
{
    private const int Width = 640;
    private const int Height = 384;

    private byte[] samples;
    private Av1PlaneRegion<byte> luma;

    [Params(8, 32, 128)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.samples = new byte[Width * Height];
        Random random = new(7);
        random.NextBytes(this.samples);
        this.luma = new Av1PlaneRegion<byte>(this.samples, Width, new Rectangle(0, 0, Width, Height));
        if (this.LibaomScalar() != this.Production())
        {
            throw new InvalidOperationException("The texture totals differ from the reference loop.");
        }
    }

    /// <summary>
    /// The reference loop over every block of the frame.
    /// </summary>
    /// <returns>The combined totals.</returns>
    [Benchmark(Baseline = true)]
    public long LibaomScalar()
    {
        int size = this.Size;
        int horizontal = 0;
        int vertical = 0;
        int diagonal = 0;
        for (int y = 0; y < Height; y += size)
        {
            for (int x = 0; x < Width; x += size)
            {
                for (int row = 0; row < size - 1; row++)
                {
                    for (int column = 0; column < size - 1; column++)
                    {
                        int offset = ((y + row) * Width) + x + column;
                        int horizontalDifference = Math.Abs(this.samples[offset + 1] - this.samples[offset]);
                        int verticalDifference = Math.Abs(this.samples[offset + Width] - this.samples[offset]);
                        horizontal += horizontalDifference;
                        vertical += verticalDifference;
                        diagonal += horizontalDifference * verticalDifference;
                    }
                }
            }
        }

        return (long)horizontal + vertical + diagonal;
    }

    /// <summary>
    /// The production traversal over every block of the frame.
    /// </summary>
    /// <returns>The combined totals.</returns>
    [Benchmark]
    public long Production()
    {
        int size = this.Size;
        int horizontal = 0;
        int vertical = 0;
        int diagonal = 0;
        for (int y = 0; y < Height; y += size)
        {
            for (int x = 0; x < Width; x += size)
            {
                Av1MotionVectorStatistics.AccumulateTexture<byte, Av1MotionVectorStatistics.ByteTextureOperator>(
                    this.luma,
                    new Point(x, y),
                    size,
                    size,
                    0,
                    ref horizontal,
                    ref vertical,
                    ref diagonal);
            }
        }

        return (long)horizontal + vertical + diagonal;
    }

    /// <summary>
    /// Measures the hardware, AVX2, Vector128 and scalar paths.
    /// </summary>
    public sealed class Configuration : ManualConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        public Configuration()
        {
            this.AddJob(Job.ShortRun.WithId("Hardware").WithArguments([new MsBuildArgument("/p:RunAnalyzers=false")]));
            this.AddJob(
                Job.ShortRun
                    .WithId("Avx2")
                    .WithEnvironmentVariable("DOTNET_EnableAVX512", "0")
                    .WithArguments([new MsBuildArgument("/p:RunAnalyzers=false")]));

            this.AddJob(
                Job.ShortRun
                    .WithId("Vector128")
                    .WithEnvironmentVariable("DOTNET_EnableAVX512", "0")
                    .WithEnvironmentVariable("DOTNET_EnableAVX2", "0")
                    .WithArguments([new MsBuildArgument("/p:RunAnalyzers=false")]));

            this.AddJob(
                Job.ShortRun
                    .WithId("Scalar")
                    .WithEnvironmentVariable("DOTNET_EnableHWIntrinsic", "0")
                    .WithArguments([new MsBuildArgument("/p:RunAnalyzers=false")]));
        }
    }
}

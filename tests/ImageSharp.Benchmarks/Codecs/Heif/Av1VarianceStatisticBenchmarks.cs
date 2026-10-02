// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Compares the smoothing variance measure of sharpness 3 with the reference's scalar loop.
/// Reference: aom_calc_variance_stat_c().
/// </summary>
[Config(typeof(Configuration))]
public class Av1VarianceStatisticBenchmarks
{
    /// <summary>
    /// The frame width.
    /// </summary>
    private const int Width = 640;

    /// <summary>
    /// The frame height.
    /// </summary>
    private const int Height = 384;

    /// <summary>
    /// The frame samples.
    /// </summary>
    private byte[] samples;

    /// <summary>
    /// The padded copy that the reference loop fills for each block.
    /// </summary>
    private byte[] padded;

    /// <summary>
    /// Gets or sets the block width and height.
    /// </summary>
    [Params(8, 16, 64)]
    public int Size { get; set; }

    /// <summary>
    /// Fills the frame and checks that both loops agree.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.samples = new byte[Width * Height];
        for (int i = 0; i < this.samples.Length; i++)
        {
            // Edges, flat runs and noise, so every lane sees large and small differences.
            this.samples[i] = (byte)((((i * 7919) ^ (i >> 5)) * 31) >> 4);
        }

        this.padded = new byte[(128 + 2) * (128 + 2)];
        if (this.LibaomScalar() != this.Production())
        {
            throw new InvalidOperationException("The variance measure differs from the reference loop.");
        }
    }

    /// <summary>
    /// The reference loop over every block of the frame: a padded copy with repeated edges, then the 1-2-1 by 1-2-1
    /// smoothing.
    /// </summary>
    /// <returns>The sum of the block measures.</returns>
    [Benchmark(Baseline = true)]
    public long LibaomScalar()
    {
        int size = this.Size;
        int paddedStride = size + 2;
        long total = 0;
        for (int y = 0; y < Height; y += size)
        {
            for (int x = 0; x < Width; x += size)
            {
                for (int row = -1; row < size + 1; row++)
                {
                    for (int column = -1; column < size + 1; column++)
                    {
                        int sourceRow = Math.Clamp(row, 0, size - 1);
                        int sourceColumn = Math.Clamp(column, 0, size - 1);
                        this.padded[((row + 1) * paddedStride) + column + 1] =
                            this.samples[((y + sourceRow) * Width) + x + sourceColumn];
                    }
                }

                long block = 0;
                for (int row = 0; row < size; row++)
                {
                    for (int column = 0; column < size; column++)
                    {
                        int top = (row * paddedStride) + column;
                        int middle = top + paddedStride;
                        int bottom = middle + paddedStride;
                        int sum = this.padded[top] + (2 * this.padded[top + 1]) + this.padded[top + 2] +
                            (2 * this.padded[middle]) + (4 * this.padded[middle + 1]) + (2 * this.padded[middle + 2]) +
                            this.padded[bottom] + (2 * this.padded[bottom + 1]) + this.padded[bottom + 2];

                        long difference = this.padded[middle + 1] - (sum >> 4);
                        block += difference * difference;
                    }
                }

                total += block << 4;
            }
        }

        return total;
    }

    /// <summary>
    /// The production kernel over every block of the frame.
    /// </summary>
    /// <returns>The sum of the block measures.</returns>
    [Benchmark]
    public long Production()
    {
        int size = this.Size;
        long total = 0;
        for (int y = 0; y < Height; y += size)
        {
            for (int x = 0; x < Width; x += size)
            {
                total += Av1VarianceStatistic.Calculate<byte, Av1MotionVectorStatistics.ByteTextureOperator>(
                    this.samples.AsSpan((y * Width) + x),
                    Width,
                    size,
                    size);
            }
        }

        return total;
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

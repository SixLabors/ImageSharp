// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures the screen-content block dilation over the 16x16 blocks of a 1080p frame at each
/// available intrinsic tier, against the per-sample neighbor fill that it replaces.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1ScreenContentBenchmarks
{
    private const int BlockArea = 256;

    /// <summary>
    /// The number of 16x16 blocks in a 1920x1088 frame.
    /// </summary>
    private const int BlockCount = (1920 / 16) * (1088 / 16);

    private readonly byte[] dilated = new byte[BlockArea];

    private byte[] blocks = [];

    /// <summary>
    /// Gets or sets the kind of content in the blocks.
    /// </summary>
    [Params("Photo", "Screen")]
    public string Content { get; set; } = "Photo";

    /// <summary>
    /// Populates deterministic photo-like or screen-like blocks outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.blocks = new byte[BlockCount * BlockArea];
        int seed = 7;
        for (int block = 0; block < BlockCount; block++)
        {
            for (int i = 0; i < BlockArea; i++)
            {
                seed = (seed * 1103515245) + 12345;
                int noise = (seed >>> 16) & 0x7FFF;
                int row = i >> 4;
                int column = i & 15;
                int value;
                if (this.Content == "Photo")
                {
                    value = (row * 3) + (column * 5) + (block & 63) + (noise % 24);
                }
                else
                {
                    // Text on a flat background: a few glyph colors and an occasional anti-aliased edge.
                    value = ((row + block) % 5 == 0 || (column + block) % 7 == 0) ? 20 : 235;
                    if (noise % 23 == 0)
                    {
                        value = 60 + (noise % 120);
                    }
                }

                this.blocks[(block * BlockArea) + i] = (byte)value;
            }
        }
    }

    /// <summary>
    /// Measures the dilation of every block.
    /// </summary>
    /// <returns>The final dilated sample, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Dilate")]
    public int Dilate()
    {
        int total = 0;
        for (int block = 0; block < BlockCount; block++)
        {
            Av1ScreenContentDetector.DilateBlock(this.blocks.AsSpan(block * BlockArea, BlockArea), this.dilated);
            total += this.dilated[block & 255];
        }

        return total;
    }

    /// <summary>
    /// Measures the histogram and neighbor fill that <see cref="Dilate"/> replaced.
    /// </summary>
    /// <returns>The final dilated sample, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Dilate")]
    public int DilateHistogram()
    {
        int total = 0;
        Span<int> counts = stackalloc int[256];
        for (int block = 0; block < BlockCount; block++)
        {
            ReadOnlySpan<byte> samples = this.blocks.AsSpan(block * BlockArea, BlockArea);
            counts.Clear();
            int dominantCount = 0;
            byte dominant = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                byte value = samples[i];
                if (++counts[value] > dominantCount)
                {
                    dominant = value;
                    dominantCount = counts[value];
                }
            }

            samples.CopyTo(this.dilated);
            for (int row = 0; row < 16; row++)
            {
                for (int column = 0; column < 16; column++)
                {
                    if (samples[(row * 16) + column] != dominant)
                    {
                        continue;
                    }

                    int left = Math.Max(column - 1, 0);
                    int right = Math.Min(column + 1, 15);
                    for (int y = Math.Max(row - 1, 0); y <= Math.Min(row + 1, 15); y++)
                    {
                        this.dilated.AsSpan((y * 16) + left, right - left + 1).Fill(dominant);
                    }
                }
            }

            total += this.dilated[block & 255];
        }

        return total;
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

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures the TPL coefficient rate estimate of 16x16 transform blocks at each available intrinsic tier, against the
/// scan-order loop that it replaces.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1TplRateBenchmarks
{
    private const int BlockCount = 1024;
    private const int BlockArea = 256;

    private readonly int[] quantized = new int[BlockCount * BlockArea];
    private readonly int[] endOfBlocks = new int[BlockCount];

    /// <summary>
    /// Populates deterministic sparse quantized blocks and their ends of block outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        ReadOnlySpan<short> inverseScan = Av1ScanOrderConstants.GetScanOrder(Av1TransformSize.Size16x16, Av1TransformType.DctDct).InverseScan;
        Random random = new(0x7A1);
        for (int block = 0; block < BlockCount; block++)
        {
            // Low frequencies carry most of the nonzero levels, as they do after quantization.
            Span<int> levels = this.quantized.AsSpan(block * BlockArea, BlockArea);
            int end = 0;
            for (int i = 0; i < BlockArea; i++)
            {
                int frequency = (i / 16) + (i % 16);
                levels[i] = random.Next(frequency + 2) == 0 ? random.Next(-40, 41) : 0;
                if (levels[i] != 0)
                {
                    end = Math.Max(end, inverseScan[i] + 1);
                }
            }

            this.endOfBlocks[block] = end;
        }
    }

    /// <summary>
    /// Measures the raster-order level bits.
    /// </summary>
    /// <returns>The total rate, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Rate")]
    public int LevelBits()
    {
        int total = 0;
        for (int block = 0; block < BlockCount; block++)
        {
            total += 1 + this.endOfBlocks[block] + Av1CoefficientMeasures.SumLevelBits(this.quantized.AsSpan(block * BlockArea, BlockArea));
        }

        return total;
    }

    /// <summary>
    /// Measures the scan-order loop that <see cref="LevelBits"/> replaced.
    /// </summary>
    /// <returns>The total rate, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Rate")]
    public int ScanLoop()
    {
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(Av1TransformSize.Size16x16, Av1TransformType.DctDct).Scan;
        int total = 0;
        for (int block = 0; block < BlockCount; block++)
        {
            ReadOnlySpan<int> levels = this.quantized.AsSpan(block * BlockArea, BlockArea);
            int rate = 1;
            for (int index = 0; index < this.endOfBlocks[block]; index++)
            {
                int level = Math.Abs(levels[scan[index]]);
                rate += BitOperations.Log2((uint)(level + 1)) + 1 + (level > 0 ? 1 : 0);
            }

            total += rate;
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

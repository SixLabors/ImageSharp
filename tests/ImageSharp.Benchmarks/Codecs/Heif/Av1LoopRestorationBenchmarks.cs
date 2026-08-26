// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures normative AV1 self-guided restoration across a full-HD-equivalent luma workload.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1LoopRestorationBenchmarks
{
    /// <summary>
    /// The width of one normative self-guided processing unit.
    /// </summary>
    private const int Width = 64;

    /// <summary>
    /// The height of one normative self-guided processing unit.
    /// </summary>
    private const int Height = 64;

    /// <summary>
    /// The three source samples required on each side of a processing unit.
    /// </summary>
    private const int Border = 3;

    /// <summary>
    /// The number of processing units covering a 1920 by 1080 luma plane.
    /// </summary>
    private const int ProcessingUnitCount = 30 * 17;

    /// <summary>
    /// The bordered source-row stride.
    /// </summary>
    private const int SourceStride = Width + (Border * 2);

    /// <summary>
    /// The self-guided parameter set activating both radius-two and radius-one filtering.
    /// </summary>
    private const int ParameterSetIndex = 0;

    /// <summary>
    /// The deterministic bordered eight-bit source block.
    /// </summary>
    private readonly ushort[] source8 = new ushort[SourceStride * (Height + (Border * 2))];

    /// <summary>
    /// The deterministic bordered twelve-bit source block.
    /// </summary>
    private readonly ushort[] source12 = new ushort[SourceStride * (Height + (Border * 2))];

    /// <summary>
    /// The restored processing-unit destination.
    /// </summary>
    private readonly ushort[] destination = new ushort[Width * Height];

    /// <summary>
    /// The caller-owned self-guided work storage.
    /// </summary>
    private readonly int[] scratch = new int[Av1SelfGuidedFilter.GetScratchLength(Width, Height)];

    /// <summary>
    /// Gets the two transmitted projection coefficients used by the measured parameter set.
    /// </summary>
    private static ReadOnlySpan<int> ProjectionCoefficients => [31, -7];

    /// <summary>
    /// Populates deterministic bordered source blocks outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int row = 0; row < Height + (Border * 2); row++)
        {
            for (int column = 0; column < SourceStride; column++)
            {
                int sample = ((row * 4051) + (column * 7919) + 127) & byte.MaxValue;
                int offset = (row * SourceStride) + column;
                this.source8[offset] = (ushort)sample;
                this.source12[offset] = (ushort)(sample << 4);
            }
        }
    }

    /// <summary>
    /// Measures eight-bit self-guided restoration for a full-HD-equivalent luma plane.
    /// </summary>
    /// <returns>The final restored sample, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("8Bit")]
    public ushort Restore8BitPlane()
    {
        for (int unit = 0; unit < ProcessingUnitCount; unit++)
        {
            Av1SelfGuidedFilter.FilterBlock(
                this.source8,
                SourceStride,
                this.destination,
                Width,
                Width,
                Height,
                8,
                ParameterSetIndex,
                ProjectionCoefficients,
                this.scratch);
        }

        return this.destination[^1];
    }

    /// <summary>
    /// Measures twelve-bit self-guided restoration for a full-HD-equivalent luma plane.
    /// </summary>
    /// <returns>The final restored sample, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("12Bit")]
    public ushort Restore12BitPlane()
    {
        for (int unit = 0; unit < ProcessingUnitCount; unit++)
        {
            Av1SelfGuidedFilter.FilterBlock(
                this.source12,
                SourceStride,
                this.destination,
                Width,
                Width,
                Height,
                12,
                ParameterSetIndex,
                ProjectionCoefficients,
                this.scratch);
        }

        return this.destination[^1];
    }

    /// <summary>
    /// Configures production-process measurements for hardware, 128-bit, and scalar filtering.
    /// </summary>
    public sealed class Configuration : ManualConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        public Configuration()
        {
            this.AddJob(Job.ShortRun.WithId("Hardware").AsBaseline());

            this.AddJob(
                Job.ShortRun
                    .WithId("Vector128")
                    .WithEnvironmentVariable("DOTNET_EnableAVX", "0"));

            this.AddJob(
                Job.ShortRun
                    .WithId("Scalar")
                    .WithEnvironmentVariable("DOTNET_EnableHWIntrinsic", "0"));
        }
    }
}

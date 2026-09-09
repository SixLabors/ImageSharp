// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures normative AV1 super-resolution filtering across a full-HD luma plane.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1SuperResolutionBenchmarks
{
    /// <summary>
    /// The displayed luma width after normative upscaling.
    /// </summary>
    private const int UpscaledWidth = 1920;

    /// <summary>
    /// The coded luma width produced by a fixed super-resolution denominator of twelve.
    /// </summary>
    private const int CodedWidth = 1280;

    /// <summary>
    /// The number of visible rows in the measured luma plane.
    /// </summary>
    private const int Height = 1080;

    /// <summary>
    /// The fixed-point source-position increment for the measured scale ratio.
    /// </summary>
    private readonly int step = Av1SuperResolutionFilter.GetConvolveStep(CodedWidth, UpscaledWidth);

    /// <summary>
    /// The initial fixed-point source position for the measured scale ratio.
    /// </summary>
    private readonly int initialSubpixel;

    /// <summary>
    /// The replicated-edge eight-bit source row.
    /// </summary>
    private readonly byte[] source8 = new byte[CodedWidth + (Av1SuperResolutionFilter.SourceBorder * 2)];

    /// <summary>
    /// The filtered eight-bit output row.
    /// </summary>
    private readonly byte[] destination8 = new byte[UpscaledWidth];

    /// <summary>
    /// The replicated-edge twelve-bit source row.
    /// </summary>
    private readonly ushort[] source12 = new ushort[CodedWidth + (Av1SuperResolutionFilter.SourceBorder * 2)];

    /// <summary>
    /// The filtered twelve-bit output row.
    /// </summary>
    private readonly ushort[] destination12 = new ushort[UpscaledWidth];

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SuperResolutionBenchmarks"/> class.
    /// </summary>
    public Av1SuperResolutionBenchmarks()
    {
        this.initialSubpixel = Av1SuperResolutionFilter.GetInitialSubpixel(CodedWidth, UpscaledWidth, this.step);
    }

    /// <summary>
    /// Populates deterministic source samples and their replicated frame edges outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        Span<byte> reconstructed8 = this.source8.AsSpan(Av1SuperResolutionFilter.SourceBorder, CodedWidth);
        Span<ushort> reconstructed12 = this.source12.AsSpan(Av1SuperResolutionFilter.SourceBorder, CodedWidth);
        for (int column = 0; column < CodedWidth; column++)
        {
            int sample = ((column * 4051) + 127) & byte.MaxValue;
            reconstructed8[column] = (byte)sample;
            reconstructed12[column] = (ushort)(sample << 4);
        }

        this.source8.AsSpan(0, Av1SuperResolutionFilter.SourceBorder).Fill(reconstructed8[0]);
        this.source8.AsSpan(Av1SuperResolutionFilter.SourceBorder + CodedWidth).Fill(reconstructed8[^1]);
        this.source12.AsSpan(0, Av1SuperResolutionFilter.SourceBorder).Fill(reconstructed12[0]);
        this.source12.AsSpan(Av1SuperResolutionFilter.SourceBorder + CodedWidth).Fill(reconstructed12[^1]);
    }

    /// <summary>
    /// Measures normative eight-bit filtering for a full-HD luma plane.
    /// </summary>
    /// <returns>The final filtered sample, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("8Bit")]
    public byte Filter8BitPlane()
    {
        for (int row = 0; row < Height; row++)
        {
            Av1SuperResolutionFilter.UpscaleRow(this.source8, this.destination8, this.step, this.initialSubpixel);
        }

        return this.destination8[^1];
    }

    /// <summary>
    /// Measures normative twelve-bit filtering for a full-HD luma plane.
    /// </summary>
    /// <returns>The final filtered sample, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("12Bit")]
    public ushort Filter12BitPlane()
    {
        for (int row = 0; row < Height; row++)
        {
            Av1SuperResolutionFilter.UpscaleRow(this.source12, this.destination12, this.step, this.initialSubpixel, 12);
        }

        return this.destination12[^1];
    }

    /// <summary>
    /// Configures production-process measurements for hardware and scalar filtering.
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
                    .WithId("Scalar")
                    .WithEnvironmentVariable("DOTNET_EnableHWIntrinsic", "0"));
        }
    }
}

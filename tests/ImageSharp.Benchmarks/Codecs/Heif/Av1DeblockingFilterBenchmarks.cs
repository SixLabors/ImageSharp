// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures AV1 deblocking across a padded full-HD reconstruction surface.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1DeblockingFilterBenchmarks
{
    /// <summary>
    /// The visible coded frame width in samples.
    /// </summary>
    private const int Width = 1920;

    /// <summary>
    /// The coded frame height including the final padded coding-tree row for a 1080-line presentation.
    /// </summary>
    private const int Height = 1088;

    /// <summary>
    /// The border reserved around the visible reconstruction surface.
    /// </summary>
    private const int Padding = 16;

    /// <summary>
    /// The number of samples between adjacent padded rows.
    /// </summary>
    private const int Stride = Width + (2 * Padding);

    /// <summary>
    /// The eight-bit padded reconstruction surface.
    /// </summary>
    private readonly byte[] samples8 = new byte[Stride * (Height + (2 * Padding))];

    /// <summary>
    /// The twelve-bit padded reconstruction surface.
    /// </summary>
    private readonly ushort[] samples12 = new ushort[Stride * (Height + (2 * Padding))];

    /// <summary>
    /// Populates smooth deterministic samples that exercise enabled narrow and wide filter masks.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int row = 0; row < Height + (2 * Padding); row++)
        {
            for (int column = 0; column < Stride; column++)
            {
                int value = 96 + ((row + (2 * column)) & 15);
                int offset = (row * Stride) + column;
                this.samples8[offset] = (byte)value;
                this.samples12[offset] = (ushort)(value << 4);
            }
        }
    }

    /// <summary>
    /// Measures both AV1 deblocking passes over an eight-bit full-HD frame.
    /// </summary>
    /// <returns>A filtered sample that keeps the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("8Bit")]
    public byte Filter8BitFrame()
    {
        for (int y = 0; y < Height; y += 4)
        {
            for (int x = 8; x < Width; x += 8)
            {
                int q0Offset = ((Padding + y) * Stride) + Padding + x;
                Av1DeblockingFilter.FilterVertical(this.samples8, q0Offset, Stride, 14, 20, 60, 3);
            }
        }

        for (int y = 8; y < Height; y += 8)
        {
            for (int x = 0; x < Width; x += 4)
            {
                int q0Offset = ((Padding + y) * Stride) + Padding + x;
                Av1DeblockingFilter.FilterHorizontal(this.samples8, q0Offset, Stride, 14, 20, 60, 3);
            }
        }

        return this.samples8[(Padding * Stride) + Padding];
    }

    /// <summary>
    /// Measures both AV1 deblocking passes over a twelve-bit full-HD frame.
    /// </summary>
    /// <returns>A filtered sample that keeps the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("12Bit")]
    public ushort Filter12BitFrame()
    {
        for (int y = 0; y < Height; y += 4)
        {
            for (int x = 8; x < Width; x += 8)
            {
                int q0Offset = ((Padding + y) * Stride) + Padding + x;
                Av1DeblockingFilter.FilterVertical(this.samples12, q0Offset, Stride, 14, 20, 60, 3, 12);
            }
        }

        for (int y = 8; y < Height; y += 8)
        {
            for (int x = 0; x < Width; x += 4)
            {
                int q0Offset = ((Padding + y) * Stride) + Padding + x;
                Av1DeblockingFilter.FilterHorizontal(this.samples12, q0Offset, Stride, 14, 20, 60, 3, 12);
            }
        }

        return this.samples12[(Padding * Stride) + Padding];
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

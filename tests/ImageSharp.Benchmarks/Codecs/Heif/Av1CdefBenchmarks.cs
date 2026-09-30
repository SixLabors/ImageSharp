// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures frame-wide AV1 CDEF direction search and filtering at each available intrinsic tier.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1CdefBenchmarks
{
    /// <summary>
    /// The luma width of a 1080p frame.
    /// </summary>
    private const int Width = 1920;

    /// <summary>
    /// The luma height of a 1080p frame, padded to the 8x8 block grid.
    /// </summary>
    private const int Height = 1088;

    /// <summary>
    /// The sentinel border around the working plane, in samples.
    /// </summary>
    private const int Border = 3;

    /// <summary>
    /// The row stride of the bordered working plane.
    /// </summary>
    private const int Stride = Width + (2 * Border);

    /// <summary>
    /// The bordered 16-bit working plane.
    /// </summary>
    private readonly ushort[] source = new ushort[Stride * (Height + (2 * Border))];

    /// <summary>
    /// The filtered eight-bit frame.
    /// </summary>
    private readonly byte[] destination = new byte[Width * Height];

    /// <summary>
    /// The top-left offset of every 8x8 block in the working plane.
    /// </summary>
    private readonly int[] offsets = new int[(Width / 8) * (Height / 8)];

    /// <summary>
    /// The direction of every block.
    /// </summary>
    private readonly int[] directions = new int[(Width / 8) * (Height / 8)];

    /// <summary>
    /// The directional variance of every block.
    /// </summary>
    private readonly int[] variances = new int[(Width / 8) * (Height / 8)];

    /// <summary>
    /// Populates a deterministic textured plane outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int row = 0; row < Height + (2 * Border); row++)
        {
            for (int column = 0; column < Stride; column++)
            {
                this.source[(row * Stride) + column] = (ushort)(((row * 7) + (column * 3) + ((row * column) % 29)) & 255);
            }
        }

        int block = 0;
        for (int row = 0; row < Height; row += 8)
        {
            for (int column = 0; column < Width; column += 8)
            {
                this.offsets[block++] = ((row + Border) * Stride) + column + Border;
            }
        }
    }

    /// <summary>
    /// Measures the direction search of every 8x8 luma block.
    /// </summary>
    /// <returns>The final direction, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("FindDirections")]
    public int FindDirections()
    {
        // The frame is searched one 64x64 unit's worth of blocks at a time, as the filter traversal does.
        for (int start = 0; start < this.offsets.Length; start += 64)
        {
            int count = Math.Min(64, this.offsets.Length - start);
            Av1CdefFilter.FindDirections(
                this.source,
                this.offsets.AsSpan(start, count),
                Stride,
                0,
                this.directions.AsSpan(start, count),
                this.variances.AsSpan(start, count));
        }

        return this.directions[^1];
    }

    /// <summary>
    /// Measures primary and secondary filtering of every 8x8 luma block.
    /// </summary>
    /// <returns>The final filtered sample, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("FilterBlock")]
    public byte FilterBlocks()
    {
        int block = 0;
        for (int row = 0; row < Height; row += 8)
        {
            for (int column = 0; column < Width; column += 8)
            {
                Av1CdefFilter.FilterBlock(
                    this.source,
                    this.offsets[block],
                    Stride,
                    this.destination,
                    (row * Width) + column,
                    Width,
                    12,
                    2,
                    block & 7,
                    6,
                    6,
                    0,
                    8,
                    8);

                block++;
            }
        }

        return this.destination[^1];
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

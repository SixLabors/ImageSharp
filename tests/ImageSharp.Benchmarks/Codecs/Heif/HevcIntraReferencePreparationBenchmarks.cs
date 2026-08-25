// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures frame-wide preparation of complete and partially substituted HEVC intra references.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class HevcIntraReferencePreparationBenchmarks
{
    /// <summary>
    /// The coded frame width.
    /// </summary>
    private const int Width = 1920;

    /// <summary>
    /// The coded frame height including the final padded coding-tree row for a 1080-line presentation.
    /// </summary>
    private const int Height = 1088;

    /// <summary>
    /// The base-two logarithm of the benchmark prediction-block side.
    /// </summary>
    private const int BlockLog2 = 5;

    /// <summary>
    /// The prediction-block side in samples.
    /// </summary>
    private const int BlockSize = 1 << BlockLog2;

    /// <summary>
    /// The availability-unit side in samples.
    /// </summary>
    private const int UnitSize = 4;

    /// <summary>
    /// The number of availability units surrounding one benchmark block.
    /// </summary>
    private const int AvailabilityUnitCount = ((BlockSize * 2) / UnitSize * 2) + 1;

    /// <summary>
    /// The complete reference availability pattern.
    /// </summary>
    private readonly bool[] completeAvailability = new bool[AvailabilityUnitCount];

    /// <summary>
    /// The partial availability pattern exercising forward substitution.
    /// </summary>
    private readonly bool[] partialAvailability = new bool[AvailabilityUnitCount];

    /// <summary>
    /// The reusable top reference destination.
    /// </summary>
    private readonly ushort[] top = new ushort[(BlockSize * 2) + 1];

    /// <summary>
    /// The reusable left reference destination.
    /// </summary>
    private readonly ushort[] left = new ushort[(BlockSize * 2) + 1];

    /// <summary>
    /// The reusable reference-line scratch storage.
    /// </summary>
    private readonly ushort[] scratch = new ushort[HevcIntraPredictor.GetReferenceScratchLength(BlockLog2, UnitSize)];

    /// <summary>
    /// The reconstructed picture providing benchmark reference samples.
    /// </summary>
    private HevcPictureBuffer picture;

    /// <summary>
    /// Allocates and initializes the reconstructed benchmark picture outside measured operations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.picture = new HevcPictureBuffer(
            Configuration.Default,
            Width + (BlockSize * 2),
            Height + (BlockSize * 2),
            12,
            12,
            1,
            false);

        for (int y = 0; y < this.picture.Height; y++)
        {
            Span<ushort> row = this.picture.GetRowSpan(HevcPlane.Y, y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = (ushort)(((37 * x) + (53 * y)) & 4095);
            }
        }

        this.completeAvailability.AsSpan().Fill(true);
        for (int i = 0; i < this.partialAvailability.Length; i++)
        {
            this.partialAvailability[i] = (i & 3) != 0;
        }
    }

    /// <summary>
    /// Releases the reconstructed benchmark picture.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.picture.Dispose();

    /// <summary>
    /// Measures the complete-border fast path across a padded 1080p coded frame.
    /// </summary>
    /// <returns>A prepared reference sample, keeping the result observable.</returns>
    [Benchmark(Baseline = true)]
    public ushort PrepareCompleteFrame() => this.PrepareFrame(this.completeAvailability);

    /// <summary>
    /// Measures partial-unit collection and substitution across a padded 1080p coded frame.
    /// </summary>
    /// <returns>A prepared reference sample, keeping the result observable.</returns>
    [Benchmark]
    public ushort PreparePartialFrame() => this.PrepareFrame(this.partialAvailability);

    /// <summary>
    /// Prepares references for every maximum-size block in the benchmark frame.
    /// </summary>
    /// <param name="availability">The repeated availability pattern.</param>
    /// <returns>The final prepared reference sample.</returns>
    private ushort PrepareFrame(ReadOnlySpan<bool> availability)
    {
        for (int y = 0; y < Height; y += BlockSize)
        {
            for (int x = 0; x < Width; x += BlockSize)
            {
                HevcIntraPredictor.PrepareReferenceSamples(
                    this.picture,
                    HevcPlane.Y,
                    x + BlockSize,
                    y + BlockSize,
                    BlockLog2,
                    UnitSize,
                    UnitSize,
                    availability,
                    this.top,
                    this.left,
                    this.scratch);
            }
        }

        return this.left[^1];
    }
}

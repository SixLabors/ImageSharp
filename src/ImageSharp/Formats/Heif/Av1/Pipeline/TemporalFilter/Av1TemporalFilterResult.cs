// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <summary>
/// Describes one temporal filter pass: the frames it used and the difference between the source and the filtered frame.
/// </summary>
internal readonly struct Av1TemporalFilterResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TemporalFilterResult"/> struct.
    /// </summary>
    /// <param name="frameCount">The number of frames filtered together.</param>
    /// <param name="framesBefore">The number of frames before the filtered frame.</param>
    /// <param name="differenceSum">The sum of the per-block luma squared differences.</param>
    /// <param name="differenceSquares">The sum of the squares of the per-block luma squared differences.</param>
    public Av1TemporalFilterResult(int frameCount, int framesBefore, long differenceSum, long differenceSquares)
    {
        this.FrameCount = frameCount;
        this.FramesBefore = framesBefore;
        this.DifferenceSum = differenceSum;
        this.DifferenceSquares = differenceSquares;
    }

    /// <summary>
    /// Gets the number of frames filtered together, including the filtered frame.
    /// </summary>
    public int FrameCount { get; }

    /// <summary>
    /// Gets the number of look-ahead frames before the filtered frame that were used.
    /// </summary>
    public int FramesBefore { get; }

    /// <summary>
    /// Gets the sum over the 64x64 blocks of the luma squared difference between the source and the filtered frame.
    /// </summary>
    public long DifferenceSum { get; }

    /// <summary>
    /// Gets the sum over the 64x64 blocks of the squared luma squared difference.
    /// </summary>
    public long DifferenceSquares { get; }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Decides whether a screen content inter frame codes integer motion vectors only, from how much of its luma repeats
/// the previous source or is flat along a row or a column, and keeps the share of the frames before it. Reference:
/// av1_is_integer_mv() with ForceIntegerMVInfo.
/// </summary>
internal sealed class Av1IntegerMotionVectorDecision
{
    /// <summary>
    /// The side of the compared blocks. Reference: FORCE_INT_MV_DECISION_BLOCK_SIZE.
    /// </summary>
    private const int BlockSize = 8;

    /// <summary>
    /// The number of frames whose share the average reads. Reference: max_history_size.
    /// </summary>
    private const int HistoryLength = 32;

    private readonly double[] shares = new double[HistoryLength];
    private int index;
    private int count;

    /// <summary>
    /// Returns whether the frame codes integer motion vectors only, and records its share of repeated or flat blocks.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The block comparison operations for the sample type.</typeparam>
    /// <param name="current">The luma plane of the source, over the coded size.</param>
    /// <param name="last">The luma plane of the previous source, over the coded size.</param>
    /// <returns><see langword="true"/> when the frame forces integer motion vectors.</returns>
    public bool Decide<TSample, TOperator>(Av1PlaneRegion<TSample> current, Av1PlaneRegion<TSample> last)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraBlockCopySearchIndex.ISearchOperation<TSample>
    {
        const double ThresholdCurrent = 0.8;
        const double ThresholdAverage = 0.95;
        int total = 0;
        int collocated = 0;
        int smooth = 0;
        ReadOnlySpan<TSample> currentSamples = current.Samples;
        ReadOnlySpan<TSample> lastSamples = last.Samples;
        for (int y = 0; y + BlockSize <= current.Height; y += BlockSize)
        {
            for (int x = 0; x + BlockSize <= current.Width; x += BlockSize)
            {
                ReadOnlySpan<TSample> currentBlock = currentSamples[current.GetOffset(x, y)..];
                total++;
                if (TOperator.BlocksEqual(currentBlock, current.Stride, lastSamples[last.GetOffset(x, y)..], last.Stride))
                {
                    collocated++;
                    continue;
                }

                if (TOperator.IsHorizontalPerfect(currentBlock, current.Stride) || TOperator.IsVerticalPerfect(currentBlock, current.Stride))
                {
                    smooth++;
                }
            }
        }

        double share = (collocated + smooth) / (double)total;
        this.shares[this.index] = share;
        this.index = (this.index + 1) % HistoryLength;
        this.count = Math.Min(this.count + 1, HistoryLength);
        if (share < ThresholdCurrent)
        {
            return false;
        }

        if (collocated == total)
        {
            return true;
        }

        double average = 0;
        for (int k = 0; k < this.count; k++)
        {
            average += this.shares[k];
        }

        average /= this.count;
        if (average < ThresholdAverage)
        {
            return false;
        }

        // The reference ends with two tests that no frame passes: fewer than no remaining blocks, and an average of
        // shares above one.
        return total - collocated - smooth < 0 || average > 1.01;
    }
}

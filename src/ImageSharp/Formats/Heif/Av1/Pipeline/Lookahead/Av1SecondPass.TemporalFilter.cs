// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// Exposes the rate-control state that the temporal filter reads for the frame being coded.
/// </content>
internal sealed partial class Av1SecondPass : IAv1ArfBoostSource
{
    /// <summary>
    /// Gets the number of frames to the next key frame. Reference: rc->frames_to_key.
    /// </summary>
    public int FramesToKey => this.framesToKey;

    /// <summary>
    /// Gets the boost of the current golden group. Reference: p_rc->gfu_boost.
    /// </summary>
    public int GoldenBoost => this.goldenBoost;

    /// <summary>
    /// Gets the number of frames since the last key frame. Reference: rc->frames_since_key.
    /// </summary>
    public int FramesSinceKey => this.framesSinceKey;

    /// <summary>
    /// Gets the position of the current frame's statistics in the lookahead statistics. Reference: the offset of
    /// twopass_frame.stats_in from stats_in_start.
    /// </summary>
    public int StatisticsPosition => this.statisticsPosition;

    /// <summary>
    /// Gets the number of lookahead statistics held. Reference: the offset of stats_in_end from stats_in_start.
    /// </summary>
    public int StatisticsCount => this.statisticsCount;

    /// <summary>
    /// Copies the correlation coefficient of every lookahead statistic. Reference: the cor_coeff of each
    /// FIRSTPASS_STATS from stats_in_start to stats_in_end.
    /// </summary>
    /// <param name="destination">Receives <see cref="StatisticsCount"/> coefficients.</param>
    public void CopyCorrelationCoefficients(Span<double> destination)
    {
        for (int i = 0; i < this.statisticsCount; i++)
        {
            destination[i] = this.statistics[i].CorrelationCoefficient;
        }
    }

    /// <inheritdoc/>
    int IAv1ArfBoostSource.CalculateArfBoost(int offset, int forwardFrames, int backwardFrames)
        => this.CalculateArfBoost(this.statisticsPosition, offset, forwardFrames, backwardFrames, false, true);
}

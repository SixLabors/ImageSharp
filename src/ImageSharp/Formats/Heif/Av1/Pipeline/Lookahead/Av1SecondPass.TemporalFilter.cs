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
    /// Gets the number of frames to the next key frame.
    /// </summary>
    public int FramesToKey => this.framesToKey;

    /// <summary>
    /// Gets the boost of the current golden group.
    /// </summary>
    public int GoldenBoost => this.goldenBoost;

    /// <summary>
    /// Gets the number of frames since the last key frame.
    /// </summary>
    public int FramesSinceKey => this.framesSinceKey;

    /// <summary>
    /// Gets the number of shown frames since the last golden refresh.
    /// </summary>
    public int FramesSinceGolden => this.framesSinceGolden;

    /// <summary>
    /// Gets the position of the statistics of the current frame in the lookahead statistics.
    /// </summary>
    public int StatisticsPosition => this.statisticsPosition;

    /// <summary>
    /// Gets the number of lookahead statistics held.
    /// </summary>
    public int StatisticsCount => this.statisticsCount;

    /// <summary>
    /// Copies the correlation coefficient of every held lookahead statistic, from the first to the last.
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

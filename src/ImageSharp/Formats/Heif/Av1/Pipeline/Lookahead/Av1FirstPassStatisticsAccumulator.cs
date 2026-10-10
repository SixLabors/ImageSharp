// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// Clears and sums first-pass statistics records over a section of frames.
/// </summary>
internal static class Av1FirstPassStatisticsAccumulator
{
    /// <summary>
    /// Adds one frame record to a section total. The logarithmic errors of the section sum the logarithms of the frame errors, not the stored
    /// logarithms of the frame.
    /// </summary>
    /// <param name="section">The section total to update.</param>
    /// <param name="frame">The frame record to add.</param>
    public static void Accumulate(ref Av1FirstPassStatistics section, Av1FirstPassStatistics frame)
    {
        section.Frame += frame.Frame;
        section.Weight += frame.Weight;
        section.IntraError += frame.IntraError;
        section.LogIntraError += Av1FirstPassMath.Log1P(frame.IntraError);
        section.LogCodedError += Av1FirstPassMath.Log1P(frame.CodedError);
        section.FrameAverageWaveletEnergy += frame.FrameAverageWaveletEnergy;
        section.CodedError += frame.CodedError;
        section.SecondReferenceCodedError += frame.SecondReferenceCodedError;
        section.LongTermCodedError += frame.LongTermCodedError;
        section.PercentInter += frame.PercentInter;
        section.PercentMotion += frame.PercentMotion;
        section.PercentSecondReference += frame.PercentSecondReference;
        section.PercentNeutral += frame.PercentNeutral;
        section.IntraSkipPercent += frame.IntraSkipPercent;
        section.InactiveZoneRows += frame.InactiveZoneRows;
        section.InactiveZoneColumns += frame.InactiveZoneColumns;
        section.MotionVectorRow += frame.MotionVectorRow;
        section.MotionVectorRowAbsolute += frame.MotionVectorRowAbsolute;
        section.MotionVectorColumn += frame.MotionVectorColumn;
        section.MotionVectorColumnAbsolute += frame.MotionVectorColumnAbsolute;
        section.MotionVectorRowVariance += frame.MotionVectorRowVariance;
        section.MotionVectorColumnVariance += frame.MotionVectorColumnVariance;
        section.MotionVectorInOutCount += frame.MotionVectorInOutCount;
        section.NewMotionVectorCount += frame.NewMotionVectorCount;
        section.Count += frame.Count;
        section.Duration += frame.Duration;
    }
}

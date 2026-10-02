// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// Clears and sums first-pass statistics records over a section of frames.
/// </summary>
internal static class Av1FirstPassStatisticsAccumulator
{
    /// <summary>
    /// Resets a section total to the empty section. The duration starts at one tick so an empty section never
    /// divides by zero, and the standard deviation of the raw error is left unchanged.
    /// Reference: av1_twopass_zero_stats().
    /// </summary>
    /// <param name="section">The section total to reset.</param>
    public static void Zero(ref Av1FirstPassStatistics section)
    {
        section.Frame = 0.0;
        section.Weight = 0.0;
        section.IntraError = 0.0;
        section.FrameAverageWaveletEnergy = 0.0;
        section.CodedError = 0.0;
        section.LogIntraError = 0.0;
        section.LogCodedError = 0.0;
        section.SecondReferenceCodedError = 0.0;
        section.LongTermCodedError = 0.0;
        section.PercentInter = 0.0;
        section.PercentMotion = 0.0;
        section.PercentSecondReference = 0.0;
        section.PercentNeutral = 0.0;
        section.IntraSkipPercent = 0.0;
        section.InactiveZoneRows = 0.0;
        section.InactiveZoneColumns = 0.0;
        section.MotionVectorRow = 0.0;
        section.MotionVectorRowAbsolute = 0.0;
        section.MotionVectorColumn = 0.0;
        section.MotionVectorColumnAbsolute = 0.0;
        section.MotionVectorRowVariance = 0.0;
        section.MotionVectorColumnVariance = 0.0;
        section.MotionVectorInOutCount = 0.0;
        section.NewMotionVectorCount = 0.0;
        section.Count = 0.0;
        section.Duration = 1.0;
        section.IsFlash = 0;
        section.NoiseVariance = 0;
        section.CorrelationCoefficient = 1.0;
    }

    /// <summary>
    /// Adds one frame record to a section total. The logarithmic errors of the section sum the logarithms of the
    /// frame errors rather than the stored logarithms of the frame. Reference: av1_accumulate_stats().
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

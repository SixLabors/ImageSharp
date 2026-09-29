// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// The first-pass statistics of one frame, or the sum of a section of frames.
/// Reference: FIRSTPASS_STATS.
/// </summary>
internal struct Av1FirstPassStatistics
{
    /// <summary>The frame number in display order.</summary>
    public double Frame;

    /// <summary>The weight of the frame from its intra and brightness factors.</summary>
    public double Weight;

    /// <summary>The intra prediction error per 16x16 block.</summary>
    public double IntraError;

    /// <summary>The average wavelet energy per 16x16 block.</summary>
    public double FrameAverageWaveletEnergy;

    /// <summary>The best of the intra and LAST prediction errors per 16x16 block.</summary>
    public double CodedError;

    /// <summary>The best of the intra and GOLDEN prediction errors per 16x16 block.</summary>
    public double SecondReferenceCodedError;

    /// <summary>The best of the intra and LAST2 prediction errors per 16x16 block.</summary>
    public double LongTermCodedError;

    /// <summary>The share of blocks where inter prediction wins.</summary>
    public double PercentInter;

    /// <summary>The share of blocks with a nonzero motion vector.</summary>
    public double PercentMotion;

    /// <summary>The share of blocks where the GOLDEN reference is best.</summary>
    public double PercentSecondReference;

    /// <summary>The share of blocks where intra and inter errors are close.</summary>
    public double PercentNeutral;

    /// <summary>The share of blocks with a very small intra error.</summary>
    public double IntraSkipPercent;

    /// <summary>The number of rows of blank image border at the top and bottom.</summary>
    public double InactiveZoneRows;

    /// <summary>The number of columns of blank image border; not measured.</summary>
    public double InactiveZoneColumns;

    /// <summary>The mean row motion.</summary>
    public double MotionVectorRow;

    /// <summary>The mean absolute row motion.</summary>
    public double MotionVectorRowAbsolute;

    /// <summary>The mean column motion.</summary>
    public double MotionVectorColumn;

    /// <summary>The mean absolute column motion.</summary>
    public double MotionVectorColumnAbsolute;

    /// <summary>The variance of the row motion.</summary>
    public double MotionVectorRowVariance;

    /// <summary>The variance of the column motion.</summary>
    public double MotionVectorColumnVariance;

    /// <summary>The balance of inward and outward pointing vectors.</summary>
    public double MotionVectorInOutCount;

    /// <summary>The count of vectors that differ from the previous nonzero vector.</summary>
    public double NewMotionVectorCount;

    /// <summary>The frame duration in time-stamp units.</summary>
    public double Duration;

    /// <summary>The number of frames in the section.</summary>
    public double Count;

    /// <summary>The standard deviation of the zero-motion error against the previous source.</summary>
    public double RawErrorStandardDeviation;

    /// <summary>A value indicating whether the frame is a flash.</summary>
    public long IsFlash;

    /// <summary>The estimated noise variance.</summary>
    public double NoiseVariance;

    /// <summary>The correlation coefficient with the previous frame.</summary>
    public double CorrelationCoefficient;

    /// <summary>The log of the intra error.</summary>
    public double LogIntraError;

    /// <summary>The log of the coded error.</summary>
    public double LogCodedError;
}

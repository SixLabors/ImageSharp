// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The temporal dependency statistics of one 16x16 block of one frame of a golden group. The distortions and the
/// dependency terms are scaled by sixteen.
/// </summary>
internal struct Av1TplBlockStatistics
{
    /// <summary>The prediction error of the winning mode against source references.</summary>
    public long SourceReferenceSse;

    /// <summary>The reconstruction error of the winning mode against source references.</summary>
    public long SourceReferenceDistortion;

    /// <summary>The prediction error of the winning mode against reconstructed references.</summary>
    public long ReconstructedReferenceSse;

    /// <summary>The reconstruction error of the winning mode against reconstructed references.</summary>
    public long ReconstructedReferenceDistortion;

    /// <summary>
    /// The compound reconstruction error with the first (index 0) or second (index 1) reference reconstructed and the
    /// other taken from the source.
    /// </summary>
    public InlineArray2<long> CompoundReconstructedDistortion;

    /// <summary>The propagated rate dependency.</summary>
    public long DependencyRate;

    /// <summary>The propagated distortion dependency.</summary>
    public long DependencyDistortion;

    /// <summary>The prediction cost of each reference, or zero for an unsearched reference.</summary>
    public InlineArray7<long> PredictionError;

    /// <summary>The transform-domain cost of the intra winner.</summary>
    public int IntraCost;

    /// <summary>The transform-domain cost of the winning mode. It is never more than the intra cost.</summary>
    public int InterCost;

    /// <summary>The coefficient rate of the winning mode against source references.</summary>
    public int SourceReferenceRate;

    /// <summary>The coefficient rate of the winning mode against reconstructed references.</summary>
    public int ReconstructedReferenceRate;

    /// <summary>The compound coefficient rates that match <see cref="CompoundReconstructedDistortion"/>.</summary>
    public InlineArray2<int> CompoundReconstructedRate;

    /// <summary>The motion vector found against each reference, in eighth samples.</summary>
    public InlineArray7<Av1MotionVector> MotionVectors;

    /// <summary>
    /// The reference index (zero for LAST) of the first and second prediction of the winner, or -1.
    /// </summary>
    public InlineArray2<sbyte> ReferenceFrameIndex;

    /// <summary>
    /// Gets the motion vector that marks an unsearched reference.
    /// </summary>
    public static Av1MotionVector InvalidMotionVector => new(short.MinValue, short.MinValue);
}

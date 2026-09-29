// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The temporal dependency statistics of one 16x16 block of one frame of a golden group. The distortions and the
/// dependency terms are scaled by sixteen. Reference: TplDepStats.
/// </summary>
internal struct Av1TplBlockStatistics
{
    /// <summary>The number of named inter references. Reference: INTER_REFS_PER_FRAME.</summary>
    public const int ReferenceCount = 7;

    /// <summary>The prediction error of the winning mode against source references. Reference: srcrf_sse.</summary>
    public long SourceReferenceSse;

    /// <summary>The reconstruction error of the winning mode against source references. Reference: srcrf_dist.</summary>
    public long SourceReferenceDistortion;

    /// <summary>The prediction error of the winning mode against reconstructed references. Reference: recrf_sse.</summary>
    public long ReconstructedReferenceSse;

    /// <summary>The reconstruction error of the winning mode against reconstructed references. Reference: recrf_dist.</summary>
    public long ReconstructedReferenceDistortion;

    /// <summary>The prediction error of the intra winner, measured only by the ducky encoder. Reference: intra_sse.</summary>
    public long IntraSse;

    /// <summary>The reconstruction error of the intra winner, measured only by the ducky encoder. Reference: intra_dist.</summary>
    public long IntraDistortion;

    /// <summary>
    /// The compound reconstruction error with the first (index 0) or second (index 1) reference reconstructed and the
    /// other taken from the source. Reference: cmp_recrf_dist.
    /// </summary>
    public InlineArray2<long> CompoundReconstructedDistortion;

    /// <summary>The propagated rate dependency. Reference: mc_dep_rate.</summary>
    public long DependencyRate;

    /// <summary>The propagated distortion dependency. Reference: mc_dep_dist.</summary>
    public long DependencyDistortion;

    /// <summary>The prediction cost of each reference, or zero for an unsearched reference. Reference: pred_error.</summary>
    public InlineArray7<long> PredictionError;

    /// <summary>The transform-domain cost of the intra winner. Reference: intra_cost.</summary>
    public int IntraCost;

    /// <summary>The transform-domain cost of the winning mode, never above the intra cost. Reference: inter_cost.</summary>
    public int InterCost;

    /// <summary>The coefficient rate of the winning mode against source references. Reference: srcrf_rate.</summary>
    public int SourceReferenceRate;

    /// <summary>The coefficient rate of the winning mode against reconstructed references. Reference: recrf_rate.</summary>
    public int ReconstructedReferenceRate;

    /// <summary>The coefficient rate of the intra winner, measured only by the ducky encoder. Reference: intra_rate.</summary>
    public int IntraRate;

    /// <summary>The compound coefficient rates matching <see cref="CompoundReconstructedDistortion"/>. Reference: cmp_recrf_rate.</summary>
    public InlineArray2<int> CompoundReconstructedRate;

    /// <summary>The motion vector found against each reference, in eighth samples. Reference: mv.</summary>
    public InlineArray7<Av1MotionVector> MotionVectors;

    /// <summary>
    /// The reference index (zero for LAST) of the first and second prediction of the winner, or -1. Reference:
    /// ref_frame_index.
    /// </summary>
    public InlineArray2<sbyte> ReferenceFrameIndex;

    /// <summary>
    /// Gets the motion vector that marks an unsearched reference. Reference: INVALID_MV.
    /// </summary>
    public static Av1MotionVector InvalidMotionVector => new(short.MinValue, short.MinValue);
}

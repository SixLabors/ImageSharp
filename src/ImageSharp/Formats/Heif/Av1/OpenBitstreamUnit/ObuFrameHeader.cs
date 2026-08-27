// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Stores the decoded syntax and derived state for one AV1 frame header.
/// </summary>
internal class ObuFrameHeader
{
    /// <summary>
    /// Stores the validity state of the eight reference-frame slots without a per-header array allocation.
    /// </summary>
    private InlineArray8<bool> referenceValid;

    /// <summary>
    /// Stores the multi-bit order hint associated with each of the eight reference-frame slots.
    /// </summary>
    private InlineArray8<uint> referenceOrderHint;

    /// <summary>
    /// Stores the reference-map index selected for each of the eight inter references.
    /// </summary>
    private InlineArray8<uint> referenceFrameIndex;

    /// <summary>
    /// Gets or sets a value indicating whether motion vectors use integer-sample precision.
    /// </summary>
    public bool ForceIntegerMotionVector { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether intra-block copy is permitted.
    /// </summary>
    public bool AllowIntraBlockCopy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reference-frame motion vectors may be used.
    /// </summary>
    public bool UseReferenceFrameMotionVectors { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether motion vectors may use high precision.
    /// </summary>
    public bool AllowHighPrecisionMotionVector { get; set; }

    /// <summary>
    /// Gets or sets the decoded tile layout.
    /// </summary>
    public ObuTileGroupHeader TilesInfo { get; set; } = new ObuTileGroupHeader();

    /// <summary>
    /// Gets or sets a value indicating whether every segment uses lossless coding.
    /// </summary>
    public bool CodedLossless { get; set; }

    /// <summary>
    /// Gets or sets the lossless-coding flag for each segment.
    /// </summary>
    public bool[] LosslessArray { get; set; } = new bool[Av1Constants.MaxSegmentCount];

    /// <summary>
    /// Gets or sets the frame quantization parameters.
    /// </summary>
    public ObuQuantizationParameters QuantizationParameters { get; set; } = new ObuQuantizationParameters();

    /// <summary>
    /// Gets or sets the frame segmentation parameters.
    /// </summary>
    public ObuSegmentationParameters SegmentationParameters { get; set; } = new ObuSegmentationParameters();

    /// <summary>
    /// Gets or sets a value indicating whether coding is lossless and no super-resolution scaling is applied.
    /// </summary>
    public bool AllLossless { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether warped motion is permitted.
    /// </summary>
    public bool AllowWarpedMotion { get; set; }

    /// <summary>
    /// Gets or sets the permitted reference prediction mode.
    /// </summary>
    public ObuReferenceMode ReferenceMode { get; set; }

    /// <summary>
    /// Gets or sets the film-grain synthesis parameters.
    /// </summary>
    public ObuFilmGrainParameters FilmGrainParameters { get; set; } = new ObuFilmGrainParameters();

    /// <summary>
    /// Gets or sets a value indicating whether the reduced transform set is used.
    /// </summary>
    public bool UseReducedTransformSet { get; set; }

    /// <summary>
    /// Gets or sets the loop-filter parameters.
    /// </summary>
    public ObuLoopFilterParameters LoopFilterParameters { get; set; } = new ObuLoopFilterParameters();

    /// <summary>
    /// Gets or sets the loop-restoration parameters.
    /// </summary>
    public ObuLoopRestorationParameters LoopRestorationParameters { get; set; } = new ObuLoopRestorationParameters();

    /// <summary>
    /// Gets or sets the constrained directional enhancement filter parameters.
    /// </summary>
    public ObuConstraintDirectionalEnhancementFilterParameters CdefParameters { get; set; } = new ObuConstraintDirectionalEnhancementFilterParameters();

    /// <summary>
    /// Gets or sets the number of mode-information columns in one stored row.
    /// </summary>
    public int ModeInfoStride { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame-end probability update is disabled.
    /// </summary>
    public bool DisableFrameEndUpdateCdf { get; set; }

    /// <summary>
    /// Gets or sets the skip-mode parameters.
    /// </summary>
    public ObuSkipModeParameters SkipModeParameters { get; set; } = new ObuSkipModeParameters();

    /// <summary>
    /// Gets or sets the transform-size selection mode.
    /// </summary>
    public Av1TransformMode TransformMode { get; set; }

    /// <summary>
    /// Gets or sets the loop-filter delta parameters.
    /// </summary>
    public ObuDeltaParameters DeltaLoopFilterParameters { get; set; } = new ObuDeltaParameters();

    /// <summary>
    /// Gets or sets the quantizer delta parameters.
    /// </summary>
    public ObuDeltaParameters DeltaQParameters { get; set; } = new ObuDeltaParameters();

    /// <summary>
    /// Gets a value indicating whether the frame uses intra prediction only.
    /// </summary>
    public bool IsIntra => this.FrameType is ObuFrameType.IntraOnlyFrame or ObuFrameType.KeyFrame;

    /// <summary>
    /// Gets or sets the decoded and rendered frame dimensions.
    /// </summary>
    internal ObuFrameSize FrameSize { get; set; } = new ObuFrameSize();

    /// <summary>
    /// Gets or sets the frame width in mode-information units.
    /// </summary>
    internal int ModeInfoColumnCount { get; set; }

    /// <summary>
    /// Gets or sets the frame height in mode-information units.
    /// </summary>
    internal int ModeInfoRowCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an existing reference frame is displayed without decoding a new frame.
    /// </summary>
    internal bool ShowExistingFrame { get; set; }

    /// <summary>
    /// Gets or sets the coded frame type.
    /// </summary>
    internal ObuFrameType FrameType { get; set; }

    /// <summary>
    /// Gets the validity state of each reference-frame slot.
    /// </summary>
    /// <returns>The mutable eight-entry reference-validity table.</returns>
    public Span<bool> GetReferenceValidity() => this.referenceValid;

    /// <summary>
    /// Gets the multi-bit order hint associated with each reference-frame slot.
    /// </summary>
    /// <returns>The mutable eight-entry reference-order-hint table.</returns>
    public Span<uint> GetReferenceOrderHints() => this.referenceOrderHint;

    /// <summary>
    /// Gets or sets a value indicating whether the decoded frame is immediately displayed.
    /// </summary>
    internal bool ShowFrame { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame may be displayed by a later header.
    /// </summary>
    internal bool ShowableFrame { get; set; }

    /// <summary>
    /// Gets or sets the reference map index selected when showing an existing frame.
    /// </summary>
    internal uint FrameToShowMapIdx { get; set; }

    /// <summary>
    /// Gets or sets the display frame identifier.
    /// </summary>
    internal uint DisplayFrameId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame can be decoded without state from earlier frames.
    /// </summary>
    internal bool ErrorResilientMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether screen-content coding tools are permitted.
    /// </summary>
    internal bool AllowScreenContentTools { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether probability adaptation is disabled for this frame.
    /// </summary>
    internal bool DisableCdfUpdate { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the current frame.
    /// </summary>
    internal uint CurrentFrameId { get; set; }

    /// <summary>
    /// Gets the reference-map index selected for each inter reference.
    /// </summary>
    /// <returns>The mutable eight-entry reference-frame-index table.</returns>
    public Span<uint> GetReferenceFrameIndices() => this.referenceFrameIndex;

    /// <summary>
    /// Gets or sets the frame order hint.
    /// </summary>
    internal uint OrderHint { get; set; }

    /// <summary>
    /// Gets or sets the reference frame supplying the initial probability context.
    /// </summary>
    internal uint PrimaryReferenceFrame { get; set; } = Av1Constants.PrimaryReferenceFrameNone;

    /// <summary>
    /// Gets or sets the bit mask of reference slots refreshed by this frame.
    /// </summary>
    internal uint RefreshFrameFlags { get; set; }

    /// <summary>
    /// Gets or sets the presentation time signaled by temporal point information.
    /// </summary>
    internal uint FramePresentationTime { get; set; }
}

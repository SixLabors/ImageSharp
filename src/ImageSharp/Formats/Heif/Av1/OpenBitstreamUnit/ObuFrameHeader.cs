// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Stores the decoded syntax and derived state for one AV1 frame header.
/// </summary>
internal sealed class ObuFrameHeader
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
    /// Stores the frame identifier associated with each of the eight reference-map slots.
    /// </summary>
    private InlineArray8<uint> referenceFrameId;

    /// <summary>
    /// Stores the reference-map slot selected for each of the seven inter reference types.
    /// </summary>
    private InlineArray8<uint> referenceFrameIndex;

    /// <summary>
    /// Stores the global-motion model associated with each of the seven inter reference types.
    /// </summary>
    private InlineArray7<Av1GlobalMotionParameters> globalMotionParameters;

    /// <summary>
    /// Stores the lossless-coding flag for each of the eight segments without a per-header array allocation.
    /// </summary>
    private InlineArray8<bool> losslessArray;

    /// <summary>
    /// Gets or sets the temporal-layer identifier carried by the primary frame-header OBU.
    /// </summary>
    public int TemporalId { get; set; }

    /// <summary>
    /// Gets or sets the spatial-layer identifier carried by the primary frame-header OBU.
    /// </summary>
    public int SpatialId { get; set; }

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
    /// Gets the component precision selected by the integer and high-precision frame flags.
    /// </summary>
    public Av1MotionVectorPrecision MotionVectorPrecision => this.ForceIntegerMotionVector
        ? Av1MotionVectorPrecision.Integer
        : this.AllowHighPrecisionMotionVector
            ? Av1MotionVectorPrecision.EighthSample
            : Av1MotionVectorPrecision.QuarterSample;

    /// <summary>
    /// Gets or sets the frame-level interpolation filter used for inter prediction.
    /// </summary>
    public Av1InterpolationFilter InterpolationFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether inter blocks may select a non-translational motion mode.
    /// </summary>
    public bool IsMotionModeSwitchable { get; set; }

    /// <summary>
    /// Gets or sets the decoded tile layout.
    /// </summary>
    public ObuTileGroupHeader TilesInfo { get; set; } = new ObuTileGroupHeader();

    /// <summary>
    /// Gets or sets a value indicating whether every segment uses lossless coding.
    /// </summary>
    public bool CodedLossless { get; set; }

    /// <summary>
    /// Gets the mutable lossless-coding flags for each segment.
    /// </summary>
    public Span<bool> LosslessArray => this.losslessArray;

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
    public ObuFrameSize FrameSize { get; set; } = new ObuFrameSize();

    /// <summary>
    /// Gets or sets the frame width in mode-information units.
    /// </summary>
    public int ModeInfoColumnCount { get; set; }

    /// <summary>
    /// Gets or sets the frame height in mode-information units.
    /// </summary>
    public int ModeInfoRowCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an existing reference frame is displayed without decoding a new frame.
    /// </summary>
    public bool ShowExistingFrame { get; set; }

    /// <summary>
    /// Gets or sets the coded frame type.
    /// </summary>
    public ObuFrameType FrameType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the decoded frame is immediately displayed.
    /// </summary>
    public bool ShowFrame { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame may be displayed by a later header.
    /// </summary>
    public bool ShowableFrame { get; set; }

    /// <summary>
    /// Gets or sets the reference map index selected when showing an existing frame.
    /// </summary>
    public uint FrameToShowMapIdx { get; set; }

    /// <summary>
    /// Gets or sets the display frame identifier.
    /// </summary>
    public uint DisplayFrameId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame can be decoded without state from earlier frames.
    /// </summary>
    public bool ErrorResilientMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether screen-content coding tools are permitted.
    /// </summary>
    public bool AllowScreenContentTools { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether probability adaptation is disabled for this frame.
    /// </summary>
    public bool DisableCdfUpdate { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the current frame.
    /// </summary>
    public uint CurrentFrameId { get; set; }

    /// <summary>
    /// Gets or sets the frame order hint.
    /// </summary>
    public uint OrderHint { get; set; }

    /// <summary>
    /// Gets or sets the zero-based inter-reference type that supplies the initial frame context, or the no-reference
    /// sentinel.
    /// </summary>
    public uint PrimaryReferenceFrame { get; set; } = Av1Constants.PrimaryReferenceFrameNone;

    /// <summary>
    /// Gets or sets the resolved reference-map slot supplying the initial frame context, or <see langword="null"/> when
    /// the frame uses the default context.
    /// </summary>
    public byte? PrimaryReferenceSlot { get; set; }

    /// <summary>
    /// Gets or sets the bit mask of reference slots refreshed by this frame.
    /// </summary>
    public uint RefreshFrameFlags { get; set; }

    /// <summary>
    /// Gets or sets the presentation time signaled by temporal point information.
    /// </summary>
    public uint FramePresentationTime { get; set; }

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
    /// Gets the frame identifier associated with each reference-map slot.
    /// </summary>
    /// <returns>The mutable eight-entry reference-frame-identifier table.</returns>
    public Span<uint> GetReferenceFrameIds() => this.referenceFrameId;

    /// <summary>
    /// Gets the reference-map slot selected for each inter reference type.
    /// </summary>
    /// <returns>The mutable seven-entry inter-reference-map table.</returns>
    public Span<uint> GetReferenceFrameIndices() => this.referenceFrameIndex[..Av1Constants.ReferencesPerFrame];

    /// <summary>
    /// Gets the global-motion model associated with each canonical inter reference type from LAST through ALTREF.
    /// </summary>
    /// <returns>The mutable seven-entry global-motion parameter table.</returns>
    public Span<Av1GlobalMotionParameters> GetGlobalMotionParameters() => this.globalMotionParameters;

    /// <summary>
    /// Invalidates retained reference slots whose frame identifiers fall outside the permitted backwards window.
    /// </summary>
    /// <param name="frameIdLength">The number of bits in the modulo frame-identifier domain.</param>
    /// <param name="deltaFrameIdLength">The number of bits used to signal reference-frame identifier deltas.</param>
    public void MarkReferenceFrames(int frameIdLength, int deltaFrameIdLength)
    {
        uint referenceWindow = 1U << deltaFrameIdLength;
        uint frameIdModulus = 1U << frameIdLength;
        Span<uint> referenceFrameIds = this.GetReferenceFrameIds();
        Span<bool> referenceValidity = this.GetReferenceValidity();

        for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
        {
            uint referenceFrameId = referenceFrameIds[slot];
            if (this.CurrentFrameId > referenceWindow)
            {
                // Without wraparound, a retained identifier is valid only in the closed interval ending at the
                // current identifier and extending referenceWindow values backwards.
                if (referenceFrameId > this.CurrentFrameId || referenceFrameId < this.CurrentFrameId - referenceWindow)
                {
                    referenceValidity[slot] = false;
                }
            }
            else
            {
                // When the backwards window crosses zero, valid identifiers occupy both ends of the modulo domain.
                // Only the open interval between the current identifier and the wrapped lower bound is invalid.
                uint wrappedLowerBound = frameIdModulus + this.CurrentFrameId - referenceWindow;
                if (referenceFrameId > this.CurrentFrameId && referenceFrameId < wrappedLowerBound)
                {
                    referenceValidity[slot] = false;
                }
            }
        }
    }
}

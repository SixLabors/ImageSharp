// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores encoder-selected prediction, transform, skip, and palette state for one block.
/// </summary>
internal struct Av1EncoderBlockModeInfo
{
    /// <summary>The residual-skip flag in the packed prediction state.</summary>
    private const byte SkipMask = 1 << 0;

    /// <summary>The compound-skip flag in the packed prediction state.</summary>
    private const byte SkipModeMask = 1 << 1;

    /// <summary>The intra-block-copy flag in the packed prediction state.</summary>
    private const byte IntraBlockCopyMask = 1 << 2;

    /// <summary>The bounded-sequence GOLDEN secondary-reference flag in the packed prediction state.</summary>
    private const byte GoldenSecondaryReferenceMask = 1 << 7;

    /// <summary>The three low bits store the segment identifier, followed by the primary reference.</summary>
    private const int ReferenceFrameShift = 3;

    /// <summary>Both segment identifiers and primary references have eight possible values.</summary>
    private const int SegmentAndReferenceMask = 7;

    /// <summary>The vertical filter follows the three prediction flags.</summary>
    private const int VerticalFilterShift = 3;

    /// <summary>The horizontal filter follows the two-bit vertical filter.</summary>
    private const int HorizontalFilterShift = 5;

    /// <summary>Two bits represent each concrete interpolation filter, excluding the frame-level switchable sentinel.</summary>
    private const int InterpolationFilterMask = 3;

    /// <summary>The masked-compound group flag in the packed compound state.</summary>
    private const byte CompoundGroupIndexMask = 1 << 0;

    /// <summary>The equal-average compound flag in the packed compound state.</summary>
    private const byte CompoundIndexMask = 1 << 1;

    /// <summary>The wedge orientation flag in the packed compound state.</summary>
    private const byte CompoundWedgeSignMask = 1 << 2;

    /// <summary>The difference-weighted mask orientation flag in the packed compound state.</summary>
    private const byte DifferenceWeightedMaskTypeMask = 1 << 3;

    /// <summary>The synthetic inter-intra secondary-reference flag in the packed compound state.</summary>
    private const byte InterIntraMask = 1 << 6;

    /// <summary>The compound blend type follows the compound syntax flags.</summary>
    private const int CompoundTypeShift = 4;

    /// <summary>Two bits represent the four compound blend types.</summary>
    private const int CompoundTypeMask = 3;

    // Primary references never use the absent-secondary sentinel. Pack their three bits beside the segment,
    // and the concrete filters beside the flags, so adding inter syntax does not enlarge the frame-wide grid.
    private byte blockSize;
    private byte partitionType;
    private byte flags;
    private byte segmentAndReference;
    private byte transformSize;
    private byte mode;
    private byte uvMode;
    private byte compoundState;
    private byte compoundWedgeIndex;

    /// <summary>
    /// Gets or sets the encoded block size.
    /// </summary>
    public Av1BlockSize BlockSize
    {
        readonly get => (Av1BlockSize)this.blockSize;
        set => this.blockSize = (byte)value;
    }

    /// <summary>
    /// Gets or sets the intra predictor blended with a single-reference inter predictor.
    /// </summary>
    public Av1InterIntraMode InterIntraMode
    {
        readonly get => (Av1InterIntraMode)((this.compoundState >> CompoundTypeShift) & CompoundTypeMask);
        set => this.compoundState = (byte)((this.compoundState & ~(CompoundTypeMask << CompoundTypeShift)) | ((int)value << CompoundTypeShift));
    }

    /// <summary>
    /// Gets or sets a value indicating whether inter-intra prediction uses a wedge mask.
    /// </summary>
    public bool UseInterIntraWedge
    {
        readonly get => (this.compoundState & CompoundGroupIndexMask) != 0;
        set => this.compoundState = value
            ? (byte)(this.compoundState | CompoundGroupIndexMask)
            : (byte)(this.compoundState & ~CompoundGroupIndexMask);
    }

    /// <summary>
    /// Gets or sets the inter-intra wedge-mask index.
    /// </summary>
    public byte InterIntraWedgeIndex
    {
        readonly get => this.compoundWedgeIndex;
        set => this.compoundWedgeIndex = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether masked compound prediction is selected.
    /// </summary>
    public bool CompoundGroupIndex
    {
        readonly get => (this.compoundState & CompoundGroupIndexMask) != 0;
        set => this.compoundState = value
            ? (byte)(this.compoundState | CompoundGroupIndexMask)
            : (byte)(this.compoundState & ~CompoundGroupIndexMask);
    }

    /// <summary>
    /// Gets or sets a value indicating whether unmasked compound prediction uses equal averaging.
    /// </summary>
    public bool CompoundIndex
    {
        readonly get => (this.compoundState & CompoundIndexMask) != 0;
        set => this.compoundState = value
            ? (byte)(this.compoundState | CompoundIndexMask)
            : (byte)(this.compoundState & ~CompoundIndexMask);
    }

    /// <summary>
    /// Gets or sets the selected compound blend type.
    /// </summary>
    public Av1CompoundType CompoundType
    {
        readonly get => (Av1CompoundType)((this.compoundState >> CompoundTypeShift) & CompoundTypeMask);
        set => this.compoundState = (byte)((this.compoundState & ~(CompoundTypeMask << CompoundTypeShift)) | ((int)value << CompoundTypeShift));
    }

    /// <summary>
    /// Gets or sets the selected wedge-mask index.
    /// </summary>
    public byte CompoundWedgeIndex
    {
        readonly get => this.compoundWedgeIndex;
        set => this.compoundWedgeIndex = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the selected wedge reverses the predictor weights.
    /// </summary>
    public bool CompoundWedgeSign
    {
        readonly get => (this.compoundState & CompoundWedgeSignMask) != 0;
        set => this.compoundState = value
            ? (byte)(this.compoundState | CompoundWedgeSignMask)
            : (byte)(this.compoundState & ~CompoundWedgeSignMask);
    }

    /// <summary>
    /// Gets or sets the selected difference-weighted mask orientation.
    /// </summary>
    public Av1DifferenceWeightedMaskType DifferenceWeightedMaskType
    {
        readonly get => (this.compoundState & DifferenceWeightedMaskTypeMask) == 0
            ? Av1DifferenceWeightedMaskType.Type38
            : Av1DifferenceWeightedMaskType.Type38Inverse;
        set => this.compoundState = value == Av1DifferenceWeightedMaskType.Type38Inverse
            ? (byte)(this.compoundState | DifferenceWeightedMaskTypeMask)
            : (byte)(this.compoundState & ~DifferenceWeightedMaskTypeMask);
    }

    /// <summary>
    /// Gets or sets the partition type that produced the block.
    /// </summary>
    public Av1PartitionType PartitionType
    {
        readonly get => (Av1PartitionType)this.partitionType;
        set => this.partitionType = (byte)value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether residual coefficients are omitted for the block.
    /// </summary>
    public bool Skip
    {
        readonly get => (this.flags & SkipMask) != 0;
        set => this.flags = value ? (byte)(this.flags | SkipMask) : (byte)(this.flags & ~SkipMask);
    }

    /// <summary>
    /// Gets or sets a value indicating whether compound skip mode is selected.
    /// </summary>
    public bool SkipMode
    {
        readonly get => (this.flags & SkipModeMask) != 0;
        set => this.flags = value ? (byte)(this.flags | SkipModeMask) : (byte)(this.flags & ~SkipModeMask);
    }

    /// <summary>
    /// Gets or sets a value indicating whether intra block copy is selected.
    /// </summary>
    public bool UseIntraBlockCopy
    {
        readonly get => (this.flags & IntraBlockCopyMask) != 0;
        set => this.flags = value ? (byte)(this.flags | IntraBlockCopyMask) : (byte)(this.flags & ~IntraBlockCopyMask);
    }

    /// <summary>
    /// Gets or sets the segmentation identifier assigned to the block.
    /// </summary>
    public int SegmentId
    {
        readonly get => this.segmentAndReference & SegmentAndReferenceMask;
        set => this.segmentAndReference = (byte)((this.segmentAndReference & ~SegmentAndReferenceMask) | value);
    }

    /// <summary>
    /// Gets or sets the luma transform size selected for the block.
    /// </summary>
    public Av1TransformSize TransformSize
    {
        readonly get => (Av1TransformSize)this.transformSize;
        set => this.transformSize = (byte)value;
    }

    /// <summary>
    /// Gets or sets the luma prediction mode written for the block.
    /// </summary>
    public Av1PredictionMode Mode
    {
        readonly get => (Av1PredictionMode)this.mode;
        set => this.mode = (byte)value;
    }

    /// <summary>
    /// Gets or sets the chroma prediction mode written for the block.
    /// </summary>
    public Av1ChromaPredictionMode UvMode
    {
        readonly get => (Av1ChromaPredictionMode)this.uvMode;
        set => this.uvMode = (byte)value;
    }

    /// <summary>
    /// Gets or sets the primary prediction reference selected for the block.
    /// </summary>
    public Av1ReferenceFrameType ReferenceFrame
    {
        readonly get => (Av1ReferenceFrameType)(this.segmentAndReference >> ReferenceFrameShift);
        set => this.segmentAndReference = (byte)((this.segmentAndReference & SegmentAndReferenceMask) | ((int)value << ReferenceFrameShift));
    }

    /// <summary>
    /// Gets or sets the optional secondary prediction reference selected for the block.
    /// </summary>
    /// <remarks>
    /// The bounded sequence encoder retains LAST and GOLDEN plus AV1's synthetic INTRA secondary reference. Inter-intra
    /// and two-reference compound are mutually exclusive, so the synthetic-reference bit shares compound syntax state.
    /// </remarks>
    public Av1ReferenceFrameType SecondaryReferenceFrame
    {
        readonly get => (this.compoundState & InterIntraMask) != 0
            ? Av1ReferenceFrameType.Intra
            : (this.flags & GoldenSecondaryReferenceMask) == 0
                ? Av1ReferenceFrameType.None
                : Av1ReferenceFrameType.Golden;
        set
        {
            this.flags = value == Av1ReferenceFrameType.Golden
                ? (byte)(this.flags | GoldenSecondaryReferenceMask)
                : (byte)(this.flags & ~GoldenSecondaryReferenceMask);
            this.compoundState = value == Av1ReferenceFrameType.Intra
                ? (byte)(this.compoundState | InterIntraMask)
                : (byte)(this.compoundState & ~InterIntraMask);
        }
    }

    /// <summary>
    /// Gets or sets the concrete vertical interpolation filter selected for the block.
    /// </summary>
    public Av1InterpolationFilter VerticalInterpolationFilter
    {
        readonly get => (Av1InterpolationFilter)((this.flags >> VerticalFilterShift) & InterpolationFilterMask);
        set => this.flags = (byte)((this.flags & ~(InterpolationFilterMask << VerticalFilterShift)) | ((int)value << VerticalFilterShift));
    }

    /// <summary>
    /// Gets or sets the concrete horizontal interpolation filter selected for the block.
    /// </summary>
    public Av1InterpolationFilter HorizontalInterpolationFilter
    {
        readonly get => (Av1InterpolationFilter)((this.flags >> HorizontalFilterShift) & InterpolationFilterMask);
        set => this.flags = (byte)((this.flags & ~(InterpolationFilterMask << HorizontalFilterShift)) | ((int)value << HorizontalFilterShift));
    }
}

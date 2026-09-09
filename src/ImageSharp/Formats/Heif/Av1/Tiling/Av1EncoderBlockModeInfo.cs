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

    // Primary references never use the absent-secondary sentinel. Pack their three bits beside the segment,
    // and the concrete filters beside the flags, so adding inter syntax does not enlarge the frame-wide grid.
    private byte blockSize;
    private byte partitionType;
    private byte flags;
    private byte segmentAndReference;
    private byte transformSize;
    private byte mode;
    private byte uvMode;

    /// <summary>
    /// Gets or sets the encoded block size.
    /// </summary>
    public Av1BlockSize BlockSize
    {
        readonly get => (Av1BlockSize)this.blockSize;
        set => this.blockSize = (byte)value;
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

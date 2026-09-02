// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores encoder-selected prediction, transform, skip, and palette state for one block.
/// </summary>
internal struct Av1EncoderBlockModeInfo
{
    private const byte SkipMask = 1 << 0;
    private const byte SkipModeMask = 1 << 1;
    private const byte IntraBlockCopyMask = 1 << 2;

    // Every stored syntax value has an AV1-defined range below 256. Byte fields and one shared flag byte
    // keep the frame-wide mode allocation compact without losing any representable encoder state.
    private byte blockSize;
    private byte partitionType;
    private byte flags;
    private byte segmentId;
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
        readonly get => this.segmentId;
        set => this.segmentId = (byte)value;
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
}

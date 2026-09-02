// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores encoder-selected prediction, transform, skip, and palette state for one block.
/// </summary>
internal class Av1EncoderBlockModeInfo
{
    /// <summary>
    /// Gets or sets the encoded block size.
    /// </summary>
    public Av1BlockSize BlockSize { get; set; }

    /// <summary>
    /// Gets or sets the partition type that produced the block.
    /// </summary>
    public Av1PartitionType PartitionType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether residual coefficients are omitted for the block.
    /// </summary>
    public bool Skip { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether compound skip mode is selected.
    /// </summary>
    public bool SkipMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether intra block copy is selected.
    /// </summary>
    public bool UseIntraBlockCopy { get; set; }

    /// <summary>
    /// Gets or sets the segmentation identifier assigned to the block.
    /// </summary>
    public int SegmentId { get; set; }

    /// <summary>
    /// Gets or sets the transform-tree depth selected for the block.
    /// </summary>
    public int TransformDepth { get; set; }

    /// <summary>
    /// Gets or sets the luma prediction mode written for the block.
    /// </summary>
    public Av1PredictionMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the chroma prediction mode written for the block.
    /// </summary>
    public Av1ChromaPredictionMode UvMode { get; set; }
}

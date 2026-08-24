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
    /// Gets the encoded block size.
    /// </summary>
    public Av1BlockSize BlockSize { get; }

    /// <summary>
    /// Gets the selected luma prediction mode.
    /// </summary>
    public Av1PredictionMode PredictionMode { get; }

    /// <summary>
    /// Gets the partition type that produced the block.
    /// </summary>
    public Av1PartitionType PartitionType { get; }

    /// <summary>
    /// Gets the selected chroma prediction mode.
    /// </summary>
    public Av1PredictionMode UvPredictionMode { get; }

    /// <summary>
    /// Gets a value indicating whether residual coefficients are omitted for the block.
    /// </summary>
    public bool Skip { get; } = true;

    /// <summary>
    /// Gets a value indicating whether compound skip mode is selected.
    /// </summary>
    public bool SkipMode { get; } = true;

    /// <summary>
    /// Gets a value indicating whether intra block copy is selected.
    /// </summary>
    public bool UseIntraBlockCopy { get; } = true;

    /// <summary>
    /// Gets the segmentation identifier assigned to the block.
    /// </summary>
    public int SegmentId { get; }

    /// <summary>
    /// Gets or sets the transform-tree depth selected for the block.
    /// </summary>
    public int TransformDepth { get; internal set; }

    /// <summary>
    /// Gets or sets the luma prediction mode written for the block.
    /// </summary>
    public Av1PredictionMode Mode { get; internal set; }

    /// <summary>
    /// Gets or sets the chroma prediction mode written for the block.
    /// </summary>
    public Av1PredictionMode UvMode { get; internal set; }
}

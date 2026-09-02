// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Holds encoder-side block decisions and transform data for one AV1 superblock.
/// </summary>
internal class Av1Superblock
{
    /// <summary>
    /// Gets or sets the reusable final-block and partition-decision workspace.
    /// </summary>
    public required Av1EncoderSuperblockWorkspace Workspace { get; set; }

    /// <summary>
    /// Gets the final encoder decisions in partition traversal order.
    /// </summary>
    public Span<Av1EncoderBlockStruct> FinalBlocks => this.Workspace.FinalBlocks;

    /// <summary>
    /// Gets or sets the tile containing the superblock.
    /// </summary>
    public required Av1TileInfo TileInfo { get; set; }

    /// <summary>
    /// Gets the selected partition type for each partition-tree node.
    /// </summary>
    public Span<byte> CodingUnitPartitionTypes => this.Workspace.PartitionTypes;

    /// <summary>
    /// Gets or sets the superblock index within the picture.
    /// </summary>
    public int Index { get; set; }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using static SixLabors.ImageSharp.Formats.Heif.Av1.Tiling.Av1TileWriter;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Holds encoder-side block decisions and transform data for one AV1 superblock.
/// </summary>
internal class Av1Superblock
{
    /// <summary>
    /// Gets or sets the final encoder decisions in partition traversal order.
    /// </summary>
    public required Av1EncoderBlockStruct[] FinalBlocks { get; set; }

    /// <summary>
    /// Gets or sets the tile containing the superblock.
    /// </summary>
    public required Av1TileInfo TileInfo { get; set; }

    /// <summary>
    /// Gets or sets the selected partition type for each partition-tree node.
    /// </summary>
    public required Av1PartitionType[] CodingUnitPartitionTypes { get; internal set; }

    /// <summary>
    /// Gets or sets the superblock index within the picture.
    /// </summary>
    public int Index { get; internal set; }
}

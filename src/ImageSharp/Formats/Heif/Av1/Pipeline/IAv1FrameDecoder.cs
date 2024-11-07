// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Interface for decoder of a single frame.
/// </summary>
internal interface IAv1FrameDecoder
{
    /// <summary>
    /// Decode a single superblock.
    /// </summary>
    /// <param name="modeInfoPosition">The top left position of the superblock, in mode info units.</param>
    /// <param name="superblockInfo">The superblock to decode</param>
    /// <param name="tileInfo">The tile in whcih the superblock is positioned.</param>
    void DecodeSuperblock(Point modeInfoPosition, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo);
}

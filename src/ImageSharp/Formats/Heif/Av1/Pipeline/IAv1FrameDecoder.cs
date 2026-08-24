// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Defines reconstruction of decoded AV1 superblocks within a single still-image frame.
/// </summary>
internal interface IAv1FrameDecoder
{
    /// <summary>
    /// Reconstructs one decoded superblock into the current frame buffer.
    /// </summary>
    /// <param name="modeInfoPosition">The superblock's top-left position in 4x4 mode-info units.</param>
    /// <param name="superblockInfo">The decoded syntax and block modes for the superblock.</param>
    /// <param name="tileInfo">The tile that contains the superblock.</param>
    void DecodeSuperblock(Point modeInfoPosition, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo);
}

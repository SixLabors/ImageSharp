// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Interface for writing of image tiles.
/// </summary>
internal interface IAv1TileWriter
{
    /// <summary>
    /// Write the information for a single tile.
    /// </summary>
    /// <param name="tileNum">The index of the tile that is to be read.</param>
    /// <returns>
    /// The bytes of encoded data in the bitstream dedicated to this tile.
    /// </returns>
    Span<byte> WriteTile(int tileNum);
}

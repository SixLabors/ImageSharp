// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Interface for reading of image tiles.
/// </summary>
internal interface IAv1TileReader
{
    /// <summary>
    /// Read the information for a single tile.
    /// </summary>
    /// <param name="tileData">
    /// The bytes of encoded data in the bitstream dedicated to this tile.
    /// </param>
    /// <param name="tileNum">The index of the tile that is to be read.</param>
    void ReadTile(Span<byte> tileData, int tileNum);
}

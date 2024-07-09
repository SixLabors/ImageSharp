// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Interface for decoding of image tiles.
/// </summary>
internal interface IAv1TileDecoder
{
    /// <summary>
    /// Decode a single tile.
    /// </summary>
    /// <param name="tileData">
    /// The bytes of encoded data in the bitstream dedicated to this tile.
    /// </param>
    /// <param name="tileNum">The index of the tile that is to be decoded.</param>
    void DecodeTile(Span<byte> tileData, int tileNum);
}

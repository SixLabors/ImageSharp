// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Gives the encoded tile payloads of one coded frame to the OBU writer.
/// </summary>
internal interface IAv1TileWriter
{
    /// <summary>
    /// Gets the encoded bytes for a single tile.
    /// </summary>
    /// <param name="tileNum">The index of the encoded tile.</param>
    /// <returns>
    /// The bytes of encoded data in the bitstream dedicated to this tile.
    /// </returns>
    ReadOnlySpan<byte> GetTileData(int tileNum);
}

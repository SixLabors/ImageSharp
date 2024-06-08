// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Interface for decoding of image tiles.
/// </summary>
internal interface IAv1TileDecoder
{
    /// <summary>
    /// Start decoding all tiles of a frame.
    /// </summary>
    void StartDecodeTiles();

    /// <summary>
    /// Decode a single tile.
    /// </summary>
    /// <param name="tileData">
    /// The bytes of encoded data in the bitstream dedicated to this tile.
    /// </param>
    /// <param name="tileNum">The index of the tile that is to be decoded.</param>
    void DecodeTile(Span<byte> tileData, int tileNum);

    /// <summary>
    /// Finshed decoding all tiles of a frame.
    /// </summary>
    /// <param name="doCdef">Apply the CDF filter.</param>
    /// <param name="doLoopRestoration">Apply the loop filters.</param>
    void FinishDecodeTiles(bool doCdef, bool doLoopRestoration);
}

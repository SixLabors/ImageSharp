// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Interface for decoding of image tiles.
/// </summary>
internal interface IAv1TileDecoder
{
    /// <summary>
    /// Gets or sets a value indicating whether a sequence header has been read.
    /// </summary>
    bool SequenceHeaderDone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show the existing frame.
    /// </summary>
    bool ShowExistingFrame { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a FrameHeader has just been read.
    /// </summary>
    bool SeenFrameHeader { get; set; }

    /// <summary>
    /// Gets Information about the frame.
    /// </summary>
    ObuFrameHeader FrameInfo { get; }

    /// <summary>
    /// Gets Information about the sequence of frames.
    /// </summary>
    ObuSequenceHeader SequenceHeader { get; }

    /// <summary>
    /// Gets information required to decode the tiles of a frame.
    /// </summary>
    ObuTileInfo TileInfo { get; }

    /// <summary>
    /// Decode a single tile.
    /// </summary>
    /// <param name="tileNum">The index of the tile that is to be decoded.</param>
    void DecodeTile(int tileNum);

    /// <summary>
    /// Finshed decoding all tiles of a frame.
    /// </summary>
    /// <param name="doCdef">Apply the CDF filter.</param>
    /// <param name="doLoopRestoration">Apply the loop filters.</param>
    void FinishDecodeTiles(bool doCdef, bool doLoopRestoration);
}

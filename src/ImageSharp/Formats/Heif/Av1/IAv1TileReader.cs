// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Defines tile-payload consumption and completion for one coded AV1 frame.
/// </summary>
internal interface IAv1TileReader
{
    /// <summary>
    /// Reads one entropy-coded tile payload into the current frame state.
    /// </summary>
    /// <param name="tileData">The bounded bitstream bytes belonging to the tile.</param>
    /// <param name="tileNum">The zero-based tile index in raster order.</param>
    void ReadTile(Span<byte> tileData, int tileNum);

    /// <summary>
    /// Completes the current coded frame after all tile payloads have been read and releases frame-scoped resources.
    /// </summary>
    void CompleteFrame();
}

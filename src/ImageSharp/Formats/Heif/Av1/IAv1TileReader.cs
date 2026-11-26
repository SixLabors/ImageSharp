// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Defines tile-payload consumption and completion for one coded AV1 frame.
/// </summary>
/// <remarks>
/// A method returns damage and does not report it. The OBU reader reports the damage one time, through the damage route of its payload. Then the OBU reader
/// stops the payload.
/// </remarks>
internal interface IAv1TileReader
{
    /// <summary>
    /// Reads one entropy-coded tile payload into the current frame state.
    /// </summary>
    /// <param name="tileData">The bounded bitstream bytes belonging to the tile.</param>
    /// <param name="tileNum">The zero-based tile index in raster order.</param>
    /// <returns>The message of damage that stops the payload, or <see langword="null"/>.</returns>
    string? ReadTile(Span<byte> tileData, int tileNum);

    /// <summary>
    /// Completes the current coded frame and releases frame-scoped resources. The caller calls this method after it reads all tile payloads.
    /// </summary>
    /// <returns>The message of damage that stops the payload, or <see langword="null"/>.</returns>
    string? CompleteFrame();
}

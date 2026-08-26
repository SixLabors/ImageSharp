// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Color;

/// <summary>
/// Exposes reconstructed eight-bit 4:2:0 component rows to the shared fixed-point HEIF converter.
/// </summary>
internal interface IHeifYuv420ToRgb8Source
{
    /// <summary>
    /// Gets one complete luma row in native sample storage.
    /// </summary>
    /// <param name="row">The zero-based luma row.</param>
    /// <returns>The reconstructed luma samples.</returns>
    public ReadOnlySpan<ushort> GetLumaRow(int row);

    /// <summary>
    /// Gets one complete blue-difference chroma row in native sample storage.
    /// </summary>
    /// <param name="row">The zero-based chroma row.</param>
    /// <returns>The reconstructed blue-difference samples.</returns>
    public ReadOnlySpan<ushort> GetChromaBlueRow(int row);

    /// <summary>
    /// Gets one complete red-difference chroma row in native sample storage.
    /// </summary>
    /// <param name="row">The zero-based chroma row.</param>
    /// <returns>The reconstructed red-difference samples.</returns>
    public ReadOnlySpan<ushort> GetChromaRedRow(int row);
}

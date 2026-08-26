// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Color;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc.Color;

/// <summary>
/// Adapts reconstructed HEVC planes to the shared fixed-point HEIF color converter.
/// </summary>
internal readonly struct HevcYuv420ToRgb8Source : IHeifYuv420ToRgb8Source
{
    /// <summary>
    /// The reconstructed HEVC picture containing the component planes.
    /// </summary>
    private readonly HevcPictureBuffer picture;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcYuv420ToRgb8Source"/> struct.
    /// </summary>
    /// <param name="picture">The reconstructed HEVC picture.</param>
    public HevcYuv420ToRgb8Source(HevcPictureBuffer picture) => this.picture = picture;

    /// <inheritdoc/>
    public ReadOnlySpan<ushort> GetLumaRow(int row) => this.picture.GetRowSpan(HevcPlane.Y, row);

    /// <inheritdoc/>
    public ReadOnlySpan<ushort> GetChromaBlueRow(int row) => this.picture.GetRowSpan(HevcPlane.Cb, row);

    /// <inheritdoc/>
    public ReadOnlySpan<ushort> GetChromaRedRow(int row) => this.picture.GetRowSpan(HevcPlane.Cr, row);
}

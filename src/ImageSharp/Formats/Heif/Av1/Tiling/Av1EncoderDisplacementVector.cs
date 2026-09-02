// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores one encoder-selected integer displacement vector in the AV1 signed component domain.
/// </summary>
internal struct Av1EncoderDisplacementVector
{
    /// <summary>
    /// The vertical component in one-eighth-sample units.
    /// </summary>
    public short Row;

    /// <summary>
    /// The horizontal component in one-eighth-sample units.
    /// </summary>
    public short Column;
}

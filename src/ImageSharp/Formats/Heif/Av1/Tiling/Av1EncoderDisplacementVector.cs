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

/// <summary>
/// Stores the reference-vector contexts selected before later blocks populate the frame grid.
/// </summary>
internal struct Av1EncoderReferenceContext
{
    /// <summary>
    /// Stores the differential reference vectors for the four usable stack entries.
    /// </summary>
    public InlineArray4<Av1EncoderDisplacementVector> References;

    /// <summary>
    /// Stores the candidate weights used by dynamic-reference-list syntax.
    /// </summary>
    public InlineArray4<ushort> Weights;

    /// <summary>
    /// Stores the packed inter-mode context.
    /// </summary>
    public ushort ModeContext;

    /// <summary>
    /// Stores the number of discovered candidates.
    /// </summary>
    public byte Count;
}

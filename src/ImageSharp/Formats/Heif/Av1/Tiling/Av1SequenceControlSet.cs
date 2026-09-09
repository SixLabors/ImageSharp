// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Holds encoder configuration and sequence-wide state shared by AV1 pictures.
/// </summary>
internal class Av1SequenceControlSet
{
    /// <summary>
    /// Gets or sets the sequence header that governs encoded pictures.
    /// </summary>
    public required ObuSequenceHeader SequenceHeader { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of encoded blocks allocated for a picture.
    /// </summary>
    public int MaxBlockCount { get; set; }
}

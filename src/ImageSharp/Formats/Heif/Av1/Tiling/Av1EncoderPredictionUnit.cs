// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores encoder-selected intra prediction modes and directional-angle adjustments for one block.
/// </summary>
internal class Av1EncoderPredictionUnit
{
    /// <summary>
    /// Gets or sets the directional angle adjustment for each prediction plane.
    /// </summary>
    public required byte[] AngleDelta { get; set; }

    /// <summary>
    /// Gets or sets the chroma-from-luma alpha magnitude index.
    /// </summary>
    public int ChromaFromLumaIndex { get; set; }

    /// <summary>
    /// Gets or sets the packed chroma-from-luma alpha signs for the U and V planes.
    /// </summary>
    public int ChromaFromLumaSigns { get; set; }
}

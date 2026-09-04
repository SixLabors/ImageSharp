// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores encoder-selected intra prediction modes and directional-angle adjustments for one block.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
internal struct Av1EncoderPredictionUnit
{
    /// <summary>
    /// Stores the signed luma and chroma directional adjustments.
    /// </summary>
    private InlineArray2<sbyte> angleDelta;

    /// <summary>
    /// Gets the directional angle adjustment for each prediction plane.
    /// </summary>
    [UnscopedRef]
    public Span<sbyte> AngleDelta => this.angleDelta;

    /// <summary>
    /// Gets or sets the chroma-from-luma alpha magnitude index.
    /// </summary>
    public byte ChromaFromLumaIndex { get; set; }

    /// <summary>
    /// Gets or sets the packed chroma-from-luma alpha signs for the U and V planes.
    /// </summary>
    public sbyte ChromaFromLumaSigns { get; set; }
}

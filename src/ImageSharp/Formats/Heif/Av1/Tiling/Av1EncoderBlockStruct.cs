// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores encoder block geometry and its selected coding-mode information.
/// </summary>
internal struct Av1EncoderBlockStruct
{
    /// <summary>
    /// Stores the luma and shared chroma palette sizes inline with the block.
    /// </summary>
    private InlineArray2<byte> paletteSize;

    /// <summary>
    /// Stores the block's prediction-unit syntax inline.
    /// </summary>
    private Av1EncoderPredictionUnit predictionUnit;

    /// <summary>
    /// Gets or sets a value indicating whether this luma block owns the corresponding chroma syntax.
    /// </summary>
    public bool HasChroma { get; set; }

    /// <summary>
    /// Gets or sets the quantizer index used for the block.
    /// </summary>
    public int QuantizationIndex { get; set; }

    /// <summary>
    /// Gets or sets the segmentation identifier assigned to the block.
    /// </summary>
    public int SegmentId { get; set; }

    /// <summary>
    /// Gets or sets the filter-intra mode selected for the block.
    /// </summary>
    public Av1FilterIntraMode FilterIntraMode { get; set; }

    /// <summary>
    /// Gets the writable palette sizes for luma and for the shared chroma mode.
    /// </summary>
    [UnscopedRef]
    public Span<byte> PaletteSize => this.paletteSize;

    /// <summary>
    /// Gets the encoder prediction-unit state for the block.
    /// </summary>
    [UnscopedRef]
    public ref Av1EncoderPredictionUnit PredictionUnit => ref this.predictionUnit;

    /// <summary>
    /// Stores the two palette-size values embedded by libaom in block mode information.
    /// </summary>
    [InlineArray(2)]
    private struct InlineArray2<T>
    {
        private T element;
    }
}

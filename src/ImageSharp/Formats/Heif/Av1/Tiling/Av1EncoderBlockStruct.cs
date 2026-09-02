// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores encoder block geometry and its selected coding-mode information.
/// </summary>
internal class Av1EncoderBlockStruct
{
    /// <summary>
    /// Stores transform-unit state inline with the block.
    /// </summary>
    private InlineArray16<Av1TransformUnit> transformBlocks;

    /// <summary>
    /// Stores the luma and shared chroma palette sizes inline with the block.
    /// </summary>
    private InlineArray2<byte> paletteSize;

    /// <summary>
    /// Stores the block's prediction-unit syntax inline.
    /// </summary>
    private Av1EncoderPredictionUnit predictionUnit;

    /// <summary>
    /// Gets the transform-unit state in transform traversal order.
    /// </summary>
    public Span<Av1TransformUnit> TransformBlocks => this.transformBlocks;

    /// <summary>
    /// Gets or sets the macroblock edge and neighbor state used while writing the block.
    /// </summary>
    public required Av1MacroBlockD MacroBlock { get; set; }

    /// <summary>
    /// Gets or sets the index used to resolve the block geometry from mode-decision scan order.
    /// </summary>
    public int ModeDecisionScanIndex { get; set; }

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
    public Span<byte> PaletteSize => this.paletteSize;

    /// <summary>
    /// Gets the encoder prediction-unit state for the block.
    /// </summary>
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

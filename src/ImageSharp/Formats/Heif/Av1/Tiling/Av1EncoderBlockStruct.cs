// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores encoder block geometry and its selected coding-mode information.
/// </summary>
internal class Av1EncoderBlockStruct
{
    /// <summary>
    /// Gets the transform-unit state in transform traversal order.
    /// </summary>
    public Av1TransformUnit[] TransformBlocks { get; } = new Av1TransformUnit[Av1Constants.MaxTransformUnitCount];

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
    /// Gets or sets the palette size for luma and for the shared chroma mode.
    /// </summary>
    public required int[] PaletteSize { get; internal set; }

    /// <summary>
    /// Gets or sets the encoder prediction-unit state for the block.
    /// </summary>
    public required Av1EncoderPredictionUnit[] PredictionUnits { get; internal set; }
}

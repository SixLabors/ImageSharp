// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the packed coding decisions that the encoder selects for one final block.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = StorageSize)]
internal struct Av1EncoderBlockStruct
{
    /// <summary>
    /// The fixed byte width of one packed final-block decision.
    /// </summary>
    public const int StorageSize = 8;

    /// <summary>
    /// Stores the block's prediction-unit syntax inline.
    /// </summary>
    private Av1EncoderPredictionUnit predictionUnit;

    // AV1 limits each of these syntax values to one byte. Byte fields stop CLR field alignment from growing every entry of the
    // 1,024-element decision workspace.
    private byte hasChroma;
    private byte quantizationIndex;
    private byte segmentId;
    private byte filterIntraMode;

    /// <summary>
    /// Gets or sets a value indicating whether this luma block owns the corresponding chroma syntax.
    /// </summary>
    public bool HasChroma
    {
        readonly get => this.hasChroma != 0;
        set => this.hasChroma = value ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Gets or sets the quantizer index used for the block.
    /// </summary>
    public int QuantizationIndex
    {
        readonly get => this.quantizationIndex;
        set => this.quantizationIndex = (byte)value;
    }

    /// <summary>
    /// Gets or sets the segmentation identifier assigned to the block.
    /// </summary>
    public int SegmentId
    {
        readonly get => this.segmentId;
        set => this.segmentId = (byte)value;
    }

    /// <summary>
    /// Gets or sets the filter-intra mode selected for the block.
    /// </summary>
    public Av1FilterIntraMode FilterIntraMode
    {
        readonly get => (Av1FilterIntraMode)this.filterIntraMode;
        set => this.filterIntraMode = (byte)value;
    }

    /// <summary>
    /// Gets or sets the dynamic-reference-list index selected for an inter block.
    /// </summary>
    /// <remarks>
    /// A block uses filter-intra prediction or inter prediction, never both. As a result, both values share one packed byte.
    /// </remarks>
    public int ReferenceMotionVectorIndex
    {
        readonly get => this.filterIntraMode;
        set => this.filterIntraMode = (byte)value;
    }

    /// <summary>
    /// Gets the encoder prediction-unit state for the block.
    /// </summary>
    [UnscopedRef]
    public ref Av1EncoderPredictionUnit PredictionUnit => ref this.predictionUnit;
}

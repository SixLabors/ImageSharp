// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores encoder block geometry and its selected coding-mode information.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = StorageSize)]
internal struct Av1EncoderBlockStruct
{
    /// <summary>
    /// The fixed byte width of one packed final-block decision.
    /// </summary>
    public const int StorageSize = 10;

    /// <summary>
    /// Stores the luma and shared chroma palette sizes inline with the block.
    /// </summary>
    private InlineArray2<byte> paletteSize;

    /// <summary>
    /// Stores the block's prediction-unit syntax inline.
    /// </summary>
    private Av1EncoderPredictionUnit predictionUnit;

    // These syntax values have AV1-defined byte-sized ranges. Storing their encoded widths explicitly
    // prevents CLR field alignment from inflating every entry in the 1,024-element decision workspace.
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

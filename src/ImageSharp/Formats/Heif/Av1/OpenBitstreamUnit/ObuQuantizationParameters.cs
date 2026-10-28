// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the AV1 quantization parameters for a frame.
/// </summary>
internal sealed class ObuQuantizationParameters
{
    /// <summary>
    /// Stores the effective quantizer index for each of the eight segments without a per-header array allocation.
    /// </summary>
    private InlineArray8<int> qIndex;

    /// <summary>
    /// Stores the three plane DC quantizer-index deltas without a per-frame array allocation.
    /// </summary>
    private InlineArray4<int> deltaQDc;

    /// <summary>
    /// Stores the three plane AC quantizer-index deltas without a per-frame array allocation.
    /// </summary>
    private InlineArray4<int> deltaQAc;

    /// <summary>
    /// Stores the three plane quantization-matrix levels without a per-frame array allocation.
    /// </summary>
    private InlineArray4<int> qMatrix;

    /// <summary>
    /// Gets or sets the base quantizer index.
    /// </summary>
    public int BaseQIndex { get; set; }

    /// <summary>
    /// Gets the mutable effective quantizer indices for each segment.
    /// </summary>
    public Span<int> QIndex => this.qIndex;

    /// <summary>
    /// Gets or sets a value indicating whether quantization matrices are enabled.
    /// </summary>
    public bool IsUsingQMatrix { get; set; }

    /// <summary>
    /// Gets the DC quantizer-index deltas for the Y, U, and V planes.
    /// </summary>
    public Span<int> DeltaQDc => this.deltaQDc[..3];

    /// <summary>
    /// Gets the AC quantizer-index deltas for the Y, U, and V planes.
    /// </summary>
    public Span<int> DeltaQAc => this.deltaQAc[..3];

    /// <summary>
    /// Gets the quantization-matrix level for the Y, U, and V planes.
    /// </summary>
    public Span<int> QMatrix => this.qMatrix[..3];

    /// <summary>
    /// Gets or sets a value indicating whether the U and V planes use separate quantizer deltas.
    /// </summary>
    public bool HasSeparateUvDelta { get; set; }
}

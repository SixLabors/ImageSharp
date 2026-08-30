// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the AV1 quantization parameters for a frame.
/// </summary>
internal class ObuQuantizationParameters
{
    /// <summary>
    /// Gets or sets the base quantizer index.
    /// </summary>
    public int BaseQIndex { get; set; }

    /// <summary>
    /// Gets or sets the effective quantizer index for each segment.
    /// </summary>
    public int[] QIndex { get; set; } = new int[Av1Constants.MaxSegmentCount];

    /// <summary>
    /// Gets or sets a value indicating whether quantization matrices are enabled.
    /// </summary>
    public bool IsUsingQMatrix { get; set; }

    /// <summary>
    /// Gets or sets the DC quantizer-index deltas for the Y, U, and V planes.
    /// </summary>
    public int[] DeltaQDc { get; set; } = new int[3];

    /// <summary>
    /// Gets or sets the AC quantizer-index deltas for the Y, U, and V planes.
    /// </summary>
    public int[] DeltaQAc { get; set; } = new int[3];

    /// <summary>
    /// Gets or sets the quantization-matrix level for the Y, U, and V planes.
    /// </summary>
    public int[] QMatrix { get; set; } = new int[3];

    /// <summary>
    /// Gets or sets a value indicating whether the U and V planes use separate quantizer deltas.
    /// </summary>
    public bool HasSeparateUvDelta { get; set; }
}

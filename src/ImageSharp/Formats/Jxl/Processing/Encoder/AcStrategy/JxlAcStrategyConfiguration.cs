// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AcStrategy;

/// <summary>
/// Retains the frame-owned source and mask memory used by sequential transform-strategy estimates.
/// </summary>
/// <param name="dequant">The frame-owned prepared matrices. This view does not dispose them.</param>
internal sealed class JxlAcStrategyConfiguration(JxlDequantMatrices dequant)
{
    /// <summary>
    /// Borrows the first source row and subsequent storage for each XYB channel. The frame must retain these allocations during strategy selection.
    /// </summary>
    [SuppressMessage(
        "StyleCop.CSharp.MaintainabilityRules",
        "SA1401:Fields should be private",
        Justification = "when an InlineArray is a property its values cannot be mutated")]
    public InlineArray3<Memory<float>> SourceRows;

    /// <summary>
    /// Gets or sets the borrowed prepared quantization matrices.
    /// </summary>
    public JxlDequantMatrices Dequant { get; set; } = dequant;

    /// <summary>
    /// Gets or sets the quantization field from its first block row.
    /// </summary>
    public Memory<float> QuantFieldRow { get; set; }

    /// <summary>
    /// Gets or sets the quantization-field stride, in eight-pixel blocks.
    /// </summary>
    public int QuantFieldStride { get; set; }

    /// <summary>
    /// Gets or sets the borrowed block-resolution masking field.
    /// </summary>
    public Memory<float> MaskingFieldRow { get; set; }

    /// <summary>
    /// Gets or sets the block-resolution mask stride, in floats.
    /// </summary>
    public int MaskingFieldStride { get; set; }

    /// <summary>
    /// Gets or sets the borrowed pixel-resolution mask from its logical origin.
    /// </summary>
    public Memory<float> Masking1x1FieldRow { get; set; }

    /// <summary>
    /// Gets or sets the pixel-mask row stride, in floats.
    /// </summary>
    public int Masking1x1FieldStride { get; set; }

    /// <summary>
    /// Gets or sets the logical pixel-mask width, excluding physical row padding.
    /// </summary>
    public int Mask1x1Width { get; set; }

    /// <summary>
    /// Gets or sets the physical source row stride, in floats.
    /// </summary>
    public int SourceStride { get; set; }

    /// <summary>
    /// Gets or sets the weight applied to masked reconstruction loss.
    /// </summary>
    public float InfoLossMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the weight applied to square roots of absolute rounded coefficients.
    /// </summary>
    public float CostDelta { get; set; }

    /// <summary>
    /// Gets or sets the weight applied to the estimated nonzero-count coding cost.
    /// </summary>
    public float ZerosMultiplier { get; set; }
}

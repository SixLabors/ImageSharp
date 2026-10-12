// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AcStrategy;

internal sealed class JxlAcStrategyConfiguration(JxlDequantMatrices dequant)
{
    [SuppressMessage(
        "StyleCop.CSharp.MaintainabilityRules",
        "SA1401:Fields should be private",
        Justification = "when an InlineArray is a property its values cannot be mutated")]
    public InlineArray3<Memory<float>> SourceRows;

    public JxlDequantMatrices Dequant { get; set; } = dequant;

    public Memory<float> QuantFieldRow { get; set; }

    public int QuantFieldStride { get; set; }

    public Memory<float> MaskingFieldRow { get; set; }

    public int MaskingFieldStride { get; set; }

    public Memory<float> Masking1x1FieldRow { get; set; }

    public int Masking1x1FieldStride { get; set; }

    public int Mask1x1Width { get; set; }

    public int SourceStride { get; set; }

    public float InfoLossMultiplier { get; set; }

    public float CostDelta { get; set; }

    public float ZerosMultiplier { get; set; }

    public ref float Pixel(int c, int x, int y)
        => ref this.SourceRows[c].Span[(y * this.SourceStride) + x];
}

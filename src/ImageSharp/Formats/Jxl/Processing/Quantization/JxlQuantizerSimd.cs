// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

/// <summary>
/// SIMD utilities used by the quantizer.
/// </summary>
internal static class JxlQuantizerSimd
{
    public static Vector<float> AdjustQuantBias(int c, Vector<int> quantI, Span<float> biases)
    {
        Vector<float> quant = quantI.As<int, float>();
        Vector<float> constSign = Vector.Create(int.MinValue).As<int, float>();
        Vector<float> sign = quant & constSign;
        Vector<float> absoluteQuant = Vector.AndNot(constSign, quant);

        Vector<int> is01 = Vector.LessThan(absoluteQuant, Vector.Create(1.125f));
        Vector<int> not0 = Vector.GreaterThan(absoluteQuant, Vector<float>.One);

        Vector<float> oneBias = Vector.ConditionalSelect(not0, Vector.Create(biases[c]) ^ sign, Vector<float>.Zero);
        Vector<float> bias = -(Vector.Create(biases[3]) * quant.ReciprocalEstimate()) + quant;

        return Vector.ConditionalSelect(is01, oneBias, bias);
    }
}

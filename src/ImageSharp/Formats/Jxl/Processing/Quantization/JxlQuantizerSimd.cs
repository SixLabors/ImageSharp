// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

/// <summary>
/// SIMD utilities used by the quantizer.
/// </summary>
internal static class JxlQuantizerSimd
{
    public static Vector<float> AdjustQuantBias(int c, Vector<int> quantI, ReadOnlySpan<float> biases)
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

    // a variant with 64 element spans
    public static void AdjustQuantBias64(int c, Span<int> quantI, ReadOnlySpan<float> biases, Span<float> output)
    {
        if (Vector<float>.Count > 64 || Vector<int>.Count > 64)
        {
            // TODO: in the future we may want to support > 64 items if users start encountering the problem
            // Problem is, this function works on strictly 64 items, and if the Vector has > 64 items,
            // then that's too much to process.
            throw new NotImplementedException("This CPU has more than 64 lanes for float or int types");
        }

        ref int quantIRef = ref MemoryMarshal.GetReference(quantI);
        ref float outputRef = ref MemoryMarshal.GetReference(output);

        // This loop should be SIMD-aligned
        for (int i = 0; i < 64; i += Vector<float>.Count)
        {
            Vector<float> result = AdjustQuantBias(
                c: c,
                quantI: Vector.LoadUnsafe(ref Unsafe.Add(ref quantIRef, i)),
                biases);

            result.StoreUnsafe(ref Unsafe.Add(ref outputRef, i));
        }
    }
}

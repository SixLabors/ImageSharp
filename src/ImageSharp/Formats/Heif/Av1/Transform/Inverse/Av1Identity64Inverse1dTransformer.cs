// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

internal class Av1Identity64Inverse1dTransformer : IAv1Transformer1d
{
    private const long Sqrt2Times4 = Av1Identity4Inverse1dTransformer.Sqrt2 >> 2;

    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 64, nameof(input));
        Guard.MustBeSizedAtLeast(output, 64, nameof(output));
        ref int inputRef = ref input[0];
        ref int outputRef = ref output[0];
        TransformScalar(ref inputRef, ref outputRef);
        TransformScalar(ref Unsafe.Add(ref inputRef, 8), ref Unsafe.Add(ref outputRef, 8));
        TransformScalar(ref Unsafe.Add(ref inputRef, 16), ref Unsafe.Add(ref outputRef, 16));
        TransformScalar(ref Unsafe.Add(ref inputRef, 24), ref Unsafe.Add(ref outputRef, 24));
        TransformScalar(ref Unsafe.Add(ref inputRef, 32), ref Unsafe.Add(ref outputRef, 32));
        TransformScalar(ref Unsafe.Add(ref inputRef, 40), ref Unsafe.Add(ref outputRef, 40));
        TransformScalar(ref Unsafe.Add(ref inputRef, 48), ref Unsafe.Add(ref outputRef, 48));
        TransformScalar(ref Unsafe.Add(ref inputRef, 56), ref Unsafe.Add(ref outputRef, 56));
    }

    /// <summary>
    /// SVT: svt_av1_iidentity64_c
    /// </summary>
    private static void TransformScalar(ref int input, ref int output)
    {
        // Normal input should fit into 32-bit. Cast to 64-bit here to avoid
        // overflow with corrupted/fuzzed input. The same for av1_iidentity/16/64_c.
        output = Av1Math.RoundShift(Sqrt2Times4 * input, Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 1) = Av1Math.RoundShift(Sqrt2Times4 * Unsafe.Add(ref input, 1), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 2) = Av1Math.RoundShift(Sqrt2Times4 * Unsafe.Add(ref input, 2), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 3) = Av1Math.RoundShift(Sqrt2Times4 * Unsafe.Add(ref input, 3), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 4) = Av1Math.RoundShift(Sqrt2Times4 * Unsafe.Add(ref input, 4), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 5) = Av1Math.RoundShift(Sqrt2Times4 * Unsafe.Add(ref input, 5), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 6) = Av1Math.RoundShift(Sqrt2Times4 * Unsafe.Add(ref input, 6), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 7) = Av1Math.RoundShift(Sqrt2Times4 * Unsafe.Add(ref input, 7), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
    }
}

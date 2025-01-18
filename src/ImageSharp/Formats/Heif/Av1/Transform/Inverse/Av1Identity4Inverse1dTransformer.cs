// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

internal class Av1Identity4Inverse1dTransformer : IAv1Transformer1d
{
    internal const int Sqrt2Bits = 12;

    // 2^12 * sqrt(2)
    internal const long Sqrt2 = 5793;

    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 4, nameof(input));
        Guard.MustBeSizedAtLeast(output, 4, nameof(output));
        TransformScalar(ref input[0], ref output[0]);
    }

    /// <summary>
    /// SVT: svt_av1_iidentity4_c
    /// </summary>
    private static void TransformScalar(ref int input, ref int output)
    {
        // Normal input should fit into 32-bit. Cast to 64-bit here to avoid
        // overflow with corrupted/fuzzed input. The same for av1_iidentity/16/64_c.
        output = Av1Math.RoundShift(Sqrt2 * input, Sqrt2Bits);
        Unsafe.Add(ref output, 1) = Av1Math.RoundShift(Sqrt2 * Unsafe.Add(ref input, 1), Sqrt2Bits);
        Unsafe.Add(ref output, 2) = Av1Math.RoundShift(Sqrt2 * Unsafe.Add(ref input, 2), Sqrt2Bits);
        Unsafe.Add(ref output, 3) = Av1Math.RoundShift(Sqrt2 * Unsafe.Add(ref input, 3), Sqrt2Bits);
    }
}

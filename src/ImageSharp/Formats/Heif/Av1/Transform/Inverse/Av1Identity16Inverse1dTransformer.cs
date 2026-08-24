// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <summary>
/// Applies the 16-point AV1 inverse identity transform to a one-dimensional coefficient vector.
/// </summary>
internal class Av1Identity16Inverse1dTransformer : IAv1Transformer1d
{
    private const long Sqrt2Times2 = Av1Identity4Inverse1dTransformer.Sqrt2 >> 1;

    /// <inheritdoc/>
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 16, nameof(input));
        Guard.MustBeSizedAtLeast(output, 16, nameof(output));
        TransformScalar(ref input[0], ref output[0]);
    }

    /// <summary>
    /// Scales 16 coefficients according to the AV1 inverse identity-transform definition.
    /// </summary>
    /// <param name="input">A reference to the first input coefficient.</param>
    /// <param name="output">A reference to the first output value.</param>
    /// <remarks>Corresponds to <c>svt_av1_iidentity16_c</c> in the original WIP reference.</remarks>
    private static void TransformScalar(ref int input, ref int output)
    {
        // Normal input should fit into 32-bit. Cast to 64-bit here to avoid
        // overflow with corrupted/fuzzed input. The same for av1_iidentity/16/64_c.
        output = Av1Math.RoundShift(Sqrt2Times2 * input, Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 1) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 1), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 2) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 2), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 3) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 3), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 4) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 4), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 5) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 5), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 6) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 6), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 7) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 7), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 8) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 8), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 9) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 9), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 10) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 10), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 11) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 11), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 12) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 12), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 13) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 13), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 14) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 14), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
        Unsafe.Add(ref output, 15) = Av1Math.RoundShift(Sqrt2Times2 * Unsafe.Add(ref input, 15), Av1Identity4Inverse1dTransformer.Sqrt2Bits);
    }
}

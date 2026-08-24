// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Applies the 16-point AV1 forward identity transform to a one-dimensional residual vector.
/// </summary>
internal class Av1Identity16Forward1dTransformer : IAv1Transformer1d
{
    private const int TwiceNewSqrt2 = 2 * 5793;

    /// <inheritdoc/>
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 16, nameof(input));
        Guard.MustBeSizedAtLeast(output, 16, nameof(output));
        TransformScalar(ref input[0], ref output[0]);
    }

    /// <summary>
    /// Scales 16 residual values according to the AV1 forward identity-transform definition.
    /// </summary>
    /// <param name="input">A reference to the first input value.</param>
    /// <param name="output">A reference to the first output coefficient.</param>
    private static void TransformScalar(ref int input, ref int output)
    {
        output = Av1Math.RoundShift((long)input * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 1) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 1) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 2) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 2) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 3) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 3) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 4) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 4) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 5) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 5) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 6) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 6) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 7) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 7) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 8) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 8) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 9) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 9) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 10) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 10) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 11) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 11) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 12) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 12) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 13) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 13) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 14) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 14) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 15) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 15) * TwiceNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
    }
}

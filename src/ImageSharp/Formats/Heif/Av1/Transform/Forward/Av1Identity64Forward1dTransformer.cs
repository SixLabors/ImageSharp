// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Identity64Forward1dTransformer : IAv1Transformer1d
{
    private const int QuadNewSqrt2 = 4 * 5793;

    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 64, nameof(input));
        Guard.MustBeSizedAtLeast(output, 64, nameof(output));
        ref int inputRef = ref input[0];
        ref int outputRef = ref output[0];
        TransformScalar(ref inputRef, ref outputRef);
        TransformScalar(ref Unsafe.Add(ref inputRef, 16), ref Unsafe.Add(ref outputRef, 16));
        TransformScalar(ref Unsafe.Add(ref inputRef, 32), ref Unsafe.Add(ref outputRef, 32));
        TransformScalar(ref Unsafe.Add(ref inputRef, 48), ref Unsafe.Add(ref outputRef, 48));
    }

    private static void TransformScalar(ref int input, ref int output)
    {
        output = Av1Math.RoundShift((long)input * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 1) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 1) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 2) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 2) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 3) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 3) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 4) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 4) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 5) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 5) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 6) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 6) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 7) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 7) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 8) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 8) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 9) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 9) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 10) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 10) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 11) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 11) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 12) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 12) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 13) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 13) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 14) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 14) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 15) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 15) * QuadNewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
    }
}

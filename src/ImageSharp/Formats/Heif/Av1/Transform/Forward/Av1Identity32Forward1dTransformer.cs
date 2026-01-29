// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Identity32Forward1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 32, nameof(input));
        Guard.MustBeSizedAtLeast(output, 32, nameof(output));
        ref int inputRef = ref input[0];
        ref int outputRef = ref output[0];
        TransformScalar(ref inputRef, ref outputRef);
        TransformScalar(ref Unsafe.Add(ref inputRef, 8), ref Unsafe.Add(ref outputRef, 8));
        TransformScalar(ref Unsafe.Add(ref inputRef, 16), ref Unsafe.Add(ref outputRef, 16));
        TransformScalar(ref Unsafe.Add(ref inputRef, 24), ref Unsafe.Add(ref outputRef, 24));
    }

    private static void TransformScalar(ref int input, ref int output)
    {
        output = input << 2;
        Unsafe.Add(ref output, 1) = Unsafe.Add(ref input, 1) << 2;
        Unsafe.Add(ref output, 2) = Unsafe.Add(ref input, 2) << 2;
        Unsafe.Add(ref output, 3) = Unsafe.Add(ref input, 3) << 2;
        Unsafe.Add(ref output, 4) = Unsafe.Add(ref input, 4) << 2;
        Unsafe.Add(ref output, 5) = Unsafe.Add(ref input, 5) << 2;
        Unsafe.Add(ref output, 6) = Unsafe.Add(ref input, 6) << 2;
        Unsafe.Add(ref output, 7) = Unsafe.Add(ref input, 7) << 2;
    }
}

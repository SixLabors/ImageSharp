// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlConvolveSeparable5
{
    public static Vector<float> HorizontalConvolve(ref float pos, Vector<float> wh0, Vector<float> wh1, Vector<float> wh2)
    {
        Vector<float> c = Vector.LoadUnsafe(ref pos);
        Vector<float> mul0 = c * wh0;

        Vector<float> l1 = Vector.LoadUnsafe(ref Unsafe.Subtract(ref pos, 1));
        Vector<float> r1 = Vector.LoadUnsafe(ref Unsafe.Add(ref pos, 1));
        Vector<float> l2 = Vector.LoadUnsafe(ref Unsafe.Subtract(ref pos, 2));
        Vector<float> r2 = Vector.LoadUnsafe(ref Unsafe.Add(ref pos, 2));

        Vector<float> sum1 = l1 + r1;
        Vector<float> mul1 = Vector.FusedMultiplyAdd(sum1, wh1, mul0);
        Vector<float> sum2 = l2 + r2;
        Vector<float> mul2 = Vector.FusedMultiplyAdd(sum2, wh2, mul1);

        return mul2;
    }
}

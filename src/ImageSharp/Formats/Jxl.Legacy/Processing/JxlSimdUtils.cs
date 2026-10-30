// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Shared SIMD-accelerated utilities.
/// </summary>
internal static partial class JxlSimdUtils
{
    // Raises 2 to the power of x
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> FastPow2f(Vector<float> x)
    {
        Vector<float> floorX = Vector.Floor(x);
        Vector<int> exponent = Vector.ConvertToInt32(floorX) + Vector.Create(127);

        Vector<float> exp = exponent.As<int, float>() << 23;
        Vector<float> frac = x - floorX;
        Vector<float> num = frac + Vector.Create(1.01749063e+01f);

        num = Vector.FusedMultiplyAdd(
            num,
            frac,
            Vector.Create(4.88687798e+01f));

        num = Vector.FusedMultiplyAdd(
            num,
            frac,
            Vector.Create(9.85506591e+01f));

        num *= exp;

        Vector<float> den =
            Vector.FusedMultiplyAdd(
                frac,
                Vector.Create(2.10242958e-01f),
                Vector.Create(-2.22328856e-02f));

        den = Vector.FusedMultiplyAdd(
            den,
            frac,
            Vector.Create(-1.94414990e+01f));

        den = Vector.FusedMultiplyAdd(
            den,
            frac,
            Vector.Create(9.85506633e+01f));

        return num / den;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> FastPowf(
        Vector<float> @base,
        Vector<float> exponent)
        => FastPow2f(Vector.Log2(@base) * exponent);

}

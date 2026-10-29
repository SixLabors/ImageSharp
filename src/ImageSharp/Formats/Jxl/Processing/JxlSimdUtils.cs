// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Shared SIMD-accelerated utilities.
/// </summary>
internal static partial class JxlSimdUtils
{
    /// <summary>
    /// Fills consecutive integers, starting with the supplied value.
    /// </summary>
    /// <typeparam name="T">The integer component type.</typeparam>
    /// <param name="span">The destination. An empty span is unchanged.</param>
    /// <param name="start">The first value. Arithmetic wraps within the component type.</param>
    public static void Iota<T>(Span<T> span, T start)
        where T : unmanaged, IBinaryInteger<T>
    {
        ref T destination = ref MemoryMarshal.GetReference(span);
        int i = 0;
        T value = start;

        // Runtime indices supply 0..N-1. Every subsequent block adds N to every lane, not a different increment to each lane.
        // Integer vector addition and the scalar tail both wrap within T. A complete store never touches the tail or an empty span.
        if (Vector512.IsHardwareAccelerated && Vector512<T>.IsSupported && span.Length >= Vector512<T>.Count)
        {
            int count = Vector512<T>.Count;
            nuint vectorCount = span.Length.Vector512Count<T>();
            Vector512<T> current = Vector512.Create(value) + Vector512<T>.Indices;
            Vector512<T> increment = Vector512.Create(T.CreateTruncating(count));

            for (nuint block = 0; block < vectorCount; block++, i += count)
            {
                current.StoreUnsafe(ref destination, (uint)i);
                current += increment;
            }

            value = current.ToScalar();
        }
        else if (Vector256.IsHardwareAccelerated && Vector256<T>.IsSupported && span.Length >= Vector256<T>.Count)
        {
            int count = Vector256<T>.Count;
            nuint vectorCount = span.Length.Vector256Count<T>();
            Vector256<T> current = Vector256.Create(value) + Vector256<T>.Indices;
            Vector256<T> increment = Vector256.Create(T.CreateTruncating(count));

            for (nuint block = 0; block < vectorCount; block++, i += count)
            {
                current.StoreUnsafe(ref destination, (uint)i);
                current += increment;
            }

            value = current.ToScalar();
        }
        else if (Vector128.IsHardwareAccelerated && Vector128<T>.IsSupported && span.Length >= Vector128<T>.Count)
        {
            int count = Vector128<T>.Count;
            nuint vectorCount = span.Length.Vector128Count<T>();
            Vector128<T> current = Vector128.Create(value) + Vector128<T>.Indices;
            Vector128<T> increment = Vector128.Create(T.CreateTruncating(count));

            for (nuint block = 0; block < vectorCount; block++, i += count)
            {
                current.StoreUnsafe(ref destination, (uint)i);
                current += increment;
            }

            value = current.ToScalar();
        }

        for (; i < span.Length; i++)
        {
            Unsafe.Add(ref destination, i) = value;
            value = unchecked(value + T.One);
        }
    }

    /// <summary>
    /// Creates consecutive unsigned integers, starting with the supplied value.
    /// </summary>
    /// <param name="start">The first value. Arithmetic wraps within UInt32.</param>
    /// <returns>One register containing start, start + 1, and the following integers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<uint> Iota(uint start) => Vector<uint>.Indices + Vector.Create(start);

    /// <summary>
    /// Vectorized floating-point error function (precise approximate).
    /// </summary>
    /// <param name="x">Vector to compute error of.</param>
    /// <returns>Vector whose each item is an error (similar to std::erf).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> FastErff(Vector<float> x)
    {
        Vector<float> zero = Vector<float>.Zero;
        Vector<float> one = Vector<float>.One;

        Vector<int> xle0 = Vector.LessThanOrEqual(x, zero);
        Vector<float> absx = Vector.Abs(x);

        Vector<float> denom1 = (absx * new Vector<float>(0.0777394369f)) + new Vector<float>(0.000205260015f);
        Vector<float> denom2 = (denom1 * absx) + new Vector<float>(0.232120216f);
        Vector<float> denom3 = (denom2 * absx) + new Vector<float>(0.277820801f);
        Vector<float> denom4 = (denom3 * absx) + one;
        Vector<float> denom5 = denom4 * denom4;
        Vector<float> invDenom5 = one / denom5;
        Vector<float> result = one - Vector.Multiply(invDenom5, invDenom5);

        // Change sign if x <= 0.
        return Vector.ConditionalSelect(xle0, -result, result);
    }

    /// <summary>
    /// Scalar floating-point error function (precise approximate).
    /// </summary>
    /// <param name="x">Value to compute error of.</param>
    /// <returns>A scalar error value (similar to std::erf).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float FastErff(float x)
    {
        float zero = 0.0f;
        float one = 1.0f;

        bool xle0 = x <= zero;
        float absx = MathF.Abs(x);

        float denom1 = (absx * 0.0777394369f) + 0.000205260015f;
        float denom2 = (denom1 * absx) + 0.232120216f;
        float denom3 = (denom2 * absx) + 0.277820801f;
        float denom4 = (denom3 * absx) + one;
        float denom5 = denom4 * denom4;
        float invDenom5 = one / denom5;
        float result = one - (invDenom5 * invDenom5);

        // Change sign if x <= 0.
        return xle0 ? -result : result;
    }

    // Raises 2 to the power of x
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> FastPow2f(Vector128<float> x)
    {
        Vector128<float> floorX = Vector128.Floor(x);
        Vector128<int> exponent = Vector128.ConvertToInt32(floorX) + Vector128.Create(127);

        Vector128<float> exp = exponent.AsSingle() << 23;
        Vector128<float> frac = x - floorX;
        Vector128<float> num = frac + Vector128.Create(1.01749063e+01f);

        num = Vector128.FusedMultiplyAdd(
            num,
            frac,
            Vector128.Create(4.88687798e+01f));

        num = Vector128.FusedMultiplyAdd(
            num,
            frac,
            Vector128.Create(9.85506591e+01f));

        num *= exp;

        Vector128<float> den =
            Vector128.FusedMultiplyAdd(
                frac,
                Vector128.Create(2.10242958e-01f),
                Vector128.Create(-2.22328856e-02f));

        den = Vector128.FusedMultiplyAdd(
            den,
            frac,
            Vector128.Create(-1.94414990e+01f));

        den = Vector128.FusedMultiplyAdd(
            den,
            frac,
            Vector128.Create(9.85506633e+01f));

        return num / den;
    }

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
    public static Vector128<float> FastPowf(
        Vector128<float> @base,
        Vector128<float> exponent)
        => FastPow2f(Vector128.Log2(@base) * exponent);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> FastPowf(
        Vector<float> @base,
        Vector<float> exponent)
        => FastPow2f(Vector.Log2(@base) * exponent);

    /// <summary>
    /// A SIMD utility method which takes in the 128 bit vector
    /// and duplicates its values to fit in the CPU vector size.
    /// For example,
    ///
    ///     128 bit vectors: A B C D    (as-is)
    ///     256 bit vectors: A B C D A B C D (duplicate once)
    ///     512 bit vectors: A B C D A B C D A B C D A B C D (duplicate three times)
    ///
    /// Vector&lt;T&gt; has support for arbitrarily large
    /// vector sizes. For example, some ARM CPUs support 2048-bit
    /// vectors through Vector&lt;T&gt;. In that specific case, this
    /// method can be used for future-proofing.
    /// </summary>
    /// <param name="vec">Vector to duplicate.</param>
    /// <returns>New vector that is duplicated across the width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<T> LoadDuplicate128<T>(Vector128<T> vec)
        where T : unmanaged
    {
        if (Vector<T>.Count == Vector64<T>.Count)
        {
            throw new NotSupportedException();
        }
        else if (Vector<T>.Count == Vector512<T>.Count)
        {
            return Vector512.Create(vec).AsVector();
        }
        else if (Vector<T>.Count == Vector256<T>.Count)
        {
            return Vector256.Create(vec).AsVector();
        }
        else if (Vector<T>.Count == Vector128<T>.Count)
        {
            return vec.AsVector();
        }
        else
        {
            Span<T> value = stackalloc T[Vector<T>.Count];

            for (int i = 0; i <= Vector<T>.Count - Vector128<T>.Count; i += Vector128<T>.Count)
            {
                vec.CopyTo(value[i..]);
            }

            return new Vector<T>(value);
        }
    }
}

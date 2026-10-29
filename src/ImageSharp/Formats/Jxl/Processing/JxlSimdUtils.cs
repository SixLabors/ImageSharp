// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Shared SIMD-accelerated utilities.
/// </summary>
internal static partial class JxlSimdUtils
{
    public static int MaxVectorSize => Vector<float>.Count * sizeof(float);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<T> ConcatLowerLower<T>(Vector256<T> a, Vector256<T> b)
        where T : unmanaged => Vector256.Create(a.GetLower(), b.GetLower());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<T> ConcatUpperUpper<T>(Vector256<T> a, Vector256<T> b)
        where T : unmanaged => Vector256.Create(a.GetUpper(), b.GetUpper());

    public static void Transpose8x8Block(Span<int> fromSpan, Span<int> toSpan, int stride)
    {
        ref int from = ref MemoryMarshal.GetReference(fromSpan);
        ref int to = ref MemoryMarshal.GetReference(toSpan);

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> i0 = Vector256.LoadUnsafe(ref from);
            Vector256<int> i1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref from, stride));
            Vector256<int> i2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref from, 2 * stride));
            Vector256<int> i3 = Vector256.LoadUnsafe(ref Unsafe.Add(ref from, 3 * stride));
            Vector256<int> i4 = Vector256.LoadUnsafe(ref Unsafe.Add(ref from, 4 * stride));
            Vector256<int> i5 = Vector256.LoadUnsafe(ref Unsafe.Add(ref from, 5 * stride));
            Vector256<int> i6 = Vector256.LoadUnsafe(ref Unsafe.Add(ref from, 6 * stride));
            Vector256<int> i7 = Vector256.LoadUnsafe(ref Unsafe.Add(ref from, 7 * stride));

            Vector256<int> q0 = Vector256_.InterleaveLower(i0, i2);
            Vector256<int> q1 = Vector256_.InterleaveLower(i1, i3);
            Vector256<int> q2 = Vector256_.InterleaveUpper(i0, i2);
            Vector256<int> q3 = Vector256_.InterleaveUpper(i1, i3);
            Vector256<int> q4 = Vector256_.InterleaveLower(i4, i6);
            Vector256<int> q5 = Vector256_.InterleaveLower(i5, i7);
            Vector256<int> q6 = Vector256_.InterleaveUpper(i4, i6);
            Vector256<int> q7 = Vector256_.InterleaveUpper(i5, i7);

            Vector256<int> r0 = Vector256_.InterleaveLower(q0, q1);
            Vector256<int> r1 = Vector256_.InterleaveUpper(q0, q1);
            Vector256<int> r2 = Vector256_.InterleaveLower(q2, q3);
            Vector256<int> r3 = Vector256_.InterleaveUpper(q2, q3);
            Vector256<int> r4 = Vector256_.InterleaveLower(q4, q5);
            Vector256<int> r5 = Vector256_.InterleaveUpper(q4, q5);
            Vector256<int> r6 = Vector256_.InterleaveLower(q6, q7);
            Vector256<int> r7 = Vector256_.InterleaveUpper(q6, q7);

            i0 = ConcatLowerLower(r4, r0);
            i1 = ConcatLowerLower(r5, r1);
            i2 = ConcatLowerLower(r6, r2);
            i3 = ConcatLowerLower(r7, r3);
            i4 = ConcatUpperUpper(r4, r0);
            i5 = ConcatUpperUpper(r5, r1);
            i6 = ConcatUpperUpper(r6, r2);
            i7 = ConcatUpperUpper(r7, r3);

            i0.StoreUnsafe(ref to);
            i1.StoreUnsafe(ref Unsafe.Add(ref to, 8));
            i2.StoreUnsafe(ref Unsafe.Add(ref to, 16));
            i3.StoreUnsafe(ref Unsafe.Add(ref to, 24));
            i4.StoreUnsafe(ref Unsafe.Add(ref to, 32));
            i5.StoreUnsafe(ref Unsafe.Add(ref to, 40));
            i6.StoreUnsafe(ref Unsafe.Add(ref to, 48));
            i7.StoreUnsafe(ref Unsafe.Add(ref to, 56));
        }
        else
        {
            // Vector128 fallback
            for (int n = 0; n < 8; n += 4)
            {
                for (int m = 0; m < 8; m += 4)
                {
                    Vector128<int> p0 = Vector128.LoadUnsafe(ref Unsafe.Add(ref from, (n * stride) + m));
                    Vector128<int> p1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref from, ((n + 1) * stride) + m));
                    Vector128<int> p2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref from, ((n + 2) * stride) + m));
                    Vector128<int> p3 = Vector128.LoadUnsafe(ref Unsafe.Add(ref from, ((n + 3) * stride) + m));

                    Vector128<int> q0 = Vector128_.InterleaveLower(p0, p2);
                    Vector128<int> q1 = Vector128_.InterleaveLower(p1, p3);
                    Vector128<int> q2 = Vector128_.InterleaveUpper(p0, p2);
                    Vector128<int> q3 = Vector128_.InterleaveUpper(p1, p3);

                    Vector128<int> r0 = Vector128_.InterleaveLower(q0, q1);
                    Vector128<int> r1 = Vector128_.InterleaveUpper(q0, q1);
                    Vector128<int> r2 = Vector128_.InterleaveLower(q2, q3);
                    Vector128<int> r3 = Vector128_.InterleaveUpper(q2, q3);

                    r0.StoreUnsafe(ref Unsafe.Add(ref to, (m * 8) + n));
                    r1.StoreUnsafe(ref Unsafe.Add(ref to, ((m + 1) * 8) + n));
                    r2.StoreUnsafe(ref Unsafe.Add(ref to, ((m + 2) * 8) + n));
                    r3.StoreUnsafe(ref Unsafe.Add(ref to, ((m + 3) * 8) + n));
                }
            }
        }
    }

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

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> Pow(Vector<float> @base, Vector<float> exponent)
    {
        Vector<float> vec = Vector.Log2(@base) * exponent;
        return vec * vec;
    }

    /// <summary>
    ///   <para>
    ///     Fills the span so its first value is equal to <paramref name="start"/>
    ///     and subsequent values increment by one. For example, with start=5,
    ///     the span's values will be:
    ///     <code>
    ///       { start, start+1, start+2, start+3, start+4, ... to the end of the span }
    ///     </code>
    ///   </para>
    ///   <para>
    ///     or, more precisely:
    ///     <code>
    ///       { 5, 6, 7, 8, 9, 10, 11, ... to the end of the span }
    ///     </code>
    ///   </para>
    ///   <seealso href="https://en.cppreference.com/cpp/algorithm/iota" />
    /// </summary>
    /// <param name="span">The span where the values are filled.</param>
    /// <param name="start">Initial value.</param>
    public static void Iota(Span<int> span, int start)
    {
        ref int spanRef = ref MemoryMarshal.GetReference(span);

        // Using fixed-size vectors so we can construct an
        // incrementMask more easily.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<int> incrementMask = Vector512.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
            Vector512<int> v = Vector512.Create(start) + (incrementMask - Vector512<int>.One);

            if ((span.Length % Vector512<int>.Count) == 0)
            {
                // Aligned length
                for (int i = 0; i < span.Length; i += Vector512<int>.Count)
                {
                    v.StoreUnsafe(ref Unsafe.Add(ref spanRef, i));
                    v += incrementMask;
                }
            }
            else
            {
                // We will need a scalar remainder
                int vectorLength = span.Length - (span.Length % Vector512<int>.Count);

                int i;
                for (i = 0; i < vectorLength; i += Vector512<int>.Count)
                {
                    v.StoreUnsafe(ref Unsafe.Add(ref spanRef, i));
                    v += incrementMask;
                }

                int val = v.ToScalar();
                for (; i < span.Length; i++)
                {
                    Unsafe.Add(ref spanRef, i) = val++;
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> incrementMask = Vector256.Create(1, 2, 3, 4, 5, 6, 7, 8);
            Vector256<int> v = Vector256.Create(start) + (incrementMask - Vector256<int>.One);

            if ((span.Length % Vector256<int>.Count) == 0)
            {
                // Aligned length
                for (int i = 0; i < span.Length; i += Vector256<int>.Count)
                {
                    v.StoreUnsafe(ref Unsafe.Add(ref spanRef, i));
                    v += incrementMask;
                }
            }
            else
            {
                // We will need a scalar remainder
                int vectorLength = span.Length - (span.Length % Vector256<int>.Count);

                int i;
                for (i = 0; i < vectorLength; i += Vector256<int>.Count)
                {
                    v.StoreUnsafe(ref Unsafe.Add(ref spanRef, i));
                    v += incrementMask;
                }

                int val = v.ToScalar();
                for (; i < span.Length; i++)
                {
                    Unsafe.Add(ref spanRef, i) = val++;
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> incrementMask = Vector128.Create(1, 2, 3, 4);
            Vector128<int> v = Vector128.Create(start) + (incrementMask - Vector128<int>.One);

            if ((span.Length % Vector128<int>.Count) == 0)
            {
                // Aligned length
                for (int i = 0; i < span.Length; i += Vector128<int>.Count)
                {
                    v.StoreUnsafe(ref Unsafe.Add(ref spanRef, i));
                    v += incrementMask;
                }
            }
            else
            {
                // We will need a scalar remainder
                int vectorLength = span.Length - (span.Length % Vector128<int>.Count);

                int i;
                for (i = 0; i < vectorLength; i += Vector128<int>.Count)
                {
                    v.StoreUnsafe(ref Unsafe.Add(ref spanRef, i));
                    v += incrementMask;
                }

                int val = v.ToScalar();
                for (; i < span.Length; i++)
                {
                    Unsafe.Add(ref spanRef, i) = val++;
                }
            }
        }
        else if (Vector64.IsHardwareAccelerated)
        {
            Vector64<int> incrementMask = Vector64.Create(1, 2);
            Vector64<int> v = Vector64.Create(start) + (incrementMask - Vector64<int>.One);

            if ((span.Length % Vector64<int>.Count) == 0)
            {
                // Aligned length
                for (int i = 0; i < span.Length; i += Vector64<int>.Count)
                {
                    v.StoreUnsafe(ref Unsafe.Add(ref spanRef, i));
                    v += incrementMask;
                }
            }
            else
            {
                // We will need a scalar remainder
                int vectorLength = span.Length - (span.Length % Vector64<int>.Count);

                int i;
                for (i = 0; i < vectorLength; i += Vector64<int>.Count)
                {
                    v.StoreUnsafe(ref Unsafe.Add(ref spanRef, i));
                    v += incrementMask;
                }

                int val = v.ToScalar();
                for (; i < span.Length; i++)
                {
                    Unsafe.Add(ref spanRef, i) = val++;
                }
            }
        }
        else
        {
            // No SIMD
            int value = start;
            spanRef = value;
            value++;
            for (int i = 1; i < span.Length; i++)
            {
                Unsafe.Add(ref spanRef, i) = value++;
            }
        }
    }

    /// <summary>
    ///   <para>
    ///     Fills the span so its first value is equal to <paramref name="start"/>
    ///     and subsequent values increment by one. For example, with start=5,
    ///     the span's values will be:
    ///     <code>
    ///       { start, start+1, start+2, start+3, start+4, ... to the end of the span }
    ///     </code>
    ///   </para>
    ///   <para>
    ///     or, more precisely:
    ///     <code>
    ///       { 5, 6, 7, 8, 9, 10, 11, ... to the end of the span }
    ///     </code>
    ///   </para>
    ///   <seealso href="https://en.cppreference.com/cpp/algorithm/iota" />
    /// </summary>
    /// <param name="span">The span where the values are filled.</param>
    /// <param name="start">Initial value.</param>
    public static void Iota<T>(Span<T> span, T start)
        where T : unmanaged, INumber<T>
    {
        // Slightly slower than the int variant
        ref T spanRef = ref MemoryMarshal.GetReference(span);

        if (Vector<T>.IsSupported && Vector.IsHardwareAccelerated)
        {
            Vector<T> incrementMask = IotaMask<T>.IncrementMask;
            Vector<T> v = Vector.Create(start) + (incrementMask - Vector<T>.One);

            if ((span.Length % Vector<T>.Count) == 0)
            {
                // Aligned length
                for (int i = 0; i < span.Length; i += Vector<T>.Count)
                {
                    v.StoreUnsafe(ref Unsafe.Add(ref spanRef, i));
                    v += incrementMask;
                }
            }
            else
            {
                // Remainder needed
                int vectorLength = span.Length - (span.Length % Vector<T>.Count);

                int i;
                for (i = 0; i < vectorLength; i += Vector<T>.Count)
                {
                    v.StoreUnsafe(ref Unsafe.Add(ref spanRef, i));
                    v += incrementMask;
                }

                T val = v.ToScalar();
                for (; i < span.Length; i++)
                {
                    Unsafe.Add(ref spanRef, i) = val++;
                }
            }
        }
        else
        {
            // Scalar (slow)
            T value = start;
            spanRef = value;
            value++;
            for (int i = 1; i < span.Length; i++)
            {
                Unsafe.Add(ref spanRef, i) = value++;
            }
        }
    }

    public static Vector<T> Iota<T>(T start)
        where T : unmanaged, INumber<T>
        => IotaMask<T>.IncrementMask + Vector.Create(start);

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

    public static uint MaxValue(Span<uint> data)
    {
        int lanes = Vector<uint>.Count;
        int lastFull = lanes * (data.Length / lanes);

        Vector<uint> max = Vector<uint>.Zero;
        ref uint dataRef = ref MemoryMarshal.GetReference(data);

        for (int i = 0; i < lastFull; i += lanes)
        {
            max = Vector.Max(max, Vector.LoadUnsafe(ref Unsafe.Add(ref dataRef, i)));
        }

        if (lastFull < data.Length)
        {
            Vector<uint> stop = Vector.Create((uint)data.Length);
            Vector<uint> fence = Iota((uint)lastFull);
            Vector<uint> take = Vector.LessThan(fence, stop);
            max = Vector.Max(
                max,
                Vector.ConditionalSelect(
                    take,
                    Vector.LoadUnsafe(ref Unsafe.Add(ref dataRef, lastFull)),
                    Vector<uint>.Zero));
        }

        // The following part is to find the largest number in
        // the vector.
        Span<uint> copy = stackalloc uint[Vector<uint>.Count];
        max.CopyTo(copy);
        return TensorPrimitives.Max<uint>(copy);
    }

    /// <summary>
    /// Incrementing values to compute the Iota function.
    /// </summary>
    /// <remarks>
    /// Creating a Vector&lt;T&gt; incrementing values would be
    /// slow as Vector&lt;T&gt; is not a fixed-size vector, leaving
    /// no other option but a slow loop. This class caches these
    /// vectors for significantly better performance, though still
    /// not as fast as an int variant.
    /// </remarks>
    /// <typeparam name="T">Type of the vector.</typeparam>
    private static class IotaMask<T>
        where T : unmanaged, INumber<T>
    {
        public static readonly Vector<T> IncrementMask;

        static IotaMask()
        {
            Span<T> values = stackalloc T[Vector<T>.Count];

            for (int i = 0; i < Vector<T>.Count; i++)
            {
                values[i] = T.CreateSaturating(i + 1);
            }

            IncrementMask = Vector.Create<T>(values);
        }
    }
}

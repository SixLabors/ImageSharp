// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Provides optimized static methods for trigonometric, logarithmic,
    /// and other common mathematical functions.
    /// </summary>
    internal static class Numerics
    {
#if SUPPORTS_RUNTIME_INTRINSICS
        public const int BlendAlphaControl = 0b_10_00_10_00;
        private const int ShuffleAlphaControl = 0b_11_11_11_11;
#endif

        /// <summary>
        /// Determine the Greatest CommonDivisor (GCD) of two numbers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }

            return a;
        }

        /// <summary>
        /// Determine the Least Common Multiple (LCM) of two numbers.
        /// See https://en.wikipedia.org/wiki/Least_common_multiple#Reduction_by_the_greatest_common_divisor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeastCommonMultiple(int a, int b)
            => a / GreatestCommonDivisor(a, b) * b;

        /// <summary>
        /// Calculates <paramref name="x"/> % 2
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Modulo2(int x) => x & 1;

        /// <summary>
        /// Calculates <paramref name="x"/> % 4
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Modulo4(int x) => x & 3;

        /// <summary>
        /// Calculates <paramref name="x"/> % 8
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Modulo8(int x) => x & 7;

        /// <summary>
        /// Fast (x mod m) calculator, with the restriction that
        /// <paramref name="m"/> should be power of 2.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ModuloP2(int x, int m) => x & (m - 1);

        /// <summary>
        /// Returns the absolute value of a 32-bit signed integer.
        /// Uses bit shifting to speed up the operation compared to <see cref="Math"/>.
        /// </summary>
        /// <param name="x">
        /// A number that is greater than <see cref="int.MinValue"/>, but less than
        /// or equal to <see cref="int.MaxValue"/>
        /// </param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Abs(int x)
        {
            int y = x >> 31;
            return (x ^ y) - y;
        }

        /// <summary>
        /// Returns a specified number raised to the power of 2
        /// </summary>
        /// <param name="x">A single-precision floating-point number</param>
        /// <returns>The number <paramref name="x" /> raised to the power of 2.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow2(float x) => x * x;

        /// <summary>
        /// Returns a specified number raised to the power of 3
        /// </summary>
        /// <param name="x">A single-precision floating-point number</param>
        /// <returns>The number <paramref name="x" /> raised to the power of 3.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow3(float x) => x * x * x;

        /// <summary>
        /// Implementation of 1D Gaussian G(x) function
        /// </summary>
        /// <param name="x">The x provided to G(x).</param>
        /// <param name="sigma">The spread of the blur.</param>
        /// <returns>The Gaussian G(x)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Gaussian(float x, float sigma)
        {
            const float Numerator = 1.0f;
            float denominator = MathF.Sqrt(2 * MathF.PI) * sigma;

            float exponentNumerator = -x * x;
            float exponentDenominator = 2 * Pow2(sigma);

            float left = Numerator / denominator;
            float right = MathF.Exp(exponentNumerator / exponentDenominator);

            return left * right;
        }

        /// <summary>
        /// Returns the result of a normalized sine cardinal function for the given value.
        /// SinC(x) = sin(pi*x)/(pi*x).
        /// </summary>
        /// <param name="f">A single-precision floating-point number to calculate the result for.</param>
        /// <returns>
        /// The sine cardinal of <paramref name="f" />.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinC(float f)
        {
            if (MathF.Abs(f) > Constants.Epsilon)
            {
                f *= MathF.PI;
                float result = MathF.Sin(f) / f;
                return MathF.Abs(result) < Constants.Epsilon ? 0F : result;
            }

            return 1F;
        }

        /// <summary>
        /// Returns the value clamped to the inclusive range of min and max.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        /// <returns>The clamped <see cref="byte"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Clamp(byte value, byte min, byte max)
        {
            // Order is important here as someone might set min to higher than max.
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Returns the value clamped to the inclusive range of min and max.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        /// <returns>The clamped <see cref="uint"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Clamp(uint value, uint min, uint max)
        {
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Returns the value clamped to the inclusive range of min and max.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        /// <returns>The clamped <see cref="uint"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int value, int min, int max)
        {
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Returns the value clamped to the inclusive range of min and max.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        /// <returns>The clamped <see cref="float"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float value, float min, float max)
        {
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Returns the value clamped to the inclusive range of min and max.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        /// <returns>The clamped <see cref="double"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp(double value, double min, double max)
        {
            if (value > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value;
        }

        /// <summary>
        /// Returns the value clamped to the inclusive range of min and max.
        /// 5x Faster than <see cref="Vector4.Clamp(Vector4, Vector4, Vector4)"/>
        /// on platforms &lt; NET 5.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        /// <returns>The clamped <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Clamp(Vector4 value, Vector4 min, Vector4 max)
            => Vector4.Min(Vector4.Max(value, min), max);

        /// <summary>
        /// Clamps the span values to the inclusive range of min and max.
        /// </summary>
        /// <param name="span">The span containing the values to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clamp(Span<byte> span, byte min, byte max)
        {
            Span<byte> remainder = span.Slice(ClampReduce(span, min, max));

            if (remainder.Length > 0)
            {
                ref byte remainderStart = ref MemoryMarshal.GetReference(remainder);
                ref byte remainderEnd = ref Unsafe.Add(ref remainderStart, remainder.Length);

                while (Unsafe.IsAddressLessThan(ref remainderStart, ref remainderEnd))
                {
                    remainderStart = Clamp(remainderStart, min, max);

                    remainderStart = ref Unsafe.Add(ref remainderStart, 1);
                }
            }
        }

        /// <summary>
        /// Clamps the span values to the inclusive range of min and max.
        /// </summary>
        /// <param name="span">The span containing the values to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clamp(Span<uint> span, uint min, uint max)
        {
            Span<uint> remainder = span.Slice(ClampReduce(span, min, max));

            if (remainder.Length > 0)
            {
                ref uint remainderStart = ref MemoryMarshal.GetReference(remainder);
                ref uint remainderEnd = ref Unsafe.Add(ref remainderStart, remainder.Length);

                while (Unsafe.IsAddressLessThan(ref remainderStart, ref remainderEnd))
                {
                    remainderStart = Clamp(remainderStart, min, max);

                    remainderStart = ref Unsafe.Add(ref remainderStart, 1);
                }
            }
        }

        /// <summary>
        /// Clamps the span values to the inclusive range of min and max.
        /// </summary>
        /// <param name="span">The span containing the values to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clamp(Span<int> span, int min, int max)
        {
            Span<int> remainder = span.Slice(ClampReduce(span, min, max));

            if (remainder.Length > 0)
            {
                ref int remainderStart = ref MemoryMarshal.GetReference(remainder);
                ref int remainderEnd = ref Unsafe.Add(ref remainderStart, remainder.Length);

                while (Unsafe.IsAddressLessThan(ref remainderStart, ref remainderEnd))
                {
                    remainderStart = Clamp(remainderStart, min, max);

                    remainderStart = ref Unsafe.Add(ref remainderStart, 1);
                }
            }
        }

        /// <summary>
        /// Clamps the span values to the inclusive range of min and max.
        /// </summary>
        /// <param name="span">The span containing the values to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clamp(Span<float> span, float min, float max)
        {
            Span<float> remainder = span.Slice(ClampReduce(span, min, max));

            if (remainder.Length > 0)
            {
                ref float remainderStart = ref MemoryMarshal.GetReference(remainder);
                ref float remainderEnd = ref Unsafe.Add(ref remainderStart, remainder.Length);

                while (Unsafe.IsAddressLessThan(ref remainderStart, ref remainderEnd))
                {
                    remainderStart = Clamp(remainderStart, min, max);

                    remainderStart = ref Unsafe.Add(ref remainderStart, 1);
                }
            }
        }

        /// <summary>
        /// Clamps the span values to the inclusive range of min and max.
        /// </summary>
        /// <param name="span">The span containing the values to clamp.</param>
        /// <param name="min">The minimum inclusive value.</param>
        /// <param name="max">The maximum inclusive value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clamp(Span<double> span, double min, double max)
        {
            Span<double> remainder = span.Slice(ClampReduce(span, min, max));

            if (remainder.Length > 0)
            {
                ref double remainderStart = ref MemoryMarshal.GetReference(remainder);
                ref double remainderEnd = ref Unsafe.Add(ref remainderStart, remainder.Length);

                while (Unsafe.IsAddressLessThan(ref remainderStart, ref remainderEnd))
                {
                    remainderStart = Clamp(remainderStart, min, max);

                    remainderStart = ref Unsafe.Add(ref remainderStart, 1);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampReduce<T>(Span<T> span, T min, T max)
            where T : unmanaged
        {
            if (Vector.IsHardwareAccelerated && span.Length >= Vector<T>.Count)
            {
                int remainder = ModuloP2(span.Length, Vector<T>.Count);
                int adjustedCount = span.Length - remainder;

                if (adjustedCount > 0)
                {
                    ClampImpl(span.Slice(0, adjustedCount), min, max);
                }

                return adjustedCount;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampImpl<T>(Span<T> span, T min, T max)
            where T : unmanaged
        {
            ref T sRef = ref MemoryMarshal.GetReference(span);
            var vmin = new Vector<T>(min);
            var vmax = new Vector<T>(max);

            int n = span.Length / Vector<T>.Count;
            int m = Modulo4(n);
            int u = n - m;

            ref Vector<T> vs0 = ref Unsafe.As<T, Vector<T>>(ref MemoryMarshal.GetReference(span));
            ref Vector<T> vs1 = ref Unsafe.Add(ref vs0, 1);
            ref Vector<T> vs2 = ref Unsafe.Add(ref vs0, 2);
            ref Vector<T> vs3 = ref Unsafe.Add(ref vs0, 3);
            ref Vector<T> vsEnd = ref Unsafe.Add(ref vs0, u);

            while (Unsafe.IsAddressLessThan(ref vs0, ref vsEnd))
            {
                vs0 = Vector.Min(Vector.Max(vmin, vs0), vmax);
                vs1 = Vector.Min(Vector.Max(vmin, vs1), vmax);
                vs2 = Vector.Min(Vector.Max(vmin, vs2), vmax);
                vs3 = Vector.Min(Vector.Max(vmin, vs3), vmax);

                vs0 = ref Unsafe.Add(ref vs0, 4);
                vs1 = ref Unsafe.Add(ref vs1, 4);
                vs2 = ref Unsafe.Add(ref vs2, 4);
                vs3 = ref Unsafe.Add(ref vs3, 4);
            }

            if (m > 0)
            {
                vs0 = ref vsEnd;
                vsEnd = ref Unsafe.Add(ref vsEnd, m);

                while (Unsafe.IsAddressLessThan(ref vs0, ref vsEnd))
                {
                    vs0 = Vector.Min(Vector.Max(vmin, vs0), vmax);

                    vs0 = ref Unsafe.Add(ref vs0, 1);
                }
            }
        }

        /// <summary>
        /// Pre-multiplies the "x", "y", "z" components of a vector by its "w" component leaving the "w" component intact.
        /// </summary>
        /// <param name="source">The <see cref="Vector4"/> to premultiply</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Premultiply(ref Vector4 source)
        {
            float w = source.W;
            source *= w;
            source.W = w;
        }

        /// <summary>
        /// Reverses the result of premultiplying a vector via <see cref="Premultiply(ref Vector4)"/>.
        /// </summary>
        /// <param name="source">The <see cref="Vector4"/> to premultiply</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnPremultiply(ref Vector4 source)
        {
            float w = source.W;
            source /= w;
            source.W = w;
        }

        /// <summary>
        /// Bulk variant of <see cref="Premultiply(ref Vector4)"/>
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Premultiply(Span<Vector4> vectors)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported && vectors.Length >= 2)
            {
                // Divide by 2 as 4 elements per Vector4 and 8 per Vector256<float>
                ref Vector256<float> vectorsBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(vectors));
                ref Vector256<float> vectorsLast = ref Unsafe.Add(ref vectorsBase, (IntPtr)((uint)vectors.Length / 2u));

                while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsLast))
                {
                    Vector256<float> source = vectorsBase;
                    Vector256<float> multiply = Avx.Shuffle(source, source, ShuffleAlphaControl);
                    vectorsBase = Avx.Blend(Avx.Multiply(source, multiply), source, BlendAlphaControl);
                    vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
                }

                if (Modulo2(vectors.Length) != 0)
                {
                    // Vector4 fits neatly in pairs. Any overlap has to be equal to 1.
                    Premultiply(ref MemoryMarshal.GetReference(vectors.Slice(vectors.Length - 1)));
                }
            }
            else
#endif
            {
                ref Vector4 vectorsStart = ref MemoryMarshal.GetReference(vectors);
                ref Vector4 vectorsEnd = ref Unsafe.Add(ref vectorsStart, vectors.Length);

                while (Unsafe.IsAddressLessThan(ref vectorsStart, ref vectorsEnd))
                {
                    Premultiply(ref vectorsStart);

                    vectorsStart = ref Unsafe.Add(ref vectorsStart, 1);
                }
            }
        }

        /// <summary>
        /// Bulk variant of <see cref="UnPremultiply(ref Vector4)"/>
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnPremultiply(Span<Vector4> vectors)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported && vectors.Length >= 2)
            {
                // Divide by 2 as 4 elements per Vector4 and 8 per Vector256<float>
                ref Vector256<float> vectorsBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(vectors));
                ref Vector256<float> vectorsLast = ref Unsafe.Add(ref vectorsBase, (IntPtr)((uint)vectors.Length / 2u));

                while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsLast))
                {
                    Vector256<float> source = vectorsBase;
                    Vector256<float> multiply = Avx.Shuffle(source, source, ShuffleAlphaControl);
                    vectorsBase = Avx.Blend(Avx.Divide(source, multiply), source, BlendAlphaControl);
                    vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
                }

                if (Modulo2(vectors.Length) != 0)
                {
                    // Vector4 fits neatly in pairs. Any overlap has to be equal to 1.
                    UnPremultiply(ref MemoryMarshal.GetReference(vectors.Slice(vectors.Length - 1)));
                }
            }
            else
#endif
            {
                ref Vector4 vectorsStart = ref MemoryMarshal.GetReference(vectors);
                ref Vector4 vectorsEnd = ref Unsafe.Add(ref vectorsStart, vectors.Length);

                while (Unsafe.IsAddressLessThan(ref vectorsStart, ref vectorsEnd))
                {
                    UnPremultiply(ref vectorsStart);

                    vectorsStart = ref Unsafe.Add(ref vectorsStart, 1);
                }
            }
        }

        /// <summary>
        /// Calculates the cube pow of all the XYZ channels of the input vectors.
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CubePowOnXYZ(Span<Vector4> vectors)
        {
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(vectors);
            ref Vector4 endRef = ref Unsafe.Add(ref baseRef, vectors.Length);

            while (Unsafe.IsAddressLessThan(ref baseRef, ref endRef))
            {
                Vector4 v = baseRef;
                float a = v.W;

                // Fast path for the default gamma exposure, which is 3. In this case we can skip
                // calling Math.Pow 3 times (one per component), as the method is an internal call and
                // introduces quite a bit of overhead. Instead, we can just manually multiply the whole
                // pixel in Vector4 format 3 times, and then restore the alpha channel before copying it
                // back to the target index in the temporary span. The whole iteration will get completely
                // inlined and traslated into vectorized instructions, with much better performance.
                v = v * v * v;
                v.W = a;

                baseRef = v;
                baseRef = ref Unsafe.Add(ref baseRef, 1);
            }
        }

        /// <summary>
        /// Calculates the cube root of all the XYZ channels of the input vectors.
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CubeRootOnXYZ(Span<Vector4> vectors)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Sse41.IsSupported)
            {
                ref Vector128<float> vectors128Ref = ref Unsafe.As<Vector4, Vector128<float>>(ref MemoryMarshal.GetReference(vectors));
                ref Vector128<float> vectors128End = ref Unsafe.Add(ref vectors128Ref, vectors.Length);

                var v128_341 = Vector128.Create(341);
                Vector128<int> v128_negativeZero = Vector128.Create(-0.0f).AsInt32();
                Vector128<int> v128_one = Vector128.Create(1.0f).AsInt32();

                var v128_13rd = Vector128.Create(1 / 3f);
                var v128_23rds = Vector128.Create(2 / 3f);

                while (Unsafe.IsAddressLessThan(ref vectors128Ref, ref vectors128End))
                {
                    Vector128<float> vecx = vectors128Ref;
                    Vector128<int> veax = vecx.AsInt32();

                    // If we can use SSE41 instructions, we can vectorize the entire cube root calculation, and also execute it
                    // directly on 32 bit floating point values. What follows is a vectorized implementation of this method:
                    // https://www.musicdsp.org/en/latest/Other/206-fast-cube-root-square-root-and-reciprocal-for-x86-sse-cpus.html.
                    // Furthermore, after the initial setup in vectorized form, we're doing two Newton approximations here
                    // using a different succession (the same used below), which should be less unstable due to not having cube pow.
                    veax = Sse2.AndNot(v128_negativeZero, veax);
                    veax = Sse2.Subtract(veax, v128_one);
                    veax = Sse2.ShiftRightArithmetic(veax, 10);
                    veax = Sse41.MultiplyLow(veax, v128_341);
                    veax = Sse2.Add(veax, v128_one);
                    veax = Sse2.AndNot(v128_negativeZero, veax);
                    veax = Sse2.Or(veax, Sse2.And(vecx.AsInt32(), v128_negativeZero));

                    Vector128<float> y4 = veax.AsSingle();

                    if (Fma.IsSupported)
                    {
                        y4 = Fma.MultiplyAdd(v128_23rds, y4, Sse.Multiply(v128_13rd, Sse.Divide(vecx, Sse.Multiply(y4, y4))));
                        y4 = Fma.MultiplyAdd(v128_23rds, y4, Sse.Multiply(v128_13rd, Sse.Divide(vecx, Sse.Multiply(y4, y4))));
                    }
                    else
                    {
                        y4 = Sse.Add(Sse.Multiply(v128_23rds, y4), Sse.Multiply(v128_13rd, Sse.Divide(vecx, Sse.Multiply(y4, y4))));
                        y4 = Sse.Add(Sse.Multiply(v128_23rds, y4), Sse.Multiply(v128_13rd, Sse.Divide(vecx, Sse.Multiply(y4, y4))));
                    }

                    y4 = Sse41.Insert(y4, vecx, 0xF0);

                    vectors128Ref = y4;
                    vectors128Ref = ref Unsafe.Add(ref vectors128Ref, 1);
                }
            }
            else
#endif
            {
                ref Vector4 vectorsRef = ref MemoryMarshal.GetReference(vectors);
                ref Vector4 vectorsEnd = ref Unsafe.Add(ref vectorsRef, vectors.Length);

                // Fallback with scalar preprocessing and vectorized approximation steps
                while (Unsafe.IsAddressLessThan(ref vectorsRef, ref vectorsEnd))
                {
                    Vector4 v = vectorsRef;

                    double
                        x64 = v.X,
                        y64 = v.Y,
                        z64 = v.Z;
                    float a = v.W;

                    ulong
                        xl = *(ulong*)&x64,
                        yl = *(ulong*)&y64,
                        zl = *(ulong*)&z64;

                    // Here we use a trick to compute the starting value x0 for the cube root. This is because doing
                    // pow(x, 1 / gamma) is the same as the gamma-th root of x, and since gamme is 3 in this case,
                    // this means what we actually want is to find the cube root of our clamped values.
                    // For more info on the  constant below, see:
                    // https://community.intel.com/t5/Intel-C-Compiler/Fast-approximate-of-transcendental-operations/td-p/1044543.
                    // Here we perform the same trick on all RGB channels separately to help the CPU execute them in paralle, and
                    // store the alpha channel to preserve it. Then we set these values to the fields of a temporary 128-bit
                    // register, and use it to accelerate two steps of the Newton approximation using SIMD.
                    xl = 0x2a9f8a7be393b600 + (xl / 3);
                    yl = 0x2a9f8a7be393b600 + (yl / 3);
                    zl = 0x2a9f8a7be393b600 + (zl / 3);

                    Vector4 y4;
                    y4.X = (float)*(double*)&xl;
                    y4.Y = (float)*(double*)&yl;
                    y4.Z = (float)*(double*)&zl;
                    y4.W = 0;

                    y4 = (2 / 3f * y4) + (1 / 3f * (v / (y4 * y4)));
                    y4 = (2 / 3f * y4) + (1 / 3f * (v / (y4 * y4)));
                    y4.W = a;

                    vectorsRef = y4;
                    vectorsRef = ref Unsafe.Add(ref vectorsRef, 1);
                }
            }
        }

#if SUPPORTS_RUNTIME_INTRINSICS

        /// <summary>
        /// Performs a linear interpolation between two values based on the given weighting.
        /// </summary>
        /// <param name="value1">The first value.</param>
        /// <param name="value2">The second value.</param>
        /// <param name="amount">Values between 0 and 1 that indicates the weight of <paramref name="value2"/>.</param>
        /// <returns>The <see cref="Vector256{Single}"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Lerp(
            in Vector256<float> value1,
            in Vector256<float> value2,
            in Vector256<float> amount)
        {
            Vector256<float> diff = Avx.Subtract(value2, value1);
            if (Fma.IsSupported)
            {
                return Fma.MultiplyAdd(diff, amount, value1);
            }
            else
            {
                return Avx.Add(Avx.Multiply(diff, amount), value1);
            }
        }
#endif

        /// <summary>
        /// Performs a linear interpolation between two values based on the given weighting.
        /// </summary>
        /// <param name="value1">The first value.</param>
        /// <param name="value2">The second value.</param>
        /// <param name="amount">A value between 0 and 1 that indicates the weight of <paramref name="value2"/>.</param>
        /// <returns>The <see cref="float"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lerp(float value1, float value2, float amount)
            => ((value2 - value1) * amount) + value1;
    }
}

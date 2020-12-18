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

namespace SixLabors.ImageSharp.ColorSpaces.Companding
{
    /// <summary>
    /// Implements sRGB companding.
    /// </summary>
    /// <remarks>
    /// For more info see:
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
    /// </remarks>
    public static class SRgbCompanding
    {
        private const int Length = Scale + 2; // 256kb @ 16bit precision.
        private const int Scale = (1 << 16) - 1;

        private static readonly Lazy<float[]> LazyCompressTable = new Lazy<float[]>(
            () =>
            {
                var result = new float[Length];

                for (int i = 0; i < result.Length; i++)
                {
                    double d = (double)i / Scale;
                    if (d <= (0.04045 / 12.92))
                    {
                        d *= 12.92;
                    }
                    else
                    {
                        d = (1.055 * Math.Pow(d, 1.0 / 2.4)) - 0.055;
                    }

                    result[i] = (float)d;
                }

                return result;
            },
            true);

        private static readonly Lazy<float[]> LazyExpandTable = new Lazy<float[]>(
            () =>
            {
                var result = new float[Length];

                for (int i = 0; i < result.Length; i++)
                {
                    double d = (double)i / Scale;
                    if (d <= 0.04045)
                    {
                        d /= 12.92;
                    }
                    else
                    {
                        d = Math.Pow((d + 0.055) / 1.055, 2.4);
                    }

                    result[i] = (float)d;
                }

                return result;
            },
            true);

        private static float[] ExpandTable => LazyExpandTable.Value;

        private static float[] CompressTable => LazyCompressTable.Value;

        /// <summary>
        /// Expands the companded vectors to their linear equivalents with respect to the energy.
        /// </summary>
        /// <param name="vectors">The span of vectors.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Expand(Span<Vector4> vectors)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported && vectors.Length >= 2)
            {
                CompandAvx2(vectors, ExpandTable);

                if (Numerics.Modulo2(vectors.Length) != 0)
                {
                    // Vector4 fits neatly in pairs. Any overlap has to be equal to 1.
                    Expand(ref MemoryMarshal.GetReference(vectors.Slice(vectors.Length - 1)));
                }
            }
            else
#endif
            {
                CompandScalar(vectors, ExpandTable);
            }
        }

        /// <summary>
        /// Compresses the uncompanded vectors to their nonlinear equivalents with respect to the energy.
        /// </summary>
        /// <param name="vectors">The span of vectors.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Compress(Span<Vector4> vectors)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported && vectors.Length >= 2)
            {
                CompandAvx2(vectors, CompressTable);

                if (Numerics.Modulo2(vectors.Length) != 0)
                {
                    // Vector4 fits neatly in pairs. Any overlap has to be equal to 1.
                    Compress(ref MemoryMarshal.GetReference(vectors.Slice(vectors.Length - 1)));
                }
            }
            else
#endif
            {
                CompandScalar(vectors, CompressTable);
            }
        }

        /// <summary>
        /// Expands a companded vector to its linear equivalent with respect to the energy.
        /// </summary>
        /// <param name="vector">The vector.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Expand(ref Vector4 vector)
        {
            // Alpha is already a linear representation of opacity so we do not want to convert it.
            vector.X = Expand(vector.X);
            vector.Y = Expand(vector.Y);
            vector.Z = Expand(vector.Z);
        }

        /// <summary>
        /// Compresses an uncompanded vector (linear) to its nonlinear equivalent.
        /// </summary>
        /// <param name="vector">The vector.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Compress(ref Vector4 vector)
        {
            // Alpha is already a linear representation of opacity so we do not want to convert it.
            vector.X = Compress(vector.X);
            vector.Y = Compress(vector.Y);
            vector.Z = Compress(vector.Z);
        }

        /// <summary>
        /// Expands a companded channel to its linear equivalent with respect to the energy.
        /// </summary>
        /// <param name="channel">The channel value.</param>
        /// <returns>The <see cref="float"/> representing the linear channel value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Expand(float channel)
            => channel <= 0.04045F ? channel / 12.92F : MathF.Pow((channel + 0.055F) / 1.055F, 2.4F);

        /// <summary>
        /// Compresses an uncompanded channel (linear) to its nonlinear equivalent.
        /// </summary>
        /// <param name="channel">The channel value.</param>
        /// <returns>The <see cref="float"/> representing the nonlinear channel value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Compress(float channel)
            => channel <= 0.0031308F ? 12.92F * channel : (1.055F * MathF.Pow(channel, 0.416666666666667F)) - 0.055F;

#if SUPPORTS_RUNTIME_INTRINSICS

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void CompandAvx2(Span<Vector4> vectors, float[] table)
        {
            fixed (float* tablePointer = &table[0])
            {
                var scale = Vector256.Create((float)Scale);
                Vector256<float> zero = Vector256<float>.Zero;
                var offset = Vector256.Create(1);

                // Divide by 2 as 4 elements per Vector4 and 8 per Vector256<float>
                ref Vector256<float> vectorsBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(vectors));
                ref Vector256<float> vectorsLast = ref Unsafe.Add(ref vectorsBase, (IntPtr)((uint)vectors.Length / 2u));

                while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsLast))
                {
                    Vector256<float> multiplied = Avx.Multiply(scale, vectorsBase);
                    multiplied = Avx.Min(Avx.Max(zero, multiplied), scale);

                    Vector256<int> truncated = Avx.ConvertToVector256Int32WithTruncation(multiplied);
                    Vector256<float> truncatedF = Avx.ConvertToVector256Single(truncated);

                    Vector256<float> low = Avx2.GatherVector256(tablePointer, truncated, sizeof(float));
                    Vector256<float> high = Avx2.GatherVector256(tablePointer, Avx2.Add(truncated, offset), sizeof(float));

                    // Alpha is already a linear representation of opacity so we do not want to convert it.
                    Vector256<float> companded = Numerics.Lerp(low, high, Avx.Subtract(multiplied, truncatedF));
                    vectorsBase = Avx.Blend(companded, vectorsBase, Numerics.BlendAlphaControl);
                    vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
                }
            }
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void CompandScalar(Span<Vector4> vectors, float[] table)
        {
            fixed (float* tablePointer = &table[0])
            {
                Vector4 zero = Vector4.Zero;
                var scale = new Vector4(Scale);
                ref Vector4 vectorsBase = ref MemoryMarshal.GetReference(vectors);
                ref Vector4 vectorsLast = ref Unsafe.Add(ref vectorsBase, vectors.Length);

                while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsLast))
                {
                    Vector4 multiplied = Numerics.Clamp(vectorsBase * Scale, zero, scale);

                    float f0 = multiplied.X;
                    float f1 = multiplied.Y;
                    float f2 = multiplied.Z;

                    uint i0 = (uint)f0;
                    uint i1 = (uint)f1;
                    uint i2 = (uint)f2;

                    // Alpha is already a linear representation of opacity so we do not want to convert it.
                    vectorsBase.X = Numerics.Lerp(tablePointer[i0], tablePointer[i0 + 1], f0 - (int)i0);
                    vectorsBase.Y = Numerics.Lerp(tablePointer[i1], tablePointer[i1 + 1], f1 - (int)i1);
                    vectorsBase.Z = Numerics.Lerp(tablePointer[i2], tablePointer[i2 + 1], f2 - (int)i2);

                    vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
                }
            }
        }
    }
}

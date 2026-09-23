// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

internal static class JxlHalfUtils
{
    /*
     * SIMD version of the following bit-twiddling implementation
     * of float to Half conversion, because Vector<T> doesn't
     * have a method like this (only reinterpretation, we want
     * conversion).
        public static ushort ConvertSingleToHalf(float f)
        {
            uint x = BitConverter.SingleToUInt32Bits(f);

            uint sign = (x >> 16) & 0x8000; // sign bit
            int exp = (int)((x >> 23) & 0xFF) - 127 + 15; // exponent rebias
            uint mantissa = x & 0x007FFFFF; // mantissa bits

            if (exp <= 0)
            {
                // Subnormal or zero
                if (exp < -10)
                {
                    return (ushort)sign;
                }

                mantissa = (mantissa | 0x00800000) >> (1 - exp);
                return (ushort)(sign | (mantissa >> 13));
            }
            else if (exp >= 31)
            {
                // Inf or NaN
                return (ushort)(sign | 0x7C00 | (mantissa >> 13));
            }
            else
            {
                // Normalized
                return (ushort)(sign | ((uint)exp << 10) | (mantissa >> 13));
            }
        }
     */
    public static Vector<ushort> ConvertSingleToHalf(Vector<float> f)
    {
        Vector<uint> x = f.As<float, uint>();

        Vector<uint> sign = (x >> 16) & Vector.Create(0x8000u);
        Vector<int> exponent = ((x >> 23) & Vector.Create(0xFFu)).As<uint, int>() - Vector.Create(127) + Vector.Create(15);
        Vector<uint> mantissa = x & Vector.Create(0x007FFFFFu);

        Vector<uint> results = Vector<uint>.Zero;

        Vector<uint> lessThanMinus10 = Vector.LessThan(exponent, Vector.Create(-10)).As<int, uint>();
        Vector<uint> gte31 = Vector.GreaterThanOrEqual(exponent, Vector.Create(31)).As<int, uint>();
        Vector<uint> notLtMinus10OrGte31 = ~(lessThanMinus10 | gte31);

        // Subnormal or zero (exp < -10) = sign
        results = Vector.ConditionalSelect(lessThanMinus10, sign, results);

        // exp <= 0 = (ushort)(sign | (((mantissa | 0x00800000) >> (1 - exp)) >> 13));
        results = Vector.ConditionalSelect(
            Vector.LessThanOrEqual(exponent, Vector<int>.Zero).As<int, uint>(),
            sign | (ShiftRightAll(mantissa.As<uint, int>() | Vector.Create(0x00800000), Vector<int>.One - exponent) >> 13).As<int, uint>(),
            results);

        // exp >= 31 = (ushort)(sign | 0x7C00 | (mantissa >> 13))
        results = Vector.ConditionalSelect(
            gte31,
            sign | Vector.Create(0x7C00u) | (mantissa >> 13),
            results);

        // anything else - normalized
        results = Vector.ConditionalSelect(
            notLtMinus10OrGte31,
            sign | (exponent.As<int, uint>() << 10) | (mantissa >> 13),
            results);

        return VectorUInt32ToUInt16(results);
    }

    private static Vector<ushort> VectorUInt32ToUInt16(Vector<uint> uint32)
    {
        Vector<uint> clipped = uint32 & Vector.Create(0xFFFFu);
        return clipped.As<uint, ushort>();
    }

    /// <summary>
    /// Vectors don't support shifting right using shift value as vector.
    /// This is a hack to do this.
    /// </summary>
    /// <typeparam name="T">Kind of vector</typeparam>
    /// <param name="vec">Input vector</param>
    /// <param name="shiftVec">Vectors with shift value for each corresponding item.</param>
    /// <returns>Shifted vectors</returns>
    private static Vector<T> ShiftRightAll<T>(Vector<T> vec, Vector<T> shiftVec)
        where T : unmanaged, IShiftOperators<T, T, T>
    {
        Span<T> firstVec = stackalloc T[Vector<T>.Count];
        Span<T> secondVec = stackalloc T[Vector<T>.Count];

        vec.CopyTo(firstVec);
        shiftVec.CopyTo(secondVec);

        ref T firstRef = ref MemoryMarshal.GetReference(firstVec);
        ref T secondRef = ref MemoryMarshal.GetReference(secondVec);

        int count = Vector<T>.Count;

        if (count == 1)
        {
            return Vector.Create(firstRef >>> secondRef);
        }
        else if (count == 2)
        {
            firstRef >>>= secondRef;
            Unsafe.Add(ref firstRef, 1) >>>= Unsafe.Add(ref secondRef, 1);
            return Vector.Create<T>(firstVec);
        }
        else if (count == 4)
        {
            firstRef >>>= secondRef;
            Unsafe.Add(ref firstRef, 1) >>>= Unsafe.Add(ref secondRef, 1);
            Unsafe.Add(ref firstRef, 2) >>>= Unsafe.Add(ref secondRef, 2);
            Unsafe.Add(ref firstRef, 3) >>>= Unsafe.Add(ref secondRef, 3);
            return Vector.Create<T>(firstVec);
        }
        else if (count == 8)
        {
            firstRef >>>= secondRef;
            Unsafe.Add(ref firstRef, 1) >>>= Unsafe.Add(ref secondRef, 1);
            Unsafe.Add(ref firstRef, 2) >>>= Unsafe.Add(ref secondRef, 2);
            Unsafe.Add(ref firstRef, 3) >>>= Unsafe.Add(ref secondRef, 3);
            Unsafe.Add(ref firstRef, 4) >>>= Unsafe.Add(ref secondRef, 4);
            Unsafe.Add(ref firstRef, 5) >>>= Unsafe.Add(ref secondRef, 5);
            Unsafe.Add(ref firstRef, 6) >>>= Unsafe.Add(ref secondRef, 6);
            Unsafe.Add(ref firstRef, 7) >>>= Unsafe.Add(ref secondRef, 7);
            return Vector.Create<T>(firstVec);
        }
        else if (count == 16)
        {
            firstRef >>>= secondRef;
            Unsafe.Add(ref firstRef, 1) >>>= Unsafe.Add(ref secondRef, 1);
            Unsafe.Add(ref firstRef, 2) >>>= Unsafe.Add(ref secondRef, 2);
            Unsafe.Add(ref firstRef, 3) >>>= Unsafe.Add(ref secondRef, 3);
            Unsafe.Add(ref firstRef, 4) >>>= Unsafe.Add(ref secondRef, 4);
            Unsafe.Add(ref firstRef, 5) >>>= Unsafe.Add(ref secondRef, 5);
            Unsafe.Add(ref firstRef, 6) >>>= Unsafe.Add(ref secondRef, 6);
            Unsafe.Add(ref firstRef, 7) >>>= Unsafe.Add(ref secondRef, 7);
            Unsafe.Add(ref firstRef, 8) >>>= Unsafe.Add(ref secondRef, 8);
            Unsafe.Add(ref firstRef, 9) >>>= Unsafe.Add(ref secondRef, 9);
            Unsafe.Add(ref firstRef, 10) >>>= Unsafe.Add(ref secondRef, 10);
            Unsafe.Add(ref firstRef, 11) >>>= Unsafe.Add(ref secondRef, 11);
            Unsafe.Add(ref firstRef, 12) >>>= Unsafe.Add(ref secondRef, 12);
            Unsafe.Add(ref firstRef, 13) >>>= Unsafe.Add(ref secondRef, 13);
            Unsafe.Add(ref firstRef, 14) >>>= Unsafe.Add(ref secondRef, 14);
            Unsafe.Add(ref firstRef, 15) >>>= Unsafe.Add(ref secondRef, 15);
            return Vector.Create<T>(firstVec);
        }
        else
        {
            // Vector too large, use a (slightly) slower loop
            // instead of duplicating too much.
            firstRef >>>= secondRef;

            for (int i = 1; i < count; i++)
            {
                Unsafe.Add(ref firstRef, i) >>>= Unsafe.Add(ref secondRef, i);
            }

            return Vector.Create<T>(firstVec);
        }
    }
}

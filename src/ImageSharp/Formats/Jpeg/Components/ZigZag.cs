// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// Holds the Jpeg UnZig array in a value/stack type.
    /// Unzig maps from the zigzag ordering to the natural ordering. For example,
    /// unzig[3] is the column and row of the fourth element in zigzag order. The
    /// value is 16, which means first column (16%8 == 0) and third row (16/8 == 2).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct ZigZag
    {
        /// <summary>
        /// Copy of <see cref="Unzig"/> in a value type
        /// </summary>
        public fixed byte Data[64];

        /// <summary>
        /// Unzig maps from the zigzag ordering to the natural ordering. For example,
        /// unzig[3] is the column and row of the fourth element in zigzag order. The
        /// value is 16, which means first column (16%8 == 0) and third row (16/8 == 2).
        /// </summary>
        private static readonly byte[] Unzig =
        {
            0,
            1, 8,
            16, 9, 2,
            3, 10, 17, 24,
            32, 25, 18, 11, 4,
            5, 12, 19, 26, 33, 40,
            48, 41, 34, 27, 20, 13, 6,
            7, 14, 21, 28, 35, 42, 49, 56,
            57, 50, 43, 36, 29, 22, 15,
            23, 30, 37, 44, 51, 58,
            59, 52, 45, 38, 31,
            39, 46, 53, 60,
            61, 54, 47,
            55, 62,
            63
        };

        /// <summary>
        /// Returns the value at the given index
        /// </summary>
        /// <param name="idx">The index</param>
        /// <returns>The <see cref="byte"/></returns>
        public byte this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref byte self = ref Unsafe.As<ZigZag, byte>(ref this);
                return Unsafe.Add(ref self, idx);
            }
        }

        /// <summary>
        /// Creates and fills an instance of <see cref="ZigZag"/> with Jpeg unzig indices
        /// </summary>
        /// <returns>The new instance</returns>
        public static ZigZag CreateUnzigTable()
        {
            ZigZag result = default;
            byte* unzigPtr = result.Data;
            Marshal.Copy(Unzig, 0, (IntPtr)unzigPtr, 64);
            return result;
        }

        /// <summary>
        /// Apply Zigging to the given quantization table, so it will be sufficient to multiply blocks for dequantizing them.
        /// </summary>
        public static Block8x8F CreateDequantizationTable(ref Block8x8F qt)
        {
            Block8x8F result = default;

            for (int i = 0; i < 64; i++)
            {
                result[Unzig[i]] = qt[i];
            }

            return result;
        }
    }
}
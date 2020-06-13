// Copyright (c) Six Labors.
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
        /// When reading corrupted data, the Huffman decoders could attempt
        /// to reference an entry beyond the end of this array (if the decoded
        /// zero run length reaches past the end of the block).  To prevent
        /// wild stores without adding an inner-loop test, we put some extra
        /// "63"s after the real entries.  This will cause the extra coefficient
        /// to be stored in location 63 of the block, not somewhere random.
        /// The worst case would be a run-length of 15, which means we need 16
        /// fake entries.
        /// </summary>
        private const int Size = 64 + 16;

        /// <summary>
        /// Copy of <see cref="Unzig"/> in a value type
        /// </summary>
        public fixed byte Data[Size];

        /// <summary>
        /// Gets the unzigs map, which maps from the zigzag ordering to the natural ordering.
        /// For example, unzig[3] is the column and row of the fourth element in zigzag order.
        /// The value is 16, which means first column (16%8 == 0) and third row (16/8 == 2).
        /// </summary>
        private static ReadOnlySpan<byte> Unzig => new byte[]
        {
            0,  1,  8, 16,  9,  2,  3, 10,
            17, 24, 32, 25, 18, 11,  4,  5,
            12, 19, 26, 33, 40, 48, 41, 34,
            27, 20, 13,  6,  7, 14, 21, 28,
            35, 42, 49, 56, 57, 50, 43, 36,
            29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46,
            53, 60, 61, 54, 47, 55, 62, 63,
            63, 63, 63, 63, 63, 63, 63, 63, // Extra entries for safety in decoder
            63, 63, 63, 63, 63, 63, 63, 63
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
            ref byte sourceRef = ref MemoryMarshal.GetReference(Unzig);
            ref byte destinationRef = ref Unsafe.AsRef<byte>(result.Data);

            Unzig.CopyTo(new Span<byte>(result.Data, Size));

            return result;
        }

        /// <summary>
        /// Apply Zigging to the given quantization table, so it will be sufficient to multiply blocks for dequantizing them.
        /// </summary>
        public static Block8x8F CreateDequantizationTable(ref Block8x8F qt)
        {
            Block8x8F result = default;

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                result[Unzig[i]] = qt[i];
            }

            return result;
        }
    }
}

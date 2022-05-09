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
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// 8x8 matrix of <see cref="short"/> coefficients.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [StructLayout(LayoutKind.Explicit)]
    public unsafe partial struct Block8x8
    {
        /// <summary>
        /// A number of scalar coefficients in a <see cref="Block8x8F"/>
        /// </summary>
        public const int Size = 64;

#pragma warning disable IDE0051 // Remove unused private member
        /// <summary>
        /// A placeholder buffer so the actual struct occupies exactly 64 * 2 bytes.
        /// </summary>
        /// <remarks>
        /// This is not used directly in the code.
        /// </remarks>
        [FieldOffset(0)]
        private fixed short data[Size];
#pragma warning restore IDE0051

        /// <summary>
        /// Gets or sets a <see cref="short"/> value at the given index
        /// </summary>
        /// <param name="idx">The index</param>
        /// <returns>The value</returns>
        public short this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                DebugGuard.MustBeBetweenOrEqualTo(idx, 0, Size - 1, nameof(idx));

                ref short selfRef = ref Unsafe.As<Block8x8, short>(ref this);
                return Unsafe.Add(ref selfRef, idx);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                DebugGuard.MustBeBetweenOrEqualTo(idx, 0, Size - 1, nameof(idx));

                ref short selfRef = ref Unsafe.As<Block8x8, short>(ref this);
                Unsafe.Add(ref selfRef, idx) = value;
            }
        }

        /// <summary>
        /// Gets or sets a value in a row+column of the 8x8 block
        /// </summary>
        /// <param name="x">The x position index in the row</param>
        /// <param name="y">The column index</param>
        /// <returns>The value</returns>
        public short this[int x, int y]
        {
            get => this[(y * 8) + x];
            set => this[(y * 8) + x] = value;
        }

        public static Block8x8 Load(Span<short> data)
        {
            Unsafe.SkipInit(out Block8x8 result);
            result.LoadFrom(data);
            return result;
        }

        /// <summary>
        /// Convert to <see cref="Block8x8F"/>
        /// </summary>
        public Block8x8F AsFloatBlock()
        {
            Block8x8F result = default;
            result.LoadFrom(ref this);
            return result;
        }

        /// <summary>
        /// Copy all elements to an array of <see cref="short"/>.
        /// </summary>
        public short[] ToArray()
        {
            short[] result = new short[Size];
            this.CopyTo(result);
            return result;
        }

        /// <summary>
        /// Copy elements into 'destination' Span of <see cref="short"/> values
        /// </summary>
        public void CopyTo(Span<short> destination)
        {
            ref byte selfRef = ref Unsafe.As<Block8x8, byte>(ref this);
            ref byte destRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<short, byte>(destination));
            Unsafe.CopyBlockUnaligned(ref destRef, ref selfRef, Size * sizeof(short));
        }

        /// <summary>
        /// Copy elements into 'destination' Span of <see cref="int"/> values
        /// </summary>
        public void CopyTo(Span<int> destination)
        {
            for (int i = 0; i < Size; i++)
            {
                destination[i] = this[i];
            }
        }

        public static Block8x8 Load(ReadOnlySpan<byte> data)
        {
            Unsafe.SkipInit(out Block8x8 result);
            result.LoadFrom(data);
            return result;
        }

        public void LoadFrom(ReadOnlySpan<byte> source)
        {
            for (int i = 0; i < Size; i++)
            {
                this[i] = source[i];
            }
        }

        /// <summary>
        /// Load raw 16bit integers from source.
        /// </summary>
        /// <param name="source">Source</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void LoadFrom(Span<short> source)
        {
            ref byte sourceRef = ref Unsafe.As<short, byte>(ref MemoryMarshal.GetReference(source));
            ref byte destRef = ref Unsafe.As<Block8x8, byte>(ref this);

            Unsafe.CopyBlockUnaligned(ref destRef, ref sourceRef, Size * sizeof(short));
        }

        /// <summary>
        /// Cast and copy <see cref="Size"/> <see cref="int"/>-s from the beginning of 'source' span.
        /// </summary>
        public void LoadFrom(Span<int> source)
        {
            for (int i = 0; i < Size; i++)
            {
                this[i] = (short)source[i];
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < Size; i++)
            {
                sb.Append(this[i]);
                if (i < Size - 1)
                {
                    sb.Append(',');
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Returns index of the last non-zero element in given matrix.
        /// </summary>
        /// <returns>
        /// Index of the last non-zero element. Returns -1 if all elements are equal to zero.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public nint GetLastNonZeroIndex()
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported)
            {
                const int equalityMask = unchecked((int)0b1111_1111_1111_1111_1111_1111_1111_1111);

                Vector256<short> zero16 = Vector256<short>.Zero;

                ref Vector256<short> mcuStride = ref Unsafe.As<Block8x8, Vector256<short>>(ref this);

                for (nint i = 3; i >= 0; i--)
                {
                    int areEqual = Avx2.MoveMask(Avx2.CompareEqual(Unsafe.Add(ref mcuStride, i), zero16).AsByte());

                    if (areEqual != equalityMask)
                    {
                        // Each 2 bits represents comparison operation for each 2-byte element in input vectors
                        // LSB represents first element in the stride
                        // MSB represents last element in the stride
                        // lzcnt operation would calculate number of zero numbers at the end

                        // Given mask is not actually suitable for lzcnt as 1's represent zero elements and 0's represent non-zero elements
                        // So we need to invert it
                        int lzcnt = BitOperations.LeadingZeroCount(~(uint)areEqual);

                        // As input number is represented by 2 bits in the mask, we need to divide lzcnt result by 2
                        // to get the exact number of zero elements in the stride
                        int strideRelativeIndex = 15 - (lzcnt / 2);
                        return (i * 16) + strideRelativeIndex;
                    }
                }

                return -1;
            }
            else
#endif
            {
                nint index = Size - 1;
                ref short elemRef = ref Unsafe.As<Block8x8, short>(ref this);

                while (index >= 0 && Unsafe.Add(ref elemRef, index) == 0)
                {
                    index--;
                }

                return index;
            }
        }

        /// <summary>
        /// Transpose the block inplace.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void TransposeInplace()
        {
            ref short elemRef = ref Unsafe.As<Block8x8, short>(ref this);

            // row #0
            Swap(ref Unsafe.Add(ref elemRef, 1), ref Unsafe.Add(ref elemRef, 8));
            Swap(ref Unsafe.Add(ref elemRef, 2), ref Unsafe.Add(ref elemRef, 16));
            Swap(ref Unsafe.Add(ref elemRef, 3), ref Unsafe.Add(ref elemRef, 24));
            Swap(ref Unsafe.Add(ref elemRef, 4), ref Unsafe.Add(ref elemRef, 32));
            Swap(ref Unsafe.Add(ref elemRef, 5), ref Unsafe.Add(ref elemRef, 40));
            Swap(ref Unsafe.Add(ref elemRef, 6), ref Unsafe.Add(ref elemRef, 48));
            Swap(ref Unsafe.Add(ref elemRef, 7), ref Unsafe.Add(ref elemRef, 56));

            // row #1
            Swap(ref Unsafe.Add(ref elemRef, 10), ref Unsafe.Add(ref elemRef, 17));
            Swap(ref Unsafe.Add(ref elemRef, 11), ref Unsafe.Add(ref elemRef, 25));
            Swap(ref Unsafe.Add(ref elemRef, 12), ref Unsafe.Add(ref elemRef, 33));
            Swap(ref Unsafe.Add(ref elemRef, 13), ref Unsafe.Add(ref elemRef, 41));
            Swap(ref Unsafe.Add(ref elemRef, 14), ref Unsafe.Add(ref elemRef, 49));
            Swap(ref Unsafe.Add(ref elemRef, 15), ref Unsafe.Add(ref elemRef, 57));

            // row #2
            Swap(ref Unsafe.Add(ref elemRef, 19), ref Unsafe.Add(ref elemRef, 26));
            Swap(ref Unsafe.Add(ref elemRef, 20), ref Unsafe.Add(ref elemRef, 34));
            Swap(ref Unsafe.Add(ref elemRef, 21), ref Unsafe.Add(ref elemRef, 42));
            Swap(ref Unsafe.Add(ref elemRef, 22), ref Unsafe.Add(ref elemRef, 50));
            Swap(ref Unsafe.Add(ref elemRef, 23), ref Unsafe.Add(ref elemRef, 58));

            // row #3
            Swap(ref Unsafe.Add(ref elemRef, 28), ref Unsafe.Add(ref elemRef, 35));
            Swap(ref Unsafe.Add(ref elemRef, 29), ref Unsafe.Add(ref elemRef, 43));
            Swap(ref Unsafe.Add(ref elemRef, 30), ref Unsafe.Add(ref elemRef, 51));
            Swap(ref Unsafe.Add(ref elemRef, 31), ref Unsafe.Add(ref elemRef, 59));

            // row #4
            Swap(ref Unsafe.Add(ref elemRef, 37), ref Unsafe.Add(ref elemRef, 44));
            Swap(ref Unsafe.Add(ref elemRef, 38), ref Unsafe.Add(ref elemRef, 52));
            Swap(ref Unsafe.Add(ref elemRef, 39), ref Unsafe.Add(ref elemRef, 60));

            // row #5
            Swap(ref Unsafe.Add(ref elemRef, 46), ref Unsafe.Add(ref elemRef, 53));
            Swap(ref Unsafe.Add(ref elemRef, 47), ref Unsafe.Add(ref elemRef, 61));

            // row #6
            Swap(ref Unsafe.Add(ref elemRef, 55), ref Unsafe.Add(ref elemRef, 62));

            static void Swap(ref short a, ref short b)
            {
                short tmp = a;
                a = b;
                b = tmp;
            }
        }

        /// <summary>
        /// Calculate the total sum of absolute differences of elements in 'a' and 'b'.
        /// </summary>
        public static long TotalDifference(ref Block8x8 a, ref Block8x8 b)
        {
            long result = 0;
            for (int i = 0; i < Size; i++)
            {
                int d = a[i] - b[i];
                result += Math.Abs(d);
            }

            return result;
        }
    }
}

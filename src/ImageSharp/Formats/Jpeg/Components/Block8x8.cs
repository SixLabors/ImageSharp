// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// 8x8 matrix of <see cref="short"/> coefficients.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct Block8x8 : IEquatable<Block8x8>
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

#if SUPPORTS_RUNTIME_INTRINSICS
        [FieldOffset(0)]
        public Vector128<short> V0;
        [FieldOffset(16)]
        public Vector128<short> V1;
        [FieldOffset(32)]
        public Vector128<short> V2;
        [FieldOffset(48)]
        public Vector128<short> V3;
        [FieldOffset(64)]
        public Vector128<short> V4;
        [FieldOffset(80)]
        public Vector128<short> V5;
        [FieldOffset(96)]
        public Vector128<short> V6;
        [FieldOffset(112)]
        public Vector128<short> V7;

        [FieldOffset(0)]
        public Vector256<short> V01;
        [FieldOffset(32)]
        public Vector256<short> V23;
        [FieldOffset(64)]
        public Vector256<short> V45;
        [FieldOffset(96)]
        public Vector256<short> V67;
#endif

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

        public static bool operator ==(Block8x8 left, Block8x8 right) => left.Equals(right);

        public static bool operator !=(Block8x8 left, Block8x8 right) => !left.Equals(right);

        /// <summary>
        /// Multiply all elements by a given <see cref="int"/>
        /// </summary>
        public static Block8x8 operator *(Block8x8 block, int value)
        {
            Block8x8 result = block;
            for (int i = 0; i < Size; i++)
            {
                int val = result[i];
                val *= value;
                result[i] = (short)val;
            }

            return result;
        }

        /// <summary>
        /// Divide all elements by a given <see cref="int"/>
        /// </summary>
        public static Block8x8 operator /(Block8x8 block, int value)
        {
            Block8x8 result = block;
            for (int i = 0; i < Size; i++)
            {
                int val = result[i];
                val /= value;
                result[i] = (short)val;
            }

            return result;
        }

        /// <summary>
        /// Add an <see cref="int"/> to all elements
        /// </summary>
        public static Block8x8 operator +(Block8x8 block, int value)
        {
            Block8x8 result = block;
            for (int i = 0; i < Size; i++)
            {
                int val = result[i];
                val += value;
                result[i] = (short)val;
            }

            return result;
        }

        /// <summary>
        /// Subtract an <see cref="int"/> from all elements
        /// </summary>
        public static Block8x8 operator -(Block8x8 block, int value)
        {
            Block8x8 result = block;
            for (int i = 0; i < Size; i++)
            {
                int val = result[i];
                val -= value;
                result[i] = (short)val;
            }

            return result;
        }

        public static Block8x8 Load(Span<short> data)
        {
            Block8x8 result = default;
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
            Unsafe.CopyBlock(ref destRef, ref selfRef, Size * sizeof(short));
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

        /// <summary>
        /// Load raw 16bit integers from source.
        /// </summary>
        /// <param name="source">Source</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void LoadFrom(Span<short> source)
        {
            ref byte s = ref Unsafe.As<short, byte>(ref MemoryMarshal.GetReference(source));
            ref byte d = ref Unsafe.As<Block8x8, byte>(ref this);

            Unsafe.CopyBlock(ref d, ref s, Size * sizeof(short));
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

        /// <inheritdoc />
        public bool Equals(Block8x8 other)
        {
            for (int i = 0; i < Size; i++)
            {
                if (this[i] != other[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Block8x8 other && this.Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (this[0] * 31) + this[1];

        /// <summary>
        /// Returns index of the last non-zero element in given matrix.
        /// </summary>
        /// <returns>
        /// Index of the last non-zero element. Returns -1 if all elements are equal to zero.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetLastNonZeroIndex()
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported)
            {
                const int equalityMask = unchecked((int)0b1111_1111_1111_1111_1111_1111_1111_1111);

                Vector256<short> zero16 = Vector256<short>.Zero;

                ref Vector256<short> mcuStride = ref Unsafe.As<Block8x8, Vector256<short>>(ref this);

                for (int i = 3; i >= 0; i--)
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
                int index = Size - 1;
                ref short elemRef = ref Unsafe.As<Block8x8, short>(ref this);

                while (index >= 0 && Unsafe.Add(ref elemRef, index) == 0)
                {
                    index--;
                }

                return index;
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

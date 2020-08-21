// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// Represents a Jpeg block with <see cref="short"/> coefficients.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal unsafe struct Block8x8 : IEquatable<Block8x8>
    {
        /// <summary>
        /// A number of scalar coefficients in a <see cref="Block8x8F"/>
        /// </summary>
        public const int Size = 64;

        /// <summary>
        /// A fixed size buffer holding the values.
        /// See: <see>
        ///         <cref>https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/unsafe-code-pointers/fixed-size-buffers</cref>
        ///     </see>
        /// </summary>
        private fixed short data[Size];

        /// <summary>
        /// Initializes a new instance of the <see cref="Block8x8"/> struct.
        /// </summary>
        /// <param name="coefficients">A <see cref="Span{T}"/> of coefficients</param>
        public Block8x8(Span<short> coefficients)
        {
            ref byte selfRef = ref Unsafe.As<Block8x8, byte>(ref this);
            ref byte sourceRef = ref Unsafe.As<short, byte>(ref MemoryMarshal.GetReference(coefficients));
            Unsafe.CopyBlock(ref selfRef, ref sourceRef, Size * sizeof(short));
        }

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
                GuardBlockIndex(idx);
                ref short selfRef = ref Unsafe.As<Block8x8, short>(ref this);
                return Unsafe.Add(ref selfRef, idx);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                GuardBlockIndex(idx);
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

        public static bool operator ==(Block8x8 left, Block8x8 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Block8x8 left, Block8x8 right)
        {
            return !left.Equals(right);
        }

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

        /// <summary>
        /// Pointer-based "Indexer" (getter part)
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="idx">Index</param>
        /// <returns>The scaleVec value at the specified index</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short GetScalarAt(Block8x8* blockPtr, int idx)
        {
            GuardBlockIndex(idx);

            short* fp = blockPtr->data;
            return fp[idx];
        }

        /// <summary>
        /// Pointer-based "Indexer" (setter part)
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="idx">Index</param>
        /// <param name="value">Value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetScalarAt(Block8x8* blockPtr, int idx, short value)
        {
            GuardBlockIndex(idx);

            short* fp = blockPtr->data;
            fp[idx] = value;
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
            var result = new short[Size];
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
        /// Cast and copy <see cref="Size"/> <see cref="int"/>-s from the beginning of 'source' span.
        /// </summary>
        public void LoadFrom(Span<int> source)
        {
            for (int i = 0; i < Size; i++)
            {
                this[i] = (short)source[i];
            }
        }

        [Conditional("DEBUG")]
        private static void GuardBlockIndex(int idx)
        {
            DebugGuard.MustBeLessThan(idx, Size, nameof(idx));
            DebugGuard.MustBeGreaterThanOrEqualTo(idx, 0, nameof(idx));
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
        public override bool Equals(object obj)
        {
            return obj is Block8x8 other && this.Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (this[0] * 31) + this[1];
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

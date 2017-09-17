using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    /// <summary>
    /// Represents a Jpeg block with <see cref="short"/> coefficiens.
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
            ref byte sourceRef = ref coefficients.NonPortableCast<short, byte>().DangerousGetPinnableReference();
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
        /// Gets or sets a value in a row+coulumn of the 8x8 block
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
            var result = default(Block8x8F);
            this.CopyToFloatBlock(ref result);
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
            ref byte destRef = ref destination.NonPortableCast<short, byte>().DangerousGetPinnableReference();
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
            var bld = new StringBuilder();
            bld.Append('[');
            for (int i = 0; i < Size; i++)
            {
                bld.Append(this[i]);
                if (i < Size - 1)
                {
                    bld.Append(',');
                }
            }

            bld.Append(']');
            return bld.ToString();
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
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is Block8x8 && this.Equals((Block8x8)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (this[0] * 31) + this[1];
        }

        /// <summary>
        /// Calculate the total sum of absoulute differences of elements in 'a' and 'b'.
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

        /// <summary>
        /// Convert values into <see cref="Block8x8F"/> as <see cref="float"/>-s
        /// </summary>
        public void CopyToFloatBlock(ref Block8x8F dest)
        {
            ref short selfRef = ref Unsafe.As<Block8x8, short>(ref this);

            dest.V0L.X = Unsafe.Add(ref selfRef, 0);
            dest.V0L.Y = Unsafe.Add(ref selfRef, 1);
            dest.V0L.Z = Unsafe.Add(ref selfRef, 2);
            dest.V0L.W = Unsafe.Add(ref selfRef, 3);
            dest.V0R.X = Unsafe.Add(ref selfRef, 4);
            dest.V0R.Y = Unsafe.Add(ref selfRef, 5);
            dest.V0R.Z = Unsafe.Add(ref selfRef, 6);
            dest.V0R.W = Unsafe.Add(ref selfRef, 7);

            dest.V1L.X = Unsafe.Add(ref selfRef, 8);
            dest.V1L.Y = Unsafe.Add(ref selfRef, 9);
            dest.V1L.Z = Unsafe.Add(ref selfRef, 10);
            dest.V1L.W = Unsafe.Add(ref selfRef, 11);
            dest.V1R.X = Unsafe.Add(ref selfRef, 12);
            dest.V1R.Y = Unsafe.Add(ref selfRef, 13);
            dest.V1R.Z = Unsafe.Add(ref selfRef, 14);
            dest.V1R.W = Unsafe.Add(ref selfRef, 15);

            dest.V2L.X = Unsafe.Add(ref selfRef, 16);
            dest.V2L.Y = Unsafe.Add(ref selfRef, 17);
            dest.V2L.Z = Unsafe.Add(ref selfRef, 18);
            dest.V2L.W = Unsafe.Add(ref selfRef, 19);
            dest.V2R.X = Unsafe.Add(ref selfRef, 20);
            dest.V2R.Y = Unsafe.Add(ref selfRef, 21);
            dest.V2R.Z = Unsafe.Add(ref selfRef, 22);
            dest.V2R.W = Unsafe.Add(ref selfRef, 23);

            dest.V3L.X = Unsafe.Add(ref selfRef, 24);
            dest.V3L.Y = Unsafe.Add(ref selfRef, 25);
            dest.V3L.Z = Unsafe.Add(ref selfRef, 26);
            dest.V3L.W = Unsafe.Add(ref selfRef, 27);
            dest.V3R.X = Unsafe.Add(ref selfRef, 28);
            dest.V3R.Y = Unsafe.Add(ref selfRef, 29);
            dest.V3R.Z = Unsafe.Add(ref selfRef, 30);
            dest.V3R.W = Unsafe.Add(ref selfRef, 31);

            dest.V4L.X = Unsafe.Add(ref selfRef, 32);
            dest.V4L.Y = Unsafe.Add(ref selfRef, 33);
            dest.V4L.Z = Unsafe.Add(ref selfRef, 34);
            dest.V4L.W = Unsafe.Add(ref selfRef, 35);
            dest.V4R.X = Unsafe.Add(ref selfRef, 36);
            dest.V4R.Y = Unsafe.Add(ref selfRef, 37);
            dest.V4R.Z = Unsafe.Add(ref selfRef, 38);
            dest.V4R.W = Unsafe.Add(ref selfRef, 39);

            dest.V5L.X = Unsafe.Add(ref selfRef, 40);
            dest.V5L.Y = Unsafe.Add(ref selfRef, 41);
            dest.V5L.Z = Unsafe.Add(ref selfRef, 42);
            dest.V5L.W = Unsafe.Add(ref selfRef, 43);
            dest.V5R.X = Unsafe.Add(ref selfRef, 44);
            dest.V5R.Y = Unsafe.Add(ref selfRef, 45);
            dest.V5R.Z = Unsafe.Add(ref selfRef, 46);
            dest.V5R.W = Unsafe.Add(ref selfRef, 47);

            dest.V6L.X = Unsafe.Add(ref selfRef, 48);
            dest.V6L.Y = Unsafe.Add(ref selfRef, 49);
            dest.V6L.Z = Unsafe.Add(ref selfRef, 50);
            dest.V6L.W = Unsafe.Add(ref selfRef, 51);
            dest.V6R.X = Unsafe.Add(ref selfRef, 52);
            dest.V6R.Y = Unsafe.Add(ref selfRef, 53);
            dest.V6R.Z = Unsafe.Add(ref selfRef, 54);
            dest.V6R.W = Unsafe.Add(ref selfRef, 55);

            dest.V7L.X = Unsafe.Add(ref selfRef, 56);
            dest.V7L.Y = Unsafe.Add(ref selfRef, 57);
            dest.V7L.Z = Unsafe.Add(ref selfRef, 58);
            dest.V7L.W = Unsafe.Add(ref selfRef, 59);
            dest.V7R.X = Unsafe.Add(ref selfRef, 60);
            dest.V7R.Y = Unsafe.Add(ref selfRef, 61);
            dest.V7R.Z = Unsafe.Add(ref selfRef, 62);
            dest.V7R.W = Unsafe.Add(ref selfRef, 63);
        }
    }
}
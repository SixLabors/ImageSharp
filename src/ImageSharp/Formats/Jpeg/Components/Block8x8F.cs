// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// Represents a Jpeg block with <see cref="float"/> coefficients.
    /// </summary>
    internal partial struct Block8x8F
    {
        /// <summary>
        /// A number of scalar coefficients in a <see cref="Block8x8F"/>
        /// </summary>
        public const int Size = 64;

#pragma warning disable SA1600 // ElementsMustBeDocumented
        public Vector4 V0L;
        public Vector4 V0R;

        public Vector4 V1L;
        public Vector4 V1R;

        public Vector4 V2L;
        public Vector4 V2R;

        public Vector4 V3L;
        public Vector4 V3R;

        public Vector4 V4L;
        public Vector4 V4R;

        public Vector4 V5L;
        public Vector4 V5R;

        public Vector4 V6L;
        public Vector4 V6R;

        public Vector4 V7L;
        public Vector4 V7R;
#pragma warning restore SA1600 // ElementsMustBeDocumented

        private static readonly Vector4 NegativeOne = new Vector4(-1);
        private static readonly Vector4 Offset = new Vector4(.5F);

        /// <summary>
        /// Get/Set scalar elements at a given index
        /// </summary>
        /// <param name="idx">The index</param>
        /// <returns>The float value at the specified index</returns>
        public float this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                GuardBlockIndex(idx);
                ref float selfRef = ref Unsafe.As<Block8x8F, float>(ref this);
                return Unsafe.Add(ref selfRef, idx);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                GuardBlockIndex(idx);
                ref float selfRef = ref Unsafe.As<Block8x8F, float>(ref this);
                Unsafe.Add(ref selfRef, idx) = value;
            }
        }

        public float this[int x, int y]
        {
            get => this[(y * 8) + x];
            set => this[(y * 8) + x] = value;
        }

        public static Block8x8F operator *(Block8x8F block, float value)
        {
            Block8x8F result = block;
            for (int i = 0; i < Size; i++)
            {
                float val = result[i];
                val *= value;
                result[i] = val;
            }

            return result;
        }

        public static Block8x8F operator /(Block8x8F block, float value)
        {
            Block8x8F result = block;
            for (int i = 0; i < Size; i++)
            {
                float val = result[i];
                val /= value;
                result[i] = val;
            }

            return result;
        }

        public static Block8x8F operator +(Block8x8F block, float value)
        {
            Block8x8F result = block;
            for (int i = 0; i < Size; i++)
            {
                float val = result[i];
                val += value;
                result[i] = val;
            }

            return result;
        }

        public static Block8x8F operator -(Block8x8F block, float value)
        {
            Block8x8F result = block;
            for (int i = 0; i < Size; i++)
            {
                float val = result[i];
                val -= value;
                result[i] = val;
            }

            return result;
        }

        public static Block8x8F Load(Span<float> data)
        {
            Block8x8F result = default;
            result.LoadFrom(data);
            return result;
        }

        public static Block8x8F Load(Span<int> data)
        {
            Block8x8F result = default;
            result.LoadFrom(data);
            return result;
        }

        /// <summary>
        /// Fill the block with defaults (zeroes)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            // The cheapest way to do this in C#:
            this = default;
        }

        /// <summary>
        /// Load raw 32bit floating point data from source
        /// </summary>
        /// <param name="source">Source</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LoadFrom(Span<float> source)
        {
            ref byte s = ref Unsafe.As<float, byte>(ref MemoryMarshal.GetReference(source));
            ref byte d = ref Unsafe.As<Block8x8F, byte>(ref this);

            Unsafe.CopyBlock(ref d, ref s, Size * sizeof(float));
        }

        /// <summary>
        /// Load raw 32bit floating point data from source
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="source">Source</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void LoadFrom(Block8x8F* blockPtr, Span<float> source)
        {
            blockPtr->LoadFrom(source);
        }

        /// <summary>
        /// Load raw 32bit floating point data from source
        /// </summary>
        /// <param name="source">Source</param>
        public unsafe void LoadFrom(Span<int> source)
        {
            fixed (Vector4* ptr = &this.V0L)
            {
                float* fp = (float*)ptr;
                for (int i = 0; i < Size; i++)
                {
                    fp[i] = source[i];
                }
            }
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        /// <param name="dest">Destination</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(Span<float> dest)
        {
            ref byte d = ref Unsafe.As<float, byte>(ref MemoryMarshal.GetReference(dest));
            ref byte s = ref Unsafe.As<Block8x8F, byte>(ref this);

            Unsafe.CopyBlock(ref d, ref s, Size * sizeof(float));
        }

        /// <summary>
        /// Convert salars to byte-s and copy to dest
        /// </summary>
        /// <param name="blockPtr">Pointer to block</param>
        /// <param name="dest">Destination</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CopyTo(Block8x8F* blockPtr, Span<byte> dest)
        {
            float* fPtr = (float*)blockPtr;
            for (int i = 0; i < Size; i++)
            {
                dest[i] = (byte)*fPtr;
                fPtr++;
            }
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="dest">Destination</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CopyTo(Block8x8F* blockPtr, Span<float> dest)
        {
            blockPtr->CopyTo(dest);
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        /// <param name="dest">Destination</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo(float[] dest)
        {
            fixed (void* ptr = &this.V0L)
            {
                Marshal.Copy((IntPtr)ptr, dest, 0, Size);
            }
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        /// <param name="dest">Destination</param>
        public unsafe void CopyTo(Span<int> dest)
        {
            fixed (Vector4* ptr = &this.V0L)
            {
                float* fp = (float*)ptr;
                for (int i = 0; i < Size; i++)
                {
                    dest[i] = (int)fp[i];
                }
            }
        }

        public float[] ToArray()
        {
            float[] result = new float[Size];
            this.CopyTo(result);
            return result;
        }

        /// <summary>
        /// Multiply all elements of the block.
        /// </summary>
        /// <param name="value">The value to multiply by</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyInplace(float value)
        {
            this.V0L *= value;
            this.V0R *= value;
            this.V1L *= value;
            this.V1R *= value;
            this.V2L *= value;
            this.V2R *= value;
            this.V3L *= value;
            this.V3R *= value;
            this.V4L *= value;
            this.V4R *= value;
            this.V5L *= value;
            this.V5R *= value;
            this.V6L *= value;
            this.V6R *= value;
            this.V7L *= value;
            this.V7R *= value;
        }

        /// <summary>
        /// Multiply all elements of the block by the corresponding elements of 'other'
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyInplace(ref Block8x8F other)
        {
            this.V0L *= other.V0L;
            this.V0R *= other.V0R;
            this.V1L *= other.V1L;
            this.V1R *= other.V1R;
            this.V2L *= other.V2L;
            this.V2R *= other.V2R;
            this.V3L *= other.V3L;
            this.V3R *= other.V3R;
            this.V4L *= other.V4L;
            this.V4R *= other.V4R;
            this.V5L *= other.V5L;
            this.V5R *= other.V5R;
            this.V6L *= other.V6L;
            this.V6R *= other.V6R;
            this.V7L *= other.V7L;
            this.V7R *= other.V7R;
        }

        /// <summary>
        /// Adds a vector to all elements of the block.
        /// </summary>
        /// <param name="diff">The added vector</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToAllInplace(Vector4 diff)
        {
            this.V0L += diff;
            this.V0R += diff;
            this.V1L += diff;
            this.V1R += diff;
            this.V2L += diff;
            this.V2R += diff;
            this.V3L += diff;
            this.V3R += diff;
            this.V4L += diff;
            this.V4R += diff;
            this.V5L += diff;
            this.V5R += diff;
            this.V6L += diff;
            this.V6R += diff;
            this.V7L += diff;
            this.V7R += diff;
        }

        /// <summary>
        /// Quantize the block.
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="qtPtr">Qt pointer</param>
        /// <param name="unzigPtr">Unzig pointer</param>
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void DequantizeBlock(Block8x8F* blockPtr, Block8x8F* qtPtr, byte* unzigPtr)
        {
            float* b = (float*)blockPtr;
            float* qtp = (float*)qtPtr;
            for (int qtIndex = 0; qtIndex < Size; qtIndex++)
            {
                byte blockIndex = unzigPtr[qtIndex];
                float* unzigPos = b + blockIndex;

                float val = *unzigPos;
                val *= qtp[qtIndex];
                *unzigPos = val;
            }
        }

        /// <summary>
        /// Quantize 'block' into 'dest' using the 'qt' quantization table:
        /// Unzig the elements of block into dest, while dividing them by elements of qt and "pre-rounding" the values.
        /// To finish the rounding it's enough to (int)-cast these values.
        /// </summary>
        /// <param name="block">Source block</param>
        /// <param name="dest">Destination block</param>
        /// <param name="qt">The quantization table</param>
        /// <param name="unzigPtr">Pointer to elements of <see cref="ZigZag"/></param>
        public static unsafe void Quantize(
            Block8x8F* block,
            Block8x8F* dest,
            Block8x8F* qt,
            byte* unzigPtr)
        {
            float* s = (float*)block;
            float* d = (float*)dest;

            for (int zig = 0; zig < Size; zig++)
            {
                d[zig] = s[unzigPtr[zig]];
            }

            DivideRoundAll(ref *dest, ref *qt);
        }

        /// <summary>
        /// Scales the 16x16 region represented by the 4 source blocks to the 8x8  DST block.
        /// </summary>
        /// <param name="destination">The destination block.</param>
        /// <param name="source">The source block.</param>
        public static unsafe void Scale16X16To8X8(Block8x8F* destination, Block8x8F* source)
        {
            float* d = (float*)destination;
            for (int i = 0; i < 4; i++)
            {
                int dstOff = ((i & 2) << 4) | ((i & 1) << 2);

                float* iSource = (float*)(source + i);

                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        int j = (16 * y) + (2 * x);
                        float sum = iSource[j] + iSource[j + 1] + iSource[j + 8] + iSource[j + 9];
                        d[(8 * y) + x + dstOff] = (sum + 2) / 4;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DivideRoundAll(ref Block8x8F a, ref Block8x8F b)
        {
            a.V0L = DivideRound(a.V0L, b.V0L);
            a.V0R = DivideRound(a.V0R, b.V0R);
            a.V1L = DivideRound(a.V1L, b.V1L);
            a.V1R = DivideRound(a.V1R, b.V1R);
            a.V2L = DivideRound(a.V2L, b.V2L);
            a.V2R = DivideRound(a.V2R, b.V2R);
            a.V3L = DivideRound(a.V3L, b.V3L);
            a.V3R = DivideRound(a.V3R, b.V3R);
            a.V4L = DivideRound(a.V4L, b.V4L);
            a.V4R = DivideRound(a.V4R, b.V4R);
            a.V5L = DivideRound(a.V5L, b.V5L);
            a.V5R = DivideRound(a.V5R, b.V5R);
            a.V6L = DivideRound(a.V6L, b.V6L);
            a.V6R = DivideRound(a.V6R, b.V6R);
            a.V7L = DivideRound(a.V7L, b.V7L);
            a.V7R = DivideRound(a.V7R, b.V7R);
        }

        public void RoundInto(ref Block8x8 dest)
        {
            for (int i = 0; i < Size; i++)
            {
                float val = this[i];
                if (val < 0)
                {
                    val -= 0.5f;
                }
                else
                {
                    val += 0.5f;
                }

                dest[i] = (short)val;
            }
        }

        public Block8x8 RoundAsInt16Block()
        {
            Block8x8 result = default;
            this.RoundInto(ref result);
            return result;
        }

        /// <summary>
        /// Level shift by +128, clip to [0..255], and round all the values in the block.
        /// </summary>
        public void NormalizeColorsAndRoundInplace()
        {
            if (SimdUtils.IsAvx2CompatibleArchitecture)
            {
                this.NormalizeColorsAndRoundInplaceAvx2();
            }
            else
            {
                this.NormalizeColorsInplace();
                this.RoundInplace();
            }
        }

        /// <summary>
        /// Rounds all values in the block.
        /// </summary>
        public void RoundInplace()
        {
            for (int i = 0; i < Size; i++)
            {
                this[i] = MathF.Round(this[i]);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<float> NormalizeAndRound(Vector<float> row, Vector<float> off, Vector<float> max)
        {
            row += off;
            row = Vector.Max(row, Vector<float>.Zero);
            row = Vector.Min(row, max);
            return row.FastRound();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 DivideRound(Vector4 dividend, Vector4 divisor)
        {
            // sign(dividend) = max(min(dividend, 1), -1)
            var sign = Vector4.Clamp(dividend, NegativeOne, Vector4.One);

            // AlmostRound(dividend/divisor) = dividend/divisior + 0.5*sign(dividend)
            return (dividend / divisor) + (sign * Offset);
        }

        [Conditional("DEBUG")]
        private static void GuardBlockIndex(int idx)
        {
            DebugGuard.MustBeLessThan(idx, Size, nameof(idx));
            DebugGuard.MustBeGreaterThanOrEqualTo(idx, 0, nameof(idx));
        }
    }
}

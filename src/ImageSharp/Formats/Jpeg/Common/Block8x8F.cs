// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Memory;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    /// <summary>
    /// Represents a Jpeg block with <see cref="float"/> coefficients.
    /// </summary>
    internal partial struct Block8x8F
    {
        // Most of the static methods of this struct are instance methods by actual semantics: they use Block8x8F* as their first parameter.
        // Example: GetScalarAt() and SetScalarAt() are really just other (optimized) versions of the indexer.
        // It's much cleaner, easier and safer to work with the code, if the methods with same semantics are next to each other.
#pragma warning disable SA1204 // StaticElementsMustAppearBeforeInstanceElements

        /// <summary>
        /// Vector count
        /// </summary>
        public const int VectorCount = 16;

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
                result[i] = (float)val;
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
                result[i] = (float)val;
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
                result[i] = (float)val;
            }

            return result;
        }

        public static Block8x8F Load(Span<float> data)
        {
            var result = default(Block8x8F);
            result.LoadFrom(data);
            return result;
        }

        public static Block8x8F Load(Span<int> data)
        {
            var result = default(Block8x8F);
            result.LoadFrom(data);
            return result;
        }

        /// <summary>
        /// Pointer-based "Indexer" (getter part)
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="idx">Index</param>
        /// <returns>The scaleVec value at the specified index</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float GetScalarAt(Block8x8F* blockPtr, int idx)
        {
            GuardBlockIndex(idx);

            float* fp = (float*)blockPtr;
            return fp[idx];
        }

        /// <summary>
        /// Pointer-based "Indexer" (setter part)
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="idx">Index</param>
        /// <param name="value">Value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetScalarAt(Block8x8F* blockPtr, int idx, float value)
        {
            GuardBlockIndex(idx);

            float* fp = (float*)blockPtr;
            fp[idx] = value;
        }

        /// <summary>
        /// Fill the block with defaults (zeroes)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            // The cheapest way to do this in C#:
            this = default(Block8x8F);
        }

        /// <summary>
        /// Load raw 32bit floating point data from source
        /// </summary>
        /// <param name="source">Source</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LoadFrom(Span<float> source)
        {
            ref byte s = ref Unsafe.As<float, byte>(ref source.DangerousGetPinnableReference());
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
        public unsafe void CopyTo(Span<float> dest)
        {
            ref byte d = ref Unsafe.As<float, byte>(ref dest.DangerousGetPinnableReference());
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

        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(BufferArea<float> area)
        {
            ref byte selfBase = ref Unsafe.As<Block8x8F, byte>(ref this);
            ref byte destBase = ref Unsafe.As<float, byte>(ref area.GetReferenceToOrigo());
            int destStride = area.Stride * sizeof(float);

            CopyRowImpl(ref selfBase, ref destBase, destStride, 0);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 1);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 2);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 3);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 4);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 5);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 6);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 7);
        }

        public void CopyTo(BufferArea<float> area, int horizontalScale, int verticalScale)
        {
            if (horizontalScale == 1 && verticalScale == 1)
            {
                this.CopyTo(area);
                return;
            }
            else if (horizontalScale == 2 && verticalScale == 2)
            {
                this.CopyTo2x2(area);
                return;
            }

            // TODO: Optimize: implement all the cases with loopless special code! (T4?)
            for (int y = 0; y < 8; y++)
            {
                int yy = y * verticalScale;
                int y8 = y * 8;

                for (int x = 0; x < 8; x++)
                {
                    int xx = x * horizontalScale;

                    float value = this[y8 + x];

                    for (int i = 0; i < verticalScale; i++)
                    {
                        for (int j = 0; j < horizontalScale; j++)
                        {
                            area[xx + j, yy + i] = value;
                        }
                    }
                }
            }
        }

        private void CopyTo2x2(BufferArea<float> area)
        {
            ref float destBase = ref area.GetReferenceToOrigo();
            int destStride = area.Stride;

            this.CopyRow2x2Impl(ref destBase, 0, destStride);
            this.CopyRow2x2Impl(ref destBase, 1, destStride);
            this.CopyRow2x2Impl(ref destBase, 2, destStride);
            this.CopyRow2x2Impl(ref destBase, 3, destStride);
            this.CopyRow2x2Impl(ref destBase, 4, destStride);
            this.CopyRow2x2Impl(ref destBase, 5, destStride);
            this.CopyRow2x2Impl(ref destBase, 6, destStride);
            this.CopyRow2x2Impl(ref destBase, 7, destStride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyRowImpl(ref byte selfBase, ref byte destBase, int destStride, int row)
        {
            ref byte s = ref Unsafe.Add(ref selfBase, row * 8 * sizeof(float));
            ref byte d = ref Unsafe.Add(ref destBase, row * destStride);
            Unsafe.CopyBlock(ref d, ref s, 8 * sizeof(float));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyRow2x2Impl(ref float destBase, int row, int destStride)
        {
            ref Vector4 selfLeft = ref Unsafe.Add(ref this.V0L, 2 * row);
            ref Vector4 selfRight = ref Unsafe.Add(ref selfLeft, 1);
            ref float destLocalOrigo = ref Unsafe.Add(ref destBase, row * 2 * destStride);

            Stride2VectorCopyImpl(ref selfLeft, ref destLocalOrigo);
            Stride2VectorCopyImpl(ref selfRight, ref Unsafe.Add(ref destLocalOrigo, 8));

            Stride2VectorCopyImpl(ref selfLeft, ref Unsafe.Add(ref destLocalOrigo, destStride));
            Stride2VectorCopyImpl(ref selfRight, ref Unsafe.Add(ref destLocalOrigo, destStride + 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Stride2VectorCopyImpl(ref Vector4 s, ref float destBase)
        {
            Unsafe.Add(ref destBase, 0) = s.X;
            Unsafe.Add(ref destBase, 1) = s.X;
            Unsafe.Add(ref destBase, 2) = s.Y;
            Unsafe.Add(ref destBase, 3) = s.Y;
            Unsafe.Add(ref destBase, 4) = s.Z;
            Unsafe.Add(ref destBase, 5) = s.Z;
            Unsafe.Add(ref destBase, 6) = s.W;
            Unsafe.Add(ref destBase, 7) = s.W;
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
        /// <param name="scaleVec">Vector to multiply by</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyAllInplace(float scaleVec)
        {
            this.V0L *= scaleVec;
            this.V0R *= scaleVec;
            this.V1L *= scaleVec;
            this.V1R *= scaleVec;
            this.V2L *= scaleVec;
            this.V2R *= scaleVec;
            this.V3L *= scaleVec;
            this.V3R *= scaleVec;
            this.V4L *= scaleVec;
            this.V4R *= scaleVec;
            this.V5L *= scaleVec;
            this.V5R *= scaleVec;
            this.V6L *= scaleVec;
            this.V6R *= scaleVec;
            this.V7L *= scaleVec;
            this.V7R *= scaleVec;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void DequantizeBlock(Block8x8F* blockPtr, Block8x8F* qtPtr, int* unzigPtr)
        {
            float* b = (float*)blockPtr;
            float* qtp = (float*)qtPtr;
            for (int zig = 0; zig < Size; zig++)
            {
                float* unzigPos = b + unzigPtr[zig];
                float val = *unzigPos;
                val *= qtp[zig];
                *unzigPos = val;
            }
        }

        /// <summary>
        /// Level shift by +128, clip to [0, 255], and write to buffer.
        /// </summary>
        /// <param name="destinationBuffer">Color buffer</param>
        /// <param name="stride">Stride offset</param>
        /// <param name="tempBlockPtr">Temp Block pointer</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyColorsTo(Span<byte> destinationBuffer, int stride, Block8x8F* tempBlockPtr)
        {
            this.NormalizeColorsInto(ref *tempBlockPtr);
            ref byte d = ref destinationBuffer.DangerousGetPinnableReference();
            float* src = (float*)tempBlockPtr;
            for (int i = 0; i < 8; i++)
            {
                ref byte dRow = ref Unsafe.Add(ref d, i * stride);
                Unsafe.Add(ref dRow, 0) = (byte)src[0];
                Unsafe.Add(ref dRow, 1) = (byte)src[1];
                Unsafe.Add(ref dRow, 2) = (byte)src[2];
                Unsafe.Add(ref dRow, 3) = (byte)src[3];
                Unsafe.Add(ref dRow, 4) = (byte)src[4];
                Unsafe.Add(ref dRow, 5) = (byte)src[5];
                Unsafe.Add(ref dRow, 6) = (byte)src[6];
                Unsafe.Add(ref dRow, 7) = (byte)src[7];
                src += 8;
            }
        }

        /// <summary>
        /// Unzig the elements of block into dest, while dividing them by elements of qt and "pre-rounding" the values.
        /// To finish the rounding it's enough to (int)-cast these values.
        /// </summary>
        /// <param name="block">Source block</param>
        /// <param name="dest">Destination block</param>
        /// <param name="qt">The quantization table</param>
        /// <param name="unzigPtr">Pointer to elements of <see cref="UnzigData"/></param>
        public static unsafe void UnzigDivRound(
            Block8x8F* block,
            Block8x8F* dest,
            Block8x8F* qt,
            int* unzigPtr)
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
            var result = default(Block8x8);
            this.RoundInto(ref result);
            return result;
        }

        public void RoundInplace()
        {
            if (Vector<float>.Count == 8 && Vector<int>.Count == 8)
            {
                ref Vector<float> row0 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V0L);
                row0 = row0.FastRound();
                ref Vector<float> row1 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V1L);
                row1 = row1.FastRound();
                ref Vector<float> row2 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V2L);
                row2 = row2.FastRound();
                ref Vector<float> row3 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V3L);
                row3 = row3.FastRound();
                ref Vector<float> row4 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V4L);
                row4 = row4.FastRound();
                ref Vector<float> row5 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V5L);
                row5 = row5.FastRound();
                ref Vector<float> row6 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V6L);
                row6 = row6.FastRound();
                ref Vector<float> row7 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V7L);
                row7 = row7.FastRound();
            }
            else
            {
                this.RoundInplaceSlow();
            }
        }

        private void RoundInplaceSlow()
        {
            for (int i = 0; i < Size; i++)
            {
                this[i] = MathF.Round(this[i]);
            }
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

        [StructLayout(LayoutKind.Explicit, Size = 8 * sizeof(float))]
        private struct Row
        {
        }
    }
}

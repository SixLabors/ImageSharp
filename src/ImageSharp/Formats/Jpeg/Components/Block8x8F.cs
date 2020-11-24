// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using System.Text;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// Represents a Jpeg block with <see cref="float"/> coefficients.
    /// </summary>
    internal partial struct Block8x8F : IEquatable<Block8x8F>
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
            [MethodImpl(InliningOptions.ShortMethod)]
            get
            {
                GuardBlockIndex(idx);
                ref float selfRef = ref Unsafe.As<Block8x8F, float>(ref this);
                return Unsafe.Add(ref selfRef, idx);
            }

            [MethodImpl(InliningOptions.ShortMethod)]
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
        /// Fill the block with defaults (zeroes).
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Clear()
        {
            // The cheapest way to do this in C#:
            this = default;
        }

        /// <summary>
        /// Load raw 32bit floating point data from source.
        /// </summary>
        /// <param name="source">Source</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void LoadFrom(Span<float> source)
        {
            ref byte s = ref Unsafe.As<float, byte>(ref MemoryMarshal.GetReference(source));
            ref byte d = ref Unsafe.As<Block8x8F, byte>(ref this);

            Unsafe.CopyBlock(ref d, ref s, Size * sizeof(float));
        }

        /// <summary>
        /// Load raw 32bit floating point data from source.
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="source">Source</param>
        [MethodImpl(InliningOptions.ShortMethod)]
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
        /// Copy raw 32bit floating point data to dest,
        /// </summary>
        /// <param name="dest">Destination</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ScaledCopyTo(Span<float> dest)
        {
            ref byte d = ref Unsafe.As<float, byte>(ref MemoryMarshal.GetReference(dest));
            ref byte s = ref Unsafe.As<Block8x8F, byte>(ref this);

            Unsafe.CopyBlock(ref d, ref s, Size * sizeof(float));
        }

        /// <summary>
        /// Convert scalars to byte-s and copy to dest,
        /// </summary>
        /// <param name="blockPtr">Pointer to block</param>
        /// <param name="dest">Destination</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static unsafe void ScaledCopyTo(Block8x8F* blockPtr, Span<byte> dest)
        {
            float* fPtr = (float*)blockPtr;
            for (int i = 0; i < Size; i++)
            {
                dest[i] = (byte)*fPtr;
                fPtr++;
            }
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest.
        /// </summary>
        /// <param name="blockPtr">The block pointer.</param>
        /// <param name="dest">The destination.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static unsafe void ScaledCopyTo(Block8x8F* blockPtr, Span<float> dest)
        {
            blockPtr->ScaledCopyTo(dest);
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        /// <param name="dest">Destination</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public unsafe void ScaledCopyTo(float[] dest)
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
        public unsafe void ScaledCopyTo(Span<int> dest)
        {
            fixed (Vector4* ptr = &this.V0L)
            {
                var fp = (float*)ptr;
                for (int i = 0; i < Size; i++)
                {
                    dest[i] = (int)fp[i];
                }
            }
        }

        public float[] ToArray()
        {
            var result = new float[Size];
            this.ScaledCopyTo(result);
            return result;
        }

        /// <summary>
        /// Multiply all elements of the block.
        /// </summary>
        /// <param name="value">The value to multiply by.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void MultiplyInPlace(float value)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx.IsSupported)
            {
                var valueVec = Vector256.Create(value);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V0L) = Avx.Multiply(Unsafe.As<Vector4, Vector256<float>>(ref this.V0L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V1L) = Avx.Multiply(Unsafe.As<Vector4, Vector256<float>>(ref this.V1L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V2L) = Avx.Multiply(Unsafe.As<Vector4, Vector256<float>>(ref this.V2L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V3L) = Avx.Multiply(Unsafe.As<Vector4, Vector256<float>>(ref this.V3L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V4L) = Avx.Multiply(Unsafe.As<Vector4, Vector256<float>>(ref this.V4L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V5L) = Avx.Multiply(Unsafe.As<Vector4, Vector256<float>>(ref this.V5L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V6L) = Avx.Multiply(Unsafe.As<Vector4, Vector256<float>>(ref this.V6L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V7L) = Avx.Multiply(Unsafe.As<Vector4, Vector256<float>>(ref this.V7L), valueVec);
            }
            else
#endif
            {
                var valueVec = new Vector4(value);
                this.V0L *= valueVec;
                this.V0R *= valueVec;
                this.V1L *= valueVec;
                this.V1R *= valueVec;
                this.V2L *= valueVec;
                this.V2R *= valueVec;
                this.V3L *= valueVec;
                this.V3R *= valueVec;
                this.V4L *= valueVec;
                this.V4R *= valueVec;
                this.V5L *= valueVec;
                this.V5R *= valueVec;
                this.V6L *= valueVec;
                this.V6R *= valueVec;
                this.V7L *= valueVec;
                this.V7R *= valueVec;
            }
        }

        /// <summary>
        /// Multiply all elements of the block by the corresponding elements of 'other'.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public unsafe void MultiplyInPlace(ref Block8x8F other)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx.IsSupported)
            {
                Unsafe.As<Vector4, Vector256<float>>(ref this.V0L)
                    = Avx.Multiply(
                        Unsafe.As<Vector4, Vector256<float>>(ref this.V0L),
                        Unsafe.As<Vector4, Vector256<float>>(ref other.V0L));

                Unsafe.As<Vector4, Vector256<float>>(ref this.V1L)
                    = Avx.Multiply(
                        Unsafe.As<Vector4, Vector256<float>>(ref this.V1L),
                        Unsafe.As<Vector4, Vector256<float>>(ref other.V1L));

                Unsafe.As<Vector4, Vector256<float>>(ref this.V2L)
                    = Avx.Multiply(
                        Unsafe.As<Vector4, Vector256<float>>(ref this.V2L),
                        Unsafe.As<Vector4, Vector256<float>>(ref other.V2L));

                Unsafe.As<Vector4, Vector256<float>>(ref this.V3L)
                    = Avx.Multiply(
                        Unsafe.As<Vector4, Vector256<float>>(ref this.V3L),
                        Unsafe.As<Vector4, Vector256<float>>(ref other.V3L));

                Unsafe.As<Vector4, Vector256<float>>(ref this.V4L)
                    = Avx.Multiply(
                        Unsafe.As<Vector4, Vector256<float>>(ref this.V4L),
                        Unsafe.As<Vector4, Vector256<float>>(ref other.V4L));

                Unsafe.As<Vector4, Vector256<float>>(ref this.V5L)
                    = Avx.Multiply(
                        Unsafe.As<Vector4, Vector256<float>>(ref this.V5L),
                        Unsafe.As<Vector4, Vector256<float>>(ref other.V5L));

                Unsafe.As<Vector4, Vector256<float>>(ref this.V6L)
                    = Avx.Multiply(
                        Unsafe.As<Vector4, Vector256<float>>(ref this.V6L),
                        Unsafe.As<Vector4, Vector256<float>>(ref other.V6L));

                Unsafe.As<Vector4, Vector256<float>>(ref this.V7L)
                    = Avx.Multiply(
                        Unsafe.As<Vector4, Vector256<float>>(ref this.V7L),
                        Unsafe.As<Vector4, Vector256<float>>(ref other.V7L));
            }
            else
#endif
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
        }

        /// <summary>
        /// Adds a vector to all elements of the block.
        /// </summary>
        /// <param name="value">The added vector.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void AddInPlace(float value)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx.IsSupported)
            {
                var valueVec = Vector256.Create(value);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V0L) = Avx.Add(Unsafe.As<Vector4, Vector256<float>>(ref this.V0L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V1L) = Avx.Add(Unsafe.As<Vector4, Vector256<float>>(ref this.V1L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V2L) = Avx.Add(Unsafe.As<Vector4, Vector256<float>>(ref this.V2L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V3L) = Avx.Add(Unsafe.As<Vector4, Vector256<float>>(ref this.V3L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V4L) = Avx.Add(Unsafe.As<Vector4, Vector256<float>>(ref this.V4L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V5L) = Avx.Add(Unsafe.As<Vector4, Vector256<float>>(ref this.V5L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V6L) = Avx.Add(Unsafe.As<Vector4, Vector256<float>>(ref this.V6L), valueVec);
                Unsafe.As<Vector4, Vector256<float>>(ref this.V7L) = Avx.Add(Unsafe.As<Vector4, Vector256<float>>(ref this.V7L), valueVec);
            }
            else
#endif
            {
                var valueVec = new Vector4(value);
                this.V0L += valueVec;
                this.V0R += valueVec;
                this.V1L += valueVec;
                this.V1R += valueVec;
                this.V2L += valueVec;
                this.V2R += valueVec;
                this.V3L += valueVec;
                this.V3R += valueVec;
                this.V4L += valueVec;
                this.V4R += valueVec;
                this.V5L += valueVec;
                this.V5R += valueVec;
                this.V6L += valueVec;
                this.V6R += valueVec;
                this.V7L += valueVec;
                this.V7R += valueVec;
            }
        }

        /// <summary>
        /// Quantize the block.
        /// </summary>
        /// <param name="blockPtr">The block pointer.</param>
        /// <param name="qtPtr">The qt pointer.</param>
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
        /// <param name="unZig">The 8x8 Unzig block.</param>
        public static unsafe void Quantize(
            ref Block8x8F block,
            ref Block8x8F dest,
            ref Block8x8F qt,
            ref ZigZag unZig)
        {
            for (int zig = 0; zig < Size; zig++)
            {
                dest[zig] = block[unZig[zig]];
            }

            DivideRoundAll(ref dest, ref qt);
        }

        /// <summary>
        /// Scales the 16x16 region represented by the 4 source blocks to the 8x8 DST block.
        /// </summary>
        /// <param name="destination">The destination block.</param>
        /// <param name="source">The source block.</param>
        public static unsafe void Scale16X16To8X8(ref Block8x8F destination, ReadOnlySpan<Block8x8F> source)
        {
            for (int i = 0; i < 4; i++)
            {
                int dstOff = ((i & 2) << 4) | ((i & 1) << 2);
                Block8x8F iSource = source[i];

                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        int j = (16 * y) + (2 * x);
                        float sum = iSource[j] + iSource[j + 1] + iSource[j + 8] + iSource[j + 9];
                        destination[(8 * y) + x + dstOff] = (sum + 2) * .25F;
                    }
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
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
        /// Level shift by +maximum/2, clip to [0..maximum], and round all the values in the block.
        /// </summary>
        public void NormalizeColorsAndRoundInPlace(float maximum)
        {
            if (SimdUtils.HasVector8)
            {
                this.NormalizeColorsAndRoundInPlaceVector8(maximum);
            }
            else
            {
                this.NormalizeColorsInPlace(maximum);
                this.RoundInPlace();
            }
        }

        /// <summary>
        /// Rounds all values in the block.
        /// </summary>
        public void RoundInPlace()
        {
            for (int i = 0; i < Size; i++)
            {
                this[i] = MathF.Round(this[i]);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void LoadFrom(ref Block8x8 source)
        {
#if SUPPORTS_EXTENDED_INTRINSICS
            if (SimdUtils.HasVector8)
            {
                this.LoadFromInt16ExtendedAvx2(ref source);
                return;
            }
#endif
            this.LoadFromInt16Scalar(ref source);
        }

        /// <summary>
        /// Loads values from <paramref name="source"/> using extended AVX2 intrinsics.
        /// </summary>
        /// <param name="source">The source <see cref="Block8x8"/></param>
        public void LoadFromInt16ExtendedAvx2(ref Block8x8 source)
        {
            DebugGuard.IsTrue(
                SimdUtils.HasVector8,
                "LoadFromUInt16ExtendedAvx2 only works on AVX2 compatible architecture!");

            ref Vector<short> sRef = ref Unsafe.As<Block8x8, Vector<short>>(ref source);
            ref Vector<float> dRef = ref Unsafe.As<Block8x8F, Vector<float>>(ref this);

            // Vector<ushort>.Count == 16 on AVX2
            // We can process 2 block rows in a single step
            SimdUtils.ExtendedIntrinsics.ConvertToSingle(sRef, out Vector<float> top, out Vector<float> bottom);
            dRef = top;
            Unsafe.Add(ref dRef, 1) = bottom;

            SimdUtils.ExtendedIntrinsics.ConvertToSingle(Unsafe.Add(ref sRef, 1), out top, out bottom);
            Unsafe.Add(ref dRef, 2) = top;
            Unsafe.Add(ref dRef, 3) = bottom;

            SimdUtils.ExtendedIntrinsics.ConvertToSingle(Unsafe.Add(ref sRef, 2), out top, out bottom);
            Unsafe.Add(ref dRef, 4) = top;
            Unsafe.Add(ref dRef, 5) = bottom;

            SimdUtils.ExtendedIntrinsics.ConvertToSingle(Unsafe.Add(ref sRef, 3), out top, out bottom);
            Unsafe.Add(ref dRef, 6) = top;
            Unsafe.Add(ref dRef, 7) = bottom;
        }

        /// <inheritdoc />
        public bool Equals(Block8x8F other)
        {
            return this.V0L == other.V0L
            && this.V0R == other.V0R
            && this.V1L == other.V1L
            && this.V1R == other.V1R
            && this.V2L == other.V2L
            && this.V2R == other.V2R
            && this.V3L == other.V3L
            && this.V3R == other.V3R
            && this.V4L == other.V4L
            && this.V4R == other.V4R
            && this.V5L == other.V5L
            && this.V5R == other.V5R
            && this.V6L == other.V6L
            && this.V6R == other.V6R
            && this.V7L == other.V7L
            && this.V7R == other.V7R;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < Size - 1; i++)
            {
                sb.Append(this[i]);
                sb.Append(',');
            }

            sb.Append(this[Size - 1]);

            sb.Append(']');
            return sb.ToString();
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static Vector<float> NormalizeAndRound(Vector<float> row, Vector<float> off, Vector<float> max)
        {
            row += off;
            row = Vector.Max(row, Vector<float>.Zero);
            row = Vector.Min(row, max);
            return row.FastRound();
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static Vector4 DivideRound(Vector4 dividend, Vector4 divisor)
        {
            // sign(dividend) = max(min(dividend, 1), -1)
            Vector4 sign = Numerics.Clamp(dividend, NegativeOne, Vector4.One);

            // AlmostRound(dividend/divisor) = dividend/divisor + 0.5*sign(dividend)
            return (dividend / divisor) + (sign * Offset);
        }

        [Conditional("DEBUG")]
        private static void GuardBlockIndex(int idx)
        {
            DebugGuard.MustBeLessThan(idx, Size, nameof(idx));
            DebugGuard.MustBeGreaterThanOrEqualTo(idx, 0, nameof(idx));
        }

        /// <summary>
        /// Transpose the block into the destination block.
        /// </summary>
        /// <param name="d">The destination block</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void TransposeInto(ref Block8x8F d)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx.IsSupported)
            {
                // https://stackoverflow.com/questions/25622745/transpose-an-8x8-float-using-avx-avx2/25627536#25627536
                Vector256<float> r0 = Avx.InsertVector128(
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V0L).ToVector256(),
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V4L),
                   1);

                Vector256<float> r1 = Avx.InsertVector128(
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V1L).ToVector256(),
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V5L),
                   1);

                Vector256<float> r2 = Avx.InsertVector128(
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V2L).ToVector256(),
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V6L),
                   1);

                Vector256<float> r3 = Avx.InsertVector128(
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V3L).ToVector256(),
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V7L),
                   1);

                Vector256<float> r4 = Avx.InsertVector128(
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V0R).ToVector256(),
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V4R),
                   1);

                Vector256<float> r5 = Avx.InsertVector128(
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V1R).ToVector256(),
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V5R),
                   1);

                Vector256<float> r6 = Avx.InsertVector128(
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V2R).ToVector256(),
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V6R),
                   1);

                Vector256<float> r7 = Avx.InsertVector128(
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V3R).ToVector256(),
                   Unsafe.As<Vector4, Vector128<float>>(ref this.V7R),
                   1);

                Vector256<float> t0 = Avx.UnpackLow(r0, r1);
                Vector256<float> t2 = Avx.UnpackLow(r2, r3);
                Vector256<float> v = Avx.Shuffle(t0, t2, 0x4E);
                Unsafe.As<Vector4, Vector256<float>>(ref d.V0L) = Avx.Blend(t0, v, 0xCC);
                Unsafe.As<Vector4, Vector256<float>>(ref d.V1L) = Avx.Blend(t2, v, 0x33);

                Vector256<float> t4 = Avx.UnpackLow(r4, r5);
                Vector256<float> t6 = Avx.UnpackLow(r6, r7);
                v = Avx.Shuffle(t4, t6, 0x4E);
                Unsafe.As<Vector4, Vector256<float>>(ref d.V4L) = Avx.Blend(t4, v, 0xCC);
                Unsafe.As<Vector4, Vector256<float>>(ref d.V5L) = Avx.Blend(t6, v, 0x33);

                Vector256<float> t1 = Avx.UnpackHigh(r0, r1);
                Vector256<float> t3 = Avx.UnpackHigh(r2, r3);
                v = Avx.Shuffle(t1, t3, 0x4E);
                Unsafe.As<Vector4, Vector256<float>>(ref d.V2L) = Avx.Blend(t1, v, 0xCC);
                Unsafe.As<Vector4, Vector256<float>>(ref d.V3L) = Avx.Blend(t3, v, 0x33);

                Vector256<float> t5 = Avx.UnpackHigh(r4, r5);
                Vector256<float> t7 = Avx.UnpackHigh(r6, r7);
                v = Avx.Shuffle(t5, t7, 0x4E);
                Unsafe.As<Vector4, Vector256<float>>(ref d.V6L) = Avx.Blend(t5, v, 0xCC);
                Unsafe.As<Vector4, Vector256<float>>(ref d.V7L) = Avx.Blend(t7, v, 0x33);
            }
            else
#endif
            {
                d.V0L.X = this.V0L.X;
                d.V1L.X = this.V0L.Y;
                d.V2L.X = this.V0L.Z;
                d.V3L.X = this.V0L.W;
                d.V4L.X = this.V0R.X;
                d.V5L.X = this.V0R.Y;
                d.V6L.X = this.V0R.Z;
                d.V7L.X = this.V0R.W;

                d.V0L.Y = this.V1L.X;
                d.V1L.Y = this.V1L.Y;
                d.V2L.Y = this.V1L.Z;
                d.V3L.Y = this.V1L.W;
                d.V4L.Y = this.V1R.X;
                d.V5L.Y = this.V1R.Y;
                d.V6L.Y = this.V1R.Z;
                d.V7L.Y = this.V1R.W;

                d.V0L.Z = this.V2L.X;
                d.V1L.Z = this.V2L.Y;
                d.V2L.Z = this.V2L.Z;
                d.V3L.Z = this.V2L.W;
                d.V4L.Z = this.V2R.X;
                d.V5L.Z = this.V2R.Y;
                d.V6L.Z = this.V2R.Z;
                d.V7L.Z = this.V2R.W;

                d.V0L.W = this.V3L.X;
                d.V1L.W = this.V3L.Y;
                d.V2L.W = this.V3L.Z;
                d.V3L.W = this.V3L.W;
                d.V4L.W = this.V3R.X;
                d.V5L.W = this.V3R.Y;
                d.V6L.W = this.V3R.Z;
                d.V7L.W = this.V3R.W;

                d.V0R.X = this.V4L.X;
                d.V1R.X = this.V4L.Y;
                d.V2R.X = this.V4L.Z;
                d.V3R.X = this.V4L.W;
                d.V4R.X = this.V4R.X;
                d.V5R.X = this.V4R.Y;
                d.V6R.X = this.V4R.Z;
                d.V7R.X = this.V4R.W;

                d.V0R.Y = this.V5L.X;
                d.V1R.Y = this.V5L.Y;
                d.V2R.Y = this.V5L.Z;
                d.V3R.Y = this.V5L.W;
                d.V4R.Y = this.V5R.X;
                d.V5R.Y = this.V5R.Y;
                d.V6R.Y = this.V5R.Z;
                d.V7R.Y = this.V5R.W;

                d.V0R.Z = this.V6L.X;
                d.V1R.Z = this.V6L.Y;
                d.V2R.Z = this.V6L.Z;
                d.V3R.Z = this.V6L.W;
                d.V4R.Z = this.V6R.X;
                d.V5R.Z = this.V6R.Y;
                d.V6R.Z = this.V6R.Z;
                d.V7R.Z = this.V6R.W;

                d.V0R.W = this.V7L.X;
                d.V1R.W = this.V7L.Y;
                d.V2R.W = this.V7L.Z;
                d.V3R.W = this.V7L.W;
                d.V4R.W = this.V7R.X;
                d.V5R.W = this.V7R.Y;
                d.V6R.W = this.V7R.Z;
                d.V7R.W = this.V7R.W;
            }
        }
    }
}

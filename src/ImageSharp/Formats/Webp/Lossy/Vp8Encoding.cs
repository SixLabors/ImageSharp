// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Methods for encoding a VP8 frame.
    /// </summary>
    internal static unsafe class Vp8Encoding
    {
        private const int KC1 = 20091 + (1 << 16);

        private const int KC2 = 35468;

        private static readonly byte[] Clip1 = new byte[255 + 510 + 1]; // clips [-255,510] to [0,255]

        private const int I16DC16 = 0 * 16 * WebpConstants.Bps;

        private const int I16TM16 = I16DC16 + 16;

        private const int I16VE16 = 1 * 16 * WebpConstants.Bps;

        private const int I16HE16 = I16VE16 + 16;

        private const int C8DC8 = 2 * 16 * WebpConstants.Bps;

        private const int C8TM8 = C8DC8 + (1 * 16);

        private const int C8VE8 = (2 * 16 * WebpConstants.Bps) + (8 * WebpConstants.Bps);

        private const int C8HE8 = C8VE8 + (1 * 16);

        public static readonly int[] Vp8I16ModeOffsets = { I16DC16, I16TM16, I16VE16, I16HE16 };

        public static readonly int[] Vp8UvModeOffsets = { C8DC8, C8TM8, C8VE8, C8HE8 };

        private const int I4DC4 = (3 * 16 * WebpConstants.Bps) + 0;

        private const int I4TM4 = I4DC4 + 4;

        private const int I4VE4 = I4DC4 + 8;

        private const int I4HE4 = I4DC4 + 12;

        private const int I4RD4 = I4DC4 + 16;

        private const int I4VR4 = I4DC4 + 20;

        private const int I4LD4 = I4DC4 + 24;

        private const int I4VL4 = I4DC4 + 28;

        private const int I4HD4 = (3 * 16 * WebpConstants.Bps) + (4 * WebpConstants.Bps);

        private const int I4HU4 = I4HD4 + 4;

        public static readonly int[] Vp8I4ModeOffsets = { I4DC4, I4TM4, I4VE4, I4HE4, I4RD4, I4VR4, I4LD4, I4VL4, I4HD4, I4HU4 };

#if SUPPORTS_RUNTIME_INTRINSICS
        public static readonly Vector128<short> K1 = Vector128.Create((short)20091).AsInt16();

        public static readonly Vector128<short> K2 = Vector128.Create((short)-30068).AsInt16();

        public static readonly Vector128<short> Four = Vector128.Create((short)4);
#endif

        static Vp8Encoding()
        {
            for (int i = -255; i <= 255 + 255; i++)
            {
                Clip1[255 + i] = Clip8b(i);
            }
        }

        // Transforms (Paragraph 14.4)
        // Does one or two inverse transforms.
        public static void ITransform(Span<byte> reference, Span<short> input, Span<byte> dst, bool doTwo, Span<int> scratch)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Sse2.IsSupported)
            //if (false)
            {
                // This implementation makes use of 16-bit fixed point versions of two
                // multiply constants:
                //    K1 = sqrt(2) * cos (pi/8) ~= 85627 / 2^16
                //    K2 = sqrt(2) * sin (pi/8) ~= 35468 / 2^16
                //
                // To be able to use signed 16-bit integers, we use the following trick to
                // have constants within range:
                // - Associated constants are obtained by subtracting the 16-bit fixed point
                //   version of one:
                //      k = K - (1 << 16)  =>  K = k + (1 << 16)
                //      K1 = 85267  =>  k1 =  20091
                //      K2 = 35468  =>  k2 = -30068
                // - The multiplication of a variable by a constant become the sum of the
                //   variable and the multiplication of that variable by the associated
                //   constant:
                //      (x * K) >> 16 = (x * (k + (1 << 16))) >> 16 = ((x * k ) >> 16) + x

                // Load and concatenate the transform coefficients (we'll do two inverse
                // transforms in parallel). In the case of only one inverse transform, the
                // second half of the vectors will just contain random value we'll never
                // use nor store.
                ref short inputRef = ref MemoryMarshal.GetReference(input);
                var in0 = Vector128.Create(Unsafe.As<short, long>(ref inputRef), 0);
                var in1 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 4)), 0);
                var in2 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 8)), 0);
                var in3 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 12)), 0);

                // a00 a10 a20 a30   x x x x
                // a01 a11 a21 a31   x x x x
                // a02 a12 a22 a32   x x x x
                // a03 a13 a23 a33   x x x x
                if (doTwo)
                {
                    var inb0 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 16)), 0);
                    var inb1 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 20)), 0);
                    var inb2 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 24)), 0);
                    var inb3 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 28)), 0);

                    in0 = Sse2.UnpackLow(in0, inb0);
                    in1 = Sse2.UnpackLow(in1, inb1);
                    in2 = Sse2.UnpackLow(in2, inb2);
                    in3 = Sse2.UnpackLow(in3, inb3);

                    // a00 a10 a20 a30   b00 b10 b20 b30
                    // a01 a11 a21 a31   b01 b11 b21 b31
                    // a02 a12 a22 a32   b02 b12 b22 b32
                    // a03 a13 a23 a33   b03 b13 b23 b33
                }

                // Vertical pass and subsequent transpose.
                // First pass, c and d calculations are longer because of the "trick" multiplications.
                Vector128<short> a = Sse2.Add(in0.AsInt16(), in2.AsInt16());
                Vector128<short> b = Sse2.Subtract(in0.AsInt16(), in2.AsInt16());

                // c = MUL(in1, K2) - MUL(in3, K1) = MUL(in1, k2) - MUL(in3, k1) + in1 - in3
                Vector128<short> c1 = Sse2.MultiplyHigh(in1.AsInt16(), K2.AsInt16());
                Vector128<short> c2 = Sse2.MultiplyHigh(in3.AsInt16(), K1.AsInt16());
                Vector128<long> c3 = Sse2.Subtract(in1, in3);
                Vector128<short> c4 = Sse2.Subtract(c1, c2);
                Vector128<short> c = Sse2.Add(c3.AsInt16(), c4.AsInt16());

                // d = MUL(in1, K1) + MUL(in3, K2) = MUL(in1, k1) + MUL(in3, k2) + in1 + in3
                Vector128<short> d1 = Sse2.MultiplyHigh(in1.AsInt16(), K1.AsInt16());
                Vector128<short> d2 = Sse2.MultiplyHigh(in3.AsInt16(), K2.AsInt16());
                Vector128<long> d3 = Sse2.Add(in1, in3);
                Vector128<short> d4 = Sse2.Add(d1, d2);
                Vector128<short> d = Sse2.Add(d3.AsInt16(), d4.AsInt16());

                // Second pass.
                Vector128<short> tmp0 = Sse2.Add(a, d);
                Vector128<short> tmp1 = Sse2.Add(b, c);
                Vector128<short> tmp2 = Sse2.Subtract(b, c);
                Vector128<short> tmp3 = Sse2.Subtract(a, d);

                // Transpose the two 4x4.
                LossyUtils.Vp8Transpose_2_4x4_16b(tmp0, tmp1, tmp2, tmp3, out Vector128<long> t0, out Vector128<long> t1, out Vector128<long> t2, out Vector128<long> t3);

                // Horizontal pass and subsequent transpose.
                // First pass, c and d calculations are longer because of the "trick" multiplications.
                Vector128<short> dc = Sse2.Add(t0.AsInt16(), Four);
                a = Sse2.Add(dc, t2.AsInt16());
                b = Sse2.Subtract(dc, t2.AsInt16());

                // c = MUL(T1, K2) - MUL(T3, K1) = MUL(T1, k2) - MUL(T3, k1) + T1 - T3
                c1 = Sse2.MultiplyHigh(t1.AsInt16(), K2);
                c2 = Sse2.MultiplyHigh(t3.AsInt16(), K1);
                c3 = Sse2.Subtract(t1, t3);
                c4 = Sse2.Subtract(c1, c2);
                c = Sse2.Add(c3.AsInt16(), c4);

                // d = MUL(T1, K1) + MUL(T3, K2) = MUL(T1, k1) + MUL(T3, k2) + T1 + T3
                d1 = Sse2.MultiplyHigh(t1.AsInt16(), K1);
                d2 = Sse2.MultiplyHigh(t3.AsInt16(), K2);
                d3 = Sse2.Add(t1, t3);
                d4 = Sse2.Add(d1, d2);
                d = Sse2.Add(d3.AsInt16(), d4);

                // Second pass.
                tmp0 = Sse2.Add(a, d);
                tmp1 = Sse2.Add(b, c);
                tmp2 = Sse2.Subtract(b, c);
                tmp3 = Sse2.Subtract(a, d);
                Vector128<short> shifted0 = Sse2.ShiftRightArithmetic(tmp0, 3);
                Vector128<short> shifted1 = Sse2.ShiftRightArithmetic(tmp1, 3);
                Vector128<short> shifted2 = Sse2.ShiftRightArithmetic(tmp2, 3);
                Vector128<short> shifted3 = Sse2.ShiftRightArithmetic(tmp3, 3);

                // Transpose the two 4x4.
                LossyUtils.Vp8Transpose_2_4x4_16b(shifted0, shifted1, shifted2, shifted3, out t0, out t1, out t2, out t3);

                // Add inverse transform to 'ref' and store.
                // Load the reference(s).
                Vector128<byte> ref0 = Vector128<byte>.Zero;
                Vector128<byte> ref1 = Vector128<byte>.Zero;
                Vector128<byte> ref2 = Vector128<byte>.Zero;
                Vector128<byte> ref3 = Vector128<byte>.Zero;
                ref byte referenceRef = ref MemoryMarshal.GetReference(reference);
                if (doTwo)
                {
                    // Load eight bytes/pixels per line.
                    ref0 = Vector128.Create(Unsafe.As<byte, long>(ref referenceRef), 0).AsByte();
                    ref1 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps)), 0).AsByte();
                    ref2 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 2)), 0).AsByte();
                    ref3 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 3)), 0).AsByte();
                }
                else
                {
                    // Load four bytes/pixels per line.
                    ref0 = Sse2.ConvertScalarToVector128Int32(Unsafe.As<byte, int>(ref referenceRef)).AsByte();
                    ref1 = Sse2.ConvertScalarToVector128Int32(Unsafe.As<byte, int>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps))).AsByte();
                    ref2 = Sse2.ConvertScalarToVector128Int32(Unsafe.As<byte, int>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 2))).AsByte();
                    ref3 = Sse2.ConvertScalarToVector128Int32(Unsafe.As<byte, int>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 3))).AsByte();
                }

                // Convert to 16b.
                ref0 = Sse2.UnpackLow(ref0, Vector128<byte>.Zero);
                ref1 = Sse2.UnpackLow(ref1, Vector128<byte>.Zero);
                ref2 = Sse2.UnpackLow(ref2, Vector128<byte>.Zero);
                ref3 = Sse2.UnpackLow(ref3, Vector128<byte>.Zero);

                // Add the inverse transform(s).
                Vector128<ushort> ref0InvAdded = Sse2.Add(ref0.AsUInt16(), t0.AsUInt16());
                Vector128<ushort> ref1InvAdded = Sse2.Add(ref1.AsUInt16(), t1.AsUInt16());
                Vector128<ushort> ref2InvAdded = Sse2.Add(ref2.AsUInt16(), t2.AsUInt16());
                Vector128<ushort> ref3InvAdded = Sse2.Add(ref3.AsUInt16(), t3.AsUInt16());

                // Unsigned saturate to 8b.
                ref0 = Sse2.PackUnsignedSaturate(ref0InvAdded.AsInt16(), ref0InvAdded.AsInt16());
                ref1 = Sse2.PackUnsignedSaturate(ref1InvAdded.AsInt16(), ref1InvAdded.AsInt16());
                ref2 = Sse2.PackUnsignedSaturate(ref2InvAdded.AsInt16(), ref2InvAdded.AsInt16());
                ref3 = Sse2.PackUnsignedSaturate(ref3InvAdded.AsInt16(), ref3InvAdded.AsInt16());

                // Unsigned saturate to 8b.
                if (doTwo)
                {
                    // Store eight bytes/pixels per line.
                    // TODO: avoid pinning, if possible.
                    fixed (byte* dstPtr = dst)
                    {
                        Sse2.StoreScalar((long*)dstPtr, ref0.AsInt64());
                        Sse2.StoreScalar((long*)(dstPtr + WebpConstants.Bps), ref0.AsInt64());
                        Sse2.StoreScalar((long*)(dstPtr + (WebpConstants.Bps * 2)), ref0.AsInt64());
                        Sse2.StoreScalar((long*)(dstPtr + (WebpConstants.Bps * 3)), ref0.AsInt64());
                    }
                }
                else
                {
                    // Store four bytes/pixels per line.
                    int output0 = Sse2.ConvertToInt32(ref0.AsInt32());
                    int output1 = Sse2.ConvertToInt32(ref1.AsInt32());
                    int output2 = Sse2.ConvertToInt32(ref2.AsInt32());
                    int output3 = Sse2.ConvertToInt32(ref3.AsInt32());

                    ref byte outputRef = ref MemoryMarshal.GetReference(dst);
                    Unsafe.As<byte, int>(ref outputRef) = output0;
                    Unsafe.As<byte, int>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps)) = output1;
                    Unsafe.As<byte, int>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps * 2)) = output2;
                    Unsafe.As<byte, int>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps * 3)) = output3;
                }
            }
            else
#endif
            {
                ITransformOne(reference, input, dst, scratch);
                if (doTwo)
                {
                    ITransformOne(reference.Slice(4), input.Slice(16), dst.Slice(4), scratch);
                }
            }
        }

        public static void ITransformOne(Span<byte> reference, Span<short> input, Span<byte> dst, Span<int> scratch)
        {
            int i;
            Span<int> tmp = scratch.Slice(0, 16);
            for (i = 0; i < 4; i++)
            {
                // vertical pass.
                int a = input[0] + input[8];
                int b = input[0] - input[8];
                int c = Mul(input[4], KC2) - Mul(input[12], KC1);
                int d = Mul(input[4], KC1) + Mul(input[12], KC2);
                tmp[0] = a + d;
                tmp[1] = b + c;
                tmp[2] = b - c;
                tmp[3] = a - d;
                tmp = tmp.Slice(4);
                input = input.Slice(1);
            }

            tmp = scratch;
            for (i = 0; i < 4; i++)
            {
                // horizontal pass.
                int dc = tmp[0] + 4;
                int a = dc + tmp[8];
                int b = dc - tmp[8];
                int c = Mul(tmp[4], KC2) - Mul(tmp[12], KC1);
                int d = Mul(tmp[4], KC1) + Mul(tmp[12], KC2);
                Store(dst, reference, 0, i, a + d);
                Store(dst, reference, 1, i, b + c);
                Store(dst, reference, 2, i, b - c);
                Store(dst, reference, 3, i, a - d);
                tmp = tmp.Slice(1);
            }
        }

        public static void FTransform2(Span<byte> src, Span<byte> reference, Span<short> output, Span<short> output2, Span<int> scratch)
        {
            FTransform(src, reference, output, scratch);
            FTransform(src.Slice(4), reference.Slice(4), output2, scratch);
        }

        public static void FTransform(Span<byte> src, Span<byte> reference, Span<short> output, Span<int> scratch)
        {
            int i;
            Span<int> tmp = scratch.Slice(0, 16);

            int srcIdx = 0;
            int refIdx = 0;
            for (i = 0; i < 4; i++)
            {
                int d0 = src[srcIdx] - reference[refIdx];   // 9bit dynamic range ([-255,255])
                int d1 = src[srcIdx + 1] - reference[refIdx + 1];
                int d2 = src[srcIdx + 2] - reference[refIdx + 2];
                int d3 = src[srcIdx + 3] - reference[refIdx + 3];
                int a0 = d0 + d3;         // 10b                      [-510,510]
                int a1 = d1 + d2;
                int a2 = d1 - d2;
                int a3 = d0 - d3;
                tmp[0 + (i * 4)] = (a0 + a1) * 8;   // 14b                      [-8160,8160]
                tmp[1 + (i * 4)] = ((a2 * 2217) + (a3 * 5352) + 1812) >> 9;      // [-7536,7542]
                tmp[2 + (i * 4)] = (a0 - a1) * 8;
                tmp[3 + (i * 4)] = ((a3 * 2217) - (a2 * 5352) + 937) >> 9;

                srcIdx += WebpConstants.Bps;
                refIdx += WebpConstants.Bps;
            }

            for (i = 0; i < 4; i++)
            {
                int a0 = tmp[0 + i] + tmp[12 + i];  // 15b
                int a1 = tmp[4 + i] + tmp[8 + i];
                int a2 = tmp[4 + i] - tmp[8 + i];
                int a3 = tmp[0 + i] - tmp[12 + i];
                output[0 + i] = (short)((a0 + a1 + 7) >> 4);            // 12b
                output[4 + i] = (short)((((a2 * 2217) + (a3 * 5352) + 12000) >> 16) + (a3 != 0 ? 1 : 0));
                output[8 + i] = (short)((a0 - a1 + 7) >> 4);
                output[12 + i] = (short)(((a3 * 2217) - (a2 * 5352) + 51000) >> 16);
            }
        }

        public static void FTransformWht(Span<short> input, Span<short> output, Span<int> scratch)
        {
            Span<int> tmp = scratch.Slice(0, 16);

            int i;
            int inputIdx = 0;
            for (i = 0; i < 4; i++)
            {
                int a0 = input[inputIdx + (0 * 16)] + input[inputIdx + (2 * 16)];  // 13b
                int a1 = input[inputIdx + (1 * 16)] + input[inputIdx + (3 * 16)];
                int a2 = input[inputIdx + (1 * 16)] - input[inputIdx + (3 * 16)];
                int a3 = input[inputIdx + (0 * 16)] - input[inputIdx + (2 * 16)];
                tmp[0 + (i * 4)] = a0 + a1;   // 14b
                tmp[1 + (i * 4)] = a3 + a2;
                tmp[2 + (i * 4)] = a3 - a2;
                tmp[3 + (i * 4)] = a0 - a1;

                inputIdx += 64;
            }

            for (i = 0; i < 4; i++)
            {
                int a0 = tmp[0 + i] + tmp[8 + i];  // 15b
                int a1 = tmp[4 + i] + tmp[12 + i];
                int a2 = tmp[4 + i] - tmp[12 + i];
                int a3 = tmp[0 + i] - tmp[8 + i];
                int b0 = a0 + a1;    // 16b
                int b1 = a3 + a2;
                int b2 = a3 - a2;
                int b3 = a0 - a1;
                output[0 + i] = (short)(b0 >> 1);     // 15b
                output[4 + i] = (short)(b1 >> 1);
                output[8 + i] = (short)(b2 >> 1);
                output[12 + i] = (short)(b3 >> 1);
            }
        }

        // luma 16x16 prediction (paragraph 12.3).
        public static void EncPredLuma16(Span<byte> dst, Span<byte> left, Span<byte> top)
        {
            DcMode(dst.Slice(I16DC16), left, top, 16, 16, 5);
            VerticalPred(dst.Slice(I16VE16), top, 16);
            HorizontalPred(dst.Slice(I16HE16), left, 16);
            TrueMotion(dst.Slice(I16TM16), left, top, 16);
        }

        // Chroma 8x8 prediction (paragraph 12.2).
        public static void EncPredChroma8(Span<byte> dst, Span<byte> left, Span<byte> top)
        {
            // U block.
            DcMode(dst.Slice(C8DC8), left, top, 8, 8, 4);
            VerticalPred(dst.Slice(C8VE8), top, 8);
            HorizontalPred(dst.Slice(C8HE8), left, 8);
            TrueMotion(dst.Slice(C8TM8), left, top, 8);

            // V block.
            dst = dst.Slice(8);
            if (top != null)
            {
                top = top.Slice(8);
            }

            if (left != null)
            {
                left = left.Slice(16);
            }

            DcMode(dst.Slice(C8DC8), left, top, 8, 8, 4);
            VerticalPred(dst.Slice(C8VE8), top, 8);
            HorizontalPred(dst.Slice(C8HE8), left, 8);
            TrueMotion(dst.Slice(C8TM8), left, top, 8);
        }

        // Left samples are top[-5 .. -2], top_left is top[-1], top are
        // located at top[0..3], and top right is top[4..7]
        public static void EncPredLuma4(Span<byte> dst, Span<byte> top, int topOffset, Span<byte> vals)
        {
            Dc4(dst.Slice(I4DC4), top, topOffset);
            Tm4(dst.Slice(I4TM4), top, topOffset);
            Ve4(dst.Slice(I4VE4), top, topOffset, vals);
            He4(dst.Slice(I4HE4), top, topOffset);
            Rd4(dst.Slice(I4RD4), top, topOffset);
            Vr4(dst.Slice(I4VR4), top, topOffset);
            Ld4(dst.Slice(I4LD4), top, topOffset);
            Vl4(dst.Slice(I4VL4), top, topOffset);
            Hd4(dst.Slice(I4HD4), top, topOffset);
            Hu4(dst.Slice(I4HU4), top, topOffset);
        }

        private static void VerticalPred(Span<byte> dst, Span<byte> top, int size)
        {
            if (top != null)
            {
                for (int j = 0; j < size; j++)
                {
                    top.Slice(0, size).CopyTo(dst.Slice(j * WebpConstants.Bps));
                }
            }
            else
            {
                Fill(dst, 127, size);
            }
        }

        public static void HorizontalPred(Span<byte> dst, Span<byte> left, int size)
        {
            if (left != null)
            {
                left = left.Slice(1); // in the reference implementation, left starts at - 1.
                for (int j = 0; j < size; j++)
                {
                    dst.Slice(j * WebpConstants.Bps, size).Fill(left[j]);
                }
            }
            else
            {
                Fill(dst, 129, size);
            }
        }

        public static void TrueMotion(Span<byte> dst, Span<byte> left, Span<byte> top, int size)
        {
            if (left != null)
            {
                if (top != null)
                {
                    Span<byte> clip = Clip1.AsSpan(255 - left[0]); // left [0] instead of left[-1], original left starts at -1
                    for (int y = 0; y < size; y++)
                    {
                        Span<byte> clipTable = clip.Slice(left[y + 1]); // left[y]
                        for (int x = 0; x < size; x++)
                        {
                            dst[x] = clipTable[top[x]];
                        }

                        dst = dst.Slice(WebpConstants.Bps);
                    }
                }
                else
                {
                    HorizontalPred(dst, left, size);
                }
            }
            else
            {
                // true motion without left samples (hence: with default 129 value)
                // is equivalent to VE prediction where you just copy the top samples.
                // Note that if top samples are not available, the default value is
                // then 129, and not 127 as in the VerticalPred case.
                if (top != null)
                {
                    VerticalPred(dst, top, size);
                }
                else
                {
                    Fill(dst, 129, size);
                }
            }
        }

        private static void DcMode(Span<byte> dst, Span<byte> left, Span<byte> top, int size, int round, int shift)
        {
            int dc = 0;
            int j;
            if (top != null)
            {
                for (j = 0; j < size; j++)
                {
                    dc += top[j];
                }

                if (left != null)
                {
                    // top and left present.
                    left = left.Slice(1); // in the reference implementation, left starts at -1.
                    for (j = 0; j < size; j++)
                    {
                        dc += left[j];
                    }
                }
                else
                {
                    // top, but no left.
                    dc += dc;
                }

                dc = (dc + round) >> shift;
            }
            else if (left != null)
            {
                // left but no top.
                left = left.Slice(1); // in the reference implementation, left starts at -1.
                for (j = 0; j < size; j++)
                {
                    dc += left[j];
                }

                dc += dc;
                dc = (dc + round) >> shift;
            }
            else
            {
                // no top, no left, nothing.
                dc = 0x80;
            }

            Fill(dst, dc, size);
        }

        private static void Dc4(Span<byte> dst, Span<byte> top, int topOffset)
        {
            uint dc = 4;
            int i;
            for (i = 0; i < 4; i++)
            {
                dc += (uint)(top[topOffset + i] + top[topOffset - 5 + i]);
            }

            Fill(dst, (int)(dc >> 3), 4);
        }

        private static void Tm4(Span<byte> dst, Span<byte> top, int topOffset)
        {
            Span<byte> clip = Clip1.AsSpan(255 - top[topOffset - 1]);
            for (int y = 0; y < 4; y++)
            {
                Span<byte> clipTable = clip.Slice(top[topOffset - 2 - y]);
                for (int x = 0; x < 4; x++)
                {
                    dst[x] = clipTable[top[topOffset + x]];
                }

                dst = dst.Slice(WebpConstants.Bps);
            }
        }

        private static void Ve4(Span<byte> dst, Span<byte> top, int topOffset, Span<byte> vals)
        {
            // vertical
            vals[0] = LossyUtils.Avg3(top[topOffset - 1], top[topOffset], top[topOffset + 1]);
            vals[1] = LossyUtils.Avg3(top[topOffset], top[topOffset + 1], top[topOffset + 2]);
            vals[2] = LossyUtils.Avg3(top[topOffset + 1], top[topOffset + 2], top[topOffset + 3]);
            vals[3] = LossyUtils.Avg3(top[topOffset + 2], top[topOffset + 3], top[topOffset + 4]);
            for (int i = 0; i < 4; i++)
            {
                vals.CopyTo(dst.Slice(i * WebpConstants.Bps));
            }
        }

        private static void He4(Span<byte> dst, Span<byte> top, int topOffset)
        {
            // horizontal
            byte x = top[topOffset - 1];
            byte i = top[topOffset - 2];
            byte j = top[topOffset - 3];
            byte k = top[topOffset - 4];
            byte l = top[topOffset - 5];

            uint val = 0x01010101U * LossyUtils.Avg3(x, i, j);
            BinaryPrimitives.WriteUInt32BigEndian(dst, val);
            val = 0x01010101U * LossyUtils.Avg3(i, j, k);
            BinaryPrimitives.WriteUInt32BigEndian(dst.Slice(1 * WebpConstants.Bps), val);
            val = 0x01010101U * LossyUtils.Avg3(j, k, l);
            BinaryPrimitives.WriteUInt32BigEndian(dst.Slice(2 * WebpConstants.Bps), val);
            val = 0x01010101U * LossyUtils.Avg3(k, l, l);
            BinaryPrimitives.WriteUInt32BigEndian(dst.Slice(3 * WebpConstants.Bps), val);
        }

        private static void Rd4(Span<byte> dst, Span<byte> top, int topOffset)
        {
            byte x = top[topOffset - 1];
            byte i = top[topOffset - 2];
            byte j = top[topOffset - 3];
            byte k = top[topOffset - 4];
            byte l = top[topOffset - 5];
            byte a = top[topOffset];
            byte b = top[topOffset + 1];
            byte c = top[topOffset + 2];
            byte d = top[topOffset + 3];

            LossyUtils.Dst(dst, 0, 3, LossyUtils.Avg3(j, k, l));
            byte ijk = LossyUtils.Avg3(i, j, k);
            LossyUtils.Dst(dst, 0, 2, ijk);
            LossyUtils.Dst(dst, 1, 3, ijk);
            byte xij = LossyUtils.Avg3(x, i, j);
            LossyUtils.Dst(dst, 0, 1, xij);
            LossyUtils.Dst(dst, 1, 2, xij);
            LossyUtils.Dst(dst, 2, 3, xij);
            byte axi = LossyUtils.Avg3(a, x, i);
            LossyUtils.Dst(dst, 0, 0, axi);
            LossyUtils.Dst(dst, 1, 1, axi);
            LossyUtils.Dst(dst, 2, 2, axi);
            LossyUtils.Dst(dst, 3, 3, axi);
            byte bax = LossyUtils.Avg3(b, a, x);
            LossyUtils.Dst(dst, 1, 0, bax);
            LossyUtils.Dst(dst, 2, 1, bax);
            LossyUtils.Dst(dst, 3, 2, bax);
            byte cba = LossyUtils.Avg3(c, b, a);
            LossyUtils.Dst(dst, 2, 0, cba);
            LossyUtils.Dst(dst, 3, 1, cba);
            LossyUtils.Dst(dst, 3, 0, LossyUtils.Avg3(d, c, b));
        }

        private static void Vr4(Span<byte> dst, Span<byte> top, int topOffset)
        {
            byte x = top[topOffset - 1];
            byte i = top[topOffset - 2];
            byte j = top[topOffset - 3];
            byte k = top[topOffset - 4];
            byte a = top[topOffset];
            byte b = top[topOffset + 1];
            byte c = top[topOffset + 2];
            byte d = top[topOffset + 3];

            byte xa = LossyUtils.Avg2(x, a);
            LossyUtils.Dst(dst, 0, 0, xa);
            LossyUtils.Dst(dst, 1, 2, xa);
            byte ab = LossyUtils.Avg2(a, b);
            LossyUtils.Dst(dst, 1, 0, ab);
            LossyUtils.Dst(dst, 2, 2, ab);
            byte bc = LossyUtils.Avg2(b, c);
            LossyUtils.Dst(dst, 2, 0, bc);
            LossyUtils.Dst(dst, 3, 2, bc);
            LossyUtils.Dst(dst, 3, 0, LossyUtils.Avg2(c, d));
            LossyUtils.Dst(dst, 0, 3, LossyUtils.Avg3(k, j, i));
            LossyUtils.Dst(dst, 0, 2, LossyUtils.Avg3(j, i, x));
            byte ixa = LossyUtils.Avg3(i, x, a);
            LossyUtils.Dst(dst, 0, 1, ixa);
            LossyUtils.Dst(dst, 1, 3, ixa);
            byte xab = LossyUtils.Avg3(x, a, b);
            LossyUtils.Dst(dst, 1, 1, xab);
            LossyUtils.Dst(dst, 2, 3, xab);
            byte abc = LossyUtils.Avg3(a, b, c);
            LossyUtils.Dst(dst, 2, 1, abc);
            LossyUtils.Dst(dst, 3, 3, abc);
            LossyUtils.Dst(dst, 3, 1, LossyUtils.Avg3(b, c, d));
        }

        private static void Ld4(Span<byte> dst, Span<byte> top, int topOffset)
        {
            byte a = top[topOffset + 0];
            byte b = top[topOffset + 1];
            byte c = top[topOffset + 2];
            byte d = top[topOffset + 3];
            byte e = top[topOffset + 4];
            byte f = top[topOffset + 5];
            byte g = top[topOffset + 6];
            byte h = top[topOffset + 7];

            LossyUtils.Dst(dst, 0, 0, LossyUtils.Avg3(a, b, c));
            byte bcd = LossyUtils.Avg3(b, c, d);
            LossyUtils.Dst(dst, 1, 0, bcd);
            LossyUtils.Dst(dst, 0, 1, bcd);
            byte cde = LossyUtils.Avg3(c, d, e);
            LossyUtils.Dst(dst, 2, 0, cde);
            LossyUtils.Dst(dst, 1, 1, cde);
            LossyUtils.Dst(dst, 0, 2, cde);
            byte def = LossyUtils.Avg3(d, e, f);
            LossyUtils.Dst(dst, 3, 0, def);
            LossyUtils.Dst(dst, 2, 1, def);
            LossyUtils.Dst(dst, 1, 2, def);
            LossyUtils.Dst(dst, 0, 3, def);
            byte efg = LossyUtils.Avg3(e, f, g);
            LossyUtils.Dst(dst, 3, 1, efg);
            LossyUtils.Dst(dst, 2, 2, efg);
            LossyUtils.Dst(dst, 1, 3, efg);
            byte fgh = LossyUtils.Avg3(f, g, h);
            LossyUtils.Dst(dst, 3, 2, fgh);
            LossyUtils.Dst(dst, 2, 3, fgh);
            LossyUtils.Dst(dst, 3, 3, LossyUtils.Avg3(g, h, h));
        }

        private static void Vl4(Span<byte> dst, Span<byte> top, int topOffset)
        {
            byte a = top[topOffset + 0];
            byte b = top[topOffset + 1];
            byte c = top[topOffset + 2];
            byte d = top[topOffset + 3];
            byte e = top[topOffset + 4];
            byte f = top[topOffset + 5];
            byte g = top[topOffset + 6];
            byte h = top[topOffset + 7];

            LossyUtils.Dst(dst, 0, 0, LossyUtils.Avg2(a, b));
            byte bc = LossyUtils.Avg2(b, c);
            LossyUtils.Dst(dst, 1, 0, bc);
            LossyUtils.Dst(dst, 0, 2, bc);
            byte cd = LossyUtils.Avg2(c, d);
            LossyUtils.Dst(dst, 2, 0, cd);
            LossyUtils.Dst(dst, 1, 2, cd);
            byte de = LossyUtils.Avg2(d, e);
            LossyUtils.Dst(dst, 3, 0, de);
            LossyUtils.Dst(dst, 2, 2, de);
            LossyUtils.Dst(dst, 0, 1, LossyUtils.Avg3(a, b, c));
            byte bcd = LossyUtils.Avg3(b, c, d);
            LossyUtils.Dst(dst, 1, 1, bcd);
            LossyUtils.Dst(dst, 0, 3, bcd);
            byte cde = LossyUtils.Avg3(c, d, e);
            LossyUtils.Dst(dst, 2, 1, cde);
            LossyUtils.Dst(dst, 1, 3, cde);
            byte def = LossyUtils.Avg3(d, e, f);
            LossyUtils.Dst(dst, 3, 1, def);
            LossyUtils.Dst(dst, 2, 3, def);
            LossyUtils.Dst(dst, 3, 2, LossyUtils.Avg3(e, f, g));
            LossyUtils.Dst(dst, 3, 3, LossyUtils.Avg3(f, g, h));
        }

        private static void Hd4(Span<byte> dst, Span<byte> top, int topOffset)
        {
            byte x = top[topOffset - 1];
            byte i = top[topOffset - 2];
            byte j = top[topOffset - 3];
            byte k = top[topOffset - 4];
            byte l = top[topOffset - 5];
            byte a = top[topOffset];
            byte b = top[topOffset + 1];
            byte c = top[topOffset + 2];

            byte ix = LossyUtils.Avg2(i, x);
            LossyUtils.Dst(dst, 0, 0, ix);
            LossyUtils.Dst(dst, 2, 1, ix);
            byte ji = LossyUtils.Avg2(j, i);
            LossyUtils.Dst(dst, 0, 1, ji);
            LossyUtils.Dst(dst, 2, 2, ji);
            byte kj = LossyUtils.Avg2(k, j);
            LossyUtils.Dst(dst, 0, 2, kj);
            LossyUtils.Dst(dst, 2, 3, kj);
            LossyUtils.Dst(dst, 0, 3, LossyUtils.Avg2(l, k));
            LossyUtils.Dst(dst, 3, 0, LossyUtils.Avg3(a, b, c));
            LossyUtils.Dst(dst, 2, 0, LossyUtils.Avg3(x, a, b));
            byte ixa = LossyUtils.Avg3(i, x, a);
            LossyUtils.Dst(dst, 1, 0, ixa);
            LossyUtils.Dst(dst, 3, 1, ixa);
            byte jix = LossyUtils.Avg3(j, i, x);
            LossyUtils.Dst(dst, 1, 1, jix);
            LossyUtils.Dst(dst, 3, 2, jix);
            byte kji = LossyUtils.Avg3(k, j, i);
            LossyUtils.Dst(dst, 1, 2, kji);
            LossyUtils.Dst(dst, 3, 3, kji);
            LossyUtils.Dst(dst, 1, 3, LossyUtils.Avg3(l, k, j));
        }

        private static void Hu4(Span<byte> dst, Span<byte> top, int topOffset)
        {
            byte i = top[topOffset - 2];
            byte j = top[topOffset - 3];
            byte k = top[topOffset - 4];
            byte l = top[topOffset - 5];

            LossyUtils.Dst(dst, 0, 0, LossyUtils.Avg2(i, j));
            byte jk = LossyUtils.Avg2(j, k);
            LossyUtils.Dst(dst, 2, 0, jk);
            LossyUtils.Dst(dst, 0, 1, jk);
            byte kl = LossyUtils.Avg2(k, l);
            LossyUtils.Dst(dst, 2, 1, kl);
            LossyUtils.Dst(dst, 0, 2, kl);
            LossyUtils.Dst(dst, 1, 0, LossyUtils.Avg3(i, j, k));
            byte jkl = LossyUtils.Avg3(j, k, l);
            LossyUtils.Dst(dst, 3, 0, jkl);
            LossyUtils.Dst(dst, 1, 1, jkl);
            byte kll = LossyUtils.Avg3(k, l, l);
            LossyUtils.Dst(dst, 3, 1, kll);
            LossyUtils.Dst(dst, 1, 2, kll);
            LossyUtils.Dst(dst, 3, 2, l);
            LossyUtils.Dst(dst, 2, 2, l);
            LossyUtils.Dst(dst, 0, 3, l);
            LossyUtils.Dst(dst, 1, 3, l);
            LossyUtils.Dst(dst, 2, 3, l);
            LossyUtils.Dst(dst, 3, 3, l);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Fill(Span<byte> dst, int value, int size)
        {
            for (int j = 0; j < size; j++)
            {
                dst.Slice(j * WebpConstants.Bps, size).Fill((byte)value);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static byte Clip8b(int v) => (v & ~0xff) == 0 ? (byte)v : v < 0 ? (byte)0 : (byte)255;

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Store(Span<byte> dst, Span<byte> reference, int x, int y, int v) => dst[x + (y * WebpConstants.Bps)] = LossyUtils.Clip8B(reference[x + (y * WebpConstants.Bps)] + (v >> 3));

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Mul(int a, int b) => (a * b) >> 16;
    }
}

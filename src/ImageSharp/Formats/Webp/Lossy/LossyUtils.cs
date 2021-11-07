// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal static unsafe class LossyUtils
    {
#if SUPPORTS_RUNTIME_INTRINSICS
        private static readonly Vector128<byte> Mean16x4Mask = Vector128.Create((short)0x00ff).AsByte();
#endif

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int Vp8Sse16X16(Span<byte> a, Span<byte> b) => GetSse(a, b, 16, 16);

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int Vp8Sse16X8(Span<byte> a, Span<byte> b) => GetSse(a, b, 16, 8);

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int Vp8Sse4X4(Span<byte> a, Span<byte> b) => GetSse(a, b, 4, 4);

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int GetSse(Span<byte> a, Span<byte> b, int w, int h)
        {
            int count = 0;
            int aOffset = 0;
            int bOffset = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int diff = a[aOffset + x] - b[bOffset + x];
                    count += diff * diff;
                }

                aOffset += WebpConstants.Bps;
                bOffset += WebpConstants.Bps;
            }

            return count;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Vp8Copy4X4(Span<byte> src, Span<byte> dst) => Copy(src, dst, 4, 4);

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Vp8Copy16X8(Span<byte> src, Span<byte> dst) => Copy(src, dst, 16, 8);

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Copy(Span<byte> src, Span<byte> dst, int w, int h)
        {
            int offset = 0;
            for (int y = 0; y < h; y++)
            {
                src.Slice(offset, w).CopyTo(dst.Slice(offset, w));
                offset += WebpConstants.Bps;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int Vp8Disto16X16(Span<byte> a, Span<byte> b, Span<ushort> w, Span<int> scratch)
        {
            int d = 0;
            for (int y = 0; y < 16 * WebpConstants.Bps; y += 4 * WebpConstants.Bps)
            {
                for (int x = 0; x < 16; x += 4)
                {
                    d += Vp8Disto4X4(a.Slice(x + y), b.Slice(x + y), w, scratch);
                }
            }

            return d;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int Vp8Disto4X4(Span<byte> a, Span<byte> b, Span<ushort> w, Span<int> scratch)
        {
            int sum1 = TTransform(a, w, scratch);
            int sum2 = TTransform(b, w, scratch);
            return Math.Abs(sum2 - sum1) >> 5;
        }

        public static void DC16(Span<byte> dst, Span<byte> yuv, int offset)
        {
            int offsetMinus1 = offset - 1;
            int offsetMinusBps = offset - WebpConstants.Bps;
            int dc = 16;
            for (int j = 0; j < 16; j++)
            {
                // DC += dst[-1 + j * BPS] + dst[j - BPS];
                dc += yuv[offsetMinus1 + (j * WebpConstants.Bps)] + yuv[offsetMinusBps + j];
            }

            Put16(dc >> 5, dst);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void TM16(Span<byte> dst, Span<byte> yuv, int offset) => TrueMotion(dst, yuv, offset, 16);

        public static void VE16(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // vertical
            Span<byte> src = yuv.Slice(offset - WebpConstants.Bps, 16);
            for (int j = 0; j < 16; j++)
            {
                // memcpy(dst + j * BPS, dst - BPS, 16);
                src.CopyTo(dst.Slice(j * WebpConstants.Bps));
            }
        }

        public static void HE16(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // horizontal
            offset--;
            for (int j = 16; j > 0; j--)
            {
                // memset(dst, dst[-1], 16);
                byte v = yuv[offset];
                Memset(dst, v, 0, 16);
                offset += WebpConstants.Bps;
                dst = dst.Slice(WebpConstants.Bps);
            }
        }

        public static void DC16NoTop(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // DC with top samples not available.
            int dc = 8;
            for (int j = 0; j < 16; j++)
            {
                // DC += dst[-1 + j * BPS];
                dc += yuv[-1 + (j * WebpConstants.Bps) + offset];
            }

            Put16(dc >> 4, dst);
        }

        public static void DC16NoLeft(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // DC with left samples not available.
            int dc = 8;
            for (int i = 0; i < 16; i++)
            {
                // DC += dst[i - BPS];
                dc += yuv[i - WebpConstants.Bps + offset];
            }

            Put16(dc >> 4, dst);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void DC16NoTopLeft(Span<byte> dst) =>
            Put16(0x80, dst); // DC with no top and left samples.

        public static void DC8uv(Span<byte> dst, Span<byte> yuv, int offset)
        {
            int dc0 = 8;
            int offsetMinus1 = offset - 1;
            int offsetMinusBps = offset - WebpConstants.Bps;
            for (int i = 0; i < 8; i++)
            {
                // dc0 += dst[i - BPS] + dst[-1 + i * BPS];
                dc0 += yuv[offsetMinusBps + i] + yuv[offsetMinus1 + (i * WebpConstants.Bps)];
            }

            Put8x8uv((byte)(dc0 >> 4), dst);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void TM8uv(Span<byte> dst, Span<byte> yuv, int offset) =>
            TrueMotion(dst, yuv, offset, 8); // TrueMotion

        public static void VE8uv(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // vertical
            Span<byte> src = yuv.Slice(offset - WebpConstants.Bps, 8);

            int endIdx = 8 * WebpConstants.Bps;
            for (int j = 0; j < endIdx; j += WebpConstants.Bps)
            {
                // memcpy(dst + j * BPS, dst - BPS, 8);
                src.CopyTo(dst.Slice(j));
            }
        }

        public static void HE8uv(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // horizontal
            offset--;
            for (int j = 0; j < 8; j++)
            {
                // memset(dst, dst[-1], 8);
                // dst += BPS;
                byte v = yuv[offset];
                Memset(dst, v, 0, 8);
                dst = dst.Slice(WebpConstants.Bps);
                offset += WebpConstants.Bps;
            }
        }

        public static void DC8uvNoTop(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // DC with no top samples.
            int dc0 = 4;
            int offsetMinusOne = offset - 1;
            int endIdx = 8 * WebpConstants.Bps;
            for (int i = 0; i < endIdx; i += WebpConstants.Bps)
            {
                // dc0 += dst[-1 + i * BPS];
                dc0 += yuv[offsetMinusOne + i];
            }

            Put8x8uv((byte)(dc0 >> 3), dst);
        }

        public static void DC8uvNoLeft(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // DC with no left samples.
            int offsetMinusBps = offset - WebpConstants.Bps;
            int dc0 = 4;
            for (int i = 0; i < 8; i++)
            {
                // dc0 += dst[i - BPS];
                dc0 += yuv[offsetMinusBps + i];
            }

            Put8x8uv((byte)(dc0 >> 3), dst);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void DC8uvNoTopLeft(Span<byte> dst) =>
            Put8x8uv(0x80, dst); // DC with nothing.

        public static void DC4(Span<byte> dst, Span<byte> yuv, int offset)
        {
            int dc = 4;
            int offsetMinusBps = offset - WebpConstants.Bps;
            int offsetMinusOne = offset - 1;
            for (int i = 0; i < 4; i++)
            {
                dc += yuv[offsetMinusBps + i] + yuv[offsetMinusOne + (i * WebpConstants.Bps)];
            }

            dc >>= 3;
            int endIndx = 4 * WebpConstants.Bps;
            for (int i = 0; i < endIndx; i += WebpConstants.Bps)
            {
                Memset(dst, (byte)dc, i, 4);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void TM4(Span<byte> dst, Span<byte> yuv, int offset) => TrueMotion(dst, yuv, offset, 4);

        public static void VE4(Span<byte> dst, Span<byte> yuv, int offset, Span<byte> vals)
        {
            // vertical
            int topOffset = offset - WebpConstants.Bps;
            vals[0] = Avg3(yuv[topOffset - 1], yuv[topOffset], yuv[topOffset + 1]);
            vals[1] = Avg3(yuv[topOffset], yuv[topOffset + 1], yuv[topOffset + 2]);
            vals[2] = Avg3(yuv[topOffset + 1], yuv[topOffset + 2], yuv[topOffset + 3]);
            vals[3] = Avg3(yuv[topOffset + 2], yuv[topOffset + 3], yuv[topOffset + 4]);
            int endIdx = 4 * WebpConstants.Bps;
            for (int i = 0; i < endIdx; i += WebpConstants.Bps)
            {
                vals.CopyTo(dst.Slice(i));
            }
        }

        public static void HE4(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // horizontal
            int offsetMinusOne = offset - 1;
            byte a = yuv[offsetMinusOne - WebpConstants.Bps];
            byte b = yuv[offsetMinusOne];
            byte c = yuv[offsetMinusOne + WebpConstants.Bps];
            byte d = yuv[offsetMinusOne + (2 * WebpConstants.Bps)];
            byte e = yuv[offsetMinusOne + (3 * WebpConstants.Bps)];
            uint val = 0x01010101U * Avg3(a, b, c);
            BinaryPrimitives.WriteUInt32BigEndian(dst, val);
            val = 0x01010101U * Avg3(b, c, d);
            BinaryPrimitives.WriteUInt32BigEndian(dst.Slice(WebpConstants.Bps), val);
            val = 0x01010101U * Avg3(c, d, e);
            BinaryPrimitives.WriteUInt32BigEndian(dst.Slice(2 * WebpConstants.Bps), val);
            val = 0x01010101U * Avg3(d, e, e);
            BinaryPrimitives.WriteUInt32BigEndian(dst.Slice(3 * WebpConstants.Bps), val);
        }

        public static void RD4(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // Down-right
            int offsetMinusOne = offset - 1;
            byte i = yuv[offsetMinusOne];
            byte j = yuv[offsetMinusOne + (1 * WebpConstants.Bps)];
            byte k = yuv[offsetMinusOne + (2 * WebpConstants.Bps)];
            byte l = yuv[offsetMinusOne + (3 * WebpConstants.Bps)];
            byte x = yuv[offsetMinusOne - WebpConstants.Bps];
            byte a = yuv[offset - WebpConstants.Bps];
            byte b = yuv[offset + 1 - WebpConstants.Bps];
            byte c = yuv[offset + 2 - WebpConstants.Bps];
            byte d = yuv[offset + 3 - WebpConstants.Bps];

            Dst(dst, 0, 3, Avg3(j, k, l));
            byte ijk = Avg3(i, j, k);
            Dst(dst, 1, 3, ijk);
            Dst(dst, 0, 2, ijk);
            byte xij = Avg3(x, i, j);
            Dst(dst, 2, 3, xij);
            Dst(dst, 1, 2, xij);
            Dst(dst, 0, 1, xij);
            byte axi = Avg3(a, x, i);
            Dst(dst, 3, 3, axi);
            Dst(dst, 2, 2, axi);
            Dst(dst, 1, 1, axi);
            Dst(dst, 0, 0, axi);
            byte bax = Avg3(b, a, x);
            Dst(dst, 3, 2, bax);
            Dst(dst, 2, 1, bax);
            Dst(dst, 1, 0, bax);
            byte cba = Avg3(c, b, a);
            Dst(dst, 3, 1, cba);
            Dst(dst, 2, 0, cba);
            Dst(dst, 3, 0, Avg3(d, c, b));
        }

        public static void VR4(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // Vertical-Right
            int offsetMinusOne = offset - 1;
            byte i = yuv[offsetMinusOne];
            byte j = yuv[offsetMinusOne + (1 * WebpConstants.Bps)];
            byte k = yuv[offsetMinusOne + (2 * WebpConstants.Bps)];
            byte x = yuv[offsetMinusOne - WebpConstants.Bps];
            byte a = yuv[offset - WebpConstants.Bps];
            byte b = yuv[offset + 1 - WebpConstants.Bps];
            byte c = yuv[offset + 2 - WebpConstants.Bps];
            byte d = yuv[offset + 3 - WebpConstants.Bps];

            byte xa = Avg2(x, a);
            Dst(dst, 0, 0, xa);
            Dst(dst, 1, 2, xa);
            byte ab = Avg2(a, b);
            Dst(dst, 1, 0, ab);
            Dst(dst, 2, 2, ab);
            byte bc = Avg2(b, c);
            Dst(dst, 2, 0, bc);
            Dst(dst, 3, 2, bc);
            Dst(dst, 3, 0, Avg2(c, d));
            Dst(dst, 0, 3, Avg3(k, j, i));
            Dst(dst, 0, 2, Avg3(j, i, x));
            byte ixa = Avg3(i, x, a);
            Dst(dst, 0, 1, ixa);
            Dst(dst, 1, 3, ixa);
            byte xab = Avg3(x, a, b);
            Dst(dst, 1, 1, xab);
            Dst(dst, 2, 3, xab);
            byte abc = Avg3(a, b, c);
            Dst(dst, 2, 1, abc);
            Dst(dst, 3, 3, abc);
            Dst(dst, 3, 1, Avg3(b, c, d));
        }

        public static void LD4(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // Down-Left
            byte a = yuv[offset - WebpConstants.Bps];
            byte b = yuv[offset + 1 - WebpConstants.Bps];
            byte c = yuv[offset + 2 - WebpConstants.Bps];
            byte d = yuv[offset + 3 - WebpConstants.Bps];
            byte e = yuv[offset + 4 - WebpConstants.Bps];
            byte f = yuv[offset + 5 - WebpConstants.Bps];
            byte g = yuv[offset + 6 - WebpConstants.Bps];
            byte h = yuv[offset + 7 - WebpConstants.Bps];

            Dst(dst, 0, 0, Avg3(a, b, c));
            byte bcd = Avg3(b, c, d);
            Dst(dst, 1, 0, bcd);
            Dst(dst, 0, 1, bcd);
            byte cde = Avg3(c, d, e);
            Dst(dst, 2, 0, cde);
            Dst(dst, 1, 1, cde);
            Dst(dst, 0, 2, cde);
            byte def = Avg3(d, e, f);
            Dst(dst, 3, 0, def);
            Dst(dst, 2, 1, def);
            Dst(dst, 1, 2, def);
            Dst(dst, 0, 3, def);
            byte efg = Avg3(e, f, g);
            Dst(dst, 3, 1, efg);
            Dst(dst, 2, 2, efg);
            Dst(dst, 1, 3, efg);
            byte fgh = Avg3(f, g, h);
            Dst(dst, 3, 2, fgh);
            Dst(dst, 2, 3, fgh);
            Dst(dst, 3, 3, Avg3(g, h, h));
        }

        public static void VL4(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // Vertical-Left
            byte a = yuv[offset - WebpConstants.Bps];
            byte b = yuv[offset + 1 - WebpConstants.Bps];
            byte c = yuv[offset + 2 - WebpConstants.Bps];
            byte d = yuv[offset + 3 - WebpConstants.Bps];
            byte e = yuv[offset + 4 - WebpConstants.Bps];
            byte f = yuv[offset + 5 - WebpConstants.Bps];
            byte g = yuv[offset + 6 - WebpConstants.Bps];
            byte h = yuv[offset + 7 - WebpConstants.Bps];

            Dst(dst, 0, 0, Avg2(a, b));
            byte bc = Avg2(b, c);
            Dst(dst, 1, 0, bc);
            Dst(dst, 0, 2, bc);
            byte cd = Avg2(c, d);
            Dst(dst, 2, 0, cd);
            Dst(dst, 1, 2, cd);
            byte de = Avg2(d, e);
            Dst(dst, 3, 0, de);
            Dst(dst, 2, 2, de);
            Dst(dst, 0, 1, Avg3(a, b, c));
            byte bcd = Avg3(b, c, d);
            Dst(dst, 1, 1, bcd);
            Dst(dst, 0, 3, bcd);
            byte cde = Avg3(c, d, e);
            Dst(dst, 2, 1, cde);
            Dst(dst, 1, 3, cde);
            byte def = Avg3(d, e, f);
            Dst(dst, 3, 1, def);
            Dst(dst, 2, 3, def);
            Dst(dst, 3, 2, Avg3(e, f, g));
            Dst(dst, 3, 3, Avg3(f, g, h));
        }

        public static void HD4(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // Horizontal-Down
            byte i = yuv[offset - 1];
            byte j = yuv[offset - 1 + (1 * WebpConstants.Bps)];
            byte k = yuv[offset - 1 + (2 * WebpConstants.Bps)];
            byte l = yuv[offset - 1 + (3 * WebpConstants.Bps)];
            byte x = yuv[offset - 1 - WebpConstants.Bps];
            byte a = yuv[offset - WebpConstants.Bps];
            byte b = yuv[offset + 1 - WebpConstants.Bps];
            byte c = yuv[offset + 2 - WebpConstants.Bps];

            byte ix = Avg2(i, x);
            Dst(dst, 0, 0, ix);
            Dst(dst, 2, 1, ix);
            byte ji = Avg2(j, i);
            Dst(dst, 0, 1, ji);
            Dst(dst, 2, 2, ji);
            byte kj = Avg2(k, j);
            Dst(dst, 0, 2, kj);
            Dst(dst, 2, 3, kj);
            Dst(dst, 0, 3, Avg2(l, k));
            Dst(dst, 3, 0, Avg3(a, b, c));
            Dst(dst, 2, 0, Avg3(x, a, b));
            byte ixa = Avg3(i, x, a);
            Dst(dst, 1, 0, ixa);
            Dst(dst, 3, 1, ixa);
            byte jix = Avg3(j, i, x);
            Dst(dst, 1, 1, jix);
            Dst(dst, 3, 2, jix);
            byte kji = Avg3(k, j, i);
            Dst(dst, 1, 2, kji);
            Dst(dst, 3, 3, kji);
            Dst(dst, 1, 3, Avg3(l, k, j));
        }

        public static void HU4(Span<byte> dst, Span<byte> yuv, int offset)
        {
            // Horizontal-Up
            byte i = yuv[offset - 1];
            byte j = yuv[offset - 1 + (1 * WebpConstants.Bps)];
            byte k = yuv[offset - 1 + (2 * WebpConstants.Bps)];
            byte l = yuv[offset - 1 + (3 * WebpConstants.Bps)];

            Dst(dst, 0, 0, Avg2(i, j));
            byte jk = Avg2(j, k);
            Dst(dst, 2, 0, jk);
            Dst(dst, 0, 1, jk);
            byte kl = Avg2(k, l);
            Dst(dst, 2, 1, kl);
            Dst(dst, 0, 2, kl);
            Dst(dst, 1, 0, Avg3(i, j, k));
            byte jkl = Avg3(j, k, l);
            Dst(dst, 3, 0, jkl);
            Dst(dst, 1, 1, jkl);
            byte kll = Avg3(k, l, l);
            Dst(dst, 3, 1, kll);
            Dst(dst, 1, 2, kll);
            Dst(dst, 3, 2, l);
            Dst(dst, 2, 2, l);
            Dst(dst, 0, 3, l);
            Dst(dst, 1, 3, l);
            Dst(dst, 2, 3, l);
            Dst(dst, 3, 3, l);
        }

        /// <summary>
        /// Paragraph 14.3: Implementation of the Walsh-Hadamard transform inversion.
        /// </summary>
        public static void TransformWht(Span<short> input, Span<short> output, Span<int> scratch)
        {
            Span<int> tmp = scratch.Slice(0, 16);
            tmp.Clear();
            for (int i = 0; i < 4; i++)
            {
                int iPlus4 = 4 + i;
                int iPlus8 = 8 + i;
                int iPlus12 = 12 + i;
                int a0 = input[i] + input[iPlus12];
                int a1 = input[iPlus4] + input[iPlus8];
                int a2 = input[iPlus4] - input[iPlus8];
                int a3 = input[i] - input[iPlus12];
                tmp[i] = a0 + a1;
                tmp[iPlus8] = a0 - a1;
                tmp[iPlus4] = a3 + a2;
                tmp[iPlus12] = a3 - a2;
            }

            int outputOffset = 0;
            for (int i = 0; i < 4; i++)
            {
                int imul4 = i * 4;
                int dc = tmp[0 + imul4] + 3;
                int a0 = dc + tmp[3 + imul4];
                int a1 = tmp[1 + imul4] + tmp[2 + imul4];
                int a2 = tmp[1 + imul4] - tmp[2 + imul4];
                int a3 = dc - tmp[3 + imul4];
                output[outputOffset + 0] = (short)((a0 + a1) >> 3);
                output[outputOffset + 16] = (short)((a3 + a2) >> 3);
                output[outputOffset + 32] = (short)((a0 - a1) >> 3);
                output[outputOffset + 48] = (short)((a3 - a2) >> 3);
                outputOffset += 64;
            }
        }

        /// <summary>
        /// Hadamard transform
        /// Returns the weighted sum of the absolute value of transformed coefficients.
        /// w[] contains a row-major 4 by 4 symmetric matrix.
        /// </summary>
        public static int TTransform(Span<byte> input, Span<ushort> w, Span<int> scratch)
        {
            int sum = 0;
            Span<int> tmp = scratch.Slice(0, 16);
            tmp.Clear();

            // horizontal pass.
            int inputOffset = 0;
            for (int i = 0; i < 4; i++)
            {
                int inputOffsetPlusOne = inputOffset + 1;
                int inputOffsetPlusTwo = inputOffset + 2;
                int inputOffsetPlusThree = inputOffset + 3;
                int a0 = input[inputOffset] + input[inputOffsetPlusTwo];
                int a1 = input[inputOffsetPlusOne] + input[inputOffsetPlusThree];
                int a2 = input[inputOffsetPlusOne] - input[inputOffsetPlusThree];
                int a3 = input[inputOffset] - input[inputOffsetPlusTwo];
                tmp[0 + (i * 4)] = a0 + a1;
                tmp[1 + (i * 4)] = a3 + a2;
                tmp[2 + (i * 4)] = a3 - a2;
                tmp[3 + (i * 4)] = a0 - a1;

                inputOffset += WebpConstants.Bps;
            }

            // vertical pass
            for (int i = 0; i < 4; i++)
            {
                int a0 = tmp[0 + i] + tmp[8 + i];
                int a1 = tmp[4 + i] + tmp[12 + i];
                int a2 = tmp[4 + i] - tmp[12 + i];
                int a3 = tmp[0 + i] - tmp[8 + i];
                int b0 = a0 + a1;
                int b1 = a3 + a2;
                int b2 = a3 - a2;
                int b3 = a0 - a1;

                sum += w[0] * Math.Abs(b0);
                sum += w[4] * Math.Abs(b1);
                sum += w[8] * Math.Abs(b2);
                sum += w[12] * Math.Abs(b3);

                w = w.Slice(1);
            }

            return sum;
        }

        public static void TransformTwo(Span<short> src, Span<byte> dst, Span<int> scratch)
        {
            TransformOne(src, dst, scratch);
            TransformOne(src.Slice(16), dst.Slice(4), scratch);
        }

        public static void TransformOne(Span<short> src, Span<byte> dst, Span<int> scratch)
        {
            Span<int> tmp = scratch.Slice(0, 16);
            tmp.Clear();
            int tmpOffset = 0;
            for (int srcOffset = 0; srcOffset < 4; srcOffset++)
            {
                // vertical pass
                int srcOffsetPlus4 = srcOffset + 4;
                int srcOffsetPlus8 = srcOffset + 8;
                int srcOffsetPlus12 = srcOffset + 12;
                int a = src[srcOffset] + src[srcOffsetPlus8];
                int b = src[srcOffset] - src[srcOffsetPlus8];
                int c = Mul2(src[srcOffsetPlus4]) - Mul1(src[srcOffsetPlus12]);
                int d = Mul1(src[srcOffsetPlus4]) + Mul2(src[srcOffsetPlus12]);
                tmp[tmpOffset++] = a + d;
                tmp[tmpOffset++] = b + c;
                tmp[tmpOffset++] = b - c;
                tmp[tmpOffset++] = a - d;
            }

            // Each pass is expanding the dynamic range by ~3.85 (upper bound).
            // The exact value is (2. + (20091 + 35468) / 65536).
            // After the second pass, maximum interval is [-3794, 3794], assuming
            // an input in [-2048, 2047] interval. We then need to add a dst value in the [0, 255] range.
            // In the worst case scenario, the input to clip_8b() can be as large as [-60713, 60968].
            tmpOffset = 0;
            int dstOffset = 0;
            for (int i = 0; i < 4; i++)
            {
                // horizontal pass
                int tmpOffsetPlus4 = tmpOffset + 4;
                int tmpOffsetPlus8 = tmpOffset + 8;
                int tmpOffsetPlus12 = tmpOffset + 12;
                int dc = tmp[tmpOffset] + 4;
                int a = dc + tmp[tmpOffsetPlus8];
                int b = dc - tmp[tmpOffsetPlus8];
                int c = Mul2(tmp[tmpOffsetPlus4]) - Mul1(tmp[tmpOffsetPlus12]);
                int d = Mul1(tmp[tmpOffsetPlus4]) + Mul2(tmp[tmpOffsetPlus12]);
                Store(dst.Slice(dstOffset), 0, 0, a + d);
                Store(dst.Slice(dstOffset), 1, 0, b + c);
                Store(dst.Slice(dstOffset), 2, 0, b - c);
                Store(dst.Slice(dstOffset), 3, 0, a - d);
                tmpOffset++;

                dstOffset += WebpConstants.Bps;
            }
        }

        public static void TransformDc(Span<short> src, Span<byte> dst)
        {
            int dc = src[0] + 4;
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    Store(dst, i, j, dc);
                }
            }
        }

        // Simplified transform when only src[0], src[1] and src[4] are non-zero
        public static void TransformAc3(Span<short> src, Span<byte> dst)
        {
            int a = src[0] + 4;
            int c4 = Mul2(src[4]);
            int d4 = Mul1(src[4]);
            int c1 = Mul2(src[1]);
            int d1 = Mul1(src[1]);
            Store2(dst, 0, a + d4, d1, c1);
            Store2(dst, 1, a + c4, d1, c1);
            Store2(dst, 2, a - c4, d1, c1);
            Store2(dst, 3, a - d4, d1, c1);
        }

        public static void TransformUv(Span<short> src, Span<byte> dst, Span<int> scratch)
        {
            TransformTwo(src.Slice(0 * 16), dst, scratch);
            TransformTwo(src.Slice(2 * 16), dst.Slice(4 * WebpConstants.Bps), scratch);
        }

        public static void TransformDcuv(Span<short> src, Span<byte> dst)
        {
            if (src[0 * 16] != 0)
            {
                TransformDc(src.Slice(0 * 16), dst);
            }

            if (src[1 * 16] != 0)
            {
                TransformDc(src.Slice(1 * 16), dst.Slice(4));
            }

            if (src[2 * 16] != 0)
            {
                TransformDc(src.Slice(2 * 16), dst.Slice(4 * WebpConstants.Bps));
            }

            if (src[3 * 16] != 0)
            {
                TransformDc(src.Slice(3 * 16), dst.Slice((4 * WebpConstants.Bps) + 4));
            }
        }

        // Simple In-loop filtering (Paragraph 15.2)
        public static void SimpleVFilter16(Span<byte> p, int offset, int stride, int thresh)
        {
            int thresh2 = (2 * thresh) + 1;
            int end = 16 + offset;
            for (int i = offset; i < end; i++)
            {
                if (NeedsFilter(p, i, stride, thresh2))
                {
                    DoFilter2(p, i, stride);
                }
            }
        }

        public static void SimpleHFilter16(Span<byte> p, int offset, int stride, int thresh)
        {
            int thresh2 = (2 * thresh) + 1;
            int end = offset + (16 * stride);
            for (int i = offset; i < end; i += stride)
            {
                if (NeedsFilter(p, i, 1, thresh2))
                {
                    DoFilter2(p, i, 1);
                }
            }
        }

        public static void SimpleVFilter16i(Span<byte> p, int offset, int stride, int thresh)
        {
            for (int k = 3; k > 0; --k)
            {
                offset += 4 * stride;
                SimpleVFilter16(p, offset, stride, thresh);
            }
        }

        public static void SimpleHFilter16i(Span<byte> p, int offset, int stride, int thresh)
        {
            for (int k = 3; k > 0; --k)
            {
                offset += 4;
                SimpleHFilter16(p, offset, stride, thresh);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void VFilter16(Span<byte> p, int offset, int stride, int thresh, int ithresh, int hevThresh)
            => FilterLoop26(p, offset, stride, 1, 16, thresh, ithresh, hevThresh);

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void HFilter16(Span<byte> p, int offset, int stride, int thresh, int ithresh, int hevThresh)
            => FilterLoop26(p, offset, 1, stride, 16, thresh, ithresh, hevThresh);

        public static void VFilter16i(Span<byte> p, int offset, int stride, int thresh, int ithresh, int hevThresh)
        {
            for (int k = 3; k > 0; --k)
            {
                offset += 4 * stride;
                FilterLoop24(p, offset, stride, 1, 16, thresh, ithresh, hevThresh);
            }
        }

        public static void HFilter16i(Span<byte> p, int offset, int stride, int thresh, int ithresh, int hevThresh)
        {
            for (int k = 3; k > 0; --k)
            {
                offset += 4;
                FilterLoop24(p, offset, 1, stride, 16, thresh, ithresh, hevThresh);
            }
        }

        // 8-pixels wide variant, for chroma filtering.
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void VFilter8(Span<byte> u, Span<byte> v, int offset, int stride, int thresh, int ithresh, int hevThresh)
        {
            FilterLoop26(u, offset, stride, 1, 8, thresh, ithresh, hevThresh);
            FilterLoop26(v, offset, stride, 1, 8, thresh, ithresh, hevThresh);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void HFilter8(Span<byte> u, Span<byte> v, int offset, int stride, int thresh, int ithresh, int hevThresh)
        {
            FilterLoop26(u, offset, 1, stride, 8, thresh, ithresh, hevThresh);
            FilterLoop26(v, offset, 1, stride, 8, thresh, ithresh, hevThresh);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void VFilter8i(Span<byte> u, Span<byte> v, int offset, int stride, int thresh, int ithresh, int hevThresh)
        {
            int offset4mulstride = offset + (4 * stride);
            FilterLoop24(u, offset4mulstride, stride, 1, 8, thresh, ithresh, hevThresh);
            FilterLoop24(v, offset4mulstride, stride, 1, 8, thresh, ithresh, hevThresh);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void HFilter8i(Span<byte> u, Span<byte> v, int offset, int stride, int thresh, int ithresh, int hevThresh)
        {
            int offsetPlus4 = offset + 4;
            FilterLoop24(u, offsetPlus4, 1, stride, 8, thresh, ithresh, hevThresh);
            FilterLoop24(v, offsetPlus4, 1, stride, 8, thresh, ithresh, hevThresh);
        }

        public static void Mean16x4(Span<byte> input, Span<uint> dc, Span<ushort> tmp)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Sse2.IsSupported)
            {
#pragma warning disable SA1503 // Braces should not be omitted
                tmp.Clear();
                fixed (byte* inputPtr = input)
                fixed (ushort* tmpPtr = tmp)
                {
                    Vector128<byte> a0 = Sse2.LoadVector128(inputPtr);
                    Vector128<byte> a1 = Sse2.LoadVector128(inputPtr + WebpConstants.Bps);
                    Vector128<byte> a2 = Sse2.LoadVector128(inputPtr + (WebpConstants.Bps * 2));
                    Vector128<byte> a3 = Sse2.LoadVector128(inputPtr + (WebpConstants.Bps * 3));
                    Vector128<short> b0 = Sse2.ShiftRightLogical(a0.AsInt16(), 8); // hi byte
                    Vector128<short> b1 = Sse2.ShiftRightLogical(a1.AsInt16(), 8);
                    Vector128<short> b2 = Sse2.ShiftRightLogical(a2.AsInt16(), 8);
                    Vector128<short> b3 = Sse2.ShiftRightLogical(a3.AsInt16(), 8);
                    Vector128<byte> c0 = Sse2.And(a0, Mean16x4Mask); // lo byte
                    Vector128<byte> c1 = Sse2.And(a1, Mean16x4Mask);
                    Vector128<byte> c2 = Sse2.And(a2, Mean16x4Mask);
                    Vector128<byte> c3 = Sse2.And(a3, Mean16x4Mask);
                    Vector128<int> d0 = Sse2.Add(b0.AsInt32(), c0.AsInt32());
                    Vector128<int> d1 = Sse2.Add(b1.AsInt32(), c1.AsInt32());
                    Vector128<int> d2 = Sse2.Add(b2.AsInt32(), c2.AsInt32());
                    Vector128<int> d3 = Sse2.Add(b3.AsInt32(), c3.AsInt32());
                    Vector128<int> e0 = Sse2.Add(d0, d1);
                    Vector128<int> e1 = Sse2.Add(d2, d3);
                    Vector128<int> f0 = Sse2.Add(e0, e1);
                    Sse2.Store(tmpPtr, f0.AsUInt16());
                }
#pragma warning restore SA1503 // Braces should not be omitted

                dc[0] = (uint)(tmp[1] + tmp[0]);
                dc[1] = (uint)(tmp[3] + tmp[2]);
                dc[2] = (uint)(tmp[5] + tmp[4]);
                dc[3] = (uint)(tmp[7] + tmp[6]);
            }
            else
#endif
            {
                for (int k = 0; k < 4; k++)
                {
                    uint avg = 0;
                    for (int y = 0; y < 4; y++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            avg += input[x + (y * WebpConstants.Bps)];
                        }
                    }

                    dc[k] = avg;
                    input = input.Slice(4); // go to next 4x4 block.
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static byte Avg2(byte a, byte b) => (byte)((a + b + 1) >> 1);

        [MethodImpl(InliningOptions.ShortMethod)]
        public static byte Avg3(byte a, byte b, byte c) => (byte)((a + (2 * b) + c + 2) >> 2);

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Dst(Span<byte> dst, int x, int y, byte v) => dst[x + (y * WebpConstants.Bps)] = v;

        [MethodImpl(InliningOptions.ShortMethod)]
        public static byte Clip8B(int v) => (byte)((v & ~0xff) == 0 ? v : v < 0 ? 0 : 255);

        // Cost of coding one event with probability 'proba'.
        public static int Vp8BitCost(int bit, byte proba) => bit == 0 ? WebpLookupTables.Vp8EntropyCost[proba] : WebpLookupTables.Vp8EntropyCost[255 - proba];

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Put16(int v, Span<byte> dst)
        {
            for (int j = 0; j < 16; j++)
            {
                Memset(dst.Slice(j * WebpConstants.Bps), (byte)v, 0, 16);
            }
        }

        private static void TrueMotion(Span<byte> dst, Span<byte> yuv, int offset, int size)
        {
            // For information about how true motion works, see rfc6386, page 52. ff and section 20.14.
            int topOffset = offset - WebpConstants.Bps;
            Span<byte> top = yuv.Slice(topOffset);
            byte p = yuv[topOffset - 1];
            int leftOffset = offset - 1;
            byte left = yuv[leftOffset];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    dst[x] = (byte)Clamp255(left + top[x] - p);
                }

                leftOffset += WebpConstants.Bps;
                left = yuv[leftOffset];
                dst = dst.Slice(WebpConstants.Bps);
            }
        }

        // Complex In-loop filtering (Paragraph 15.3)
        private static void FilterLoop24(
            Span<byte> p,
            int offset,
            int hStride,
            int vStride,
            int size,
            int thresh,
            int ithresh,
            int hevThresh)
        {
            int thresh2 = (2 * thresh) + 1;
            while (size-- > 0)
            {
                if (NeedsFilter2(p, offset, hStride, thresh2, ithresh))
                {
                    if (Hev(p, offset, hStride, hevThresh))
                    {
                        DoFilter2(p, offset, hStride);
                    }
                    else
                    {
                        DoFilter4(p, offset, hStride);
                    }
                }

                offset += vStride;
            }
        }

        private static void FilterLoop26(
            Span<byte> p,
            int offset,
            int hStride,
            int vStride,
            int size,
            int thresh,
            int ithresh,
            int hevThresh)
        {
            int thresh2 = (2 * thresh) + 1;
            while (size-- > 0)
            {
                if (NeedsFilter2(p, offset, hStride, thresh2, ithresh))
                {
                    if (Hev(p, offset, hStride, hevThresh))
                    {
                        DoFilter2(p, offset, hStride);
                    }
                    else
                    {
                        DoFilter6(p, offset, hStride);
                    }
                }

                offset += vStride;
            }
        }

        private static void DoFilter2(Span<byte> p, int offset, int step)
        {
            // 4 pixels in, 2 pixels out.
            int p1 = p[offset - (2 * step)];
            int p0 = p[offset - step];
            int q0 = p[offset];
            int q1 = p[offset + step];
            int a = (3 * (q0 - p0)) + WebpLookupTables.Sclip1(p1 - q1);
            int a1 = WebpLookupTables.Sclip2((a + 4) >> 3);
            int a2 = WebpLookupTables.Sclip2((a + 3) >> 3);
            p[offset - step] = WebpLookupTables.Clip1(p0 + a2);
            p[offset] = WebpLookupTables.Clip1(q0 - a1);
        }

        private static void DoFilter4(Span<byte> p, int offset, int step)
        {
            // 4 pixels in, 4 pixels out.
            int offsetMinus2Step = offset - (2 * step);
            int p1 = p[offsetMinus2Step];
            int p0 = p[offset - step];
            int q0 = p[offset];
            int q1 = p[offset + step];
            int a = 3 * (q0 - p0);
            int a1 = WebpLookupTables.Sclip2((a + 4) >> 3);
            int a2 = WebpLookupTables.Sclip2((a + 3) >> 3);
            int a3 = (a1 + 1) >> 1;
            p[offsetMinus2Step] = WebpLookupTables.Clip1(p1 + a3);
            p[offset - step] = WebpLookupTables.Clip1(p0 + a2);
            p[offset] = WebpLookupTables.Clip1(q0 - a1);
            p[offset + step] = WebpLookupTables.Clip1(q1 - a3);
        }

        private static void DoFilter6(Span<byte> p, int offset, int step)
        {
            // 6 pixels in, 6 pixels out.
            int step2 = 2 * step;
            int step3 = 3 * step;
            int offsetMinusStep = offset - step;
            int p2 = p[offset - step3];
            int p1 = p[offset - step2];
            int p0 = p[offsetMinusStep];
            int q0 = p[offset];
            int q1 = p[offset + step];
            int q2 = p[offset + step2];
            int a = WebpLookupTables.Sclip1((3 * (q0 - p0)) + WebpLookupTables.Sclip1(p1 - q1));

            // a is in [-128,127], a1 in [-27,27], a2 in [-18,18] and a3 in [-9,9]
            int a1 = ((27 * a) + 63) >> 7;  // eq. to ((3 * a + 7) * 9) >> 7
            int a2 = ((18 * a) + 63) >> 7;  // eq. to ((2 * a + 7) * 9) >> 7
            int a3 = ((9 * a) + 63) >> 7;  // eq. to ((1 * a + 7) * 9) >> 7
            p[offset - step3] = WebpLookupTables.Clip1(p2 + a3);
            p[offset - step2] = WebpLookupTables.Clip1(p1 + a2);
            p[offsetMinusStep] = WebpLookupTables.Clip1(p0 + a1);
            p[offset] = WebpLookupTables.Clip1(q0 - a1);
            p[offset + step] = WebpLookupTables.Clip1(q1 - a2);
            p[offset + step2] = WebpLookupTables.Clip1(q2 - a3);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static bool NeedsFilter(Span<byte> p, int offset, int step, int t)
        {
            int p1 = p[offset + (-2 * step)];
            int p0 = p[offset - step];
            int q0 = p[offset];
            int q1 = p[offset + step];
            return (4 * WebpLookupTables.Abs0(p0 - q0)) + WebpLookupTables.Abs0(p1 - q1) <= t;
        }

        private static bool NeedsFilter2(Span<byte> p, int offset, int step, int t, int it)
        {
            int step2 = 2 * step;
            int step3 = 3 * step;
            int p3 = p[offset - (4 * step)];
            int p2 = p[offset - step3];
            int p1 = p[offset - step2];
            int p0 = p[offset - step];
            int q0 = p[offset];
            int q1 = p[offset + step];
            int q2 = p[offset + step2];
            int q3 = p[offset + step3];
            if ((4 * WebpLookupTables.Abs0(p0 - q0)) + WebpLookupTables.Abs0(p1 - q1) > t)
            {
                return false;
            }

            return WebpLookupTables.Abs0(p3 - p2) <= it && WebpLookupTables.Abs0(p2 - p1) <= it &&
                   WebpLookupTables.Abs0(p1 - p0) <= it && WebpLookupTables.Abs0(q3 - q2) <= it &&
                   WebpLookupTables.Abs0(q2 - q1) <= it && WebpLookupTables.Abs0(q1 - q0) <= it;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static bool Hev(Span<byte> p, int offset, int step, int thresh)
        {
            int p1 = p[offset - (2 * step)];
            int p0 = p[offset - step];
            int q0 = p[offset];
            int q1 = p[offset + step];
            return WebpLookupTables.Abs0(p1 - p0) > thresh || WebpLookupTables.Abs0(q1 - q0) > thresh;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Store(Span<byte> dst, int x, int y, int v)
        {
            int index = x + (y * WebpConstants.Bps);
            dst[index] = Clip8B(dst[index] + (v >> 3));
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Store2(Span<byte> dst, int y, int dc, int d, int c)
        {
            Store(dst, 0, y, dc + d);
            Store(dst, 1, y, dc + c);
            Store(dst, 2, y, dc - c);
            Store(dst, 3, y, dc - d);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Mul1(int a) => ((a * 20091) >> 16) + a;

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Mul2(int a) => (a * 35468) >> 16;

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Put8x8uv(byte value, Span<byte> dst)
        {
            int end = 8 * WebpConstants.Bps;
            for (int j = 0; j < end; j += WebpConstants.Bps)
            {
                // memset(dst + j * BPS, value, 8);
                Memset(dst, value, j, 8);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Memset(Span<byte> dst, byte value, int startIdx, int count)
        {
            int end = startIdx + count;
            for (int i = startIdx; i < end; i++)
            {
                dst[i] = value;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Clamp255(int x) => x < 0 ? 0 : x > 255 ? 255 : x;
    }
}

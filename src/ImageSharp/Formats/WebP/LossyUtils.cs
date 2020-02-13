// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal static class LossyUtils
    {
        private static void Put16(int v, Span<byte> dst)
        {
            for (int j = 0; j < 16; ++j)
            {
                Memset(dst.Slice(j * WebPConstants.Bps), (byte)v, 0, 16);
            }
        }

        public static void DC16_C(Span<byte> dst, byte[] yuv, int offset)
        {
            int dc = 16;
            int j;
            for (j = 0; j < 16; ++j)
            {
                // DC += dst[-1 + j * BPS] + dst[j - BPS];
                dc += yuv[-1 + (j * WebPConstants.Bps) + offset] + yuv[j - WebPConstants.Bps + offset];
            }

            Put16(dc >> 5, dst);
        }

        public static void TM16_C(Span<byte> dst)
        {

        }

        public static void VE16_C(Span<byte> dst, byte[] yuv, int offset)
        {
            // vertical
            Span<byte> src = yuv.AsSpan(offset - WebPConstants.Bps, 16);
            for (int j = 0; j < 16; ++j)
            {
                // memcpy(dst + j * BPS, dst - BPS, 16);
                src.CopyTo(dst.Slice(j * WebPConstants.Bps));
            }
        }

        public static void HE16_C(Span<byte> dst, byte[] yuv, int offset)
        {
            // horizontal
            for (int j = 16; j > 0; --j)
            {
                // memset(dst, dst[-1], 16);
                byte v = yuv[offset - 1];
                Memset(dst, v, 0, 16);
                offset += WebPConstants.Bps;
                dst = dst.Slice(WebPConstants.Bps);
            }
        }

        public static void DC16NoTop_C(Span<byte> dst, byte[] yuv, int offset)
        {
            // DC with top samples not available.
            int dc = 8;
            for (int j = 0; j < 16; ++j)
            {
                // DC += dst[-1 + j * BPS];
                dc += yuv[-1 + (j * WebPConstants.Bps) + offset];
            }

            Put16(dc >> 4, dst);
        }

        public static void DC16NoLeft_C(Span<byte> dst, byte[] yuv, int offset)
        {
            // DC with left samples not available.
            int dc = 8;
            for (int i = 0; i < 16; ++i)
            {
                // DC += dst[i - BPS];
                dc += yuv[i - WebPConstants.Bps + offset];
            }

            Put16(dc >> 4, dst);
        }

        public static void DC16NoTopLeft_C(Span<byte> dst)
        {
            // DC with no top and left samples.
            Put16(0x80, dst);
        }

        public static void DC8uv_C(Span<byte> dst, byte[] yuv, int offset)
        {
            int dc0 = 8;
            for (int i = 0; i < 8; ++i)
            {
                // dc0 += dst[i - BPS] + dst[-1 + i * BPS];
                dc0 += yuv[offset + i - WebPConstants.Bps] + yuv[offset - 1 + (i * WebPConstants.Bps)];
            }

            Put8x8uv((byte)(dc0 >> 4), dst);
        }

        public static void TM8uv_C(Span<byte> dst)
        {
            // TrueMotion
        }

        public static void VE8uv_C(Span<byte> dst, Span<byte> src)
        {
            // vertical
            for (int j = 0; j < 8; ++j)
            {
                // memcpy(dst + j * BPS, dst - BPS, 8);
                src.CopyTo(dst.Slice(j * WebPConstants.Bps));
            }
        }

        public static void HE8uv_C(Span<byte> dst, byte[] yuv, int offset)
        {
            // horizontal
            for (int j = 0; j < 8; ++j)
            {
                // memset(dst, dst[-1], 8);
                byte v = yuv[offset - 1];
                Memset(dst, v, 0, 8);
                dst = dst.Slice(WebPConstants.Bps);
            }
        }

        public static void DC8uvNoTop_C(Span<byte> dst, byte[] yuv, int offset)
        {
            // DC with no top samples.
            int dc0 = 4;
            for (int i = 0; i < 8; ++i)
            {
                // dc0 += dst[-1 + i * BPS];
                dc0 += yuv[offset - 1 + (i * WebPConstants.Bps)];
            }

            Put8x8uv((byte)(dc0 >> 3), dst);
        }

        public static void DC8uvNoLeft_C(Span<byte> dst, byte[] yuv, int offset)
        {
            // DC with no left samples.
            int dc0 = 4;
            for (int i = 0; i < 8; ++i)
            {
                // dc0 += dst[i - BPS];
                dc0 += yuv[offset + i - WebPConstants.Bps];
            }

            Put8x8uv((byte)(dc0 >> 3), dst);
        }

        public static void DC8uvNoTopLeft_C(Span<byte> dst)
        {
            // DC with nothing.
            Put8x8uv(0x80, dst);
        }

        public static void DC4_C(Span<byte> dst)
        {

        }

        public static void TM4_C(Span<byte> dst)
        {

        }

        public static void VE4_C(Span<byte> dst)
        {

        }

        public static void HE4_C(Span<byte> dst)
        {

        }

        public static void RD4_C(Span<byte> dst)
        {

        }

        public static void VR4_C(Span<byte> dst)
        {

        }

        public static void LD4_C(Span<byte> dst)
        {

        }

        public static void VL4_C(Span<byte> dst)
        {

        }

        public static void HD4_C(Span<byte> dst)
        {
            
        }

        public static void HU4_C(Span<byte> dst)
        {

        }

        public static void Transform(Span<short> src, Span<byte> dst, bool doTwo)
        {
            TransformOne(src, dst);
            if (doTwo)
            {
                TransformOne(src, dst);
            }
        }

        public static void TransformOne(Span<short> src, Span<byte> dst)
        {
            var tmp = new int[4 * 4];
            int tmpOffset = 0;
            int srcOffset = 0;
            for (int i = 0; i < 4; ++i)
            {
                // vertical pass
                int a = src[srcOffset] + src[srcOffset + 8]; // [-4096, 4094]
                int b = src[srcOffset] - src[srcOffset + 8]; // [-4095, 4095]
                int c = Mul2(src[4]) - Mul1(src[12]); // [-3783, 3783]
                int d = Mul1(src[4]) + Mul2(src[12]); // [-3785, 3781]
                tmp[tmpOffset] = a + d; // [-7881, 7875]
                tmp[tmpOffset + 1] = b + c; // [-7878, 7878]
                tmp[tmpOffset + 2] = b - c; // [-7878, 7878]
                tmp[tmpOffset + 3] = a - d; // [-7877, 7879]
                tmpOffset += 4;
                srcOffset++;
            }

            // Each pass is expanding the dynamic range by ~3.85 (upper bound).
            // The exact value is (2. + (20091 + 35468) / 65536).
            // After the second pass, maximum interval is [-3794, 3794], assuming
            // an input in [-2048, 2047] interval. We then need to add a dst value
            // in the [0, 255] range.
            // In the worst case scenario, the input to clip_8b() can be as large as
            // [-60713, 60968].
            tmpOffset = 0;
            for (int i = 0; i < 4; ++i)
            {
                // horizontal pass
                int dc = tmp[tmpOffset] + 4;
                int a = dc + tmp[tmpOffset + 8];
                int b = dc - tmp[tmpOffset + 8];
                int c = Mul2(tmp[tmpOffset + 4]) - Mul1(tmp[tmpOffset + 12]);
                int d = Mul1(tmp[tmpOffset + 4]) + Mul2(tmp[tmpOffset + 12]);
                Store(dst, 0, 0, a + d);
                Store(dst, 1, 0, b + c);
                Store(dst, 2, 0, b - c);
                Store(dst, 3, 0, a - d);
                tmpOffset++;
                dst = dst.Slice(WebPConstants.Bps);
            }
        }

        public static void TransformDc(Span<short> src, Span<byte> dst)
        {
            int dc = src[0] + 4;
            for (int j = 0; j < 4; ++j)
            {
                for (int i = 0; i < 4; ++i)
                {
                    Store(dst, i, j, dc);
                }
            }
        }

        // Simplified transform when only in[0], in[1] and in[4] are non-zero
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

        public static void TransformUv(Span<short> src, Span<byte> dst)
        {
            Transform(src.Slice(0 * 16), dst, true);
            Transform(src.Slice(2 * 16), dst.Slice(4 * WebPConstants.Bps), true);
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
                TransformDc(src.Slice(2 * 16), dst.Slice(4 * WebPConstants.Bps));
            }

            if (src[3 * 16] != 0)
            {
                TransformDc(src.Slice(3 * 16), dst.Slice((4 * WebPConstants.Bps) + 4));
            }
        }

        private static void Store(Span<byte> dst, int x, int y, int v)
        {
            dst[x + (y * WebPConstants.Bps)] = Clip8B(dst[x + (y * WebPConstants.Bps)] + (v >> 3));
        }

        private static void Store2(Span<byte> dst, int y, int dc, int d, int c)
        {
            Store(dst, 0, y, dc + d);
            Store(dst, 1, y, dc + c);
            Store(dst, 2, y, dc - c);
            Store(dst, 3, y, dc - d);
        }

        private static int Mul1(int a)
        {
            return ((a * 20091) >> 16) + a;
        }

        private static int Mul2(int a)
        {
            return (a * 35468) >> 16;
        }

        private static byte Clip8B(int v)
        {
            return (byte)((v & ~0xff) is 0 ? v : (v < 0) ? 0 : 255);
        }

        private static void Put8x8uv(byte value, Span<byte> dst)
        {
            for (int j = 0; j < 8; ++j)
            {
                // memset(dst + j * BPS, value, 8);
                Memset(dst, value, j * WebPConstants.Bps, 8);
            }
        }

        private static void Memset(Span<byte> dst, byte value, int startIdx, int count)
        {
            for (int i = 0; i < count; i++)
            {
                dst[startIdx + i] = value;
            }
        }

        private static byte Avg2(byte a, byte b)
        {
            return (byte)((a + b + 1) >> 1);
        }

        private static byte Avg3(byte a, byte b, byte c)
        {
            return (byte)((a + (2 * b) + c + 2) >> 2);
        }
    }
}

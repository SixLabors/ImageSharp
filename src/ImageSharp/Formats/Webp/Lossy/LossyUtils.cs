// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

internal static class LossyUtils
{
    // Note: method name in libwebp reference implementation is called VP8SSE16x16.
    [MethodImpl(InliningOptions.ShortMethod)]
    public static int Vp8_Sse16x16(Span<byte> a, Span<byte> b)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            return Vp8_Sse16xN_Vector256(a, b, 4);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            return Vp8_16xN_Vector128(a, b, 8);
        }

        return Vp8_SseNxN(a, b, 16, 16);
    }

    // Note: method name in libwebp reference implementation is called VP8SSE16x8.
    [MethodImpl(InliningOptions.ShortMethod)]
    public static int Vp8_Sse16x8(Span<byte> a, Span<byte> b)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            return Vp8_Sse16xN_Vector256(a, b, 2);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            return Vp8_16xN_Vector128(a, b, 4);
        }

        return Vp8_SseNxN(a, b, 16, 8);
    }

    // Note: method name in libwebp reference implementation is called VP8SSE4x4.
    [MethodImpl(InliningOptions.ShortMethod)]
    public static int Vp8_Sse4x4(Span<byte> a, Span<byte> b)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            // Load values.
            ref byte aRef = ref MemoryMarshal.GetReference(a);
            ref byte bRef = ref MemoryMarshal.GetReference(b);
            Vector256<byte> a0 = Vector256.Create(
                Unsafe.As<byte, Vector128<byte>>(ref aRef),
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, WebpConstants.Bps)));
            Vector256<byte> a1 = Vector256.Create(
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, WebpConstants.Bps * 2)),
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, WebpConstants.Bps * 3)));
            Vector256<byte> b0 = Vector256.Create(
                Unsafe.As<byte, Vector128<byte>>(ref bRef),
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, WebpConstants.Bps)));
            Vector256<byte> b1 = Vector256.Create(
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, WebpConstants.Bps * 2)),
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, WebpConstants.Bps * 3)));

            // Combine pair of lines.
            Vector256<int> a01 = Vector256_.UnpackLow(a0.AsInt32(), a1.AsInt32());
            Vector256<int> b01 = Vector256_.UnpackLow(b0.AsInt32(), b1.AsInt32());

            // Convert to 16b.
            Vector256<byte> a01s = Vector256_.UnpackLow(a01.AsByte(), Vector256<byte>.Zero);
            Vector256<byte> b01s = Vector256_.UnpackLow(b01.AsByte(), Vector256<byte>.Zero);

            // subtract, square and accumulate.
            Vector256<short> d0 = Vector256_.SubtractSaturate(a01s.AsInt16(), b01s.AsInt16());
            Vector256<int> e0 = Vector256_.MultiplyAddAdjacent(d0, d0);

            return ReduceSumVector256(e0);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            // Load values.
            ref byte aRef = ref MemoryMarshal.GetReference(a);
            ref byte bRef = ref MemoryMarshal.GetReference(b);
            Vector128<byte> a0 = Unsafe.As<byte, Vector128<byte>>(ref aRef);
            Vector128<byte> a1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, WebpConstants.Bps));
            Vector128<byte> a2 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, WebpConstants.Bps * 2));
            Vector128<byte> a3 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, WebpConstants.Bps * 3));
            Vector128<byte> b0 = Unsafe.As<byte, Vector128<byte>>(ref bRef);
            Vector128<byte> b1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, WebpConstants.Bps));
            Vector128<byte> b2 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, WebpConstants.Bps * 2));
            Vector128<byte> b3 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, WebpConstants.Bps * 3));

            // Combine pair of lines.
            Vector128<int> a01 = Vector128_.UnpackLow(a0.AsInt32(), a1.AsInt32());
            Vector128<int> a23 = Vector128_.UnpackLow(a2.AsInt32(), a3.AsInt32());
            Vector128<int> b01 = Vector128_.UnpackLow(b0.AsInt32(), b1.AsInt32());
            Vector128<int> b23 = Vector128_.UnpackLow(b2.AsInt32(), b3.AsInt32());

            // Convert to 16b.
            Vector128<byte> a01s = Vector128_.UnpackLow(a01.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> a23s = Vector128_.UnpackLow(a23.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> b01s = Vector128_.UnpackLow(b01.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> b23s = Vector128_.UnpackLow(b23.AsByte(), Vector128<byte>.Zero);

            // subtract, square and accumulate.
            Vector128<short> d0 = Vector128_.SubtractSaturate(a01s.AsInt16(), b01s.AsInt16());
            Vector128<short> d1 = Vector128_.SubtractSaturate(a23s.AsInt16(), b23s.AsInt16());
            Vector128<int> e0 = Vector128_.MultiplyAddAdjacent(d0, d0);
            Vector128<int> e1 = Vector128_.MultiplyAddAdjacent(d1, d1);
            Vector128<int> sum = e0 + e1;

            return ReduceSumVector128(sum);
        }

        return Vp8_SseNxN(a, b, 4, 4);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public static int Vp8_SseNxN(Span<byte> a, Span<byte> b, int w, int h)
    {
        int count = 0;
        int offset = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int diff = a[offset + x] - b[offset + x];
                count += diff * diff;
            }

            offset += WebpConstants.Bps;
        }

        return count;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int Vp8_16xN_Vector128(Span<byte> a, Span<byte> b, int numPairs)
    {
        Vector128<int> sum = Vector128<int>.Zero;
        nuint offset = 0;
        ref byte aRef = ref MemoryMarshal.GetReference(a);
        ref byte bRef = ref MemoryMarshal.GetReference(b);
        for (int i = 0; i < numPairs; i++)
        {
            // Load values.
            Vector128<byte> a0 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, offset));
            Vector128<byte> b0 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, offset));
            Vector128<byte> a1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, offset + WebpConstants.Bps));
            Vector128<byte> b1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, offset + WebpConstants.Bps));

            Vector128<int> sum1 = SubtractAndAccumulateVector128(a0, b0);
            Vector128<int> sum2 = SubtractAndAccumulateVector128(a1, b1);
            sum += sum1 + sum2;

            offset += 2 * WebpConstants.Bps;
        }

        return ReduceSumVector128(sum);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int Vp8_Sse16xN_Vector256(Span<byte> a, Span<byte> b, int numPairs)
    {
        Vector256<int> sum = Vector256<int>.Zero;
        nuint offset = 0;
        ref byte aRef = ref MemoryMarshal.GetReference(a);
        ref byte bRef = ref MemoryMarshal.GetReference(b);
        for (int i = 0; i < numPairs; i++)
        {
            // Load values.
            Vector256<byte> a0 = Vector256.Create(
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, offset)),
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, offset + WebpConstants.Bps)));
            Vector256<byte> b0 = Vector256.Create(
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, offset)),
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, offset + WebpConstants.Bps)));
            Vector256<byte> a1 = Vector256.Create(
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, offset + (2 * WebpConstants.Bps))),
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref aRef, offset + (3 * WebpConstants.Bps))));
            Vector256<byte> b1 = Vector256.Create(
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, offset + (2 * WebpConstants.Bps))),
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref bRef, offset + (3 * WebpConstants.Bps))));

            Vector256<int> sum1 = SubtractAndAccumulateVector256(a0, b0);
            Vector256<int> sum2 = SubtractAndAccumulateVector256(a1, b1);
            sum += sum1 + sum2;

            offset += 4 * WebpConstants.Bps;
        }

        return ReduceSumVector256(sum);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector128<int> SubtractAndAccumulateVector128(Vector128<byte> a, Vector128<byte> b)
    {
        // Take abs(a-b) in 8b.
        Vector128<byte> ab = Vector128_.SubtractSaturate(a, b);
        Vector128<byte> ba = Vector128_.SubtractSaturate(b, a);
        Vector128<byte> absAb = ab | ba;

        // Zero-extend to 16b.
        Vector128<byte> c0 = Vector128_.UnpackLow(absAb, Vector128<byte>.Zero);
        Vector128<byte> c1 = Vector128_.UnpackHigh(absAb, Vector128<byte>.Zero);

        // Multiply with self.
        Vector128<int> sum1 = Vector128_.MultiplyAddAdjacent(c0.AsInt16(), c0.AsInt16());
        Vector128<int> sum2 = Vector128_.MultiplyAddAdjacent(c1.AsInt16(), c1.AsInt16());

        return sum1 + sum2;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector256<int> SubtractAndAccumulateVector256(Vector256<byte> a, Vector256<byte> b)
    {
        // Take abs(a-b) in 8b.
        Vector256<byte> ab = Vector256_.SubtractSaturate(a, b);
        Vector256<byte> ba = Vector256_.SubtractSaturate(b, a);
        Vector256<byte> absAb = ab | ba;

        // Zero-extend to 16b.
        Vector256<byte> c0 = Vector256_.UnpackLow(absAb, Vector256<byte>.Zero);
        Vector256<byte> c1 = Vector256_.UnpackHigh(absAb, Vector256<byte>.Zero);

        // Multiply with self.
        Vector256<int> sum1 = Vector256_.MultiplyAddAdjacent(c0.AsInt16(), c0.AsInt16());
        Vector256<int> sum2 = Vector256_.MultiplyAddAdjacent(c1.AsInt16(), c1.AsInt16());

        return sum1 + sum2;
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
        const int dataSize = (4 * WebpConstants.Bps) - 16;
        for (int y = 0; y < 16 * WebpConstants.Bps; y += 4 * WebpConstants.Bps)
        {
            for (int x = 0; x < 16; x += 4)
            {
                d += Vp8Disto4X4(a.Slice(x + y, dataSize), b.Slice(x + y, dataSize), w, scratch);
            }
        }

        return d;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public static int Vp8Disto4X4(Span<byte> a, Span<byte> b, Span<ushort> w, Span<int> scratch)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            int diffSum = TTransformVector128(a, b, w);
            return Math.Abs(diffSum) >> 5;
        }

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
            src.CopyTo(dst[(j * WebpConstants.Bps)..]);
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
            dst = dst[WebpConstants.Bps..];
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

        const int endIdx = 8 * WebpConstants.Bps;
        for (int j = 0; j < endIdx; j += WebpConstants.Bps)
        {
            // memcpy(dst + j * BPS, dst - BPS, 8);
            src.CopyTo(dst[j..]);
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
            dst = dst[WebpConstants.Bps..];
            offset += WebpConstants.Bps;
        }
    }

    public static void DC8uvNoTop(Span<byte> dst, Span<byte> yuv, int offset)
    {
        // DC with no top samples.
        int dc0 = 4;
        int offsetMinusOne = offset - 1;
        const int endIdx = 8 * WebpConstants.Bps;
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
        const int endIndx = 4 * WebpConstants.Bps;
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
        const int endIdx = 4 * WebpConstants.Bps;
        for (int i = 0; i < endIdx; i += WebpConstants.Bps)
        {
            vals.CopyTo(dst[i..]);
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
        BinaryPrimitives.WriteUInt32BigEndian(dst[WebpConstants.Bps..], val);
        val = 0x01010101U * Avg3(c, d, e);
        BinaryPrimitives.WriteUInt32BigEndian(dst[(2 * WebpConstants.Bps)..], val);
        val = 0x01010101U * Avg3(d, e, e);
        BinaryPrimitives.WriteUInt32BigEndian(dst[(3 * WebpConstants.Bps)..], val);
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
        Span<int> tmp = scratch[..16];
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
        Span<int> tmp = scratch[..16];
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

            w = w[1..];
        }

        return sum;
    }

    /// <summary>
    /// Hadamard transform
    /// Returns the weighted sum of the absolute value of transformed coefficients.
    /// w[] contains a row-major 4 by 4 symmetric matrix.
    /// </summary>
    public static int TTransformVector128(Span<byte> inputA, Span<byte> inputB, Span<ushort> w)
    {
        // Load and combine inputs.
        Vector128<byte> ina0 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(inputA));
        Vector128<byte> ina1 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(inputA.Slice(WebpConstants.Bps, 16)));
        Vector128<byte> ina2 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(inputA.Slice(WebpConstants.Bps * 2, 16)));
        Vector128<long> ina3 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(inputA.Slice(WebpConstants.Bps * 3, 16))).AsInt64();
        Vector128<byte> inb0 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(inputB));
        Vector128<byte> inb1 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(inputB.Slice(WebpConstants.Bps, 16)));
        Vector128<byte> inb2 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(inputB.Slice(WebpConstants.Bps * 2, 16)));
        Vector128<long> inb3 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(inputB.Slice(WebpConstants.Bps * 3, 16))).AsInt64();

        // Combine inA and inB (we'll do two transforms in parallel).
        Vector128<int> inab0 = Vector128_.UnpackLow(ina0.AsInt32(), inb0.AsInt32());
        Vector128<int> inab1 = Vector128_.UnpackLow(ina1.AsInt32(), inb1.AsInt32());
        Vector128<int> inab2 = Vector128_.UnpackLow(ina2.AsInt32(), inb2.AsInt32());
        Vector128<int> inab3 = Vector128_.UnpackLow(ina3.AsInt32(), inb3.AsInt32());
        Vector128<short> tmp0 = Vector128.WidenLower(inab0.AsByte()).AsInt16();
        Vector128<short> tmp1 = Vector128.WidenLower(inab1.AsByte()).AsInt16();
        Vector128<short> tmp2 = Vector128.WidenLower(inab2.AsByte()).AsInt16();
        Vector128<short> tmp3 = Vector128.WidenLower(inab3.AsByte()).AsInt16();

        // a00 a01 a02 a03   b00 b01 b02 b03
        // a10 a11 a12 a13   b10 b11 b12 b13
        // a20 a21 a22 a23   b20 b21 b22 b23
        // a30 a31 a32 a33   b30 b31 b32 b33
        // Vertical pass first to avoid a transpose (vertical and horizontal passes
        // are commutative because w/kWeightY is symmetric) and subsequent transpose.
        // Calculate a and b (two 4x4 at once).
        Vector128<short> a0 = tmp0 + tmp2;
        Vector128<short> a1 = tmp1 + tmp3;
        Vector128<short> a2 = tmp1 - tmp3;
        Vector128<short> a3 = tmp0 - tmp2;
        Vector128<short> b0 = a0 + a1;
        Vector128<short> b1 = a3 + a2;
        Vector128<short> b2 = a3 - a2;
        Vector128<short> b3 = a0 - a1;

        // a00 a01 a02 a03   b00 b01 b02 b03
        // a10 a11 a12 a13   b10 b11 b12 b13
        // a20 a21 a22 a23   b20 b21 b22 b23
        // a30 a31 a32 a33   b30 b31 b32 b33
        // Transpose the two 4x4.
        Vp8Transpose_2_4x4_16bVector128(b0, b1, b2, b3, out Vector128<long> output0, out Vector128<long> output1, out Vector128<long> output2, out Vector128<long> output3);

        // a00 a10 a20 a30   b00 b10 b20 b30
        // a01 a11 a21 a31   b01 b11 b21 b31
        // a02 a12 a22 a32   b02 b12 b22 b32
        // a03 a13 a23 a33   b03 b13 b23 b33
        // Horizontal pass and difference of weighted sums.
        Vector128<ushort> w0 = Unsafe.As<ushort, Vector128<ushort>>(ref MemoryMarshal.GetReference(w));
        Vector128<ushort> w8 = Unsafe.As<ushort, Vector128<ushort>>(ref MemoryMarshal.GetReference(w.Slice(8, 8)));

        // Calculate a and b (two 4x4 at once).
        a0 = output0.AsInt16() + output2.AsInt16();
        a1 = output1.AsInt16() + output3.AsInt16();
        a2 = output1.AsInt16() - output3.AsInt16();
        a3 = output0.AsInt16() - output2.AsInt16();
        b0 = a0 + a1;
        b1 = a3 + a2;
        b2 = a3 - a2;
        b3 = a0 - a1;

        // Separate the transforms of inA and inB.
        Vector128<long> ab0 = Vector128_.UnpackLow(b0.AsInt64(), b1.AsInt64());
        Vector128<long> ab2 = Vector128_.UnpackLow(b2.AsInt64(), b3.AsInt64());
        Vector128<long> bb0 = Vector128_.UnpackHigh(b0.AsInt64(), b1.AsInt64());
        Vector128<long> bb2 = Vector128_.UnpackHigh(b2.AsInt64(), b3.AsInt64());

        Vector128<short> ab0Abs = Vector128.Abs(ab0.AsInt16());
        Vector128<short> ab2Abs = Vector128.Abs(ab2.AsInt16());
        Vector128<short> b0Abs = Vector128.Abs(bb0.AsInt16());
        Vector128<short> bb2Abs = Vector128.Abs(bb2.AsInt16());

        // weighted sums.
        Vector128<int> ab0mulw0 = Vector128_.MultiplyAddAdjacent(ab0Abs, w0.AsInt16());
        Vector128<int> ab2mulw8 = Vector128_.MultiplyAddAdjacent(ab2Abs, w8.AsInt16());
        Vector128<int> b0mulw0 = Vector128_.MultiplyAddAdjacent(b0Abs, w0.AsInt16());
        Vector128<int> bb2mulw8 = Vector128_.MultiplyAddAdjacent(bb2Abs, w8.AsInt16());
        Vector128<int> ab0ab2Sum = ab0mulw0 + ab2mulw8;
        Vector128<int> b0w0bb2w8Sum = b0mulw0 + bb2mulw8;

        // difference of weighted sums.
        Vector128<int> result = ab0ab2Sum - b0w0bb2w8Sum;

        return ReduceSumVector128(result);
    }

    // Transpose two 4x4 16b matrices horizontally stored in registers.
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void Vp8Transpose_2_4x4_16bVector128(Vector128<short> b0, Vector128<short> b1, Vector128<short> b2, Vector128<short> b3, out Vector128<long> output0, out Vector128<long> output1, out Vector128<long> output2, out Vector128<long> output3)
    {
        // Transpose the two 4x4.
        // a00 a01 a02 a03   b00 b01 b02 b03
        // a10 a11 a12 a13   b10 b11 b12 b13
        // a20 a21 a22 a23   b20 b21 b22 b23
        // a30 a31 a32 a33   b30 b31 b32 b33
        Vector128<short> transpose00 = Vector128_.UnpackLow(b0, b1);
        Vector128<short> transpose01 = Vector128_.UnpackLow(b2, b3);
        Vector128<short> transpose02 = Vector128_.UnpackHigh(b0, b1);
        Vector128<short> transpose03 = Vector128_.UnpackHigh(b2, b3);

        // a00 a10 a01 a11   a02 a12 a03 a13
        // a20 a30 a21 a31   a22 a32 a23 a33
        // b00 b10 b01 b11   b02 b12 b03 b13
        // b20 b30 b21 b31   b22 b32 b23 b33
        Vector128<int> transpose10 = Vector128_.UnpackLow(transpose00.AsInt32(), transpose01.AsInt32());
        Vector128<int> transpose11 = Vector128_.UnpackLow(transpose02.AsInt32(), transpose03.AsInt32());
        Vector128<int> transpose12 = Vector128_.UnpackHigh(transpose00.AsInt32(), transpose01.AsInt32());
        Vector128<int> transpose13 = Vector128_.UnpackHigh(transpose02.AsInt32(), transpose03.AsInt32());

        // a00 a10 a20 a30 a01 a11 a21 a31
        // b00 b10 b20 b30 b01 b11 b21 b31
        // a02 a12 a22 a32 a03 a13 a23 a33
        // b02 b12 a22 b32 b03 b13 b23 b33
        output0 = Vector128_.UnpackLow(transpose10.AsInt64(), transpose11.AsInt64());
        output1 = Vector128_.UnpackHigh(transpose10.AsInt64(), transpose11.AsInt64());
        output2 = Vector128_.UnpackLow(transpose12.AsInt64(), transpose13.AsInt64());
        output3 = Vector128_.UnpackHigh(transpose12.AsInt64(), transpose13.AsInt64());

        // a00 a10 a20 a30   b00 b10 b20 b30
        // a01 a11 a21 a31   b01 b11 b21 b31
        // a02 a12 a22 a32   b02 b12 b22 b32
        // a03 a13 a23 a33   b03 b13 b23 b33
    }

    // Transforms (Paragraph 14.4).
    // Does two transforms.
    public static void TransformTwo(Span<short> src, Span<byte> dst, Span<int> scratch)
    {
        if (Vector128.IsHardwareAccelerated)
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

            // Load and concatenate the transform coefficients (we'll do two transforms
            // in parallel).
            ref short srcRef = ref MemoryMarshal.GetReference(src);
            Vector128<long> in0 = Vector128.Create(Unsafe.As<short, long>(ref srcRef), 0);
            Vector128<long> in1 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref srcRef, 4)), 0);
            Vector128<long> in2 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref srcRef, 8)), 0);
            Vector128<long> in3 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref srcRef, 12)), 0);

            // a00 a10 a20 a30   x x x x
            // a01 a11 a21 a31   x x x x
            // a02 a12 a22 a32   x x x x
            // a03 a13 a23 a33   x x x x
            Vector128<long> inb0 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref srcRef, 16)), 0);
            Vector128<long> inb1 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref srcRef, 20)), 0);
            Vector128<long> inb2 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref srcRef, 24)), 0);
            Vector128<long> inb3 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref srcRef, 28)), 0);

            in0 = Vector128_.UnpackLow(in0, inb0);
            in1 = Vector128_.UnpackLow(in1, inb1);
            in2 = Vector128_.UnpackLow(in2, inb2);
            in3 = Vector128_.UnpackLow(in3, inb3);

            // a00 a10 a20 a30   b00 b10 b20 b30
            // a01 a11 a21 a31   b01 b11 b21 b31
            // a02 a12 a22 a32   b02 b12 b22 b32
            // a03 a13 a23 a33   b03 b13 b23 b33

            // Vertical pass and subsequent transpose.
            // First pass, c and d calculations are longer because of the "trick" multiplications.
            Vector128<short> a = in0.AsInt16() + in2.AsInt16();
            Vector128<short> b = in0.AsInt16() - in2.AsInt16();

            Vector128<short> k1 = Vector128.Create((short)20091);
            Vector128<short> k2 = Vector128.Create((short)-30068);

            // c = MUL(in1, K2) - MUL(in3, K1) = MUL(in1, k2) - MUL(in3, k1) + in1 - in3
            Vector128<short> c1 = Vector128_.MultiplyHigh(in1.AsInt16(), k2);
            Vector128<short> c2 = Vector128_.MultiplyHigh(in3.AsInt16(), k1);
            Vector128<short> c3 = in1.AsInt16() - in3.AsInt16();
            Vector128<short> c4 = c1 - c2;
            Vector128<short> c = c3.AsInt16() + c4;

            // d = MUL(in1, K1) + MUL(in3, K2) = MUL(in1, k1) + MUL(in3, k2) + in1 + in3
            Vector128<short> d1 = Vector128_.MultiplyHigh(in1.AsInt16(), k1);
            Vector128<short> d2 = Vector128_.MultiplyHigh(in3.AsInt16(), k2);
            Vector128<short> d3 = in1.AsInt16() + in3.AsInt16();
            Vector128<short> d4 = d1 + d2;
            Vector128<short> d = d3 + d4;

            // Second pass.
            Vector128<short> tmp0 = a.AsInt16() + d;
            Vector128<short> tmp1 = b.AsInt16() + c;
            Vector128<short> tmp2 = b.AsInt16() - c;
            Vector128<short> tmp3 = a.AsInt16() - d;

            // Transpose the two 4x4.
            Vp8Transpose_2_4x4_16bVector128(tmp0, tmp1, tmp2, tmp3, out Vector128<long> t0, out Vector128<long> t1, out Vector128<long> t2, out Vector128<long> t3);

            // Horizontal pass and subsequent transpose.
            // First pass, c and d calculations are longer because of the "trick" multiplications.
            Vector128<short> dc = t0.AsInt16() + Vector128.Create((short)4);
            a = dc + t2.AsInt16();
            b = dc - t2.AsInt16();

            // c = MUL(T1, K2) - MUL(T3, K1) = MUL(T1, k2) - MUL(T3, k1) + T1 - T3
            c1 = Vector128_.MultiplyHigh(t1.AsInt16(), k2);
            c2 = Vector128_.MultiplyHigh(t3.AsInt16(), k1);
            c3 = t1.AsInt16() - t3.AsInt16();
            c4 = c1 - c2;
            c = c3 + c4;

            // d = MUL(T1, K1) + MUL(T3, K2) = MUL(T1, k1) + MUL(T3, k2) + T1 + T3
            d1 = Vector128_.MultiplyHigh(t1.AsInt16(), k1);
            d2 = Vector128_.MultiplyHigh(t3.AsInt16(), k2);
            d3 = t1.AsInt16() + t3.AsInt16();
            d4 = d1 + d2;
            d = d3 + d4;

            // Second pass.
            tmp0 = a + d;
            tmp1 = b + c;
            tmp2 = b - c;
            tmp3 = a - d;
            Vector128<short> shifted0 = Vector128.ShiftRightArithmetic(tmp0, 3);
            Vector128<short> shifted1 = Vector128.ShiftRightArithmetic(tmp1, 3);
            Vector128<short> shifted2 = Vector128.ShiftRightArithmetic(tmp2, 3);
            Vector128<short> shifted3 = Vector128.ShiftRightArithmetic(tmp3, 3);

            // Transpose the two 4x4.
            Vp8Transpose_2_4x4_16bVector128(shifted0, shifted1, shifted2, shifted3, out t0, out t1, out t2, out t3);

            // Add inverse transform to 'dst' and store.
            // Load the reference(s).
            // Load eight bytes/pixels per line.
            ref byte dstRef = ref MemoryMarshal.GetReference(dst);
            Vector128<byte> dst0 = Vector128.Create(Unsafe.As<byte, long>(ref dstRef), 0).AsByte();
            Vector128<byte> dst1 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref dstRef, WebpConstants.Bps)), 0).AsByte();
            Vector128<byte> dst2 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref dstRef, WebpConstants.Bps * 2)), 0).AsByte();
            Vector128<byte> dst3 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref dstRef, WebpConstants.Bps * 3)), 0).AsByte();

            // Convert to 16b.
            dst0 = Vector128_.UnpackLow(dst0, Vector128<byte>.Zero);
            dst1 = Vector128_.UnpackLow(dst1, Vector128<byte>.Zero);
            dst2 = Vector128_.UnpackLow(dst2, Vector128<byte>.Zero);
            dst3 = Vector128_.UnpackLow(dst3, Vector128<byte>.Zero);

            // Add the inverse transform(s).
            dst0 = (dst0.AsInt16() + t0.AsInt16()).AsByte();
            dst1 = (dst1.AsInt16() + t1.AsInt16()).AsByte();
            dst2 = (dst2.AsInt16() + t2.AsInt16()).AsByte();
            dst3 = (dst3.AsInt16() + t3.AsInt16()).AsByte();

            // Unsigned saturate to 8b.
            dst0 = Vector128_.PackUnsignedSaturate(dst0.AsInt16(), dst0.AsInt16());
            dst1 = Vector128_.PackUnsignedSaturate(dst1.AsInt16(), dst1.AsInt16());
            dst2 = Vector128_.PackUnsignedSaturate(dst2.AsInt16(), dst2.AsInt16());
            dst3 = Vector128_.PackUnsignedSaturate(dst3.AsInt16(), dst3.AsInt16());

            // Store the results.
            // Store eight bytes/pixels per line.
            ref byte outputRef = ref MemoryMarshal.GetReference(dst);
            Unsafe.As<byte, Vector64<byte>>(ref outputRef) = dst0.GetLower();
            Unsafe.As<byte, Vector64<byte>>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps)) = dst1.GetLower();
            Unsafe.As<byte, Vector64<byte>>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps * 2)) = dst2.GetLower();
            Unsafe.As<byte, Vector64<byte>>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps * 3)) = dst3.GetLower();
        }
        else
        {
            TransformOne(src, dst, scratch);
            TransformOne(src[16..], dst[4..], scratch);
        }
    }

    public static void TransformOne(Span<short> src, Span<byte> dst, Span<int> scratch)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            // Load and concatenate the transform coefficients.
            ref short srcRef = ref MemoryMarshal.GetReference(src);
            Vector128<long> in0 = Vector128.Create(Unsafe.As<short, long>(ref srcRef), 0);
            Vector128<long> in1 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref srcRef, 4)), 0);
            Vector128<long> in2 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref srcRef, 8)), 0);
            Vector128<long> in3 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref srcRef, 12)), 0);

            // a00 a10 a20 a30   x x x x
            // a01 a11 a21 a31   x x x x
            // a02 a12 a22 a32   x x x x
            // a03 a13 a23 a33   x x x x

            // Vertical pass and subsequent transpose.
            // First pass, c and d calculations are longer because of the "trick" multiplications.
            Vector128<short> a = in0.AsInt16() + in2.AsInt16();
            Vector128<short> b = in0.AsInt16() - in2.AsInt16();

            Vector128<short> k1 = Vector128.Create((short)20091);
            Vector128<short> k2 = Vector128.Create((short)-30068);

            // c = MUL(in1, K2) - MUL(in3, K1) = MUL(in1, k2) - MUL(in3, k1) + in1 - in3
            Vector128<short> c1 = Vector128_.MultiplyHigh(in1.AsInt16(), k2);
            Vector128<short> c2 = Vector128_.MultiplyHigh(in3.AsInt16(), k1);
            Vector128<short> c3 = in1.AsInt16() - in3.AsInt16();
            Vector128<short> c4 = c1 - c2;
            Vector128<short> c = c3.AsInt16() + c4;

            // d = MUL(in1, K1) + MUL(in3, K2) = MUL(in1, k1) + MUL(in3, k2) + in1 + in3
            Vector128<short> d1 = Vector128_.MultiplyHigh(in1.AsInt16(), k1);
            Vector128<short> d2 = Vector128_.MultiplyHigh(in3.AsInt16(), k2);
            Vector128<short> d3 = in1.AsInt16() + in3.AsInt16();
            Vector128<short> d4 = d1 + d2;
            Vector128<short> d = d3 + d4;

            // Second pass.
            Vector128<short> tmp0 = a.AsInt16() + d;
            Vector128<short> tmp1 = b.AsInt16() + c;
            Vector128<short> tmp2 = b.AsInt16() - c;
            Vector128<short> tmp3 = a.AsInt16() - d;

            // Transpose the two 4x4.
            Vp8Transpose_2_4x4_16bVector128(tmp0, tmp1, tmp2, tmp3, out Vector128<long> t0, out Vector128<long> t1, out Vector128<long> t2, out Vector128<long> t3);

            // Horizontal pass and subsequent transpose.
            // First pass, c and d calculations are longer because of the "trick" multiplications.
            Vector128<short> dc = t0.AsInt16() + Vector128.Create((short)4);
            a = dc + t2.AsInt16();
            b = dc - t2.AsInt16();

            // c = MUL(T1, K2) - MUL(T3, K1) = MUL(T1, k2) - MUL(T3, k1) + T1 - T3
            c1 = Vector128_.MultiplyHigh(t1.AsInt16(), k2);
            c2 = Vector128_.MultiplyHigh(t3.AsInt16(), k1);
            c3 = t1.AsInt16() - t3.AsInt16();
            c4 = c1 - c2;
            c = c3 + c4;

            // d = MUL(T1, K1) + MUL(T3, K2) = MUL(T1, k1) + MUL(T3, k2) + T1 + T3
            d1 = Vector128_.MultiplyHigh(t1.AsInt16(), k1);
            d2 = Vector128_.MultiplyHigh(t3.AsInt16(), k2);
            d3 = t1.AsInt16() + t3.AsInt16();
            d4 = d1 + d2;
            d = d3 + d4;

            // Second pass.
            tmp0 = a + d;
            tmp1 = b + c;
            tmp2 = b - c;
            tmp3 = a - d;
            Vector128<short> shifted0 = Vector128.ShiftRightArithmetic(tmp0, 3);
            Vector128<short> shifted1 = Vector128.ShiftRightArithmetic(tmp1, 3);
            Vector128<short> shifted2 = Vector128.ShiftRightArithmetic(tmp2, 3);
            Vector128<short> shifted3 = Vector128.ShiftRightArithmetic(tmp3, 3);

            // Transpose the two 4x4.
            Vp8Transpose_2_4x4_16bVector128(shifted0, shifted1, shifted2, shifted3, out t0, out t1, out t2, out t3);

            // Add inverse transform to 'dst' and store.
            // Load the reference(s).
            // Load four bytes/pixels per line.
            ref byte dstRef = ref MemoryMarshal.GetReference(dst);
            Vector128<byte> dst0 = Vector128.CreateScalar(Unsafe.As<byte, int>(ref dstRef)).AsByte();
            Vector128<byte> dst1 = Vector128.CreateScalar(Unsafe.As<byte, int>(ref Unsafe.Add(ref dstRef, WebpConstants.Bps))).AsByte();
            Vector128<byte> dst2 = Vector128.CreateScalar(Unsafe.As<byte, int>(ref Unsafe.Add(ref dstRef, WebpConstants.Bps * 2))).AsByte();
            Vector128<byte> dst3 = Vector128.CreateScalar(Unsafe.As<byte, int>(ref Unsafe.Add(ref dstRef, WebpConstants.Bps * 3))).AsByte();

            // Convert to 16b.
            dst0 = Vector128_.UnpackLow(dst0, Vector128<byte>.Zero);
            dst1 = Vector128_.UnpackLow(dst1, Vector128<byte>.Zero);
            dst2 = Vector128_.UnpackLow(dst2, Vector128<byte>.Zero);
            dst3 = Vector128_.UnpackLow(dst3, Vector128<byte>.Zero);

            // Add the inverse transform(s).
            dst0 = (dst0.AsInt16() + t0.AsInt16()).AsByte();
            dst1 = (dst1.AsInt16() + t1.AsInt16()).AsByte();
            dst2 = (dst2.AsInt16() + t2.AsInt16()).AsByte();
            dst3 = (dst3.AsInt16() + t3.AsInt16()).AsByte();

            // Unsigned saturate to 8b.
            dst0 = Vector128_.PackUnsignedSaturate(dst0.AsInt16(), dst0.AsInt16());
            dst1 = Vector128_.PackUnsignedSaturate(dst1.AsInt16(), dst1.AsInt16());
            dst2 = Vector128_.PackUnsignedSaturate(dst2.AsInt16(), dst2.AsInt16());
            dst3 = Vector128_.PackUnsignedSaturate(dst3.AsInt16(), dst3.AsInt16());

            // Store the results.
            // Store four bytes/pixels per line.
            ref byte outputRef = ref MemoryMarshal.GetReference(dst);
            int output0 = dst0.AsInt32().ToScalar();
            int output1 = dst1.AsInt32().ToScalar();
            int output2 = dst2.AsInt32().ToScalar();
            int output3 = dst3.AsInt32().ToScalar();
            Unsafe.As<byte, int>(ref outputRef) = output0;
            Unsafe.As<byte, int>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps)) = output1;
            Unsafe.As<byte, int>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps * 2)) = output2;
            Unsafe.As<byte, int>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps * 3)) = output3;
        }
        else
        {
            Span<int> tmp = scratch[..16];
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
                Store(dst[dstOffset..], 0, 0, a + d);
                Store(dst[dstOffset..], 1, 0, b + c);
                Store(dst[dstOffset..], 2, 0, b - c);
                Store(dst[dstOffset..], 3, 0, a - d);
                tmpOffset++;

                dstOffset += WebpConstants.Bps;
            }
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
        TransformTwo(src[..], dst, scratch);
        TransformTwo(src[(2 * 16)..], dst[(4 * WebpConstants.Bps)..], scratch);
    }

    public static void TransformDcuv(Span<short> src, Span<byte> dst)
    {
        if (src[0 * 16] != 0)
        {
            TransformDc(src[..], dst);
        }

        if (src[1 * 16] != 0)
        {
            TransformDc(src[(1 * 16)..], dst[4..]);
        }

        if (src[2 * 16] != 0)
        {
            TransformDc(src[(2 * 16)..], dst[(4 * WebpConstants.Bps)..]);
        }

        if (src[3 * 16] != 0)
        {
            TransformDc(src[(3 * 16)..], dst[((4 * WebpConstants.Bps) + 4)..]);
        }
    }

    // Simple In-loop filtering (Paragraph 15.2)
    public static void SimpleVFilter16(Span<byte> p, int offset, int stride, int thresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            // Load.
            ref byte pRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(p), (uint)offset);

            Vector128<byte> p1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Subtract(ref pRef, 2 * stride));
            Vector128<byte> p0 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Subtract(ref pRef, stride));
            Vector128<byte> q0 = Unsafe.As<byte, Vector128<byte>>(ref pRef);
            Vector128<byte> q1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)stride));

            DoFilter2Vector128(ref p1, ref p0, ref q0, ref q1, thresh);

            // Store.
            ref byte outputRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(p), (uint)offset);
            Unsafe.As<byte, Vector128<sbyte>>(ref Unsafe.Subtract(ref outputRef, stride)) = p0.AsSByte();
            Unsafe.As<byte, Vector128<sbyte>>(ref outputRef) = q0.AsSByte();
        }
        else
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
    }

    public static void SimpleHFilter16(Span<byte> p, int offset, int stride, int thresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            // Beginning of p1
            ref byte pRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(p), (uint)(offset - 2));

            Load16x4Vector128(ref pRef, ref Unsafe.Add(ref pRef, 8 * (uint)stride), stride, out Vector128<byte> p1, out Vector128<byte> p0, out Vector128<byte> q0, out Vector128<byte> q1);
            DoFilter2Vector128(ref p1, ref p0, ref q0, ref q1, thresh);
            Store16x4Vector128(p1, p0, q0, q1, ref pRef, ref Unsafe.Add(ref pRef, 8 * (uint)stride), stride);
        }
        else
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
    }

    public static void SimpleVFilter16i(Span<byte> p, int offset, int stride, int thresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            for (int k = 3; k > 0; k--)
            {
                offset += 4 * stride;
                SimpleVFilter16(p, offset, stride, thresh);
            }
        }
        else
        {
            for (int k = 3; k > 0; k--)
            {
                offset += 4 * stride;
                SimpleVFilter16(p, offset, stride, thresh);
            }
        }
    }

    public static void SimpleHFilter16i(Span<byte> p, int offset, int stride, int thresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            for (int k = 3; k > 0; k--)
            {
                offset += 4;
                SimpleHFilter16(p, offset, stride, thresh);
            }
        }
        else
        {
            for (int k = 3; k > 0; k--)
            {
                offset += 4;
                SimpleHFilter16(p, offset, stride, thresh);
            }
        }
    }

    // On macroblock edges.
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void VFilter16(Span<byte> p, int offset, int stride, int thresh, int ithresh, int hevThresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref byte pRef = ref MemoryMarshal.GetReference(p);
            Vector128<byte> t1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset - (4 * stride))));
            Vector128<byte> p2 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset - (3 * stride))));
            Vector128<byte> p1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset - (2 * stride))));
            Vector128<byte> p0 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset - stride)));

            Vector128<byte> mask = AbsVector128(p1, p0);
            mask = Vector128.Max(mask, AbsVector128(t1, p2));
            mask = Vector128.Max(mask, AbsVector128(p2, p1));

            Vector128<byte> q0 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)offset));
            Vector128<byte> q1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset + stride)));
            Vector128<byte> q2 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset + (2 * stride))));
            t1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset + (3 * stride))));

            mask = Vector128.Max(mask, AbsVector128(q1, q0));
            mask = Vector128.Max(mask, AbsVector128(t1, q2));
            mask = Vector128.Max(mask, AbsVector128(q2, q1));

            ComplexMaskVector128(p1, p0, q0, q1, thresh, ithresh, ref mask);
            DoFilter6Vector128(ref p2, ref p1, ref p0, ref q0, ref q1, ref q2, mask, hevThresh);

            // Store.
            ref byte outputRef = ref MemoryMarshal.GetReference(p);
            Unsafe.As<byte, Vector128<int>>(ref Unsafe.Add(ref outputRef, (uint)(offset - (3 * stride)))) = p2.AsInt32();
            Unsafe.As<byte, Vector128<int>>(ref Unsafe.Add(ref outputRef, (uint)(offset - (2 * stride)))) = p1.AsInt32();
            Unsafe.As<byte, Vector128<int>>(ref Unsafe.Add(ref outputRef, (uint)(offset - stride))) = p0.AsInt32();
            Unsafe.As<byte, Vector128<int>>(ref Unsafe.Add(ref outputRef, (uint)offset)) = q0.AsInt32();
            Unsafe.As<byte, Vector128<int>>(ref Unsafe.Add(ref outputRef, (uint)(offset + stride))) = q1.AsInt32();
            Unsafe.As<byte, Vector128<int>>(ref Unsafe.Add(ref outputRef, (uint)(offset + (2 * stride)))) = q2.AsInt32();
        }
        else
        {
            FilterLoop26(p, offset, stride, 1, 16, thresh, ithresh, hevThresh);
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public static void HFilter16(Span<byte> p, int offset, int stride, int thresh, int ithresh, int hevThresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref byte pRef = ref MemoryMarshal.GetReference(p);
            ref byte bRef = ref Unsafe.Add(ref pRef, (uint)offset - 4);
            Load16x4Vector128(ref bRef, ref Unsafe.Add(ref bRef, 8 * (uint)stride), stride, out Vector128<byte> p3, out Vector128<byte> p2, out Vector128<byte> p1, out Vector128<byte> p0);

            Vector128<byte> mask = AbsVector128(p1, p0);
            mask = Vector128.Max(mask, AbsVector128(p3, p2));
            mask = Vector128.Max(mask, AbsVector128(p2, p1));

            Load16x4Vector128(ref Unsafe.Add(ref pRef, (uint)offset), ref Unsafe.Add(ref pRef, (uint)(offset + (8 * stride))), stride, out Vector128<byte> q0, out Vector128<byte> q1, out Vector128<byte> q2, out Vector128<byte> q3);

            mask = Vector128.Max(mask, AbsVector128(q1, q0));
            mask = Vector128.Max(mask, AbsVector128(q3, q2));
            mask = Vector128.Max(mask, AbsVector128(q2, q1));

            ComplexMaskVector128(p1, p0, q0, q1, thresh, ithresh, ref mask);
            DoFilter6Vector128(ref p2, ref p1, ref p0, ref q0, ref q1, ref q2, mask, hevThresh);

            Store16x4Vector128(p3, p2, p1, p0, ref bRef, ref Unsafe.Add(ref bRef, 8 * (uint)stride), stride);
            Store16x4Vector128(q0, q1, q2, q3, ref Unsafe.Add(ref pRef, (uint)offset), ref Unsafe.Add(ref pRef, (uint)(offset + (8 * stride))), stride);
        }
        else
        {
            FilterLoop26(p, offset, 1, stride, 16, thresh, ithresh, hevThresh);
        }
    }

    public static void VFilter16i(Span<byte> p, int offset, int stride, int thresh, int ithresh, int hevThresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref byte pRef = ref MemoryMarshal.GetReference(p);
            Vector128<byte> p3 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)offset));
            Vector128<byte> p2 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset + stride)));
            Vector128<byte> p1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset + (2 * stride))));
            Vector128<byte> p0 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset + (3 * stride))));

            for (int k = 3; k > 0; k--)
            {
                // Beginning of p1.
                Span<byte> b = p[(offset + (2 * stride))..];
                offset += 4 * stride;

                Vector128<byte> mask = AbsVector128(p0, p1);
                mask = Vector128.Max(mask, AbsVector128(p3, p2));
                mask = Vector128.Max(mask, AbsVector128(p2, p1));

                p3 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)offset));
                p2 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset + stride)));
                Vector128<byte> tmp1 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset + (2 * stride))));
                Vector128<byte> tmp2 = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pRef, (uint)(offset + (3 * stride))));

                mask = Vector128.Max(mask, AbsVector128(tmp1, tmp2));
                mask = Vector128.Max(mask, AbsVector128(p3, p2));
                mask = Vector128.Max(mask, AbsVector128(p2, tmp1));

                // p3 and p2 are not just temporary variables here: they will be
                // re-used for next span. And q2/q3 will become p1/p0 accordingly.
                ComplexMaskVector128(p1, p0, p3, p2, thresh, ithresh, ref mask);
                DoFilter4Vector128(ref p1, ref p0, ref p3, ref p2, mask, hevThresh);

                // Store.
                ref byte outputRef = ref MemoryMarshal.GetReference(b);
                Unsafe.As<byte, Vector128<int>>(ref outputRef) = p1.AsInt32();
                Unsafe.As<byte, Vector128<int>>(ref Unsafe.Add(ref outputRef, (uint)stride)) = p0.AsInt32();
                Unsafe.As<byte, Vector128<int>>(ref Unsafe.Add(ref outputRef, (uint)(stride * 2))) = p3.AsInt32();
                Unsafe.As<byte, Vector128<int>>(ref Unsafe.Add(ref outputRef, (uint)(stride * 3))) = p2.AsInt32();

                // Rotate samples.
                p1 = tmp1;
                p0 = tmp2;
            }
        }
        else
        {
            for (int k = 3; k > 0; k--)
            {
                offset += 4 * stride;
                FilterLoop24(p, offset, stride, 1, 16, thresh, ithresh, hevThresh);
            }
        }
    }

    public static void HFilter16i(Span<byte> p, int offset, int stride, int thresh, int ithresh, int hevThresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref byte pRef = ref MemoryMarshal.GetReference(p);
            Load16x4Vector128(ref Unsafe.Add(ref pRef, (uint)offset), ref Unsafe.Add(ref pRef, (uint)(offset + (8 * stride))), stride, out Vector128<byte> p3, out Vector128<byte> p2, out Vector128<byte> p1, out Vector128<byte> p0);

            Vector128<byte> mask;
            for (int k = 3; k > 0; k--)
            {
                // Beginning of p1.
                ref byte bRef = ref Unsafe.Add(ref pRef, (uint)offset + 2);

                // Beginning of q0 (and next span).
                offset += 4;

                // Compute partial mask.
                mask = AbsVector128(p1, p0);
                mask = Vector128.Max(mask, AbsVector128(p3, p2));
                mask = Vector128.Max(mask, AbsVector128(p2, p1));

                Load16x4Vector128(ref Unsafe.Add(ref pRef, (uint)offset), ref Unsafe.Add(ref pRef, (uint)(offset + (8 * stride))), stride, out p3, out p2, out Vector128<byte> tmp1, out Vector128<byte> tmp2);

                mask = Vector128.Max(mask, AbsVector128(tmp1, tmp2));
                mask = Vector128.Max(mask, AbsVector128(p3, p2));
                mask = Vector128.Max(mask, AbsVector128(p2, tmp1));

                ComplexMaskVector128(p1, p0, p3, p2, thresh, ithresh, ref mask);
                DoFilter4Vector128(ref p1, ref p0, ref p3, ref p2, mask, hevThresh);

                Store16x4Vector128(p1, p0, p3, p2, ref bRef, ref Unsafe.Add(ref bRef, 8 * (uint)stride), stride);

                // Rotate samples.
                p1 = tmp1;
                p0 = tmp2;
            }
        }
        else
        {
            for (int k = 3; k > 0; k--)
            {
                offset += 4;
                FilterLoop24(p, offset, 1, stride, 16, thresh, ithresh, hevThresh);
            }
        }
    }

    // 8-pixels wide variant, for chroma filtering.
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void VFilter8(Span<byte> u, Span<byte> v, int offset, int stride, int thresh, int ithresh, int hevThresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            // Load uv h-edges.
            ref byte uRef = ref MemoryMarshal.GetReference(u);
            ref byte vRef = ref MemoryMarshal.GetReference(v);
            Vector128<byte> t1 = LoadUvEdgeVector128(ref uRef, ref vRef, offset - (4 * stride));
            Vector128<byte> p2 = LoadUvEdgeVector128(ref uRef, ref vRef, offset - (3 * stride));
            Vector128<byte> p1 = LoadUvEdgeVector128(ref uRef, ref vRef, offset - (2 * stride));
            Vector128<byte> p0 = LoadUvEdgeVector128(ref uRef, ref vRef, offset - stride);

            Vector128<byte> mask = AbsVector128(p1, p0);
            mask = Vector128.Max(mask, AbsVector128(t1, p2));
            mask = Vector128.Max(mask, AbsVector128(p2, p1));

            Vector128<byte> q0 = LoadUvEdgeVector128(ref uRef, ref vRef, offset);
            Vector128<byte> q1 = LoadUvEdgeVector128(ref uRef, ref vRef, offset + stride);
            Vector128<byte> q2 = LoadUvEdgeVector128(ref uRef, ref vRef, offset + (2 * stride));
            t1 = LoadUvEdgeVector128(ref uRef, ref vRef, offset + (3 * stride));

            mask = Vector128.Max(mask, AbsVector128(q1, q0));
            mask = Vector128.Max(mask, AbsVector128(t1, q2));
            mask = Vector128.Max(mask, AbsVector128(q2, q1));

            ComplexMaskVector128(p1, p0, q0, q1, thresh, ithresh, ref mask);
            DoFilter6Vector128(ref p2, ref p1, ref p0, ref q0, ref q1, ref q2, mask, hevThresh);

            // Store.
            StoreUvVector128(p2, ref uRef, ref vRef, offset - (3 * stride));
            StoreUvVector128(p1, ref uRef, ref vRef, offset - (2 * stride));
            StoreUvVector128(p0, ref uRef, ref vRef, offset - stride);
            StoreUvVector128(q0, ref uRef, ref vRef, offset);
            StoreUvVector128(q1, ref uRef, ref vRef, offset + (1 * stride));
            StoreUvVector128(q2, ref uRef, ref vRef, offset + (2 * stride));
        }
        else
        {
            FilterLoop26(u, offset, stride, 1, 8, thresh, ithresh, hevThresh);
            FilterLoop26(v, offset, stride, 1, 8, thresh, ithresh, hevThresh);
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public static void HFilter8(Span<byte> u, Span<byte> v, int offset, int stride, int thresh, int ithresh, int hevThresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref byte uRef = ref MemoryMarshal.GetReference(u);
            ref byte vRef = ref MemoryMarshal.GetReference(v);
            Load16x4Vector128(ref Unsafe.Add(ref uRef, (uint)offset - 4), ref Unsafe.Add(ref vRef, (uint)offset - 4), stride, out Vector128<byte> p3, out Vector128<byte> p2, out Vector128<byte> p1, out Vector128<byte> p0);

            Vector128<byte> mask = AbsVector128(p1, p0);
            mask = Vector128.Max(mask, AbsVector128(p3, p2));
            mask = Vector128.Max(mask, AbsVector128(p2, p1));

            Load16x4Vector128(ref Unsafe.Add(ref uRef, (uint)offset), ref Unsafe.Add(ref vRef, (uint)offset), stride, out Vector128<byte> q0, out Vector128<byte> q1, out Vector128<byte> q2, out Vector128<byte> q3);

            mask = Vector128.Max(mask, AbsVector128(q1, q0));
            mask = Vector128.Max(mask, AbsVector128(q3, q2));
            mask = Vector128.Max(mask, AbsVector128(q2, q1));

            ComplexMaskVector128(p1, p0, q0, q1, thresh, ithresh, ref mask);
            DoFilter6Vector128(ref p2, ref p1, ref p0, ref q0, ref q1, ref q2, mask, hevThresh);

            Store16x4Vector128(p3, p2, p1, p0, ref Unsafe.Add(ref uRef, (uint)offset - 4), ref Unsafe.Add(ref vRef, (uint)offset - 4), stride);
            Store16x4Vector128(q0, q1, q2, q3, ref Unsafe.Add(ref uRef, (uint)offset), ref Unsafe.Add(ref vRef, (uint)offset), stride);
        }
        else
        {
            FilterLoop26(u, offset, 1, stride, 8, thresh, ithresh, hevThresh);
            FilterLoop26(v, offset, 1, stride, 8, thresh, ithresh, hevThresh);
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public static void VFilter8i(Span<byte> u, Span<byte> v, int offset, int stride, int thresh, int ithresh, int hevThresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            // Load uv h-edges.
            ref byte uRef = ref MemoryMarshal.GetReference(u);
            ref byte vRef = ref MemoryMarshal.GetReference(v);
            Vector128<byte> t2 = LoadUvEdgeVector128(ref uRef, ref vRef, offset);
            Vector128<byte> t1 = LoadUvEdgeVector128(ref uRef, ref vRef, offset + stride);
            Vector128<byte> p1 = LoadUvEdgeVector128(ref uRef, ref vRef, offset + (stride * 2));
            Vector128<byte> p0 = LoadUvEdgeVector128(ref uRef, ref vRef, offset + (stride * 3));

            Vector128<byte> mask = AbsVector128(p1, p0);
            mask = Vector128.Max(mask, AbsVector128(t2, t1));
            mask = Vector128.Max(mask, AbsVector128(t1, p1));

            offset += 4 * stride;

            Vector128<byte> q0 = LoadUvEdgeVector128(ref uRef, ref vRef, offset);
            Vector128<byte> q1 = LoadUvEdgeVector128(ref uRef, ref vRef, offset + stride);
            t1 = LoadUvEdgeVector128(ref uRef, ref vRef, offset + (stride * 2));
            t2 = LoadUvEdgeVector128(ref uRef, ref vRef, offset + (stride * 3));

            mask = Vector128.Max(mask, AbsVector128(q1, q0));
            mask = Vector128.Max(mask, AbsVector128(t2, t1));
            mask = Vector128.Max(mask, AbsVector128(t1, q1));

            ComplexMaskVector128(p1, p0, q0, q1, thresh, ithresh, ref mask);
            DoFilter4Vector128(ref p1, ref p0, ref q0, ref q1, mask, hevThresh);

            // Store.
            StoreUvVector128(p1, ref uRef, ref vRef, offset + (-2 * stride));
            StoreUvVector128(p0, ref uRef, ref vRef, offset + (-1 * stride));
            StoreUvVector128(q0, ref uRef, ref vRef, offset);
            StoreUvVector128(q1, ref uRef, ref vRef, offset + stride);
        }
        else
        {
            int offset4mulstride = offset + (4 * stride);
            FilterLoop24(u, offset4mulstride, stride, 1, 8, thresh, ithresh, hevThresh);
            FilterLoop24(v, offset4mulstride, stride, 1, 8, thresh, ithresh, hevThresh);
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public static void HFilter8i(Span<byte> u, Span<byte> v, int offset, int stride, int thresh, int ithresh, int hevThresh)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref byte uRef = ref MemoryMarshal.GetReference(u);
            ref byte vRef = ref MemoryMarshal.GetReference(v);
            Load16x4Vector128(ref Unsafe.Add(ref uRef, (uint)offset), ref Unsafe.Add(ref vRef, (uint)offset), stride, out Vector128<byte> t2, out Vector128<byte> t1, out Vector128<byte> p1, out Vector128<byte> p0);

            Vector128<byte> mask = AbsVector128(p1, p0);
            mask = Vector128.Max(mask, AbsVector128(t2, t1));
            mask = Vector128.Max(mask, AbsVector128(t1, p1));

            // Beginning of q0.
            offset += 4;

            Load16x4Vector128(ref Unsafe.Add(ref uRef, (uint)offset), ref Unsafe.Add(ref vRef, (uint)offset), stride, out Vector128<byte> q0, out Vector128<byte> q1, out t1, out t2);

            mask = Vector128.Max(mask, AbsVector128(q1, q0));
            mask = Vector128.Max(mask, AbsVector128(t2, t1));
            mask = Vector128.Max(mask, AbsVector128(t1, q1));

            ComplexMaskVector128(p1, p0, q0, q1, thresh, ithresh, ref mask);
            DoFilter4Vector128(ref p1, ref p0, ref q0, ref q1, mask, hevThresh);

            // Beginning of p1.
            offset -= 2;
            Store16x4Vector128(p1, p0, q0, q1, ref Unsafe.Add(ref uRef, (uint)offset), ref Unsafe.Add(ref vRef, (uint)offset), stride);
        }
        else
        {
            int offsetPlus4 = offset + 4;
            FilterLoop24(u, offsetPlus4, 1, stride, 8, thresh, ithresh, hevThresh);
            FilterLoop24(v, offsetPlus4, 1, stride, 8, thresh, ithresh, hevThresh);
        }
    }

    public static void Mean16x4(Span<byte> input, Span<uint> dc)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<byte> mean16x4Mask = Vector128.Create((short)0x00ff).AsByte();

            Vector128<byte> a0 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            Vector128<byte> a1 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input.Slice(WebpConstants.Bps, 16)));
            Vector128<byte> a2 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input.Slice(WebpConstants.Bps * 2, 16)));
            Vector128<byte> a3 = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input.Slice(WebpConstants.Bps * 3, 16)));
            Vector128<short> b0 = Vector128.ShiftRightLogical(a0.AsInt16(), 8); // hi byte
            Vector128<short> b1 = Vector128.ShiftRightLogical(a1.AsInt16(), 8);
            Vector128<short> b2 = Vector128.ShiftRightLogical(a2.AsInt16(), 8);
            Vector128<short> b3 = Vector128.ShiftRightLogical(a3.AsInt16(), 8);
            Vector128<byte> c0 = a0 & mean16x4Mask; // lo byte
            Vector128<byte> c1 = a1 & mean16x4Mask;
            Vector128<byte> c2 = a2 & mean16x4Mask;
            Vector128<byte> c3 = a3 & mean16x4Mask;
            Vector128<int> d0 = b0.AsInt32() + c0.AsInt32();
            Vector128<int> d1 = b1.AsInt32() + c1.AsInt32();
            Vector128<int> d2 = b2.AsInt32() + c2.AsInt32();
            Vector128<int> d3 = b3.AsInt32() + c3.AsInt32();
            Vector128<int> e0 = d0 + d1;
            Vector128<int> e1 = d2 + d3;
            Vector128<int> f0 = e0 + e1;
            Vector128<short> hadd = Vector128_.HorizontalAdd(f0.AsInt16(), f0.AsInt16());
            Vector128<uint> wide = Vector128_.UnpackLow(hadd, Vector128<short>.Zero).AsUInt32();

            ref uint outputRef = ref MemoryMarshal.GetReference(dc);
            Unsafe.As<uint, Vector128<uint>>(ref outputRef) = wide;
        }
        else
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
                input = input[4..]; // go to next 4x4 block.
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

    /// <summary>
    /// Reduces elements of the vector into one sum.
    /// </summary>
    /// <param name="accumulator">The accumulator to reduce.</param>
    /// <returns>The sum of all elements.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static int ReduceSumVector256(Vector256<int> accumulator)
    {
        // Add upper lane to lower lane.
        Vector128<int> vsum = accumulator.GetLower() + accumulator.GetUpper();

        // Add odd to even.
        vsum += Vector128_.ShuffleNative(vsum, 0b_11_11_01_01);

        // Add high to low.
        vsum += Vector128_.ShuffleNative(vsum, 0b_11_10_11_10);

        return vsum.ToScalar();
    }

    /// <summary>
    /// Reduces elements of the vector into one sum.
    /// </summary>
    /// <param name="accumulator">The accumulator to reduce.</param>
    /// <returns>The sum of all elements.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static int ReduceSumVector128(Vector128<int> accumulator)
    {
        // Add odd to even.
        Vector128<int> vsum = accumulator + Vector128_.ShuffleNative(accumulator, 0b_11_11_01_01);

        // Add high to low.
        vsum += Vector128_.ShuffleNative(vsum, 0b_11_10_11_10);

        return vsum.ToScalar();
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void Put16(int v, Span<byte> dst)
    {
        for (int j = 0; j < 16; j++)
        {
            Memset(dst[(j * WebpConstants.Bps)..], (byte)v, 0, 16);
        }
    }

    private static void TrueMotion(Span<byte> dst, Span<byte> yuv, int offset, int size)
    {
        // For information about how true motion works, see rfc6386, page 52. ff and section 20.14.
        int topOffset = offset - WebpConstants.Bps;
        Span<byte> top = yuv[topOffset..];
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
            dst = dst[WebpConstants.Bps..];
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

    // Applies filter on 2 pixels (p0 and q0)
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

    // Applies filter on 2 pixels (p0 and q0)
    private static void DoFilter2Vector128(ref Vector128<byte> p1, ref Vector128<byte> p0, ref Vector128<byte> q0, ref Vector128<byte> q1, int thresh)
    {
        Vector128<byte> signBit = Vector128.Create((byte)0x80);

        // Convert p1/q1 to byte (for GetBaseDelta).
        Vector128<byte> p1s = p1 ^ signBit;
        Vector128<byte> q1s = q1 ^ signBit;
        Vector128<byte> mask = NeedsFilterVector128(p1, p0, q0, q1, thresh);

        // Flip sign.
        p0 ^= signBit;
        q0 ^= signBit;

        Vector128<byte> a = GetBaseDeltaVector128(p1s.AsSByte(), p0.AsSByte(), q0.AsSByte(), q1s.AsSByte()).AsByte();

        // Mask filter values we don't care about.
        a &= mask;

        DoSimpleFilterVector128(ref p0, ref q0, a);

        // Flip sign.
        p0 ^= signBit;
        q0 ^= signBit;
    }

    // Applies filter on 4 pixels (p1, p0, q0 and q1)
    private static void DoFilter4Vector128(ref Vector128<byte> p1, ref Vector128<byte> p0, ref Vector128<byte> q0, ref Vector128<byte> q1, Vector128<byte> mask, int tresh)
    {
        // Compute hev mask.
        Vector128<byte> notHev = GetNotHevVector128(ref p1, ref p0, ref q0, ref q1, tresh);

        Vector128<byte> signBit = Vector128.Create((byte)0x80);

        // Convert to signed values.
        p1 ^= signBit;
        p0 ^= signBit;
        q0 ^= signBit;
        q1 ^= signBit;

        Vector128<sbyte> t1 = Vector128_.SubtractSaturate(p1.AsSByte(), q1.AsSByte()); // p1 - q1
        t1 = (~notHev & t1.AsByte()).AsSByte(); // hev(p1 - q1)
        Vector128<sbyte> t2 = Vector128_.SubtractSaturate(q0.AsSByte(), p0.AsSByte()); // q0 - p0
        t1 = Vector128_.AddSaturate(t1, t2); // hev(p1 - q1) + 1 * (q0 - p0)
        t1 = Vector128_.AddSaturate(t1, t2); // hev(p1 - q1) + 2 * (q0 - p0)
        t1 = Vector128_.AddSaturate(t1, t2); // hev(p1 - q1) + 3 * (q0 - p0)
        t1 = (t1.AsByte() & mask).AsSByte(); // mask filter values we don't care about.

        t2 = Vector128_.AddSaturate(t1, Vector128.Create((byte)3).AsSByte());                  // 3 * (q0 - p0) + hev(p1 - q1) + 3
        Vector128<sbyte> t3 = Vector128_.AddSaturate(t1, Vector128.Create((byte)4).AsSByte()); // 3 * (q0 - p0) + hev(p1 - q1) + 4
        t2 = SignedShift8bVector128(t2.AsByte()); // (3 * (q0 - p0) + hev(p1 - q1) + 3) >> 3
        t3 = SignedShift8bVector128(t3.AsByte()); // (3 * (q0 - p0) + hev(p1 - q1) + 4) >> 3
        p0 = Vector128_.AddSaturate(p0.AsSByte(), t2).AsByte(); // p0 += t2
        q0 = Vector128_.SubtractSaturate(q0.AsSByte(), t3).AsByte(); // q0 -= t3
        p0 ^= signBit;
        q0 ^= signBit;

        // This is equivalent to signed (a + 1) >> 1 calculation.
        t2 = t3 + signBit.AsSByte();
        t3 = Vector128_.Average(t2.AsByte(), Vector128<byte>.Zero).AsSByte();
        t3 -= Vector128.Create((sbyte)64);

        t3 = (notHev & t3.AsByte()).AsSByte(); // if !hev
        q1 = Vector128_.SubtractSaturate(q1.AsSByte(), t3).AsByte(); // q1 -= t3
        p1 = Vector128_.AddSaturate(p1.AsSByte(), t3).AsByte(); // p1 += t3
        p1 = p1.AsByte() ^ signBit;
        q1 = q1.AsByte() ^ signBit;
    }

    // Applies filter on 6 pixels (p2, p1, p0, q0, q1 and q2)
    private static void DoFilter6Vector128(ref Vector128<byte> p2, ref Vector128<byte> p1, ref Vector128<byte> p0, ref Vector128<byte> q0, ref Vector128<byte> q1, ref Vector128<byte> q2, Vector128<byte> mask, int tresh)
    {
        // Compute hev mask.
        Vector128<byte> notHev = GetNotHevVector128(ref p1, ref p0, ref q0, ref q1, tresh);

        // Convert to signed values.
        Vector128<byte> signBit = Vector128.Create((byte)0x80);
        p1 ^= signBit;
        p0 ^= signBit;
        q0 ^= signBit;
        q1 ^= signBit;
        p2 ^= signBit;
        q2 ^= signBit;

        Vector128<sbyte> a = GetBaseDeltaVector128(p1.AsSByte(), p0.AsSByte(), q0.AsSByte(), q1.AsSByte());

        // Do simple filter on pixels with hev.
        Vector128<byte> m = ~notHev & mask;
        Vector128<byte> f = a.AsByte() & m;
        DoSimpleFilterVector128(ref p0, ref q0, f);

        // Do strong filter on pixels with not hev.
        m = notHev & mask;
        f = a.AsByte() & m;
        Vector128<byte> flow = Vector128_.UnpackLow(Vector128<byte>.Zero, f);
        Vector128<byte> fhigh = Vector128_.UnpackHigh(Vector128<byte>.Zero, f);

        Vector128<short> nine = Vector128.Create((short)0x0900);
        Vector128<short> f9Low = Vector128_.MultiplyHigh(flow.AsInt16(), nine); // Filter (lo) * 9
        Vector128<short> f9High = Vector128_.MultiplyHigh(fhigh.AsInt16(), nine); // Filter (hi) * 9

        Vector128<short> sixtyThree = Vector128.Create((short)63);
        Vector128<short> a2Low = f9Low + sixtyThree; // Filter * 9 + 63
        Vector128<short> a2High = f9High + sixtyThree; // Filter * 9 + 63

        Vector128<short> a1Low = a2Low + f9Low; // Filter * 18 + 63
        Vector128<short> a1High = a2High + f9High; // // Filter * 18 + 63

        Vector128<short> a0Low = a1Low + f9Low; // Filter * 27 + 63
        Vector128<short> a0High = a1High + f9High; // Filter * 27 + 63

        Update2PixelsVector128(ref p2, ref q2, a2Low, a2High);
        Update2PixelsVector128(ref p1, ref q1, a1Low, a1High);
        Update2PixelsVector128(ref p0, ref q0, a0Low, a0High);
    }

    private static void DoSimpleFilterVector128(ref Vector128<byte> p0, ref Vector128<byte> q0, Vector128<byte> fl)
    {
        Vector128<sbyte> v3 = Vector128_.AddSaturate(fl.AsSByte(), Vector128.Create((byte)3).AsSByte());
        Vector128<sbyte> v4 = Vector128_.AddSaturate(fl.AsSByte(), Vector128.Create((byte)4).AsSByte());

        v4 = SignedShift8bVector128(v4.AsByte()).AsSByte(); // v4 >> 3
        v3 = SignedShift8bVector128(v3.AsByte()).AsSByte(); // v3 >> 3
        q0 = Vector128_.SubtractSaturate(q0.AsSByte(), v4).AsByte(); // q0 -= v4
        p0 = Vector128_.AddSaturate(p0.AsSByte(), v3).AsByte(); // p0 += v3
    }

    private static Vector128<byte> GetNotHevVector128(ref Vector128<byte> p1, ref Vector128<byte> p0, ref Vector128<byte> q0, ref Vector128<byte> q1, int hevThresh)
    {
        Vector128<byte> t1 = AbsVector128(p1, p0);
        Vector128<byte> t2 = AbsVector128(q1, q0);

        Vector128<byte> h = Vector128.Create((byte)hevThresh);
        Vector128<byte> tMax = Vector128.Max(t1, t2);

        Vector128<byte> tMaxH = Vector128_.SubtractSaturate(tMax, h);

        // not_hev <= t1 && not_hev <= t2
        return Vector128.Equals(tMaxH, Vector128<byte>.Zero);
    }

    // Applies filter on 4 pixels (p1, p0, q0 and q1)
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

    // Applies filter on 6 pixels (p2, p1, p0, q0, q1 and q2)
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

    private static Vector128<byte> NeedsFilterVector128(Vector128<byte> p1, Vector128<byte> p0, Vector128<byte> q0, Vector128<byte> q1, int thresh)
    {
        Vector128<byte> mthresh = Vector128.Create((byte)thresh);
        Vector128<byte> t1 = AbsVector128(p1, q1); // abs(p1 - q1)
        Vector128<byte> fe = Vector128.Create((byte)0xFE);
        Vector128<byte> t2 = t1 & fe; // set lsb of each byte to zero.
        Vector128<short> t3 = Vector128.ShiftRightLogical(t2.AsInt16(), 1); // abs(p1 - q1) / 2

        Vector128<byte> t4 = AbsVector128(p0, q0); // abs(p0 - q0)
        Vector128<byte> t5 = Vector128_.AddSaturate(t4, t4); // abs(p0 - q0) * 2
        Vector128<byte> t6 = Vector128_.AddSaturate(t5.AsByte(), t3.AsByte()); // abs(p0-q0)*2 + abs(p1-q1)/2

        Vector128<byte> t7 = Vector128_.SubtractSaturate(t6, mthresh.AsByte()); // mask <= m_thresh

        return Vector128.Equals(t7, Vector128<byte>.Zero);
    }

    private static void Load16x4Vector128(ref byte r0, ref byte r8, int stride, out Vector128<byte> p1, out Vector128<byte> p0, out Vector128<byte> q0, out Vector128<byte> q1)
    {
        // Assume the pixels around the edge (|) are numbered as follows
        //                00 01 | 02 03
        //                10 11 | 12 13
        //                 ...  |  ...
        //                e0 e1 | e2 e3
        //                f0 f1 | f2 f3
        //
        // r0 is pointing to the 0th row (00)
        // r8 is pointing to the 8th row (80)

        // Load
        // p1 = 71 61 51 41 31 21 11 01 70 60 50 40 30 20 10 00
        // q0 = 73 63 53 43 33 23 13 03 72 62 52 42 32 22 12 02
        // p0 = f1 e1 d1 c1 b1 a1 91 81 f0 e0 d0 c0 b0 a0 90 80
        // q1 = f3 e3 d3 c3 b3 a3 93 83 f2 e2 d2 c2 b2 a2 92 82
        Load8x4Vector128(ref r0, (uint)stride, out Vector128<byte> t1, out Vector128<byte> t2);
        Load8x4Vector128(ref r8, (uint)stride, out p0, out q1);

        // p1 = f0 e0 d0 c0 b0 a0 90 80 70 60 50 40 30 20 10 00
        // p0 = f1 e1 d1 c1 b1 a1 91 81 71 61 51 41 31 21 11 01
        // q0 = f2 e2 d2 c2 b2 a2 92 82 72 62 52 42 32 22 12 02
        // q1 = f3 e3 d3 c3 b3 a3 93 83 73 63 53 43 33 23 13 03
        p1 = Vector128_.UnpackLow(t1.AsInt64(), p0.AsInt64()).AsByte();
        p0 = Vector128_.UnpackHigh(t1.AsInt64(), p0.AsInt64()).AsByte();
        q0 = Vector128_.UnpackLow(t2.AsInt64(), q1.AsInt64()).AsByte();
        q1 = Vector128_.UnpackHigh(t2.AsInt64(), q1.AsInt64()).AsByte();
    }

    // Reads 8 rows across a vertical edge.
    private static void Load8x4Vector128(ref byte bRef, nuint stride, out Vector128<byte> p, out Vector128<byte> q)
    {
        // A0 = 63 62 61 60 23 22 21 20 43 42 41 40 03 02 01 00
        // A1 = 73 72 71 70 33 32 31 30 53 52 51 50 13 12 11 10
        uint a00 = Unsafe.As<byte, uint>(ref Unsafe.Add(ref bRef, 6 * stride));
        uint a01 = Unsafe.As<byte, uint>(ref Unsafe.Add(ref bRef, 2 * stride));
        uint a02 = Unsafe.As<byte, uint>(ref Unsafe.Add(ref bRef, 4 * stride));
        uint a03 = Unsafe.As<byte, uint>(ref Unsafe.Add(ref bRef, 0 * stride));
        Vector128<byte> a0 = Vector128.Create(a03, a02, a01, a00).AsByte();
        uint a10 = Unsafe.As<byte, uint>(ref Unsafe.Add(ref bRef, 7 * stride));
        uint a11 = Unsafe.As<byte, uint>(ref Unsafe.Add(ref bRef, 3 * stride));
        uint a12 = Unsafe.As<byte, uint>(ref Unsafe.Add(ref bRef, 5 * stride));
        uint a13 = Unsafe.As<byte, uint>(ref Unsafe.Add(ref bRef, 1 * stride));
        Vector128<byte> a1 = Vector128.Create(a13, a12, a11, a10).AsByte();

        // B0 = 53 43 52 42 51 41 50 40 13 03 12 02 11 01 10 00
        // B1 = 73 63 72 62 71 61 70 60 33 23 32 22 31 21 30 20
        Vector128<sbyte> b0 = Vector128_.UnpackLow(a0.AsSByte(), a1.AsSByte());
        Vector128<sbyte> b1 = Vector128_.UnpackHigh(a0.AsSByte(), a1.AsSByte());

        // C0 = 33 23 13 03 32 22 12 02 31 21 11 01 30 20 10 00
        // C1 = 73 63 53 43 72 62 52 42 71 61 51 41 70 60 50 40
        Vector128<short> c0 = Vector128_.UnpackLow(b0.AsInt16(), b1.AsInt16());
        Vector128<short> c1 = Vector128_.UnpackHigh(b0.AsInt16(), b1.AsInt16());

        // *p = 71 61 51 41 31 21 11 01 70 60 50 40 30 20 10 00
        // *q = 73 63 53 43 33 23 13 03 72 62 52 42 32 22 12 02
        p = Vector128_.UnpackLow(c0.AsInt32(), c1.AsInt32()).AsByte();
        q = Vector128_.UnpackHigh(c0.AsInt32(), c1.AsInt32()).AsByte();
    }

    // Transpose back and store
    private static void Store16x4Vector128(Vector128<byte> p1, Vector128<byte> p0, Vector128<byte> q0, Vector128<byte> q1, ref byte r0Ref, ref byte r8Ref, int stride)
    {
        // p0 = 71 70 61 60 51 50 41 40 31 30 21 20 11 10 01 00
        // p1 = f1 f0 e1 e0 d1 d0 c1 c0 b1 b0 a1 a0 91 90 81 80
        Vector128<byte> p0s = Vector128_.UnpackLow(p1, p0);
        Vector128<byte> p1s = Vector128_.UnpackHigh(p1, p0);

        // q0 = 73 72 63 62 53 52 43 42 33 32 23 22 13 12 03 02
        // q1 = f3 f2 e3 e2 d3 d2 c3 c2 b3 b2 a3 a2 93 92 83 82
        Vector128<byte> q0s = Vector128_.UnpackLow(q0, q1);
        Vector128<byte> q1s = Vector128_.UnpackHigh(q0, q1);

        // p0 = 33 32 31 30 23 22 21 20 13 12 11 10 03 02 01 00
        // q0 = 73 72 71 70 63 62 61 60 53 52 51 50 43 42 41 40
        Vector128<byte> t1 = p0s;
        p0s = Vector128_.UnpackLow(t1.AsInt16(), q0s.AsInt16()).AsByte();
        q0s = Vector128_.UnpackHigh(t1.AsInt16(), q0s.AsInt16()).AsByte();

        // p1 = b3 b2 b1 b0 a3 a2 a1 a0 93 92 91 90 83 82 81 80
        // q1 = f3 f2 f1 f0 e3 e2 e1 e0 d3 d2 d1 d0 c3 c2 c1 c0
        t1 = p1s;
        p1s = Vector128_.UnpackLow(t1.AsInt16(), q1s.AsInt16()).AsByte();
        q1s = Vector128_.UnpackHigh(t1.AsInt16(), q1s.AsInt16()).AsByte();

        Store4x4Vector128(p0s, ref r0Ref, stride);
        Store4x4Vector128(q0s, ref Unsafe.Add(ref r0Ref, 4 * (uint)stride), stride);

        Store4x4Vector128(p1s, ref r8Ref, stride);
        Store4x4Vector128(q1s, ref Unsafe.Add(ref r8Ref, 4 * (uint)stride), stride);
    }

    private static void Store4x4Vector128(Vector128<byte> x, ref byte dstRef, int stride)
    {
        int offset = 0;
        for (int i = 0; i < 4; i++)
        {
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dstRef, (uint)offset)) = x.AsInt32().ToScalar();
            x = Vector128_.ShiftRightBytesInVector(x, 4);
            offset += stride;
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector128<sbyte> GetBaseDeltaVector128(Vector128<sbyte> p1, Vector128<sbyte> p0, Vector128<sbyte> q0, Vector128<sbyte> q1)
    {
        // Beware of addition order, for saturation!
        Vector128<sbyte> p1q1 = Vector128_.SubtractSaturate(p1, q1); // p1 - q1
        Vector128<sbyte> q0p0 = Vector128_.SubtractSaturate(q0, p0); // q0 - p0
        Vector128<sbyte> s1 = Vector128_.AddSaturate(p1q1, q0p0); // p1 - q1 + 1 * (q0 - p0)
        Vector128<sbyte> s2 = Vector128_.AddSaturate(q0p0, s1); // p1 - q1 + 2 * (q0 - p0)
        return Vector128_.AddSaturate(q0p0, s2); // p1 - q1 + 3 * (q0 - p0)
    }

    // Shift each byte of "x" by 3 bits while preserving by the sign bit.
    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector128<sbyte> SignedShift8bVector128(Vector128<byte> x)
    {
        Vector128<byte> low0 = Vector128_.UnpackLow(Vector128<byte>.Zero, x);
        Vector128<byte> high0 = Vector128_.UnpackHigh(Vector128<byte>.Zero, x);
        Vector128<short> low1 = Vector128.ShiftRightArithmetic(low0.AsInt16(), 3 + 8);
        Vector128<short> high1 = Vector128.ShiftRightArithmetic(high0.AsInt16(), 3 + 8);

        return Vector128_.PackSignedSaturate(low1, high1);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void ComplexMaskVector128(Vector128<byte> p1, Vector128<byte> p0, Vector128<byte> q0, Vector128<byte> q1, int thresh, int ithresh, ref Vector128<byte> mask)
    {
        Vector128<byte> it = Vector128.Create((byte)ithresh);
        Vector128<byte> diff = Vector128_.SubtractSaturate(mask, it);
        Vector128<byte> threshMask = Vector128.Equals(diff, Vector128<byte>.Zero);
        Vector128<byte> filterMask = NeedsFilterVector128(p1, p0, q0, q1, thresh);

        mask = threshMask & filterMask;
    }

    // Updates values of 2 pixels at MB edge during complex filtering.
    // Update operations:
    // q = q - delta and p = p + delta; where delta = [(a_hi >> 7), (a_lo >> 7)]
    // Pixels 'pi' and 'qi' are int8_t on input, uint8_t on output (sign flip).
    private static void Update2PixelsVector128(ref Vector128<byte> pi, ref Vector128<byte> qi, Vector128<short> a0Low, Vector128<short> a0High)
    {
        Vector128<byte> signBit = Vector128.Create((byte)0x80);
        Vector128<short> a1Low = Vector128.ShiftRightArithmetic(a0Low, 7);
        Vector128<short> a1High = Vector128.ShiftRightArithmetic(a0High, 7);
        Vector128<sbyte> delta = Vector128_.PackSignedSaturate(a1Low, a1High);
        pi = Vector128_.AddSaturate(pi.AsSByte(), delta).AsByte();
        qi = Vector128_.SubtractSaturate(qi.AsSByte(), delta).AsByte();
        pi ^= signBit.AsByte();
        qi ^= signBit.AsByte();
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector128<byte> LoadUvEdgeVector128(ref byte uRef, ref byte vRef, int offset)
    {
        Vector128<long> uVec = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref uRef, (uint)offset)), 0);
        Vector128<long> vVec = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref vRef, (uint)offset)), 0);
        return Vector128_.UnpackLow(uVec, vVec).AsByte();
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void StoreUvVector128(Vector128<byte> x, ref byte uRef, ref byte vRef, int offset)
    {
        Unsafe.As<byte, Vector64<byte>>(ref Unsafe.Add(ref uRef, (uint)offset)) = x.GetLower();
        Unsafe.As<byte, Vector64<byte>>(ref Unsafe.Add(ref vRef, (uint)offset)) = x.GetUpper();
    }

    // Compute abs(p - q) = subs(p - q) OR subs(q - p)
    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector128<byte> AbsVector128(Vector128<byte> p, Vector128<byte> q)
        => Vector128_.SubtractSaturate(q, p) | Vector128_.SubtractSaturate(p, q);

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
        const int end = 8 * WebpConstants.Bps;
        for (int j = 0; j < end; j += WebpConstants.Bps)
        {
            // memset(dst + j * BPS, value, 8);
            Memset(dst, value, j, 8);
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void Memset(Span<byte> dst, byte value, int startIdx, int count) => dst.Slice(startIdx, count).Fill(value);

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int Clamp255(int x) => Numerics.Clamp(x, 0, 255);
}

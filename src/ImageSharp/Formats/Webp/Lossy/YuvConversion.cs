// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

internal static class YuvConversion
{
    /// <summary>
    /// Fixed-point precision for RGB->YUV.
    /// </summary>
    private const int YuvFix = 16;

    private const int YuvHalf = 1 << (YuvFix - 1);

    // UpSample from YUV to RGB.
    // Given samples laid out in a square as:
    //  [a b]
    //  [c d]
    // we interpolate u/v as:
    //  ([9*a + 3*b + 3*c +   d    3*a + 9*b + 3*c +   d] + [8 8]) / 16
    //  ([3*a +   b + 9*c + 3*d      a + 3*b + 3*c + 9*d]   [8 8]) / 16
    public static void UpSample(Span<byte> topY, Span<byte> bottomY, Span<byte> topU, Span<byte> topV, Span<byte> curU, Span<byte> curV, Span<byte> topDst, Span<byte> bottomDst, int len, byte[] uvBuffer)
    {
        if (Sse41.IsSupported)
        {
            UpSampleSse41(topY, bottomY, topU, topV, curU, curV, topDst, bottomDst, len, uvBuffer);
        }
        else
        {
            UpSampleScalar(topY, bottomY, topU, topV, curU, curV, topDst, bottomDst, len);
        }
    }

    private static void UpSampleScalar(Span<byte> topY, Span<byte> bottomY, Span<byte> topU, Span<byte> topV, Span<byte> curU, Span<byte> curV, Span<byte> topDst, Span<byte> bottomDst, int len)
    {
        const int xStep = 3;
        int lastPixelPair = (len - 1) >> 1;
        uint tluv = LoadUv(topU[0], topV[0]); // top-left sample
        uint luv = LoadUv(curU[0], curV[0]); // left-sample
        uint uv0 = ((3 * tluv) + luv + 0x00020002u) >> 2;
        YuvToBgr(topY[0], (int)(uv0 & 0xff), (int)(uv0 >> 16), topDst);

        if (!bottomY.IsEmpty)
        {
            uv0 = ((3 * luv) + tluv + 0x00020002u) >> 2;
            YuvToBgr(bottomY[0], (int)uv0 & 0xff, (int)(uv0 >> 16), bottomDst);
        }

        for (int x = 1; x <= lastPixelPair; x++)
        {
            uint tuv = LoadUv(topU[x], topV[x]); // top sample
            uint uv = LoadUv(curU[x], curV[x]); // sample

            // Precompute invariant values associated with first and second diagonals.
            uint avg = tluv + tuv + luv + uv + 0x00080008u;
            uint diag12 = (avg + (2 * (tuv + luv))) >> 3;
            uint diag03 = (avg + (2 * (tluv + uv))) >> 3;
            uv0 = (diag12 + tluv) >> 1;
            uint uv1 = (diag03 + tuv) >> 1;
            int xMul2 = x * 2;
            YuvToBgr(topY[xMul2 - 1], (int)(uv0 & 0xff), (int)(uv0 >> 16), topDst[((xMul2 - 1) * xStep)..]);
            YuvToBgr(topY[xMul2 - 0], (int)(uv1 & 0xff), (int)(uv1 >> 16), topDst[((xMul2 - 0) * xStep)..]);

            if (!bottomY.IsEmpty)
            {
                uv0 = (diag03 + luv) >> 1;
                uv1 = (diag12 + uv) >> 1;
                YuvToBgr(bottomY[xMul2 - 1], (int)(uv0 & 0xff), (int)(uv0 >> 16), bottomDst[((xMul2 - 1) * xStep)..]);
                YuvToBgr(bottomY[xMul2 + 0], (int)(uv1 & 0xff), (int)(uv1 >> 16), bottomDst[((xMul2 + 0) * xStep)..]);
            }

            tluv = tuv;
            luv = uv;
        }

        if ((len & 1) == 0)
        {
            uv0 = ((3 * tluv) + luv + 0x00020002u) >> 2;
            YuvToBgr(topY[len - 1], (int)(uv0 & 0xff), (int)(uv0 >> 16), topDst[((len - 1) * xStep)..]);
            if (!bottomY.IsEmpty)
            {
                uv0 = ((3 * luv) + tluv + 0x00020002u) >> 2;
                YuvToBgr(bottomY[len - 1], (int)(uv0 & 0xff), (int)(uv0 >> 16), bottomDst[((len - 1) * xStep)..]);
            }
        }
    }

    // We compute (9*a + 3*b + 3*c + d + 8) / 16 as follows
    // u = (9*a + 3*b + 3*c + d + 8) / 16
    //   = (a + (a + 3*b + 3*c + d) / 8 + 1) / 2
    //   = (a + m + 1) / 2
    // where m = (a + 3*b + 3*c + d) / 8
    //         = ((a + b + c + d) / 2 + b + c) / 4
    //
    // Let's say  k = (a + b + c + d) / 4.
    // We can compute k as
    // k = (s + t + 1) / 2 - ((a^d) | (b^c) | (s^t)) & 1
    // where s = (a + d + 1) / 2 and t = (b + c + 1) / 2
    //
    // Then m can be written as
    // m = (k + t + 1) / 2 - (((b^c) & (s^t)) | (k^t)) & 1
    private static void UpSampleSse41(Span<byte> topY, Span<byte> bottomY, Span<byte> topU, Span<byte> topV, Span<byte> curU, Span<byte> curV, Span<byte> topDst, Span<byte> bottomDst, int len, byte[] uvBuffer)
    {
        const int xStep = 3;
        Array.Clear(uvBuffer);
        Span<byte> ru = uvBuffer.AsSpan(15);
        Span<byte> rv = ru[32..];

        // Treat the first pixel in regular way.
        int uDiag = ((topU[0] + curU[0]) >> 1) + 1;
        int vDiag = ((topV[0] + curV[0]) >> 1) + 1;
        int u0t = (topU[0] + uDiag) >> 1;
        int v0t = (topV[0] + vDiag) >> 1;
        YuvToBgr(topY[0], u0t, v0t, topDst);
        if (!bottomY.IsEmpty)
        {
            int u0b = (curU[0] + uDiag) >> 1;
            int v0b = (curV[0] + vDiag) >> 1;
            YuvToBgr(bottomY[0], u0b, v0b, bottomDst);
        }

        // For UpSample32Pixels, 17 u/v values must be read-able for each block.
        int pos;
        int uvPos;
        ref byte topURef = ref MemoryMarshal.GetReference(topU);
        ref byte topVRef = ref MemoryMarshal.GetReference(topV);
        ref byte curURef = ref MemoryMarshal.GetReference(curU);
        ref byte curVRef = ref MemoryMarshal.GetReference(curV);
        if (!bottomY.IsEmpty)
        {
            for (pos = 1, uvPos = 0; pos + 32 + 1 <= len; pos += 32, uvPos += 16)
            {
                UpSample32Pixels(ref Unsafe.Add(ref topURef, (uint)uvPos), ref Unsafe.Add(ref curURef, (uint)uvPos), ru);
                UpSample32Pixels(ref Unsafe.Add(ref topVRef, (uint)uvPos), ref Unsafe.Add(ref curVRef, (uint)uvPos), rv);
                ConvertYuvToBgrWithBottomYSse41(topY, bottomY, topDst, bottomDst, ru, rv, pos, xStep);
            }
        }
        else
        {
            for (pos = 1, uvPos = 0; pos + 32 + 1 <= len; pos += 32, uvPos += 16)
            {
                UpSample32Pixels(ref Unsafe.Add(ref topURef, (uint)uvPos), ref Unsafe.Add(ref curURef, (uint)uvPos), ru);
                UpSample32Pixels(ref Unsafe.Add(ref topVRef, (uint)uvPos), ref Unsafe.Add(ref curVRef, (uint)uvPos), rv);
                ConvertYuvToBgrSse41(topY, topDst, ru, rv, pos, xStep);
            }
        }

        // Process last block.
        if (len > 1)
        {
            int leftOver = ((len + 1) >> 1) - (pos >> 1);
            Span<byte> tmpTopDst = ru[(4 * 32)..];
            Span<byte> tmpBottomDst = tmpTopDst[(4 * 32)..];
            Span<byte> tmpTop = tmpBottomDst[(4 * 32)..];
            Span<byte> tmpBottom = bottomY.IsEmpty ? null : tmpTop[32..];
            UpSampleLastBlock(topU[uvPos..], curU[uvPos..], leftOver, ru);
            UpSampleLastBlock(topV[uvPos..], curV[uvPos..], leftOver, rv);

            topY[pos..len].CopyTo(tmpTop);
            if (!bottomY.IsEmpty)
            {
                bottomY[pos..len].CopyTo(tmpBottom);
                ConvertYuvToBgrWithBottomYSse41(tmpTop, tmpBottom, tmpTopDst, tmpBottomDst, ru, rv, 0, xStep);
            }
            else
            {
                ConvertYuvToBgrSse41(tmpTop, tmpTopDst, ru, rv, 0, xStep);
            }

            tmpTopDst[..((len - pos) * xStep)].CopyTo(topDst[(pos * xStep)..]);
            if (!bottomY.IsEmpty)
            {
                tmpBottomDst[..((len - pos) * xStep)].CopyTo(bottomDst[(pos * xStep)..]);
            }
        }
    }

    // Loads 17 pixels each from rows r1 and r2 and generates 32 pixels.
    private static void UpSample32Pixels(ref byte r1, ref byte r2, Span<byte> output)
    {
        // Load inputs.
        Vector128<byte> a = Unsafe.As<byte, Vector128<byte>>(ref r1);
        Vector128<byte> b = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref r1, 1));
        Vector128<byte> c = Unsafe.As<byte, Vector128<byte>>(ref r2);
        Vector128<byte> d = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref r2, 1));

        Vector128<byte> s = Sse2.Average(a, d); // s = (a + d + 1) / 2
        Vector128<byte> t = Sse2.Average(b, c); // t = (b + c + 1) / 2
        Vector128<byte> st = Sse2.Xor(s, t); // st = s^t

        Vector128<byte> ad = Sse2.Xor(a, d); // ad = a^d
        Vector128<byte> bc = Sse2.Xor(b, c); // bc = b^c

        Vector128<byte> t1 = Sse2.Or(ad, bc); // (a^d) | (b^c)
        Vector128<byte> t2 = Sse2.Or(t1, st); // (a^d) | (b^c) | (s^t)
        Vector128<byte> t3 = Sse2.And(t2, Vector128.Create((byte)1)); // (a^d) | (b^c) | (s^t) & 1
        Vector128<byte> t4 = Sse2.Average(s, t);
        Vector128<byte> k = Sse2.Subtract(t4, t3); // k = (a + b + c + d) / 4

        Vector128<byte> diag1 = GetM(k, st, bc, t);
        Vector128<byte> diag2 = GetM(k, st, ad, s);

        // Pack the alternate pixels.
        PackAndStore(a, b, diag1, diag2, output); // store top.
        PackAndStore(c, d, diag2, diag1, output[(2 * 32)..]);
    }

    private static void UpSampleLastBlock(Span<byte> tb, Span<byte> bb, int numPixels, Span<byte> output)
    {
        Span<byte> r1 = stackalloc byte[17];
        Span<byte> r2 = stackalloc byte[17];
        tb[..numPixels].CopyTo(r1);
        bb[..numPixels].CopyTo(r2);

        // Replicate last byte.
        int length = 17 - numPixels;
        if (length > 0)
        {
            r1.Slice(numPixels, length).Fill(r1[numPixels - 1]);
            r2.Slice(numPixels, length).Fill(r2[numPixels - 1]);
        }

        ref byte r1Ref = ref MemoryMarshal.GetReference(r1);
        ref byte r2Ref = ref MemoryMarshal.GetReference(r2);
        UpSample32Pixels(ref r1Ref, ref r2Ref, output);
    }

    // Computes out = (k + in + 1) / 2 - ((ij & (s^t)) | (k^in)) & 1
    private static Vector128<byte> GetM(Vector128<byte> k, Vector128<byte> st, Vector128<byte> ij, Vector128<byte> input)
    {
        Vector128<byte> tmp0 = Sse2.Average(k, input); // (k + in + 1) / 2
        Vector128<byte> tmp1 = Sse2.And(ij, st); // (ij) & (s^t)
        Vector128<byte> tmp2 = Sse2.Xor(k, input); // (k^in)
        Vector128<byte> tmp3 = Sse2.Or(tmp1, tmp2); // ((ij) & (s^t)) | (k^in)
        Vector128<byte> tmp4 = Sse2.And(tmp3, Vector128.Create((byte)1)); // & 1 -> lsb_correction

        return Sse2.Subtract(tmp0, tmp4); // (k + in + 1) / 2 - lsb_correction
    }

    private static void PackAndStore(Vector128<byte> a, Vector128<byte> b, Vector128<byte> da, Vector128<byte> db, Span<byte> output)
    {
        Vector128<byte> ta = Sse2.Average(a, da); // (9a + 3b + 3c +  d + 8) / 16
        Vector128<byte> tb = Sse2.Average(b, db); // (3a + 9b +  c + 3d + 8) / 16
        Vector128<byte> t1 = Sse2.UnpackLow(ta, tb);
        Vector128<byte> t2 = Sse2.UnpackHigh(ta, tb);

        ref byte output0Ref = ref MemoryMarshal.GetReference(output);
        ref byte output1Ref = ref Unsafe.Add(ref output0Ref, 16);
        Unsafe.As<byte, Vector128<byte>>(ref output0Ref) = t1;
        Unsafe.As<byte, Vector128<byte>>(ref output1Ref) = t2;
    }

    /// <summary>
    /// Converts the pixel values of the image to YUV.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
    /// <param name="frame">The frame to convert.</param>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="y">Span to store the luma component of the image.</param>
    /// <param name="u">Span to store the u component of the image.</param>
    /// <param name="v">Span to store the v component of the image.</param>
    /// <returns>true, if the image contains alpha data.</returns>
    public static bool ConvertRgbToYuv<TPixel>(Buffer2DRegion<TPixel> frame, Configuration configuration, MemoryAllocator memoryAllocator, Span<byte> y, Span<byte> u, Span<byte> v)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = frame.Width;
        int height = frame.Height;
        int uvWidth = (width + 1) >> 1;

        // Temporary storage for accumulated R/G/B values during conversion to U/V.
        using IMemoryOwner<ushort> tmpRgb = memoryAllocator.Allocate<ushort>(4 * uvWidth);
        using IMemoryOwner<Bgra32> bgraRow0Buffer = memoryAllocator.Allocate<Bgra32>(width);
        using IMemoryOwner<Bgra32> bgraRow1Buffer = memoryAllocator.Allocate<Bgra32>(width);
        Span<ushort> tmpRgbSpan = tmpRgb.GetSpan();
        Span<Bgra32> bgraRow0 = bgraRow0Buffer.GetSpan();
        Span<Bgra32> bgraRow1 = bgraRow1Buffer.GetSpan();
        int uvRowIndex = 0;
        int rowIndex;
        bool hasAlpha = false;
        for (rowIndex = 0; rowIndex < height - 1; rowIndex += 2)
        {
            Span<TPixel> rowSpan = frame.DangerousGetRowSpan(rowIndex);
            Span<TPixel> nextRowSpan = frame.DangerousGetRowSpan(rowIndex + 1);
            PixelOperations<TPixel>.Instance.ToBgra32(configuration, rowSpan, bgraRow0);
            PixelOperations<TPixel>.Instance.ToBgra32(configuration, nextRowSpan, bgraRow1);

            bool rowsHaveAlpha = WebpCommonUtils.CheckNonOpaque(bgraRow0) && WebpCommonUtils.CheckNonOpaque(bgraRow1);
            if (rowsHaveAlpha)
            {
                hasAlpha = true;
            }

            // Downsample U/V planes, two rows at a time.
            if (!rowsHaveAlpha)
            {
                AccumulateRgb(bgraRow0, bgraRow1, tmpRgbSpan, width);
            }
            else
            {
                AccumulateRgba(bgraRow0, bgraRow1, tmpRgbSpan, width);
            }

            ConvertRgbaToUv(tmpRgbSpan, u[(uvRowIndex * uvWidth)..], v[(uvRowIndex * uvWidth)..], uvWidth);
            uvRowIndex++;

            ConvertRgbaToY(bgraRow0, y[(rowIndex * width)..], width);
            ConvertRgbaToY(bgraRow1, y[((rowIndex + 1) * width)..], width);
        }

        // Extra last row.
        if ((height & 1) != 0)
        {
            Span<TPixel> rowSpan = frame.DangerousGetRowSpan(rowIndex);
            PixelOperations<TPixel>.Instance.ToBgra32(configuration, rowSpan, bgraRow0);
            ConvertRgbaToY(bgraRow0, y[(rowIndex * width)..], width);

            if (!WebpCommonUtils.CheckNonOpaque(bgraRow0))
            {
                AccumulateRgb(bgraRow0, bgraRow0, tmpRgbSpan, width);
            }
            else
            {
                AccumulateRgba(bgraRow0, bgraRow0, tmpRgbSpan, width);
                hasAlpha = true;
            }

            ConvertRgbaToUv(tmpRgbSpan, u[(uvRowIndex * uvWidth)..], v[(uvRowIndex * uvWidth)..], uvWidth);
        }

        return hasAlpha;
    }

    /// <summary>
    /// Converts a rgba pixel row to Y.
    /// </summary>
    /// <param name="rowSpan">The row span to convert.</param>
    /// <param name="y">The destination span for y.</param>
    /// <param name="width">The width.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void ConvertRgbaToY(Span<Bgra32> rowSpan, Span<byte> y, int width)
    {
        for (int x = 0; x < width; x++)
        {
            y[x] = (byte)RgbToY(rowSpan[x].R, rowSpan[x].G, rowSpan[x].B, YuvHalf);
        }
    }

    /// <summary>
    /// Converts a rgb row of pixels to UV.
    /// </summary>
    /// <param name="rgb">The RGB pixel row.</param>
    /// <param name="u">The destination span for u.</param>
    /// <param name="v">The destination span for v.</param>
    /// <param name="width">The width.</param>
    public static void ConvertRgbaToUv(Span<ushort> rgb, Span<byte> u, Span<byte> v, int width)
    {
        int rgbIdx = 0;
        for (int i = 0; i < width; i += 1, rgbIdx += 4)
        {
            int r = rgb[rgbIdx], g = rgb[rgbIdx + 1], b = rgb[rgbIdx + 2];
            u[i] = (byte)RgbToU(r, g, b, YuvHalf << 2);
            v[i] = (byte)RgbToV(r, g, b, YuvHalf << 2);
        }
    }

    public static void AccumulateRgb(Span<Bgra32> rowSpan, Span<Bgra32> nextRowSpan, Span<ushort> dst, int width)
    {
        Bgra32 bgra0;
        Bgra32 bgra1;
        int i, j;
        int dstIdx = 0;
        for (i = 0, j = 0; i < (width >> 1); i += 1, j += 2, dstIdx += 4)
        {
            bgra0 = rowSpan[j];
            bgra1 = rowSpan[j + 1];
            Bgra32 bgra2 = nextRowSpan[j];
            Bgra32 bgra3 = nextRowSpan[j + 1];

            dst[dstIdx] = (ushort)LinearToGamma(
                GammaToLinear(bgra0.R) +
                        GammaToLinear(bgra1.R) +
                        GammaToLinear(bgra2.R) +
                        GammaToLinear(bgra3.R),
                0);
            dst[dstIdx + 1] = (ushort)LinearToGamma(
                GammaToLinear(bgra0.G) +
                        GammaToLinear(bgra1.G) +
                        GammaToLinear(bgra2.G) +
                        GammaToLinear(bgra3.G),
                0);
            dst[dstIdx + 2] = (ushort)LinearToGamma(
                GammaToLinear(bgra0.B) +
                        GammaToLinear(bgra1.B) +
                        GammaToLinear(bgra2.B) +
                        GammaToLinear(bgra3.B),
                0);
        }

        if ((width & 1) != 0)
        {
            bgra0 = rowSpan[j];
            bgra1 = nextRowSpan[j];

            dst[dstIdx] = (ushort)LinearToGamma(GammaToLinear(bgra0.R) + GammaToLinear(bgra1.R), 1);
            dst[dstIdx + 1] = (ushort)LinearToGamma(GammaToLinear(bgra0.G) + GammaToLinear(bgra1.G), 1);
            dst[dstIdx + 2] = (ushort)LinearToGamma(GammaToLinear(bgra0.B) + GammaToLinear(bgra1.B), 1);
        }
    }

    public static void AccumulateRgba(Span<Bgra32> rowSpan, Span<Bgra32> nextRowSpan, Span<ushort> dst, int width)
    {
        Bgra32 bgra0;
        Bgra32 bgra1;
        int i, j;
        int dstIdx = 0;
        for (i = 0, j = 0; i < width >> 1; i += 1, j += 2, dstIdx += 4)
        {
            bgra0 = rowSpan[j];
            bgra1 = rowSpan[j + 1];
            Bgra32 bgra2 = nextRowSpan[j];
            Bgra32 bgra3 = nextRowSpan[j + 1];
            uint a = (uint)(bgra0.A + bgra1.A + bgra2.A + bgra3.A);
            int r, g, b;
            if (a is 4 * 0xff or 0)
            {
                r = (ushort)LinearToGamma(
                    GammaToLinear(bgra0.R) +
                    GammaToLinear(bgra1.R) +
                    GammaToLinear(bgra2.R) +
                    GammaToLinear(bgra3.R),
                    0);
                g = (ushort)LinearToGamma(
                    GammaToLinear(bgra0.G) +
                    GammaToLinear(bgra1.G) +
                    GammaToLinear(bgra2.G) +
                    GammaToLinear(bgra3.G),
                    0);
                b = (ushort)LinearToGamma(
                    GammaToLinear(bgra0.B) +
                    GammaToLinear(bgra1.B) +
                    GammaToLinear(bgra2.B) +
                    GammaToLinear(bgra3.B),
                    0);
            }
            else
            {
                r = LinearToGammaWeighted(bgra0.R, bgra1.R, bgra2.R, bgra3.R, bgra0.A, bgra1.A, bgra2.A, bgra3.A, a);
                g = LinearToGammaWeighted(bgra0.G, bgra1.G, bgra2.G, bgra3.G, bgra0.A, bgra1.A, bgra2.A, bgra3.A, a);
                b = LinearToGammaWeighted(bgra0.B, bgra1.B, bgra2.B, bgra3.B, bgra0.A, bgra1.A, bgra2.A, bgra3.A, a);
            }

            dst[dstIdx] = (ushort)r;
            dst[dstIdx + 1] = (ushort)g;
            dst[dstIdx + 2] = (ushort)b;
            dst[dstIdx + 3] = (ushort)a;
        }

        if ((width & 1) != 0)
        {
            bgra0 = rowSpan[j];
            bgra1 = nextRowSpan[j];
            uint a = (uint)(2u * (bgra0.A + bgra1.A));
            int r, g, b;
            if (a is 4 * 0xff or 0)
            {
                r = (ushort)LinearToGamma(GammaToLinear(bgra0.R) + GammaToLinear(bgra1.R), 1);
                g = (ushort)LinearToGamma(GammaToLinear(bgra0.G) + GammaToLinear(bgra1.G), 1);
                b = (ushort)LinearToGamma(GammaToLinear(bgra0.B) + GammaToLinear(bgra1.B), 1);
            }
            else
            {
                r = LinearToGammaWeighted(bgra0.R, bgra1.R, bgra0.R, bgra1.R, bgra0.A, bgra1.A, bgra0.A, bgra1.A, a);
                g = LinearToGammaWeighted(bgra0.G, bgra1.G, bgra0.G, bgra1.G, bgra0.A, bgra1.A, bgra0.A, bgra1.A, a);
                b = LinearToGammaWeighted(bgra0.B, bgra1.B, bgra0.B, bgra1.B, bgra0.A, bgra1.A, bgra0.A, bgra1.A, a);
            }

            dst[dstIdx] = (ushort)r;
            dst[dstIdx + 1] = (ushort)g;
            dst[dstIdx + 2] = (ushort)b;
            dst[dstIdx + 3] = (ushort)a;
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int LinearToGammaWeighted(byte rgb0, byte rgb1, byte rgb2, byte rgb3, byte a0, byte a1, byte a2, byte a3, uint totalA)
    {
        uint sum = (a0 * GammaToLinear(rgb0)) + (a1 * GammaToLinear(rgb1)) + (a2 * GammaToLinear(rgb2)) + (a3 * GammaToLinear(rgb3));
        return LinearToGamma((sum * WebpLookupTables.InvAlpha[totalA]) >> (WebpConstants.AlphaFix - 2), 0);
    }

    // Convert a linear value 'v' to YUV_FIX+2 fixed-point precision
    // U/V value, suitable for RGBToU/V calls.
    [MethodImpl(InliningOptions.ShortMethod)]
    private static int LinearToGamma(uint baseValue, int shift)
    {
        int y = Interpolate((int)(baseValue << shift));   // Final uplifted value.
        return (y + WebpConstants.GammaTabRounder) >> WebpConstants.GammaTabFix;    // Descale.
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static uint GammaToLinear(byte v) => WebpLookupTables.GammaToLinearTab[v];

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int Interpolate(int v)
    {
        int tabPos = v >> (WebpConstants.GammaTabFix + 2);    // integer part.
        int x = v & ((WebpConstants.GammaTabScale << 2) - 1);  // fractional part.
        int v0 = WebpLookupTables.LinearToGammaTab[tabPos];
        int v1 = WebpLookupTables.LinearToGammaTab[tabPos + 1];
        int y = (v1 * x) + (v0 * ((WebpConstants.GammaTabScale << 2) - x));   // interpolate

        return y;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int RgbToY(byte r, byte g, byte b, int rounding)
    {
        int luma = (16839 * r) + (33059 * g) + (6420 * b);
        return (luma + rounding + (16 << YuvFix)) >> YuvFix;  // No need to clip.
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int RgbToU(int r, int g, int b, int rounding)
    {
        int u = (-9719 * r) - (19081 * g) + (28800 * b);
        return ClipUv(u, rounding);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int RgbToV(int r, int g, int b, int rounding)
    {
        int v = (+28800 * r) - (24116 * g) - (4684 * b);
        return ClipUv(v, rounding);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int ClipUv(int uv, int rounding)
    {
        uv = (uv + rounding + (128 << (YuvFix + 2))) >> (YuvFix + 2);
        return (uv & ~0xff) == 0 ? uv : uv < 0 ? 0 : 255;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint LoadUv(byte u, byte v) =>
        (uint)(u | (v << 16)); // We process u and v together stashed into 32bit(16bit each).

    [MethodImpl(InliningOptions.ShortMethod)]
    public static void YuvToBgr(int y, int u, int v, Span<byte> bgr)
    {
        bgr[2] = (byte)YuvToR(y, v);
        bgr[1] = (byte)YuvToG(y, u, v);
        bgr[0] = (byte)YuvToB(y, u);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void ConvertYuvToBgrSse41(Span<byte> topY, Span<byte> topDst, Span<byte> ru, Span<byte> rv, int curX, int step) => YuvToBgrSse41(topY[curX..], ru, rv, topDst[(curX * step)..]);

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void ConvertYuvToBgrWithBottomYSse41(Span<byte> topY, Span<byte> bottomY, Span<byte> topDst, Span<byte> bottomDst, Span<byte> ru, Span<byte> rv, int curX, int step)
    {
        YuvToBgrSse41(topY[curX..], ru, rv, topDst[(curX * step)..]);
        YuvToBgrSse41(bottomY[curX..], ru[64..], rv[64..], bottomDst[(curX * step)..]);
    }

    private static void YuvToBgrSse41(Span<byte> y, Span<byte> u, Span<byte> v, Span<byte> dst)
    {
        ref byte yRef = ref MemoryMarshal.GetReference(y);
        ref byte uRef = ref MemoryMarshal.GetReference(u);
        ref byte vRef = ref MemoryMarshal.GetReference(v);
        ConvertYuv444ToBgrSse41(ref yRef, ref uRef, ref vRef, out Vector128<short> r0, out Vector128<short> g0, out Vector128<short> b0);
        ConvertYuv444ToBgrSse41(ref Unsafe.Add(ref yRef, 8), ref Unsafe.Add(ref uRef, 8), ref Unsafe.Add(ref vRef, 8), out Vector128<short> r1, out Vector128<short> g1, out Vector128<short> b1);
        ConvertYuv444ToBgrSse41(ref Unsafe.Add(ref yRef, 16), ref Unsafe.Add(ref uRef, 16), ref Unsafe.Add(ref vRef, 16), out Vector128<short> r2, out Vector128<short> g2, out Vector128<short> b2);
        ConvertYuv444ToBgrSse41(ref Unsafe.Add(ref yRef, 24), ref Unsafe.Add(ref uRef, 24), ref Unsafe.Add(ref vRef, 24), out Vector128<short> r3, out Vector128<short> g3, out Vector128<short> b3);

        // Cast to 8b and store as BBBBGGGGRRRR.
        Vector128<byte> bgr0 = Sse2.PackUnsignedSaturate(b0, b1);
        Vector128<byte> bgr1 = Sse2.PackUnsignedSaturate(b2, b3);
        Vector128<byte> bgr2 = Sse2.PackUnsignedSaturate(g0, g1);
        Vector128<byte> bgr3 = Sse2.PackUnsignedSaturate(g2, g3);
        Vector128<byte> bgr4 = Sse2.PackUnsignedSaturate(r0, r1);
        Vector128<byte> bgr5 = Sse2.PackUnsignedSaturate(r2, r3);

        // Pack as BGRBGRBGRBGR.
        PlanarTo24bSse41(bgr0, bgr1, bgr2, bgr3, bgr4, bgr5, dst);
    }

    // Pack the planar buffers
    // rrrr... rrrr... gggg... gggg... bbbb... bbbb....
    // triplet by triplet in the output buffer rgb as rgbrgbrgbrgb ...
    private static void PlanarTo24bSse41(Vector128<byte> input0, Vector128<byte> input1, Vector128<byte> input2, Vector128<byte> input3, Vector128<byte> input4, Vector128<byte> input5, Span<byte> rgb)
    {
        // The input is 6 registers of sixteen 8b but for the sake of explanation,
        // let's take 6 registers of four 8b values.
        // To pack, we will keep taking one every two 8b integer and move it
        // around as follows:
        // Input:
        //   r0r1r2r3 | r4r5r6r7 | g0g1g2g3 | g4g5g6g7 | b0b1b2b3 | b4b5b6b7
        // Split the 6 registers in two sets of 3 registers: the first set as the even
        // 8b bytes, the second the odd ones:
        //   r0r2r4r6 | g0g2g4g6 | b0b2b4b6 | r1r3r5r7 | g1g3g5g7 | b1b3b5b7
        // Repeat the same permutations twice more:
        //   r0r4g0g4 | b0b4r1r5 | g1g5b1b5 | r2r6g2g6 | b2b6r3r7 | g3g7b3b7
        //   r0g0b0r1 | g1b1r2g2 | b2r3g3b3 | r4g4b4r5 | g5b5r6g6 | b6r7g7b7

        // Process R.
        ChannelMixing(
            input0,
            input1,
            Vector128.Create(0, 255, 255, 1, 255, 255, 2, 255, 255, 3, 255, 255, 4, 255, 255, 5),        // PlanarTo24Shuffle0
            Vector128.Create(255, 255, 6, 255, 255, 7, 255, 255, 8, 255, 255, 9, 255, 255, 10, 255),     // PlanarTo24Shuffle1
            Vector128.Create(255, 11, 255, 255, 12, 255, 255, 13, 255, 255, 14, 255, 255, 15, 255, 255), // PlanarTo24Shuffle2
            out Vector128<byte> r0,
            out Vector128<byte> r1,
            out Vector128<byte> r2,
            out Vector128<byte> r3,
            out Vector128<byte> r4,
            out Vector128<byte> r5);

        // Process G.
        // Same as before, just shifted to the left by one and including the right padding.
        ChannelMixing(
            input2,
            input3,
            Vector128.Create(255, 0, 255, 255, 1, 255, 255, 2, 255, 255, 3, 255, 255, 4, 255, 255),      // PlanarTo24Shuffle3
            Vector128.Create(5, 255, 255, 6, 255, 255, 7, 255, 255, 8, 255, 255, 9, 255, 255, 10),       // PlanarTo24Shuffle4
            Vector128.Create(255, 255, 11, 255, 255, 12, 255, 255, 13, 255, 255, 14, 255, 255, 15, 255), // PlanarTo24Shuffle5
            out Vector128<byte> g0,
            out Vector128<byte> g1,
            out Vector128<byte> g2,
            out Vector128<byte> g3,
            out Vector128<byte> g4,
            out Vector128<byte> g5);

        // Process B.
        ChannelMixing(
            input4,
            input5,
            Vector128.Create(255, 255, 0, 255, 255, 1, 255, 255, 2, 255, 255, 3, 255, 255, 4, 255),     // PlanarTo24Shuffle6
            Vector128.Create(255, 5, 255, 255, 6, 255, 255, 7, 255, 255, 8, 255, 255, 9, 255, 255),     // PlanarTo24Shuffle7
            Vector128.Create(10, 255, 255, 11, 255, 255, 12, 255, 255, 13, 255, 255, 14, 255, 255, 15), // PlanarTo24Shuffle8
            out Vector128<byte> b0,
            out Vector128<byte> b1,
            out Vector128<byte> b2,
            out Vector128<byte> b3,
            out Vector128<byte> b4,
            out Vector128<byte> b5);

        // OR the different channels.
        Vector128<byte> rg0 = Sse2.Or(r0, g0);
        Vector128<byte> rg1 = Sse2.Or(r1, g1);
        Vector128<byte> rg2 = Sse2.Or(r2, g2);
        Vector128<byte> rg3 = Sse2.Or(r3, g3);
        Vector128<byte> rg4 = Sse2.Or(r4, g4);
        Vector128<byte> rg5 = Sse2.Or(r5, g5);

        ref byte outputRef = ref MemoryMarshal.GetReference(rgb);
        Unsafe.As<byte, Vector128<byte>>(ref outputRef) = Sse2.Or(rg0, b0);
        Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref outputRef, 16)) = Sse2.Or(rg1, b1);
        Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref outputRef, 32)) = Sse2.Or(rg2, b2);
        Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref outputRef, 48)) = Sse2.Or(rg3, b3);
        Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref outputRef, 64)) = Sse2.Or(rg4, b4);
        Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref outputRef, 80)) = Sse2.Or(rg5, b5);
    }

    // Shuffles the input buffer as A0 0 0 A1 0 0 A2
    private static void ChannelMixing(
        Vector128<byte> input0,
        Vector128<byte> input1,
        Vector128<byte> shuffle0,
        Vector128<byte> shuffle1,
        Vector128<byte> shuffle2,
        out Vector128<byte> output0,
        out Vector128<byte> output1,
        out Vector128<byte> output2,
        out Vector128<byte> output3,
        out Vector128<byte> output4,
        out Vector128<byte> output5)
    {
        output0 = Ssse3.Shuffle(input0, shuffle0);
        output1 = Ssse3.Shuffle(input0, shuffle1);
        output2 = Ssse3.Shuffle(input0, shuffle2);
        output3 = Ssse3.Shuffle(input1, shuffle0);
        output4 = Ssse3.Shuffle(input1, shuffle1);
        output5 = Ssse3.Shuffle(input1, shuffle2);
    }

    // Convert 32 samples of YUV444 to B/G/R
    private static void ConvertYuv444ToBgrSse41(ref byte y, ref byte u, ref byte v, out Vector128<short> r, out Vector128<short> g, out Vector128<short> b)
    {
        // Load the bytes into the *upper* part of 16b words. That's "<< 8", basically.
        Vector128<byte> y0 = Unsafe.As<byte, Vector128<byte>>(ref y);
        Vector128<byte> u0 = Unsafe.As<byte, Vector128<byte>>(ref u);
        Vector128<byte> v0 = Unsafe.As<byte, Vector128<byte>>(ref v);
        y0 = Sse2.UnpackLow(Vector128<byte>.Zero, y0);
        u0 = Sse2.UnpackLow(Vector128<byte>.Zero, u0);
        v0 = Sse2.UnpackLow(Vector128<byte>.Zero, v0);

        // These constants are 14b fixed-point version of ITU-R BT.601 constants.
        // R = (19077 * y             + 26149 * v - 14234) >> 6
        // G = (19077 * y -  6419 * u - 13320 * v +  8708) >> 6
        // B = (19077 * y + 33050 * u             - 17685) >> 6
        var k19077 = Vector128.Create((ushort)19077);
        var k26149 = Vector128.Create((ushort)26149);
        var k14234 = Vector128.Create((ushort)14234);

        Vector128<ushort> y1 = Sse2.MultiplyHigh(y0.AsUInt16(), k19077);
        Vector128<ushort> r0 = Sse2.MultiplyHigh(v0.AsUInt16(), k26149);
        Vector128<ushort> g0 = Sse2.MultiplyHigh(u0.AsUInt16(), Vector128.Create((ushort)6419));
        Vector128<ushort> g1 = Sse2.MultiplyHigh(v0.AsUInt16(), Vector128.Create((ushort)13320));

        Vector128<ushort> r1 = Sse2.Subtract(y1.AsUInt16(), k14234);
        Vector128<ushort> r2 = Sse2.Add(r1, r0);

        Vector128<ushort> g2 = Sse2.Add(y1.AsUInt16(), Vector128.Create((ushort)8708));
        Vector128<ushort> g3 = Sse2.Add(g0, g1);
        Vector128<ushort> g4 = Sse2.Subtract(g2, g3);

        Vector128<ushort> b0 = Sse2.MultiplyHigh(u0.AsUInt16(), Vector128.Create(26, 129, 26, 129, 26, 129, 26, 129, 26, 129, 26, 129, 26, 129, 26, 129).AsUInt16());
        Vector128<ushort> b1 = Sse2.AddSaturate(b0, y1);
        Vector128<ushort> b2 = Sse2.SubtractSaturate(b1, Vector128.Create((ushort)17685));

        // Use logical shift for B2, which can be larger than 32767.
        r = Sse2.ShiftRightArithmetic(r2.AsInt16(), 6); // range: [-14234, 30815]
        g = Sse2.ShiftRightArithmetic(g4.AsInt16(), 6); // range: [-10953, 27710]
        b = Sse2.ShiftRightLogical(b2.AsInt16(), 6); // range: [0, 34238]
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public static int YuvToB(int y, int u) => Clip8(MultHi(y, 19077) + MultHi(u, 33050) - 17685);

    [MethodImpl(InliningOptions.ShortMethod)]
    public static int YuvToG(int y, int u, int v) => Clip8(MultHi(y, 19077) - MultHi(u, 6419) - MultHi(v, 13320) + 8708);

    [MethodImpl(InliningOptions.ShortMethod)]
    public static int YuvToR(int y, int v) => Clip8(MultHi(y, 19077) + MultHi(v, 26149) - 14234);

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int MultHi(int v, int coeff) => (v * coeff) >> 8;

    [MethodImpl(InliningOptions.ShortMethod)]
    private static byte Clip8(int v)
    {
        const int yuvMask = (256 << 6) - 1;
        return (byte)((v & ~yuvMask) == 0 ? v >> 6 : v < 0 ? 0 : 255);
    }
}

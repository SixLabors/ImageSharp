// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal static class YuvConversion
    {
        /// <summary>
        /// Fixed-point precision for RGB->YUV.
        /// </summary>
        private const int YuvFix = 16;

        private const int YuvHalf = 1 << (YuvFix - 1);

        /// <summary>
        /// Converts the RGB values of the image to YUV.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
        /// <param name="image">The image to convert.</param>
        /// <param name="configuration">The global configuration.</param>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="y">Span to store the luma component of the image.</param>
        /// <param name="u">Span to store the u component of the image.</param>
        /// <param name="v">Span to store the v component of the image.</param>
        public static void ConvertRgbToYuv<TPixel>(Image<TPixel> image, Configuration configuration, MemoryAllocator memoryAllocator, Span<byte> y, Span<byte> u, Span<byte> v)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Width;
            int height = image.Height;
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
            for (rowIndex = 0; rowIndex < height - 1; rowIndex += 2)
            {
                Span<TPixel> rowSpan = image.DangerousGetRowSpan(rowIndex);
                Span<TPixel> nextRowSpan = image.DangerousGetRowSpan(rowIndex + 1);
                PixelOperations<TPixel>.Instance.ToBgra32(configuration, rowSpan, bgraRow0);
                PixelOperations<TPixel>.Instance.ToBgra32(configuration, nextRowSpan, bgraRow1);

                bool rowsHaveAlpha = WebpCommonUtils.CheckNonOpaque(bgraRow0) && WebpCommonUtils.CheckNonOpaque(bgraRow1);

                // Downsample U/V planes, two rows at a time.
                if (!rowsHaveAlpha)
                {
                    AccumulateRgb(bgraRow0, bgraRow1, tmpRgbSpan, width);
                }
                else
                {
                    AccumulateRgba(bgraRow0, bgraRow1, tmpRgbSpan, width);
                }

                ConvertRgbaToUv(tmpRgbSpan, u.Slice(uvRowIndex * uvWidth), v.Slice(uvRowIndex * uvWidth), uvWidth);
                uvRowIndex++;

                ConvertRgbaToY(bgraRow0, y.Slice(rowIndex * width), width);
                ConvertRgbaToY(bgraRow1, y.Slice((rowIndex + 1) * width), width);
            }

            // Extra last row.
            if ((height & 1) != 0)
            {
                Span<TPixel> rowSpan = image.DangerousGetRowSpan(rowIndex);
                PixelOperations<TPixel>.Instance.ToBgra32(configuration, rowSpan, bgraRow0);
                ConvertRgbaToY(bgraRow0, y.Slice(rowIndex * width), width);

                if (!WebpCommonUtils.CheckNonOpaque(bgraRow0))
                {
                    AccumulateRgb(bgraRow0, bgraRow0, tmpRgbSpan, width);
                }
                else
                {
                    AccumulateRgba(bgraRow0, bgraRow0, tmpRgbSpan, width);
                }

                ConvertRgbaToUv(tmpRgbSpan, u.Slice(uvRowIndex * uvWidth), v.Slice(uvRowIndex * uvWidth), uvWidth);
            }
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
    }
}

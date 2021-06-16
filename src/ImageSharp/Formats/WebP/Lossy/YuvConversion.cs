// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
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
        /// Checks if the image is not opaque.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type of the image,</typeparam>
        /// <param name="image">The image to check.</param>
        /// <param name="rowIdxStart">The row to start with.</param>
        /// <param name="rowIdxEnd">The row to end with.</param>
        /// <returns>Returns true if alpha has non-0xff values.</returns>
        public static bool CheckNonOpaque<TPixel>(Image<TPixel> image, int rowIdxStart, int rowIdxEnd)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 rgba = default;
            for (int rowIndex = rowIdxStart; rowIndex <= rowIdxEnd; rowIndex++)
            {
                Span<TPixel> rowSpan = image.GetPixelRowSpan(rowIndex);
                for (int x = 0; x < image.Width; x++)
                {
                    TPixel color = rowSpan[x];
                    color.ToRgba32(ref rgba);
                    if (rgba.A != 255)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Converts a rgba pixel row to Y.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="rowSpan">The row span to convert.</param>
        /// <param name="y">The destination span for y.</param>
        /// <param name="width">The width.</param>
        public static void ConvertRgbaToY<TPixel>(Span<TPixel> rowSpan, Span<byte> y, int width)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 rgba = default;
            for (int x = 0; x < width; x++)
            {
                TPixel color = rowSpan[x];
                color.ToRgba32(ref rgba);
                y[x] = (byte)RgbToY(rgba.R, rgba.G, rgba.B, YuvHalf);
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

        public static void AccumulateRgb<TPixel>(Span<TPixel> rowSpan, Span<TPixel> nextRowSpan, Span<ushort> dst, int width)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 rgba0 = default;
            Rgba32 rgba1 = default;
            Rgba32 rgba2 = default;
            Rgba32 rgba3 = default;
            int i, j;
            int dstIdx = 0;
            for (i = 0, j = 0; i < (width >> 1); i += 1, j += 2, dstIdx += 4)
            {
                TPixel color = rowSpan[j];
                color.ToRgba32(ref rgba0);
                color = rowSpan[j + 1];
                color.ToRgba32(ref rgba1);
                color = nextRowSpan[j];
                color.ToRgba32(ref rgba2);
                color = nextRowSpan[j + 1];
                color.ToRgba32(ref rgba3);

                dst[dstIdx] = (ushort)LinearToGamma(
                    GammaToLinear(rgba0.R) +
                            GammaToLinear(rgba1.R) +
                            GammaToLinear(rgba2.R) +
                            GammaToLinear(rgba3.R), 0);
                dst[dstIdx + 1] = (ushort)LinearToGamma(
                    GammaToLinear(rgba0.G) +
                            GammaToLinear(rgba1.G) +
                            GammaToLinear(rgba2.G) +
                            GammaToLinear(rgba3.G), 0);
                dst[dstIdx + 2] = (ushort)LinearToGamma(
                    GammaToLinear(rgba0.B) +
                            GammaToLinear(rgba1.B) +
                            GammaToLinear(rgba2.B) +
                            GammaToLinear(rgba3.B), 0);
            }

            if ((width & 1) != 0)
            {
                TPixel color = rowSpan[j];
                color.ToRgba32(ref rgba0);
                color = nextRowSpan[j];
                color.ToRgba32(ref rgba1);

                dst[dstIdx] = (ushort)LinearToGamma(GammaToLinear(rgba0.R) + GammaToLinear(rgba1.R), 1);
                dst[dstIdx + 1] = (ushort)LinearToGamma(GammaToLinear(rgba0.G) + GammaToLinear(rgba1.G), 1);
                dst[dstIdx + 2] = (ushort)LinearToGamma(GammaToLinear(rgba0.B) + GammaToLinear(rgba1.B), 1);
            }
        }

        public static void AccumulateRgba<TPixel>(Span<TPixel> rowSpan, Span<TPixel> nextRowSpan, Span<ushort> dst, int width)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 rgba0 = default;
            Rgba32 rgba1 = default;
            Rgba32 rgba2 = default;
            Rgba32 rgba3 = default;
            int i, j;
            int dstIdx = 0;
            for (i = 0, j = 0; i < (width >> 1); i += 1, j += 2, dstIdx += 4)
            {
                TPixel color = rowSpan[j];
                color.ToRgba32(ref rgba0);
                color = rowSpan[j + 1];
                color.ToRgba32(ref rgba1);
                color = nextRowSpan[j];
                color.ToRgba32(ref rgba2);
                color = nextRowSpan[j + 1];
                color.ToRgba32(ref rgba3);
                uint a = (uint)(rgba0.A + rgba1.A + rgba2.A + rgba3.A);
                int r, g, b;
                if (a == 4 * 0xff || a == 0)
                {
                    r = (ushort)LinearToGamma(
                        GammaToLinear(rgba0.R) +
                        GammaToLinear(rgba1.R) +
                        GammaToLinear(rgba2.R) +
                        GammaToLinear(rgba3.R), 0);
                    g = (ushort)LinearToGamma(
                        GammaToLinear(rgba0.G) +
                        GammaToLinear(rgba1.G) +
                        GammaToLinear(rgba2.G) +
                        GammaToLinear(rgba3.G), 0);
                    b = (ushort)LinearToGamma(
                        GammaToLinear(rgba0.B) +
                        GammaToLinear(rgba1.B) +
                        GammaToLinear(rgba2.B) +
                        GammaToLinear(rgba3.B), 0);
                }
                else
                {
                    r = LinearToGammaWeighted(rgba0.R, rgba1.R, rgba2.R, rgba3.R, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                    g = LinearToGammaWeighted(rgba0.G, rgba1.G, rgba2.G, rgba3.G, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                    b = LinearToGammaWeighted(rgba0.B, rgba1.B, rgba2.B, rgba3.B, rgba0.A, rgba1.A, rgba2.A, rgba3.A, a);
                }

                dst[dstIdx] = (ushort)r;
                dst[dstIdx + 1] = (ushort)g;
                dst[dstIdx + 2] = (ushort)b;
                dst[dstIdx + 3] = (ushort)a;
            }

            if ((width & 1) != 0)
            {
                TPixel color = rowSpan[j];
                color.ToRgba32(ref rgba0);
                color = nextRowSpan[j];
                color.ToRgba32(ref rgba1);
                uint a = (uint)(2u * (rgba0.A + rgba1.A));
                int r, g, b;
                if (a == 4 * 0xff || a == 0)
                {
                    r = (ushort)LinearToGamma(GammaToLinear(rgba0.R) + GammaToLinear(rgba1.R), 1);
                    g = (ushort)LinearToGamma(GammaToLinear(rgba0.G) + GammaToLinear(rgba1.G), 1);
                    b = (ushort)LinearToGamma(GammaToLinear(rgba0.B) + GammaToLinear(rgba1.B), 1);
                }
                else
                {
                    r = LinearToGammaWeighted(rgba0.R, rgba1.R, rgba0.R, rgba1.R, rgba0.A, rgba1.A, rgba0.A, rgba1.A, a);
                    g = LinearToGammaWeighted(rgba0.G, rgba1.G, rgba0.G, rgba1.G, rgba0.A, rgba1.A, rgba0.A, rgba1.A, a);
                    b = LinearToGammaWeighted(rgba0.B, rgba1.B, rgba0.B, rgba1.B, rgba0.A, rgba1.A, rgba0.A, rgba1.A, a);
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
            return ((uv & ~0xff) == 0) ? uv : (uv < 0) ? 0 : 255;
        }
    }
}

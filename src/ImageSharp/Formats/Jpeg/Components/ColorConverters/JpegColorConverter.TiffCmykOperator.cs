// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Implements non-inverted TIFF JPEG CMYK conversion for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct TiffCmykOperator : IJpegColorConverterOperator
    {
        /// <inheritdoc/>
        public static JpegColorSpace ColorSpace => JpegColorSpace.TiffCmyk;

        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float c0, ref float c1, ref float c2, float c3, float maximumValue, float halfValue, float scale)
        {
            // TIFF stores conventional CMYK rather than Adobe's inverted representation. Normalize every
            // component, invert C/M/Y, and let the remaining light after K modulate each RGB channel.
            float k = 1F - (c3 * scale);
            c0 = (1F - (c0 * scale)) * k;
            c1 = (1F - (c1 * scale)) * k;
            c2 = (1F - (c2 * scale)) * k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, Vector128<float> c3, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale)
        {
            // K remains lane-aligned with its C/M/Y sample while one-minus performs the non-inverted CMYK mapping.
            Vector128<float> k = Vector128<float>.One - (c3 * scale);
            c0 = (Vector128<float>.One - (c0 * scale)) * k;
            c1 = (Vector128<float>.One - (c1 * scale)) * k;
            c2 = (Vector128<float>.One - (c2 * scale)) * k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, Vector256<float> c3, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale)
        {
            // Eight conventional CMYK samples convert independently without channel rearrangement.
            Vector256<float> k = Vector256<float>.One - (c3 * scale);
            c0 = (Vector256<float>.One - (c0 * scale)) * k;
            c1 = (Vector256<float>.One - (c1 * scale)) * k;
            c2 = (Vector256<float>.One - (c2 * scale)) * k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, Vector512<float> c3, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale)
        {
            // Sixteen conventional CMYK samples convert independently without channel rearrangement.
            Vector512<float> k = Vector512<float>.One - (c3 * scale);
            c0 = (Vector512<float>.One - (c0 * scale)) * k;
            c1 = (Vector512<float>.One - (c1 * scale)) * k;
            c2 = (Vector512<float>.One - (c2 * scale)) * k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(float r, float g, float b, float maximumValue, float halfValue, float scale, out float c0, out float c1, out float c2, out float c3)
        {
            float c = maximumValue - r;
            float m = maximumValue - g;
            float y = maximumValue - b;
            float k = MathF.Min(c, MathF.Min(m, y));

            // Removing the shared black contribution requires division by the remaining range. Pure black
            // consumes that range completely, so its chromatic components are defined as zero.
            if (k >= maximumValue)
            {
                c = 0;
                m = 0;
                y = 0;
            }
            else
            {
                // One reciprocal normalizes C, M, and Y against their shared remaining range.
                float reciprocal = 1F / (maximumValue - k);
                c = (c - k) * reciprocal;
                m = (m - k) * reciprocal;
                y = (y - k) * reciprocal;
            }

            // TIFF stores conventional CMYK: scale normalized C/M/Y back into the sample domain and retain K.
            c0 = c * maximumValue;
            c1 = m * maximumValue;
            c2 = y * maximumValue;
            c3 = k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector128<float> r, Vector128<float> g, Vector128<float> b, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
        {
            Vector128<float> c = maximumValue - r;
            Vector128<float> m = maximumValue - g;
            Vector128<float> y = maximumValue - b;
            Vector128<float> k = Vector128.Min(c, Vector128.Min(m, y));

            // The all-bits mask clears the undefined zero-divisor result only in pure-black lanes.
            Vector128<float> nonBlack = ~Vector128.Equals(k, maximumValue);
            Vector128<float> reciprocal = Vector128<float>.One / (maximumValue - k);
            c0 = (((c - k) * reciprocal) & nonBlack) * maximumValue;
            c1 = (((m - k) * reciprocal) & nonBlack) * maximumValue;
            c2 = (((y - k) * reciprocal) & nonBlack) * maximumValue;
            c3 = k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector256<float> r, Vector256<float> g, Vector256<float> b, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
        {
            Vector256<float> c = maximumValue - r;
            Vector256<float> m = maximumValue - g;
            Vector256<float> y = maximumValue - b;
            Vector256<float> k = Vector256.Min(c, Vector256.Min(m, y));

            // Eight lanes independently clear the pure-black singularity before returning conventional CMYK.
            Vector256<float> nonBlack = ~Vector256.Equals(k, maximumValue);
            Vector256<float> reciprocal = Vector256<float>.One / (maximumValue - k);
            c0 = (((c - k) * reciprocal) & nonBlack) * maximumValue;
            c1 = (((m - k) * reciprocal) & nonBlack) * maximumValue;
            c2 = (((y - k) * reciprocal) & nonBlack) * maximumValue;
            c3 = k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector512<float> r, Vector512<float> g, Vector512<float> b, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
        {
            Vector512<float> c = maximumValue - r;
            Vector512<float> m = maximumValue - g;
            Vector512<float> y = maximumValue - b;
            Vector512<float> k = Vector512.Min(c, Vector512.Min(m, y));

            // Sixteen lanes retain the same branchless singularity handling and component layout.
            Vector512<float> nonBlack = ~Vector512.Equals(k, maximumValue);
            Vector512<float> reciprocal = Vector512<float>.One / (maximumValue - k);
            c0 = (((c - k) * reciprocal) & nonBlack) * maximumValue;
            c1 = (((m - k) * reciprocal) & nonBlack) * maximumValue;
            c2 = (((y - k) * reciprocal) & nonBlack) * maximumValue;
            c3 = k;
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Implements non-inverted TIFF JPEG YccK conversion for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct TiffYccKOperator : IJpegColorConverterOperator
    {
        private const float SourceScale = 1F / 255F;

        /// <inheritdoc/>
        public static JpegColorSpace ColorSpace => JpegColorSpace.TiffYccK;

        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float c0, ref float c1, ref float c2, float c3, float maximumValue, float halfValue, float scale)
        {
            float y = c0 * scale;
            float cb = (c1 - halfValue) * scale;
            float cr = (c2 - halfValue) * scale;
            float k = 1F - (c3 * scale);

            // TIFF YccK is non-inverted: decode normalized YCbCr without integer rounding, then let the
            // remaining light after K modulate all three channels.
            c0 = (y + (YCbCrOperator.RCrMult * cr)) * k;
            c1 = (y - (YCbCrOperator.GCbMult * cb) - (YCbCrOperator.GCrMult * cr)) * k;
            c2 = (y + (YCbCrOperator.BCbMult * cb)) * k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, Vector128<float> c3, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale)
        {
            Vector128<float> y = c0 * scale;
            Vector128<float> cb = (c1 - halfValue) * scale;
            Vector128<float> cr = (c2 - halfValue) * scale;
            Vector128<float> k = Vector128<float>.One - (c3 * scale);

            // Four lanes apply the non-rounded YCbCr matrix before their lane-aligned K modulation.
            c0 = Vector128.MultiplyAddEstimate(cr, Vector128.Create(YCbCrOperator.RCrMult), y) * k;
            c1 = Vector128.MultiplyAddEstimate(cr, Vector128.Create(-YCbCrOperator.GCrMult), Vector128.MultiplyAddEstimate(cb, Vector128.Create(-YCbCrOperator.GCbMult), y)) * k;
            c2 = Vector128.MultiplyAddEstimate(cb, Vector128.Create(YCbCrOperator.BCbMult), y) * k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, Vector256<float> c3, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale)
        {
            Vector256<float> y = c0 * scale;
            Vector256<float> cb = (c1 - halfValue) * scale;
            Vector256<float> cr = (c2 - halfValue) * scale;
            Vector256<float> k = Vector256<float>.One - (c3 * scale);

            // Eight lanes apply the non-rounded YCbCr matrix before their lane-aligned K modulation.
            c0 = Vector256.MultiplyAddEstimate(cr, Vector256.Create(YCbCrOperator.RCrMult), y) * k;
            c1 = Vector256.MultiplyAddEstimate(cr, Vector256.Create(-YCbCrOperator.GCrMult), Vector256.MultiplyAddEstimate(cb, Vector256.Create(-YCbCrOperator.GCbMult), y)) * k;
            c2 = Vector256.MultiplyAddEstimate(cb, Vector256.Create(YCbCrOperator.BCbMult), y) * k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, Vector512<float> c3, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale)
        {
            Vector512<float> y = c0 * scale;
            Vector512<float> cb = (c1 - halfValue) * scale;
            Vector512<float> cr = (c2 - halfValue) * scale;
            Vector512<float> k = Vector512<float>.One - (c3 * scale);

            // Sixteen lanes apply the non-rounded YCbCr matrix before their lane-aligned K modulation.
            c0 = Vector512.MultiplyAddEstimate(cr, Vector512.Create(YCbCrOperator.RCrMult), y) * k;
            c1 = Vector512.MultiplyAddEstimate(cr, Vector512.Create(-YCbCrOperator.GCrMult), Vector512.MultiplyAddEstimate(cb, Vector512.Create(-YCbCrOperator.GCbMult), y)) * k;
            c2 = Vector512.MultiplyAddEstimate(cb, Vector512.Create(YCbCrOperator.BCbMult), y) * k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(float r, float g, float b, float maximumValue, float halfValue, float scale, out float c0, out float c1, out float c2, out float c3)
        {
            r *= SourceScale;
            g *= SourceScale;
            b *= SourceScale;
            float k = 1F - MathF.Max(r, MathF.Max(g, b));

            // Dividing by the brightest channel removes K before YCbCr projection. Pure black has no
            // chromatic direction, so it maps to zero luma and the neutral chroma midpoint.
            if (k >= 1F)
            {
                c0 = 0;
                c1 = halfValue;
                c2 = halfValue;
                c3 = maximumValue;
                return;
            }

            float divisor = 1F / (1F - k);
            r *= divisor;
            g *= divisor;
            b *= divisor;
            c0 = ((0.299F * r) + (0.587F * g) + (0.114F * b)) * maximumValue;
            c1 = halfValue + (((-0.168736F * r) + (-0.331264F * g) + (0.5F * b)) * maximumValue);
            c2 = halfValue + (((0.5F * r) + (-0.418688F * g) + (-0.081312F * b)) * maximumValue);
            c3 = k * maximumValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector128<float> r, Vector128<float> g, Vector128<float> b, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
        {
            Vector128<float> sourceScale = Vector128.Create(SourceScale);
            r *= sourceScale;
            g *= sourceScale;
            b *= sourceScale;
            Vector128<float> k = Vector128<float>.One - Vector128.Max(r, Vector128.Max(g, b));

            // The mask assigns no chromatic direction to pure-black lanes while preserving neighboring pixels.
            Vector128<float> nonBlack = ~Vector128.Equals(k, Vector128<float>.One);
            Vector128<float> divisor = Vector128<float>.One / (Vector128<float>.One - k);
            r = (r * divisor) & nonBlack;
            g = (g * divisor) & nonBlack;
            b = (b * divisor) & nonBlack;
            c0 = Vector128.MultiplyAddEstimate(Vector128.Create(0.299F), r, Vector128.MultiplyAddEstimate(Vector128.Create(0.587F), g, Vector128.Create(0.114F) * b)) * maximumValue;
            c1 = halfValue + (Vector128.MultiplyAddEstimate(Vector128.Create(-0.168736F), r, Vector128.MultiplyAddEstimate(Vector128.Create(-0.331264F), g, Vector128.Create(0.5F) * b)) * maximumValue);
            c2 = halfValue + (Vector128.MultiplyAddEstimate(Vector128.Create(0.5F), r, Vector128.MultiplyAddEstimate(Vector128.Create(-0.418688F), g, Vector128.Create(-0.081312F) * b)) * maximumValue);
            c3 = k * maximumValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector256<float> r, Vector256<float> g, Vector256<float> b, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
        {
            Vector256<float> sourceScale = Vector256.Create(SourceScale);
            r *= sourceScale;
            g *= sourceScale;
            b *= sourceScale;
            Vector256<float> k = Vector256<float>.One - Vector256.Max(r, Vector256.Max(g, b));

            // Eight lanes normalize chromatic direction independently and retain neutral chroma for black.
            Vector256<float> nonBlack = ~Vector256.Equals(k, Vector256<float>.One);
            Vector256<float> divisor = Vector256<float>.One / (Vector256<float>.One - k);
            r = (r * divisor) & nonBlack;
            g = (g * divisor) & nonBlack;
            b = (b * divisor) & nonBlack;
            c0 = Vector256.MultiplyAddEstimate(Vector256.Create(0.299F), r, Vector256.MultiplyAddEstimate(Vector256.Create(0.587F), g, Vector256.Create(0.114F) * b)) * maximumValue;
            c1 = halfValue + (Vector256.MultiplyAddEstimate(Vector256.Create(-0.168736F), r, Vector256.MultiplyAddEstimate(Vector256.Create(-0.331264F), g, Vector256.Create(0.5F) * b)) * maximumValue);
            c2 = halfValue + (Vector256.MultiplyAddEstimate(Vector256.Create(0.5F), r, Vector256.MultiplyAddEstimate(Vector256.Create(-0.418688F), g, Vector256.Create(-0.081312F) * b)) * maximumValue);
            c3 = k * maximumValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector512<float> r, Vector512<float> g, Vector512<float> b, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
        {
            Vector512<float> sourceScale = Vector512.Create(SourceScale);
            r *= sourceScale;
            g *= sourceScale;
            b *= sourceScale;
            Vector512<float> k = Vector512<float>.One - Vector512.Max(r, Vector512.Max(g, b));

            // Sixteen lanes normalize chromatic direction independently and retain neutral chroma for black.
            Vector512<float> nonBlack = ~Vector512.Equals(k, Vector512<float>.One);
            Vector512<float> divisor = Vector512<float>.One / (Vector512<float>.One - k);
            r = (r * divisor) & nonBlack;
            g = (g * divisor) & nonBlack;
            b = (b * divisor) & nonBlack;
            c0 = Vector512.MultiplyAddEstimate(Vector512.Create(0.299F), r, Vector512.MultiplyAddEstimate(Vector512.Create(0.587F), g, Vector512.Create(0.114F) * b)) * maximumValue;
            c1 = halfValue + (Vector512.MultiplyAddEstimate(Vector512.Create(-0.168736F), r, Vector512.MultiplyAddEstimate(Vector512.Create(-0.331264F), g, Vector512.Create(0.5F) * b)) * maximumValue);
            c2 = halfValue + (Vector512.MultiplyAddEstimate(Vector512.Create(0.5F), r, Vector512.MultiplyAddEstimate(Vector512.Create(-0.418688F), g, Vector512.Create(-0.081312F) * b)) * maximumValue);
            c3 = k * maximumValue;
        }
    }
}

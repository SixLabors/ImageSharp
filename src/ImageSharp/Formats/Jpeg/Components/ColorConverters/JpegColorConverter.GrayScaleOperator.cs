// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Implements grayscale expansion and RGB luminance reduction for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct GrayScaleOperator : IJpegColorConverterOperator
    {
        /// <inheritdoc/>
        public static JpegColorSpace ColorSpace => JpegColorSpace.Grayscale;

        /// <inheritdoc/>
        public static int ComponentCount => 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float c0, ref float c1, ref float c2, float c3, float maximumValue, float halfValue, float scale)
        {
            // JPEG stores luminance in the integer sample domain. Normalize it once, then duplicate the
            // same value into all three RGB planes. Keeping it local also prevents potentially aliasing
            // byref stores from forcing the JIT to reload c0 between assignments.
            float luminance = c0 * scale;
            c0 = luminance;
            c1 = luminance;
            c2 = luminance;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, Vector128<float> c3, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale)
        {
            // Each XMM lane is one independent luminance sample. Reusing the normalized vector for R, G,
            // and B avoids recomputing the scale and keeps it live across potentially aliasing byref stores.
            Vector128<float> luminance = c0 * scale;
            c0 = luminance;
            c1 = luminance;
            c2 = luminance;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, Vector256<float> c3, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale)
        {
            // Eight luminance samples occupy the YMM lanes. The local retains the normalized vector across
            // all three output stores even when the destination planes alias.
            Vector256<float> luminance = c0 * scale;
            c0 = luminance;
            c1 = luminance;
            c2 = luminance;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, Vector512<float> c3, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale)
        {
            // Sixteen luminance samples occupy the ZMM lanes. The local retains the normalized vector across
            // all three output stores without shuffles, interleaving, or source reloads.
            Vector512<float> luminance = c0 * scale;
            c0 = luminance;
            c1 = luminance;
            c2 = luminance;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(float r, float g, float b, float maximumValue, float halfValue, float scale, out float c0, out float c1, out float c2, out float c3)
        {
            // Rec.601 luma weights operate directly in the encoder sample domain. Only c0 is stored for a
            // one-component model; the remaining out values exist solely to satisfy the common operator shape.
            c0 = (0.299F * r) + (0.587F * g) + (0.114F * b);
            c1 = 0;
            c2 = 0;
            c3 = 0;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector128<float> r, Vector128<float> g, Vector128<float> b, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
        {
            // The nested estimate gives each pixel the same multiply-add grouping as the scalar Rec.601 formula.
            c0 = Vector128.MultiplyAddEstimate(Vector128.Create(0.299F), r, Vector128.MultiplyAddEstimate(Vector128.Create(0.587F), g, Vector128.Create(0.114F) * b));
            c1 = default;
            c2 = default;
            c3 = default;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector256<float> r, Vector256<float> g, Vector256<float> b, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
        {
            // YMM lanes evaluate the same Rec.601 equation independently, with no horizontal lane reduction.
            c0 = Vector256.MultiplyAddEstimate(Vector256.Create(0.299F), r, Vector256.MultiplyAddEstimate(Vector256.Create(0.587F), g, Vector256.Create(0.114F) * b));
            c1 = default;
            c2 = default;
            c3 = default;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector512<float> r, Vector512<float> g, Vector512<float> b, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
        {
            // ZMM lanes retain the same arithmetic order as narrower paths so only SIMD width changes.
            c0 = Vector512.MultiplyAddEstimate(Vector512.Create(0.299F), r, Vector512.MultiplyAddEstimate(Vector512.Create(0.587F), g, Vector512.Create(0.114F) * b));
            c1 = default;
            c2 = default;
            c3 = default;
        }
    }
}

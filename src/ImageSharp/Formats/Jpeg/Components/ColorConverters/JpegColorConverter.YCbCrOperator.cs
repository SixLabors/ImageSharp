// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Implements the JPEG YCbCr conversion formula for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct YCbCrOperator : IJpegColorConverterOperator
    {
        /// <summary>
        /// The BT.601 red contribution from centered Cr.
        /// </summary>
        public const float RCrMult = 1.402F;

        /// <summary>
        /// The BT.601 green contribution from centered Cb.
        /// </summary>
        public const float GCbMult = (float)(0.114 * 1.772 / 0.587);

        /// <summary>
        /// The BT.601 green contribution from centered Cr.
        /// </summary>
        public const float GCrMult = (float)(0.299 * 1.402 / 0.587);

        /// <summary>
        /// The BT.601 blue contribution from centered Cb.
        /// </summary>
        public const float BCbMult = 1.772F;

        /// <inheritdoc/>
        public static JpegColorSpace ColorSpace => JpegColorSpace.YCbCr;

        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float c0, ref float c1, ref float c2, float c3, float maximumValue, float halfValue, float scale)
        {
            float y = c0;
            float cb = c1 - halfValue;
            float cr = c2 - halfValue;

            // c0/c1/c2 initially mean Y/Cb/Cr. Chroma is centered around zero before applying
            // the BT.601 matrix, then integer-domain RGB is rounded away from zero and normalized
            // to [nominally] 0..1. Values intentionally remain unclamped because quantizing RGB into the
            // destination pixel format owns saturation; retaining overshoot avoids discarding color information.
            c0 = MathF.Round(y + (RCrMult * cr), MidpointRounding.AwayFromZero) * scale;
            c1 = MathF.Round(y - (GCbMult * cb) - (GCrMult * cr), MidpointRounding.AwayFromZero) * scale;
            c2 = MathF.Round(y + (BCbMult * cb), MidpointRounding.AwayFromZero) * scale;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, Vector128<float> c3, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale)
        {
            Vector128<float> y = c0;
            Vector128<float> cb = c1 - halfValue;
            Vector128<float> cr = c2 - halfValue;

            // Lanes are four independent Y/Cb/Cr samples. MultiplyAddEstimate maps to FMA where available:
            // R uses Cr, B uses Cb, and G subtracts both chroma contributions. Rounding occurs in the sample
            // domain before the common normalization scale so all precisions use integer JPEG sample semantics.
            Vector128<float> r = Vector128.MultiplyAddEstimate(cr, Vector128.Create(RCrMult), y);
            Vector128<float> g = Vector128.MultiplyAddEstimate(cr, Vector128.Create(-GCrMult), Vector128.MultiplyAddEstimate(cb, Vector128.Create(-GCbMult), y));
            Vector128<float> b = Vector128.MultiplyAddEstimate(cb, Vector128.Create(BCbMult), y);

            c0 = Vector128.Round(r) * scale;
            c1 = Vector128.Round(g) * scale;
            c2 = Vector128.Round(b) * scale;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, Vector256<float> c3, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale)
        {
            Vector256<float> y = c0;
            Vector256<float> cb = c1 - halfValue;
            Vector256<float> cr = c2 - halfValue;

            // These eight lanes have the same layout and BT.601 arithmetic as the Vector128 overload.
            // Keeping an explicit overload allows the JIT to emit native YMM operations without a width
            // switch or decomposing the vector into smaller values.
            Vector256<float> r = Vector256.MultiplyAddEstimate(cr, Vector256.Create(RCrMult), y);
            Vector256<float> g = Vector256.MultiplyAddEstimate(cr, Vector256.Create(-GCrMult), Vector256.MultiplyAddEstimate(cb, Vector256.Create(-GCbMult), y));
            Vector256<float> b = Vector256.MultiplyAddEstimate(cb, Vector256.Create(BCbMult), y);

            c0 = Vector256.Round(r) * scale;
            c1 = Vector256.Round(g) * scale;
            c2 = Vector256.Round(b) * scale;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, Vector512<float> c3, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale)
        {
            Vector512<float> y = c0;
            Vector512<float> cb = c1 - halfValue;
            Vector512<float> cr = c2 - halfValue;

            // Sixteen independent samples occupy the ZMM lanes. The explicit constants are broadcasts;
            // assembly inspection verifies the JIT hoists them from the loop and retains fused operations.
            // The formula and rounding order remain identical to the narrower overloads.
            Vector512<float> r = Vector512.MultiplyAddEstimate(cr, Vector512.Create(RCrMult), y);
            Vector512<float> g = Vector512.MultiplyAddEstimate(cr, Vector512.Create(-GCrMult), Vector512.MultiplyAddEstimate(cb, Vector512.Create(-GCbMult), y));
            Vector512<float> b = Vector512.MultiplyAddEstimate(cb, Vector512.Create(BCbMult), y);

            c0 = Vector512.Round(r) * scale;
            c1 = Vector512.Round(g) * scale;
            c2 = Vector512.Round(b) * scale;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(float r, float g, float b, float maximumValue, float halfValue, float scale, out float c0, out float c1, out float c2, out float c3)
        {
            // The RGB inputs are unnormalized 0..255 encoder lanes. The BT.601 luma weights form Y,
            // while the signed chroma projections are biased by halfValue into the JPEG sample domain.
            // YCbCr has no fourth component, so c3 is a compile-time-unused placeholder for the shared loop.
            c0 = (0.299F * r) + (0.587F * g) + (0.114F * b);
            c1 = halfValue - (0.168736F * r) - (0.331264F * g) + (0.5F * b);
            c2 = halfValue + (0.5F * r) - (0.418688F * g) - (0.081312F * b);
            c3 = 0;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector128<float> r, Vector128<float> g, Vector128<float> b, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
        {
            // Each vector holds four consecutive values from one RGB plane. The nested multiply-add sequence
            // produces four Y lanes, four Cb lanes, and four Cr lanes without transposition. The association
            // exposes two FMA opportunities per output while preserving the scalar formula's term grouping.
            c0 = Vector128.MultiplyAddEstimate(Vector128.Create(0.299F), r, Vector128.MultiplyAddEstimate(Vector128.Create(0.587F), g, Vector128.Create(0.114F) * b));
            c1 = halfValue + Vector128.MultiplyAddEstimate(Vector128.Create(-0.168736F), r, Vector128.MultiplyAddEstimate(Vector128.Create(-0.331264F), g, Vector128.Create(0.5F) * b));
            c2 = halfValue + Vector128.MultiplyAddEstimate(Vector128.Create(0.5F), r, Vector128.MultiplyAddEstimate(Vector128.Create(-0.418688F), g, Vector128.Create(-0.081312F) * b));
            c3 = default;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector256<float> r, Vector256<float> g, Vector256<float> b, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
        {
            // Eight planar RGB samples use the identical association as Vector128, allowing direct YMM FMA
            // generation while preserving the component-per-vector output layout.
            c0 = Vector256.MultiplyAddEstimate(Vector256.Create(0.299F), r, Vector256.MultiplyAddEstimate(Vector256.Create(0.587F), g, Vector256.Create(0.114F) * b));
            c1 = halfValue + Vector256.MultiplyAddEstimate(Vector256.Create(-0.168736F), r, Vector256.MultiplyAddEstimate(Vector256.Create(-0.331264F), g, Vector256.Create(0.5F) * b));
            c2 = halfValue + Vector256.MultiplyAddEstimate(Vector256.Create(0.5F), r, Vector256.MultiplyAddEstimate(Vector256.Create(-0.418688F), g, Vector256.Create(-0.081312F) * b));
            c3 = default;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector512<float> r, Vector512<float> g, Vector512<float> b, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
        {
            // Sixteen planar RGB samples use the same nested form. Constants are lane broadcasts and c3 is
            // deliberately zero because the shared traversal removes the unused fourth store for this operator.
            c0 = Vector512.MultiplyAddEstimate(Vector512.Create(0.299F), r, Vector512.MultiplyAddEstimate(Vector512.Create(0.587F), g, Vector512.Create(0.114F) * b));
            c1 = halfValue + Vector512.MultiplyAddEstimate(Vector512.Create(-0.168736F), r, Vector512.MultiplyAddEstimate(Vector512.Create(-0.331264F), g, Vector512.Create(0.5F) * b));
            c2 = halfValue + Vector512.MultiplyAddEstimate(Vector512.Create(0.5F), r, Vector512.MultiplyAddEstimate(Vector512.Create(-0.418688F), g, Vector512.Create(-0.081312F) * b));
            c3 = default;
        }
    }
}

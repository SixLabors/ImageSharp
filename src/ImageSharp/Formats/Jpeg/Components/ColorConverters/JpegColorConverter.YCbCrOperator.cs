// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

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
        public static float GetProfileChromaOffset(float maximumValue, float halfValue) => halfValue / maximumValue;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(
            ref float c0,
            ref float c1,
            ref float c2,
            ref float c3,
            float maximumValue,
            float halfValue,
            float scale,
            IccColorSpaceType profileSpace)
        {
            // An RGB profile reads RGB after the JPEG matrix. A native YCbCr profile reads the stored components.
            // Center chroma on the integer JPEG midpoint before normalization: 128/255 is not the model's 0.5.
            if (profileSpace == IccColorSpaceType.YCbCr)
            {
                c0 *= scale;
                c1 *= scale;
                c2 *= scale;
            }
            else
            {
                ConvertToRgb(ref c0, ref c1, ref c2, halfValue);

                // The matrix can overshoot the integer JPEG device range. Bound RGB before ICC curves,
                // but retain its fractional precision until the destination pixel is packed.
                c0 = ColorNumerics.ClampToFinite(c0 * scale, 0F, 1F);
                c1 = ColorNumerics.ClampToFinite(c1 * scale, 0F, 1F);
                c2 = ColorNumerics.ClampToFinite(c2 * scale, 0F, 1F);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(
            ref Vector128<float> c0,
            ref Vector128<float> c1,
            ref Vector128<float> c2,
            ref Vector128<float> c3,
            Vector128<float> maximumValue,
            Vector128<float> halfValue,
            Vector128<float> scale,
            IccColorSpaceType profileSpace)
        {
            // An RGB profile reads RGB after the JPEG matrix. A native YCbCr profile reads the stored components.
            // Center chroma on the integer JPEG midpoint before normalization: 128/255 is not the model's 0.5.
            if (profileSpace == IccColorSpaceType.YCbCr)
            {
                c0 *= scale;
                c1 *= scale;
                c2 *= scale;
            }
            else
            {
                ConvertToRgb(ref c0, ref c1, ref c2, halfValue);

                // The matrix can overshoot the integer JPEG device range. Bound RGB before ICC curves,
                // but retain its fractional precision until the destination pixel is packed.
                c0 = ColorNumerics.ClampToFinite(c0 * scale, Vector128<float>.Zero, Vector128<float>.One);
                c1 = ColorNumerics.ClampToFinite(c1 * scale, Vector128<float>.Zero, Vector128<float>.One);
                c2 = ColorNumerics.ClampToFinite(c2 * scale, Vector128<float>.Zero, Vector128<float>.One);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(
            ref Vector256<float> c0,
            ref Vector256<float> c1,
            ref Vector256<float> c2,
            ref Vector256<float> c3,
            Vector256<float> maximumValue,
            Vector256<float> halfValue,
            Vector256<float> scale,
            IccColorSpaceType profileSpace)
        {
            // An RGB profile reads RGB after the JPEG matrix. A native YCbCr profile reads the stored components.
            // Center chroma on the integer JPEG midpoint before normalization: 128/255 is not the model's 0.5.
            if (profileSpace == IccColorSpaceType.YCbCr)
            {
                c0 *= scale;
                c1 *= scale;
                c2 *= scale;
            }
            else
            {
                ConvertToRgb(ref c0, ref c1, ref c2, halfValue);

                // The matrix can overshoot the integer JPEG device range. Bound RGB before ICC curves,
                // but retain its fractional precision until the destination pixel is packed.
                c0 = ColorNumerics.ClampToFinite(c0 * scale, Vector256<float>.Zero, Vector256<float>.One);
                c1 = ColorNumerics.ClampToFinite(c1 * scale, Vector256<float>.Zero, Vector256<float>.One);
                c2 = ColorNumerics.ClampToFinite(c2 * scale, Vector256<float>.Zero, Vector256<float>.One);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(
            ref Vector512<float> c0,
            ref Vector512<float> c1,
            ref Vector512<float> c2,
            ref Vector512<float> c3,
            Vector512<float> maximumValue,
            Vector512<float> halfValue,
            Vector512<float> scale,
            IccColorSpaceType profileSpace)
        {
            // An RGB profile reads RGB after the JPEG matrix. A native YCbCr profile reads the stored components.
            // Center chroma on the integer JPEG midpoint before normalization: 128/255 is not the model's 0.5.
            if (profileSpace == IccColorSpaceType.YCbCr)
            {
                c0 *= scale;
                c1 *= scale;
                c2 *= scale;
            }
            else
            {
                ConvertToRgb(ref c0, ref c1, ref c2, halfValue);

                // The matrix can overshoot the integer JPEG device range. Bound RGB before ICC curves,
                // but retain its fractional precision until the destination pixel is packed.
                c0 = ColorNumerics.ClampToFinite(c0 * scale, Vector512<float>.Zero, Vector512<float>.One);
                c1 = ColorNumerics.ClampToFinite(c1 * scale, Vector512<float>.Zero, Vector512<float>.One);
                c2 = ColorNumerics.ClampToFinite(c2 * scale, Vector512<float>.Zero, Vector512<float>.One);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromProfile(
            ref float c0,
            ref float c1,
            ref float c2,
            ref float c3,
            float maximumValue,
            float halfValue,
            float scale,
            IccColorSpaceType profileSpace)
        {
            // Reverse the decoder's model transform only for an RGB profile. The caller supplies
            // the normalized integer midpoint; native YCbCr profile samples need only JPEG scaling.
            if (profileSpace != IccColorSpaceType.YCbCr)
            {
                ConvertFromRgb(c0, c1, c2, 1F, halfValue, 1F, out c0, out c1, out c2, out _);
            }

            c0 *= maximumValue;
            c1 *= maximumValue;
            c2 *= maximumValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromProfile(
            ref Vector128<float> c0,
            ref Vector128<float> c1,
            ref Vector128<float> c2,
            ref Vector128<float> c3,
            Vector128<float> maximumValue,
            Vector128<float> halfValue,
            Vector128<float> scale,
            IccColorSpaceType profileSpace)
        {
            // Reverse the decoder's model transform only for an RGB profile. The caller supplies
            // the normalized integer midpoint; native YCbCr profile samples need only JPEG scaling.
            if (profileSpace != IccColorSpaceType.YCbCr)
            {
                ConvertFromRgb(c0, c1, c2, Vector128<float>.One, halfValue, Vector128<float>.One, out c0, out c1, out c2, out _);
            }

            c0 *= maximumValue;
            c1 *= maximumValue;
            c2 *= maximumValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromProfile(
            ref Vector256<float> c0,
            ref Vector256<float> c1,
            ref Vector256<float> c2,
            ref Vector256<float> c3,
            Vector256<float> maximumValue,
            Vector256<float> halfValue,
            Vector256<float> scale,
            IccColorSpaceType profileSpace)
        {
            // Reverse the decoder's model transform only for an RGB profile. The caller supplies
            // the normalized integer midpoint; native YCbCr profile samples need only JPEG scaling.
            if (profileSpace != IccColorSpaceType.YCbCr)
            {
                ConvertFromRgb(c0, c1, c2, Vector256<float>.One, halfValue, Vector256<float>.One, out c0, out c1, out c2, out _);
            }

            c0 *= maximumValue;
            c1 *= maximumValue;
            c2 *= maximumValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromProfile(
            ref Vector512<float> c0,
            ref Vector512<float> c1,
            ref Vector512<float> c2,
            ref Vector512<float> c3,
            Vector512<float> maximumValue,
            Vector512<float> halfValue,
            Vector512<float> scale,
            IccColorSpaceType profileSpace)
        {
            // Reverse the decoder's model transform only for an RGB profile. The caller supplies
            // the normalized integer midpoint; native YCbCr profile samples need only JPEG scaling.
            if (profileSpace != IccColorSpaceType.YCbCr)
            {
                ConvertFromRgb(c0, c1, c2, Vector512<float>.One, halfValue, Vector512<float>.One, out c0, out c1, out c2, out _);
            }

            c0 *= maximumValue;
            c1 *= maximumValue;
            c2 *= maximumValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float c0, ref float c1, ref float c2, float c3, float maximumValue, float halfValue, float scale)
        {
            ConvertToRgb(ref c0, ref c1, ref c2, halfValue);

            // Ordinary JPEG conversion rounds in the sample domain before normalizing.
            c0 = MathF.Round(c0, MidpointRounding.AwayFromZero) * scale;
            c1 = MathF.Round(c1, MidpointRounding.AwayFromZero) * scale;
            c2 = MathF.Round(c2, MidpointRounding.AwayFromZero) * scale;
        }

        /// <summary>
        /// Applies the JPEG YCbCr matrix without rounding or normalizing its component values.
        /// </summary>
        /// <param name="c0">The Y samples, replaced by the first decoded channel.</param>
        /// <param name="c1">The Cb samples, replaced by the second decoded channel.</param>
        /// <param name="c2">The Cr samples, replaced by the third decoded channel.</param>
        /// <param name="halfValue">The JPEG chroma midpoint.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float c0, ref float c1, ref float c2, float halfValue)
        {
            float y = c0;
            float cb = c1 - halfValue;
            float cr = c2 - halfValue;

            // Each lane is an independent sample. ICC needs the fractional device values, while
            // the ordinary JPEG wrapper above applies its existing integer rounding afterward.
            c0 = y + (RCrMult * cr);
            c1 = y - (GCbMult * cb) - (GCrMult * cr);
            c2 = y + (BCbMult * cb);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector128<float> c0,
            ref Vector128<float> c1,
            ref Vector128<float> c2,
            Vector128<float> c3,
            Vector128<float> maximumValue,
            Vector128<float> halfValue,
            Vector128<float> scale)
        {
            ConvertToRgb(ref c0, ref c1, ref c2, halfValue);

            // Ordinary JPEG conversion rounds in the sample domain before normalizing.
            c0 = Vector128.Round(c0) * scale;
            c1 = Vector128.Round(c1) * scale;
            c2 = Vector128.Round(c2) * scale;
        }

        /// <summary>
        /// Applies the JPEG YCbCr matrix without rounding or normalizing its component values.
        /// </summary>
        /// <param name="c0">The Y samples, replaced by the first decoded channel.</param>
        /// <param name="c1">The Cb samples, replaced by the second decoded channel.</param>
        /// <param name="c2">The Cr samples, replaced by the third decoded channel.</param>
        /// <param name="halfValue">The JPEG chroma midpoint.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, Vector128<float> halfValue)
        {
            Vector128<float> y = c0;
            Vector128<float> cb = c1 - halfValue;
            Vector128<float> cr = c2 - halfValue;

            // Each lane is an independent sample. ICC needs the fractional device values, while
            // the ordinary JPEG wrapper above applies its existing integer rounding afterward.
            c0 = Vector128.MultiplyAddEstimate(cr, Vector128.Create(RCrMult), y);
            c1 = Vector128.MultiplyAddEstimate(cr, Vector128.Create(-GCrMult), Vector128.MultiplyAddEstimate(cb, Vector128.Create(-GCbMult), y));
            c2 = Vector128.MultiplyAddEstimate(cb, Vector128.Create(BCbMult), y);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector256<float> c0,
            ref Vector256<float> c1,
            ref Vector256<float> c2,
            Vector256<float> c3,
            Vector256<float> maximumValue,
            Vector256<float> halfValue,
            Vector256<float> scale)
        {
            ConvertToRgb(ref c0, ref c1, ref c2, halfValue);

            // Ordinary JPEG conversion rounds in the sample domain before normalizing.
            c0 = Vector256.Round(c0) * scale;
            c1 = Vector256.Round(c1) * scale;
            c2 = Vector256.Round(c2) * scale;
        }

        /// <summary>
        /// Applies the JPEG YCbCr matrix without rounding or normalizing its component values.
        /// </summary>
        /// <param name="c0">The Y samples, replaced by the first decoded channel.</param>
        /// <param name="c1">The Cb samples, replaced by the second decoded channel.</param>
        /// <param name="c2">The Cr samples, replaced by the third decoded channel.</param>
        /// <param name="halfValue">The JPEG chroma midpoint.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, Vector256<float> halfValue)
        {
            Vector256<float> y = c0;
            Vector256<float> cb = c1 - halfValue;
            Vector256<float> cr = c2 - halfValue;

            // Each lane is an independent sample. ICC needs the fractional device values, while
            // the ordinary JPEG wrapper above applies its existing integer rounding afterward.
            c0 = Vector256.MultiplyAddEstimate(cr, Vector256.Create(RCrMult), y);
            c1 = Vector256.MultiplyAddEstimate(cr, Vector256.Create(-GCrMult), Vector256.MultiplyAddEstimate(cb, Vector256.Create(-GCbMult), y));
            c2 = Vector256.MultiplyAddEstimate(cb, Vector256.Create(BCbMult), y);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector512<float> c0,
            ref Vector512<float> c1,
            ref Vector512<float> c2,
            Vector512<float> c3,
            Vector512<float> maximumValue,
            Vector512<float> halfValue,
            Vector512<float> scale)
        {
            ConvertToRgb(ref c0, ref c1, ref c2, halfValue);

            // Ordinary JPEG conversion rounds in the sample domain before normalizing.
            c0 = Vector512.Round(c0) * scale;
            c1 = Vector512.Round(c1) * scale;
            c2 = Vector512.Round(c2) * scale;
        }

        /// <summary>
        /// Applies the JPEG YCbCr matrix without rounding or normalizing its component values.
        /// </summary>
        /// <param name="c0">The Y samples, replaced by the first decoded channel.</param>
        /// <param name="c1">The Cb samples, replaced by the second decoded channel.</param>
        /// <param name="c2">The Cr samples, replaced by the third decoded channel.</param>
        /// <param name="halfValue">The JPEG chroma midpoint.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, Vector512<float> halfValue)
        {
            Vector512<float> y = c0;
            Vector512<float> cb = c1 - halfValue;
            Vector512<float> cr = c2 - halfValue;

            // Each lane is an independent sample. ICC needs the fractional device values, while
            // the ordinary JPEG wrapper above applies its existing integer rounding afterward.
            c0 = Vector512.MultiplyAddEstimate(cr, Vector512.Create(RCrMult), y);
            c1 = Vector512.MultiplyAddEstimate(cr, Vector512.Create(-GCrMult), Vector512.MultiplyAddEstimate(cb, Vector512.Create(-GCbMult), y));
            c2 = Vector512.MultiplyAddEstimate(cb, Vector512.Create(BCbMult), y);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            float r,
            float g,
            float b,
            float maximumValue,
            float halfValue,
            float scale,
            out float c0,
            out float c1,
            out float c2,
            out float c3)
        {
            // RGB and halfValue use the same component units. The BT.601 luma weights form Y,
            // while the signed chroma projections are biased by halfValue into the JPEG sample domain.
            // YCbCr has no fourth component, so c3 is a compile-time-unused placeholder for the shared loop.
            c0 = (0.299F * r) + (0.587F * g) + (0.114F * b);
            c1 = halfValue - (0.168736F * r) - (0.331264F * g) + (0.5F * b);
            c2 = halfValue + (0.5F * r) - (0.418688F * g) - (0.081312F * b);
            c3 = 0;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<float> r,
            Vector128<float> g,
            Vector128<float> b,
            Vector128<float> maximumValue,
            Vector128<float> halfValue,
            Vector128<float> scale,
            out Vector128<float> c0,
            out Vector128<float> c1,
            out Vector128<float> c2,
            out Vector128<float> c3)
        {
            // Each vector holds four consecutive values from one RGB plane. The nested multiply-add sequence
            // produces four Y lanes, four Cb lanes, and four Cr lanes without transposition. The association
            // exposes two FMA opportunities per output while preserving the scalar formula's term grouping.
            c0 = Vector128.MultiplyAddEstimate(
                Vector128.Create(0.299F),
                r,
                Vector128.MultiplyAddEstimate(Vector128.Create(0.587F), g, Vector128.Create(0.114F) * b));

            c1 = halfValue + Vector128.MultiplyAddEstimate(
                Vector128.Create(-0.168736F),
                r,
                Vector128.MultiplyAddEstimate(Vector128.Create(-0.331264F), g, Vector128.Create(0.5F) * b));

            c2 = halfValue + Vector128.MultiplyAddEstimate(
                Vector128.Create(0.5F),
                r,
                Vector128.MultiplyAddEstimate(Vector128.Create(-0.418688F), g, Vector128.Create(-0.081312F) * b));

            c3 = default;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<float> r,
            Vector256<float> g,
            Vector256<float> b,
            Vector256<float> maximumValue,
            Vector256<float> halfValue,
            Vector256<float> scale,
            out Vector256<float> c0,
            out Vector256<float> c1,
            out Vector256<float> c2,
            out Vector256<float> c3)
        {
            // Eight planar RGB samples use the identical association as Vector128, allowing direct YMM FMA
            // generation while preserving the component-per-vector output layout.
            c0 = Vector256.MultiplyAddEstimate(
                Vector256.Create(0.299F),
                r,
                Vector256.MultiplyAddEstimate(Vector256.Create(0.587F), g, Vector256.Create(0.114F) * b));

            c1 = halfValue + Vector256.MultiplyAddEstimate(
                Vector256.Create(-0.168736F),
                r,
                Vector256.MultiplyAddEstimate(Vector256.Create(-0.331264F), g, Vector256.Create(0.5F) * b));

            c2 = halfValue + Vector256.MultiplyAddEstimate(
                Vector256.Create(0.5F),
                r,
                Vector256.MultiplyAddEstimate(Vector256.Create(-0.418688F), g, Vector256.Create(-0.081312F) * b));

            c3 = default;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<float> r,
            Vector512<float> g,
            Vector512<float> b,
            Vector512<float> maximumValue,
            Vector512<float> halfValue,
            Vector512<float> scale,
            out Vector512<float> c0,
            out Vector512<float> c1,
            out Vector512<float> c2,
            out Vector512<float> c3)
        {
            // Sixteen planar RGB samples use the same nested form. Constants are lane broadcasts and c3 is
            // deliberately zero because the shared traversal removes the unused fourth store for this operator.
            c0 = Vector512.MultiplyAddEstimate(
                Vector512.Create(0.299F),
                r,
                Vector512.MultiplyAddEstimate(Vector512.Create(0.587F), g, Vector512.Create(0.114F) * b));

            c1 = halfValue + Vector512.MultiplyAddEstimate(
                Vector512.Create(-0.168736F),
                r,
                Vector512.MultiplyAddEstimate(Vector512.Create(-0.331264F), g, Vector512.Create(0.5F) * b));

            c2 = halfValue + Vector512.MultiplyAddEstimate(
                Vector512.Create(0.5F),
                r,
                Vector512.MultiplyAddEstimate(Vector512.Create(-0.418688F), g, Vector512.Create(-0.081312F) * b));

            c3 = default;
        }
    }
}

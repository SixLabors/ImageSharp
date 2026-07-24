// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Implements direct JPEG RGB normalization and planar RGB copying for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct RgbOperator : IJpegColorConverterOperator
    {
        /// <inheritdoc/>
        public static JpegColorSpace ColorSpace => JpegColorSpace.RGB;

        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref float c0,
            ref float c1,
            ref float c2,
            float c3,
            float maximumValue,
            float halfValue,
            float scale)
        {
            // The JPEG planes already represent R, G, and B. Conversion therefore consists only of moving
            // each integer-domain sample into the normalized floating-point domain consumed by pixel packing.
            c0 *= scale;
            c1 *= scale;
            c2 *= scale;
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
            // Four samples from each planar channel remain in their lanes while sharing one normalization vector.
            c0 *= scale;
            c1 *= scale;
            c2 *= scale;
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
            // Eight samples per plane are normalized independently without channel shuffles.
            c0 *= scale;
            c1 *= scale;
            c2 *= scale;
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
            // Sixteen samples per plane are normalized independently without changing planar ordering.
            c0 *= scale;
            c1 *= scale;
            c2 *= scale;
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
            // Encoder RGB lanes already use the JPEG sample domain, so the direct color model copies them.
            c0 = r;
            c1 = g;
            c2 = b;
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
            // The planar vectors map one-to-one to JPEG components; the fourth result is statically discarded.
            c0 = r;
            c1 = g;
            c2 = b;
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
            // The planar vectors map one-to-one to JPEG components; no arithmetic or rearrangement is required.
            c0 = r;
            c1 = g;
            c2 = b;
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
            // The widest path is likewise a register-to-register planar copy for sixteen pixels.
            c0 = r;
            c1 = g;
            c2 = b;
            c3 = default;
        }

        /// <inheritdoc/>
        public static void ConvertToRgbInPlaceWithIcc(
            Configuration configuration,
            IccProfile profile,
            in ComponentValues values,
            float maximumValue)
            => RgbScalar.ConvertToRgbInPlaceWithIcc(configuration, profile, values, maximumValue);
    }
}

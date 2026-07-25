// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Implements inverted JPEG CMYK conversion for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct CmykOperator : IJpegColorConverterOperator
    {
        /// <inheritdoc/>
        public static JpegColorSpace ColorSpace => JpegColorSpace.Cmyk;

        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float c0, ref float c1, ref float c2, float c3, float maximumValue, float halfValue, float scale)
        {
            // Adobe-style CMYK stores inverted component samples. Multiplying K by scale twice folds the
            // two sample-domain divisions into one factor before it modulates the C, M, and Y planes.
            float scaledK = c3 * scale * scale;
            c0 *= scaledK;
            c1 *= scaledK;
            c2 *= scaledK;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, Vector128<float> c3, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale)
        {
            // Each K lane supplies the common modulation factor for the corresponding C, M, and Y lanes.
            Vector128<float> scaledK = c3 * scale * scale;
            c0 *= scaledK;
            c1 *= scaledK;
            c2 *= scaledK;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, Vector256<float> c3, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale)
        {
            // Eight independent CMYK samples remain lane-aligned throughout the modulation.
            Vector256<float> scaledK = c3 * scale * scale;
            c0 *= scaledK;
            c1 *= scaledK;
            c2 *= scaledK;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, Vector512<float> c3, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale)
        {
            // Sixteen independent CMYK samples remain lane-aligned throughout the modulation.
            Vector512<float> scaledK = c3 * scale * scale;
            c0 *= scaledK;
            c1 *= scaledK;
            c2 *= scaledK;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(float r, float g, float b, float maximumValue, float halfValue, float scale, out float c0, out float c1, out float c2, out float c3)
        {
            float c = maximumValue - r;
            float m = maximumValue - g;
            float y = maximumValue - b;
            float k = MathF.Min(c, MathF.Min(m, y));

            // Pure black makes the chromatic divisor zero. In that case chromatic ink is defined as zero;
            // otherwise remove K and normalize the remaining C, M, and Y contributions.
            if (k >= maximumValue)
            {
                c = 0;
                m = 0;
                y = 0;
            }
            else
            {
                // The same remaining range normalizes every chromatic channel. Computing its reciprocal once
                // replaces three divisions with one division and three multiplies.
                float reciprocal = 1F / (maximumValue - k);
                c = (c - k) * reciprocal;
                m = (m - k) * reciprocal;
                y = (y - k) * reciprocal;
            }

            // JPEG CMYK is inverted, including K, so normalized chromatic values are reflected around max.
            c0 = maximumValue - (c * maximumValue);
            c1 = maximumValue - (m * maximumValue);
            c2 = maximumValue - (y * maximumValue);
            c3 = maximumValue - k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector128<float> r, Vector128<float> g, Vector128<float> b, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
        {
            Vector128<float> c = maximumValue - r;
            Vector128<float> m = maximumValue - g;
            Vector128<float> y = maximumValue - b;
            Vector128<float> k = Vector128.Min(c, Vector128.Min(m, y));

            // The all-bits mask clears the undefined zero-divisor result for pure-black lanes without a branch.
            Vector128<float> nonBlack = ~Vector128.Equals(k, maximumValue);
            Vector128<float> reciprocal = Vector128<float>.One / (maximumValue - k);
            c = ((c - k) * reciprocal) & nonBlack;
            m = ((m - k) * reciprocal) & nonBlack;
            y = ((y - k) * reciprocal) & nonBlack;

            c0 = maximumValue - (c * maximumValue);
            c1 = maximumValue - (m * maximumValue);
            c2 = maximumValue - (y * maximumValue);
            c3 = maximumValue - k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector256<float> r, Vector256<float> g, Vector256<float> b, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
        {
            Vector256<float> c = maximumValue - r;
            Vector256<float> m = maximumValue - g;
            Vector256<float> y = maximumValue - b;
            Vector256<float> k = Vector256.Min(c, Vector256.Min(m, y));

            // Masking preserves lane independence when a vector mixes pure black with chromatic pixels.
            Vector256<float> nonBlack = ~Vector256.Equals(k, maximumValue);
            Vector256<float> reciprocal = Vector256<float>.One / (maximumValue - k);
            c = ((c - k) * reciprocal) & nonBlack;
            m = ((m - k) * reciprocal) & nonBlack;
            y = ((y - k) * reciprocal) & nonBlack;

            c0 = maximumValue - (c * maximumValue);
            c1 = maximumValue - (m * maximumValue);
            c2 = maximumValue - (y * maximumValue);
            c3 = maximumValue - k;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector512<float> r, Vector512<float> g, Vector512<float> b, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
        {
            Vector512<float> c = maximumValue - r;
            Vector512<float> m = maximumValue - g;
            Vector512<float> y = maximumValue - b;
            Vector512<float> k = Vector512.Min(c, Vector512.Min(m, y));

            // AVX-512 still uses a full floating-point mask value here because bitwise clearing exactly matches
            // the narrower operator semantics and lets the JIT select the most suitable native instructions.
            Vector512<float> nonBlack = ~Vector512.Equals(k, maximumValue);
            Vector512<float> reciprocal = Vector512<float>.One / (maximumValue - k);
            c = ((c - k) * reciprocal) & nonBlack;
            m = ((m - k) * reciprocal) & nonBlack;
            y = ((y - k) * reciprocal) & nonBlack;

            c0 = maximumValue - (c * maximumValue);
            c1 = maximumValue - (m * maximumValue);
            c2 = maximumValue - (y * maximumValue);
            c3 = maximumValue - k;
        }

        /// <inheritdoc/>
        public static void ConvertToRgbInPlaceWithIcc(Configuration configuration, IccProfile profile, in ComponentValues values, float maximumValue)
        {
            using IMemoryOwner<float> memoryOwner = configuration.MemoryAllocator.Allocate<float>(values.Component0.Length * 4);
            Span<float> packed = memoryOwner.Memory.Span;

            Span<float> c0 = values.Component0;
            Span<float> c1 = values.Component1;
            Span<float> c2 = values.Component2;
            Span<float> c3 = values.Component3;

            // JPEG CMYK stores inverted components, while the ICC converter consumes normalized conventional CMYK.
            PackedInvertNormalizeInterleave4(c0, c1, c2, c3, packed, maximumValue);

            Span<Cmyk> source = MemoryMarshal.Cast<float, Cmyk>(packed);
            Span<Rgb> destination = MemoryMarshal.Cast<float, Rgb>(packed)[..source.Length];
            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
            };

            ColorProfileConverter converter = new(options);
            converter.Convert<Cmyk, Rgb>(source, destination);
            UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..source.Length], c0, c1, c2);
        }
    }
}

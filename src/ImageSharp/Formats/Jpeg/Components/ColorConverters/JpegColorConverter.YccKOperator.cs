// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Implements inverted JPEG YccK conversion for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct YccKOperator : IJpegColorConverterOperator
    {
        /// <inheritdoc/>
        public static JpegColorSpace ColorSpace => JpegColorSpace.Ycck;

        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float c0, ref float c1, ref float c2, float c3, float maximumValue, float halfValue, float scale)
        {
            float y = c0;
            float cb = c1 - halfValue;
            float cr = c2 - halfValue;
            float scaledK = c3 * scale * scale;

            // YccK first reconstructs inverted RGB in the integer sample domain. Rounding must occur before
            // subtracting from max and applying K because changing that order changes encoded JPEG semantics.
            c0 = (maximumValue - MathF.Round(y + (YCbCrOperator.RCrMult * cr), MidpointRounding.AwayFromZero)) * scaledK;
            c1 = (maximumValue - MathF.Round(y - (YCbCrOperator.GCbMult * cb) - (YCbCrOperator.GCrMult * cr), MidpointRounding.AwayFromZero)) * scaledK;
            c2 = (maximumValue - MathF.Round(y + (YCbCrOperator.BCbMult * cb), MidpointRounding.AwayFromZero)) * scaledK;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, Vector128<float> c3, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale)
        {
            Vector128<float> y = c0;
            Vector128<float> cb = c1 - halfValue;
            Vector128<float> cr = c2 - halfValue;
            Vector128<float> scaledK = c3 * scale * scale;

            // Four lanes reconstruct YCbCr concurrently; each rounded result is inverted and modulated by its K lane.
            Vector128<float> r = Vector128_.MultiplyAddEstimate(cr, Vector128.Create(YCbCrOperator.RCrMult), y);
            Vector128<float> g = Vector128_.MultiplyAddEstimate(cr, Vector128.Create(-YCbCrOperator.GCrMult), Vector128_.MultiplyAddEstimate(cb, Vector128.Create(-YCbCrOperator.GCbMult), y));
            Vector128<float> b = Vector128_.MultiplyAddEstimate(cb, Vector128.Create(YCbCrOperator.BCbMult), y);
            c0 = (maximumValue - Vector128_.RoundToNearestInteger(r)) * scaledK;
            c1 = (maximumValue - Vector128_.RoundToNearestInteger(g)) * scaledK;
            c2 = (maximumValue - Vector128_.RoundToNearestInteger(b)) * scaledK;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, Vector256<float> c3, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale)
        {
            Vector256<float> y = c0;
            Vector256<float> cb = c1 - halfValue;
            Vector256<float> cr = c2 - halfValue;
            Vector256<float> scaledK = c3 * scale * scale;

            // Eight lanes retain planar alignment from Y/Cb/Cr/K through normalized RGB.
            Vector256<float> r = Vector256_.MultiplyAddEstimate(cr, Vector256.Create(YCbCrOperator.RCrMult), y);
            Vector256<float> g = Vector256_.MultiplyAddEstimate(cr, Vector256.Create(-YCbCrOperator.GCrMult), Vector256_.MultiplyAddEstimate(cb, Vector256.Create(-YCbCrOperator.GCbMult), y));
            Vector256<float> b = Vector256_.MultiplyAddEstimate(cb, Vector256.Create(YCbCrOperator.BCbMult), y);
            c0 = (maximumValue - Vector256_.RoundToNearestInteger(r)) * scaledK;
            c1 = (maximumValue - Vector256_.RoundToNearestInteger(g)) * scaledK;
            c2 = (maximumValue - Vector256_.RoundToNearestInteger(b)) * scaledK;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, Vector512<float> c3, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale)
        {
            Vector512<float> y = c0;
            Vector512<float> cb = c1 - halfValue;
            Vector512<float> cr = c2 - halfValue;
            Vector512<float> scaledK = c3 * scale * scale;

            // Sixteen lanes use the same matrix, rounding, inversion, and K modulation order as scalar code.
            Vector512<float> r = Vector512_.MultiplyAddEstimate(cr, Vector512.Create(YCbCrOperator.RCrMult), y);
            Vector512<float> g = Vector512_.MultiplyAddEstimate(cr, Vector512.Create(-YCbCrOperator.GCrMult), Vector512_.MultiplyAddEstimate(cb, Vector512.Create(-YCbCrOperator.GCbMult), y));
            Vector512<float> b = Vector512_.MultiplyAddEstimate(cb, Vector512.Create(YCbCrOperator.BCbMult), y);
            c0 = (maximumValue - Vector512_.RoundToNearestInteger(r)) * scaledK;
            c1 = (maximumValue - Vector512_.RoundToNearestInteger(g)) * scaledK;
            c2 = (maximumValue - Vector512_.RoundToNearestInteger(b)) * scaledK;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(float r, float g, float b, float maximumValue, float halfValue, float scale, out float c0, out float c1, out float c2, out float c3)
        {
            // CMYK extraction supplies inverted chromatic samples and K. Reflecting the first three results
            // reconstructs the chromatic RGB that YCbCr encodes, while K passes through untouched.
            CmykOperator.ConvertFromRgb(r, g, b, maximumValue, halfValue, scale, out float c, out float m, out float y, out c3);

            YCbCrOperator.ConvertFromRgb(maximumValue - c, maximumValue - m, maximumValue - y, maximumValue, halfValue, scale, out c0, out c1, out c2, out _);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector128<float> r, Vector128<float> g, Vector128<float> b, Vector128<float> maximumValue, Vector128<float> halfValue, Vector128<float> scale, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
        {
            // Static constrained calls inline both stages, keeping four pixels in registers without materializing CMYK planes.
            CmykOperator.ConvertFromRgb(r, g, b, maximumValue, halfValue, scale, out Vector128<float> c, out Vector128<float> m, out Vector128<float> y, out c3);

            YCbCrOperator.ConvertFromRgb(maximumValue - c, maximumValue - m, maximumValue - y, maximumValue, halfValue, scale, out c0, out c1, out c2, out _);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector256<float> r, Vector256<float> g, Vector256<float> b, Vector256<float> maximumValue, Vector256<float> halfValue, Vector256<float> scale, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
        {
            // Eight pixels flow through CMYK extraction and YCbCr projection entirely in YMM registers.
            CmykOperator.ConvertFromRgb(r, g, b, maximumValue, halfValue, scale, out Vector256<float> c, out Vector256<float> m, out Vector256<float> y, out c3);

            YCbCrOperator.ConvertFromRgb(maximumValue - c, maximumValue - m, maximumValue - y, maximumValue, halfValue, scale, out c0, out c1, out c2, out _);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(Vector512<float> r, Vector512<float> g, Vector512<float> b, Vector512<float> maximumValue, Vector512<float> halfValue, Vector512<float> scale, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
        {
            // Sixteen pixels flow through both mathematical stages in registers without materializing intermediate planes.
            CmykOperator.ConvertFromRgb(r, g, b, maximumValue, halfValue, scale, out Vector512<float> c, out Vector512<float> m, out Vector512<float> y, out c3);

            YCbCrOperator.ConvertFromRgb(maximumValue - c, maximumValue - m, maximumValue - y, maximumValue, halfValue, scale, out c0, out c1, out c2, out _);
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

            // Adobe-style JPEG YccK is inverted; normalize it before applying the format-defined YccK-to-CMYK transform.
            PackedInvertNormalizeInterleave4(c0, c1, c2, c3, packed, maximumValue);

            ColorProfileConverter converter = new();
            Span<Cmyk> source = MemoryMarshal.Cast<float, Cmyk>(packed);
            converter.Convert<YccK, Cmyk>(MemoryMarshal.Cast<Cmyk, YccK>(source), source);

            Span<Rgb> destination = MemoryMarshal.Cast<float, Rgb>(packed)[..source.Length];
            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
            };

            converter = new ColorProfileConverter(options);
            converter.Convert<Cmyk, Rgb>(source, destination);
            UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..source.Length], c0, c1, c2);
        }
    }
}

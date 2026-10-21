// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp.ColorConverters;

internal abstract partial class WebpColorConverterBase
{
    /// <summary>
    /// Preserves native YCbCr samples for a YCbCr source ICC profile.
    /// </summary>
    internal readonly struct YCbCrIccOperator : IWebpColorConverterOperator
    {
        /// <inheritdoc />
        public static void ConvertToSamples(ref int c0, ref int c1, ref int c2)
        {
            // Keep interpolated Y, Cb, and Cr codes for the YCbCr profile transform.
        }

        /// <inheritdoc />
        public static void ConvertToSamples(ref Vector128<int> c0, ref Vector128<int> c1, ref Vector128<int> c2)
        {
            // Preserve these source channel codes; this operator does not apply the YCbCr-to-BGR matrix.
        }

        /// <inheritdoc />
        public static void ConvertToSamples(ref Vector256<int> c0, ref Vector256<int> c1, ref Vector256<int> c2)
        {
            // Preserve these source channel codes; this operator does not apply the YCbCr-to-BGR matrix.
        }

        /// <inheritdoc />
        public static void ConvertToSamples(ref Vector512<int> c0, ref Vector512<int> c1, ref Vector512<int> c2)
        {
            // Preserve these source channel codes; this operator does not apply the YCbCr-to-BGR matrix.
        }

        /// <inheritdoc />
        public static void ConvertSamplesWithIcc(
            Configuration configuration, ReadOnlySpan<byte> source, ColorProfileConverter converter, Span<Vector4> vectors, Span<Rgb> rgb)
        {
            // A YCbCr source profile consumes normalized Y, Cb, Cr codes directly. The two color structs each contain three floats.
            // Each block reads three source components before writing three RGB components, so both views can share this storage.
            Span<YCbCr> yCbCr = MemoryMarshal.Cast<Rgb, YCbCr>(rgb);
            SimdUtils.ByteToNormalizedFloat(source, MemoryMarshal.Cast<YCbCr, float>(yCbCr));
            converter.Convert<YCbCr, Rgb>(yCbCr, rgb);
            Rgb.ToScaledVector4(rgb, vectors);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(int r, int g, int b, int inputShift, out int c0, out int c1, out int c2)
        {
            // WebP encodes the same Y, Cb, Cr codes whether decoding later retains them for an ICC transform or converts them to BGR.
            YCbCrOperator.ConvertFromRgb(r, g, b, inputShift, out c0, out c1, out c2);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<int> r, Vector128<int> g, Vector128<int> b, int inputShift, out Vector128<int> c0, out Vector128<int> c1, out Vector128<int> c2)
        {
            // WebP encodes the same Y, Cb, Cr codes whether decoding later retains them for an ICC transform or converts them to BGR.
            YCbCrOperator.ConvertFromRgb(r, g, b, inputShift, out c0, out c1, out c2);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<int> r, Vector256<int> g, Vector256<int> b, int inputShift, out Vector256<int> c0, out Vector256<int> c1, out Vector256<int> c2)
        {
            // WebP encodes the same Y, Cb, Cr codes whether decoding later retains them for an ICC transform or converts them to BGR.
            YCbCrOperator.ConvertFromRgb(r, g, b, inputShift, out c0, out c1, out c2);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<int> r, Vector512<int> g, Vector512<int> b, int inputShift, out Vector512<int> c0, out Vector512<int> c1, out Vector512<int> c2)
        {
            // WebP encodes the same Y, Cb, Cr codes whether decoding later retains them for an ICC transform or converts them to BGR.
            YCbCrOperator.ConvertFromRgb(r, g, b, inputShift, out c0, out c1, out c2);
        }
    }
}

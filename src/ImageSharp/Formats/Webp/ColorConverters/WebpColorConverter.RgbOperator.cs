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
    /// Preserves native RGB sample bytes for the RGB profile converter.
    /// </summary>
    internal readonly struct RgbOperator : IWebpColorConverterOperator
    {
        /// <inheritdoc />
        public static void ConvertToSamples(ref int c0, ref int c1, ref int c2)
        {
            // Native RGB already has its destination byte codes.
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
            // Three-byte decoder output stores B, G, R. The pixel API exposes straight RGB vectors to the ICC transform.
            ReadOnlySpan<Bgr24> bgr = MemoryMarshal.Cast<byte, Bgr24>(source);
            PixelOperations<Bgr24>.Instance.ToUnassociatedScaledVector4(configuration, bgr, vectors);
            converter.Convert(vectors, rgb);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(int r, int g, int b, int inputShift, out int c0, out int c1, out int c2)
        {
            // The source planes are R, G, B; native packed RGB output uses B, G, R byte order.
            // Remove any fractional input bits while preserving that component order.
            c0 = b >> inputShift;
            c1 = g >> inputShift;
            c2 = r >> inputShift;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<int> r, Vector128<int> g, Vector128<int> b, int inputShift, out Vector128<int> c0, out Vector128<int> c1, out Vector128<int> c2)
        {
            // The source planes are R, G, B; native packed RGB output uses B, G, R byte order.
            // Remove any fractional input bits while preserving that component order.
            c0 = b >> inputShift;
            c1 = g >> inputShift;
            c2 = r >> inputShift;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<int> r, Vector256<int> g, Vector256<int> b, int inputShift, out Vector256<int> c0, out Vector256<int> c1, out Vector256<int> c2)
        {
            // The source planes are R, G, B; native packed RGB output uses B, G, R byte order.
            // Remove any fractional input bits while preserving that component order.
            c0 = b >> inputShift;
            c1 = g >> inputShift;
            c2 = r >> inputShift;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<int> r, Vector512<int> g, Vector512<int> b, int inputShift, out Vector512<int> c0, out Vector512<int> c1, out Vector512<int> c2)
        {
            // The source planes are R, G, B; native packed RGB output uses B, G, R byte order.
            // Remove any fractional input bits while preserving that component order.
            c0 = b >> inputShift;
            c1 = g >> inputShift;
            c2 = r >> inputShift;
        }
    }
}

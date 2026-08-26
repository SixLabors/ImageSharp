// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

internal abstract partial class HeifColorConverterBase
{
    /// <summary>
    /// Implements coefficient-based YCbCr conversion for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct HeifCoefficientColorOperator : IHeifColorOperator
    {
        /// <inheritdoc/>
        public static bool ChromaUsesLumaRange => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float y, ref float cb, ref float cr, in HeifColorConversionParameters parameters)
        {
            // Preserve the H.273 operation order. Pre-dividing the two green contributions changes rounding at
            // exact output-code boundaries for high-bit-depth images.
            float r = y + (parameters.RedChromaScale * cr);
            float g = y - ((2F * ((parameters.GreenRedChromaNumerator * cr) +
                (parameters.GreenBlueChromaNumerator * cb))) / parameters.Kg);

            float b = y + (parameters.BlueChromaScale * cb);

            y = r;
            cb = g;
            cr = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in HeifColorConversionParameters parameters)
        {
            Vector128<float> r = y + (Vector128.Create(parameters.RedChromaScale) * cr);
            Vector128<float> greenRed = Vector128.Create(parameters.GreenRedChromaNumerator) * cr;
            Vector128<float> greenBlue = Vector128.Create(parameters.GreenBlueChromaNumerator) * cb;
            Vector128<float> g = y - ((Vector128.Create(2F) * (greenRed + greenBlue)) / Vector128.Create(parameters.Kg));
            Vector128<float> b = y + (Vector128.Create(parameters.BlueChromaScale) * cb);

            y = r;
            cb = g;
            cr = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in HeifColorConversionParameters parameters)
        {
            Vector256<float> r = y + (Vector256.Create(parameters.RedChromaScale) * cr);
            Vector256<float> greenRed = Vector256.Create(parameters.GreenRedChromaNumerator) * cr;
            Vector256<float> greenBlue = Vector256.Create(parameters.GreenBlueChromaNumerator) * cb;
            Vector256<float> g = y - ((Vector256.Create(2F) * (greenRed + greenBlue)) / Vector256.Create(parameters.Kg));
            Vector256<float> b = y + (Vector256.Create(parameters.BlueChromaScale) * cb);

            y = r;
            cb = g;
            cr = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in HeifColorConversionParameters parameters)
        {
            Vector512<float> r = y + (Vector512.Create(parameters.RedChromaScale) * cr);
            Vector512<float> greenRed = Vector512.Create(parameters.GreenRedChromaNumerator) * cr;
            Vector512<float> greenBlue = Vector512.Create(parameters.GreenBlueChromaNumerator) * cb;
            Vector512<float> g = y - ((Vector512.Create(2F) * (greenRed + greenBlue)) / Vector512.Create(parameters.Kg));
            Vector512<float> b = y + (Vector512.Create(parameters.BlueChromaScale) * cb);

            y = r;
            cb = g;
            cr = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            float r,
            float g,
            float b,
            in HeifColorConversionParameters parameters,
            out float y,
            out float cb,
            out float cr)
        {
            // Luma is shared by both chroma equations, so calculate it once before projecting blue and red.
            y = (parameters.Kr * r) + (parameters.Kg * g) + (parameters.Kb * b);
            cb = (b - y) / parameters.BlueChromaScale;
            cr = (r - y) / parameters.RedChromaScale;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<float> r,
            Vector128<float> g,
            Vector128<float> b,
            in HeifColorConversionParameters parameters,
            out Vector128<float> y,
            out Vector128<float> cb,
            out Vector128<float> cr)
        {
            y = Vector128.MultiplyAddEstimate(
                Vector128.Create(parameters.Kr),
                r,
                Vector128.MultiplyAddEstimate(Vector128.Create(parameters.Kg), g, Vector128.Create(parameters.Kb) * b));
            cb = (b - y) / Vector128.Create(parameters.BlueChromaScale);
            cr = (r - y) / Vector128.Create(parameters.RedChromaScale);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<float> r,
            Vector256<float> g,
            Vector256<float> b,
            in HeifColorConversionParameters parameters,
            out Vector256<float> y,
            out Vector256<float> cb,
            out Vector256<float> cr)
        {
            y = Vector256.MultiplyAddEstimate(
                Vector256.Create(parameters.Kr),
                r,
                Vector256.MultiplyAddEstimate(Vector256.Create(parameters.Kg), g, Vector256.Create(parameters.Kb) * b));
            cb = (b - y) / Vector256.Create(parameters.BlueChromaScale);
            cr = (r - y) / Vector256.Create(parameters.RedChromaScale);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<float> r,
            Vector512<float> g,
            Vector512<float> b,
            in HeifColorConversionParameters parameters,
            out Vector512<float> y,
            out Vector512<float> cb,
            out Vector512<float> cr)
        {
            y = Vector512.MultiplyAddEstimate(
                Vector512.Create(parameters.Kr),
                r,
                Vector512.MultiplyAddEstimate(Vector512.Create(parameters.Kg), g, Vector512.Create(parameters.Kb) * b));
            cb = (b - y) / Vector512.Create(parameters.BlueChromaScale);
            cr = (r - y) / Vector512.Create(parameters.RedChromaScale);
        }
    }
}

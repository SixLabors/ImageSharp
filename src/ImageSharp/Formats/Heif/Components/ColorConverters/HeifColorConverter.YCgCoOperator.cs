// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

internal abstract partial class HeifColorConverterBase
{
    /// <summary>
    /// Implements YCgCo conversion for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct HeifYCgCoColorOperator : IHeifColorOperator
    {
        /// <inheritdoc/>
        public static bool ChromaUsesLumaRange => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float y, ref float cg, ref float co, in HeifColorConversionParameters parameters)
        {
            // Reusing Y - Cg for both outer primaries keeps the inverse transform to four additions.
            float temporary = y - cg;
            float r = temporary + co;
            float g = y + cg;
            float b = temporary - co;
            y = r;
            cg = g;
            co = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> y, ref Vector128<float> cg, ref Vector128<float> co, in HeifColorConversionParameters parameters)
        {
            Vector128<float> temporary = y - cg;
            Vector128<float> r = temporary + co;
            Vector128<float> g = y + cg;
            Vector128<float> b = temporary - co;
            y = r;
            cg = g;
            co = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> y, ref Vector256<float> cg, ref Vector256<float> co, in HeifColorConversionParameters parameters)
        {
            Vector256<float> temporary = y - cg;
            Vector256<float> r = temporary + co;
            Vector256<float> g = y + cg;
            Vector256<float> b = temporary - co;
            y = r;
            cg = g;
            co = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> y, ref Vector512<float> cg, ref Vector512<float> co, in HeifColorConversionParameters parameters)
        {
            Vector512<float> temporary = y - cg;
            Vector512<float> r = temporary + co;
            Vector512<float> g = y + cg;
            Vector512<float> b = temporary - co;
            y = r;
            cg = g;
            co = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            float r,
            float g,
            float b,
            in HeifColorConversionParameters parameters,
            out float y,
            out float cg,
            out float co)
        {
            // R + B is shared by Y and Cg, while Co is the half-scaled red/blue difference.
            float sum = r + b;
            y = (0.5F * g) + (0.25F * sum);
            cg = (0.5F * g) - (0.25F * sum);
            co = 0.5F * (r - b);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<float> r,
            Vector128<float> g,
            Vector128<float> b,
            in HeifColorConversionParameters parameters,
            out Vector128<float> y,
            out Vector128<float> cg,
            out Vector128<float> co)
        {
            Vector128<float> sum = r + b;
            y = Vector128.MultiplyAddEstimate(Vector128.Create(0.5F), g, Vector128.Create(0.25F) * sum);
            cg = Vector128.MultiplyAddEstimate(Vector128.Create(0.5F), g, Vector128.Create(-0.25F) * sum);
            co = Vector128.Create(0.5F) * (r - b);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<float> r,
            Vector256<float> g,
            Vector256<float> b,
            in HeifColorConversionParameters parameters,
            out Vector256<float> y,
            out Vector256<float> cg,
            out Vector256<float> co)
        {
            Vector256<float> sum = r + b;
            y = Vector256.MultiplyAddEstimate(Vector256.Create(0.5F), g, Vector256.Create(0.25F) * sum);
            cg = Vector256.MultiplyAddEstimate(Vector256.Create(0.5F), g, Vector256.Create(-0.25F) * sum);
            co = Vector256.Create(0.5F) * (r - b);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<float> r,
            Vector512<float> g,
            Vector512<float> b,
            in HeifColorConversionParameters parameters,
            out Vector512<float> y,
            out Vector512<float> cg,
            out Vector512<float> co)
        {
            Vector512<float> sum = r + b;
            y = Vector512.MultiplyAddEstimate(Vector512.Create(0.5F), g, Vector512.Create(0.25F) * sum);
            cg = Vector512.MultiplyAddEstimate(Vector512.Create(0.5F), g, Vector512.Create(-0.25F) * sum);
            co = Vector512.Create(0.5F) * (r - b);
        }
    }
}

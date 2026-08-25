// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal abstract partial class Av1ColorConverterBase
{
    /// <summary>
    /// Implements direct G, B, and R plane mapping for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct Av1IdentityColorOperator : IAv1ColorOperator
    {
        /// <inheritdoc/>
        public static bool ChromaUsesLumaRange => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float green, ref float blue, ref float red, in Av1ColorConversionParameters parameters)
        {
            // Identity-matrix AV1 stores the planes in G, B, R order. Rotate the three references in place
            // so the shared traversal always leaves component0/component1/component2 as R, G, B.
            float g = green;
            green = red;
            red = blue;
            blue = g;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector128<float> green,
            ref Vector128<float> blue,
            ref Vector128<float> red,
            in Av1ColorConversionParameters parameters)
        {
            Vector128<float> g = green;
            green = red;
            red = blue;
            blue = g;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector256<float> green,
            ref Vector256<float> blue,
            ref Vector256<float> red,
            in Av1ColorConversionParameters parameters)
        {
            Vector256<float> g = green;
            green = red;
            red = blue;
            blue = g;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector512<float> green,
            ref Vector512<float> blue,
            ref Vector512<float> red,
            in Av1ColorConversionParameters parameters)
        {
            Vector512<float> g = green;
            green = red;
            red = blue;
            blue = g;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            float red,
            float green,
            float blue,
            in Av1ColorConversionParameters parameters,
            out float component0,
            out float component1,
            out float component2)
        {
            // Identity-matrix AV1 stores RGB input as G, B, R without matrix arithmetic.
            component0 = green;
            component1 = blue;
            component2 = red;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<float> red,
            Vector128<float> green,
            Vector128<float> blue,
            in Av1ColorConversionParameters parameters,
            out Vector128<float> component0,
            out Vector128<float> component1,
            out Vector128<float> component2)
        {
            component0 = green;
            component1 = blue;
            component2 = red;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<float> red,
            Vector256<float> green,
            Vector256<float> blue,
            in Av1ColorConversionParameters parameters,
            out Vector256<float> component0,
            out Vector256<float> component1,
            out Vector256<float> component2)
        {
            component0 = green;
            component1 = blue;
            component2 = red;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<float> red,
            Vector512<float> green,
            Vector512<float> blue,
            in Av1ColorConversionParameters parameters,
            out Vector512<float> component0,
            out Vector512<float> component1,
            out Vector512<float> component2)
        {
            component0 = green;
            component1 = blue;
            component2 = red;
        }
    }
}

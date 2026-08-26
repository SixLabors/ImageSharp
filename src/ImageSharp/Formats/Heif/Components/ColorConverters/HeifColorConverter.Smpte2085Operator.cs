// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

internal abstract partial class HeifColorConverterBase
{
    /// <summary>
    /// Implements SMPTE ST 2085 YDzDx conversion for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct HeifSmpte2085ColorOperator : IHeifColorOperator
    {
        /// <summary>
        /// The SMPTE ST 2085 blue primary normalization factor.
        /// </summary>
        public const float BlueNormalization = 0.986566F;

        /// <summary>
        /// The SMPTE ST 2085 green contribution to the red primary.
        /// </summary>
        public const float RedGreenContribution = 0.991902F;

        /// <inheritdoc/>
        public static bool ChromaUsesLumaRange => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float y, ref float dz, ref float dx, in HeifColorConversionParameters parameters)
        {
            // The encoded YDzDx planes carry green directly. The two difference planes restore blue and red.
            float g = y;
            float b = ((2F * dz) + y) / BlueNormalization;
            float r = (2F * dx) + (RedGreenContribution * y);
            y = r;
            dz = g;
            dx = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> y, ref Vector128<float> dz, ref Vector128<float> dx, in HeifColorConversionParameters parameters)
        {
            Vector128<float> g = y;
            Vector128<float> b = Vector128.MultiplyAddEstimate(Vector128.Create(2F), dz, y) / Vector128.Create(BlueNormalization);
            Vector128<float> r = Vector128.MultiplyAddEstimate(Vector128.Create(2F), dx, Vector128.Create(RedGreenContribution) * y);
            y = r;
            dz = g;
            dx = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> y, ref Vector256<float> dz, ref Vector256<float> dx, in HeifColorConversionParameters parameters)
        {
            Vector256<float> g = y;
            Vector256<float> b = Vector256.MultiplyAddEstimate(Vector256.Create(2F), dz, y) / Vector256.Create(BlueNormalization);
            Vector256<float> r = Vector256.MultiplyAddEstimate(Vector256.Create(2F), dx, Vector256.Create(RedGreenContribution) * y);
            y = r;
            dz = g;
            dx = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> y, ref Vector512<float> dz, ref Vector512<float> dx, in HeifColorConversionParameters parameters)
        {
            Vector512<float> g = y;
            Vector512<float> b = Vector512.MultiplyAddEstimate(Vector512.Create(2F), dz, y) / Vector512.Create(BlueNormalization);
            Vector512<float> r = Vector512.MultiplyAddEstimate(Vector512.Create(2F), dx, Vector512.Create(RedGreenContribution) * y);
            y = r;
            dz = g;
            dx = b;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            float r,
            float g,
            float b,
            in HeifColorConversionParameters parameters,
            out float y,
            out float dz,
            out float dx)
        {
            // Y is the green primary; Dz and Dx are half-scaled blue and red differences.
            y = g;
            dz = ((BlueNormalization * b) - y) * 0.5F;
            dx = (r - (RedGreenContribution * y)) * 0.5F;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<float> r,
            Vector128<float> g,
            Vector128<float> b,
            in HeifColorConversionParameters parameters,
            out Vector128<float> y,
            out Vector128<float> dz,
            out Vector128<float> dx)
        {
            y = g;
            dz = Vector128.Create(0.5F) * Vector128.MultiplyAddEstimate(Vector128.Create(BlueNormalization), b, -y);
            dx = Vector128.Create(0.5F) * Vector128.MultiplyAddEstimate(Vector128.Create(-RedGreenContribution), y, r);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<float> r,
            Vector256<float> g,
            Vector256<float> b,
            in HeifColorConversionParameters parameters,
            out Vector256<float> y,
            out Vector256<float> dz,
            out Vector256<float> dx)
        {
            y = g;
            dz = Vector256.Create(0.5F) * Vector256.MultiplyAddEstimate(Vector256.Create(BlueNormalization), b, -y);
            dx = Vector256.Create(0.5F) * Vector256.MultiplyAddEstimate(Vector256.Create(-RedGreenContribution), y, r);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<float> r,
            Vector512<float> g,
            Vector512<float> b,
            in HeifColorConversionParameters parameters,
            out Vector512<float> y,
            out Vector512<float> dz,
            out Vector512<float> dx)
        {
            y = g;
            dz = Vector512.Create(0.5F) * Vector512.MultiplyAddEstimate(Vector512.Create(BlueNormalization), b, -y);
            dx = Vector512.Create(0.5F) * Vector512.MultiplyAddEstimate(Vector512.Create(-RedGreenContribution), y, r);
        }
    }
}

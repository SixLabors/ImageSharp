// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides full-range fixed-point coefficient conversion. Each signed 32-bit lane carries one luma or duplicated
/// chroma sample. Matrix coefficients use a common fixed-point scale; every component rounds once after its complete
/// weighted sum, then clips before the enclosing row kernel narrows and packs the RGB result.
/// </content>
internal static partial class HeifYuv420ToRgb8Converter
{
    /// <summary>
    /// Implements full-range coefficient-based YCbCr conversion for scalar and SIMD lanes.
    /// </summary>
    private readonly struct FixedPointCoefficientOperator : IHeifYuv420ToRgb8Operator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(ushort y, ushort cb, ushort cr, in FixedPointParameters parameters, out byte r, out byte g, out byte b)
        {
            int centeredBlue = cb - ChromaMidpoint;
            int centeredRed = cr - ChromaMidpoint;

            // All overloads preserve this term grouping and round once after the complete contribution for a
            // component has been accumulated, so vector width cannot change an output code value.
            int red = y + (((parameters.RedCr * centeredRed) + RoundingBias) >> CoefficientShift);
            int green = y + (((parameters.GreenCb * centeredBlue) + (parameters.GreenCr * centeredRed) + RoundingBias) >> CoefficientShift);
            int blue = y + (((parameters.BlueCb * centeredBlue) + RoundingBias) >> CoefficientShift);

            r = (byte)Numerics.Clamp(red, 0, byte.MaxValue);
            g = (byte)Numerics.Clamp(green, 0, byte.MaxValue);
            b = (byte)Numerics.Clamp(blue, 0, byte.MaxValue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            Vector128<int> y,
            Vector128<int> cb,
            Vector128<int> cr,
            in Vector128Parameters parameters,
            out Vector128<int> r,
            out Vector128<int> g,
            out Vector128<int> b)
        {
            cb -= parameters.ChromaMidpoint;
            cr -= parameters.ChromaMidpoint;
            r = Vector128.Clamp(y + (((parameters.RedCr * cr) + parameters.RoundingBias) >> CoefficientShift), default, parameters.Maximum);
            g = Vector128.Clamp(y + (((parameters.GreenCb * cb) + (parameters.GreenCr * cr) + parameters.RoundingBias) >> CoefficientShift), default, parameters.Maximum);
            b = Vector128.Clamp(y + (((parameters.BlueCb * cb) + parameters.RoundingBias) >> CoefficientShift), default, parameters.Maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            Vector256<int> y,
            Vector256<int> cb,
            Vector256<int> cr,
            in Vector256Parameters parameters,
            out Vector256<int> r,
            out Vector256<int> g,
            out Vector256<int> b)
        {
            cb -= parameters.ChromaMidpoint;
            cr -= parameters.ChromaMidpoint;
            r = Vector256.Clamp(y + (((parameters.RedCr * cr) + parameters.RoundingBias) >> CoefficientShift), default, parameters.Maximum);
            g = Vector256.Clamp(y + (((parameters.GreenCb * cb) + (parameters.GreenCr * cr) + parameters.RoundingBias) >> CoefficientShift), default, parameters.Maximum);
            b = Vector256.Clamp(y + (((parameters.BlueCb * cb) + parameters.RoundingBias) >> CoefficientShift), default, parameters.Maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            Vector512<int> y,
            Vector512<int> cb,
            Vector512<int> cr,
            in Vector512Parameters parameters,
            out Vector512<int> r,
            out Vector512<int> g,
            out Vector512<int> b)
        {
            cb -= parameters.ChromaMidpoint;
            cr -= parameters.ChromaMidpoint;
            r = Vector512.Clamp(y + (((parameters.RedCr * cr) + parameters.RoundingBias) >> CoefficientShift), default, parameters.Maximum);
            g = Vector512.Clamp(y + (((parameters.GreenCb * cb) + (parameters.GreenCr * cr) + parameters.RoundingBias) >> CoefficientShift), default, parameters.Maximum);
            b = Vector512.Clamp(y + (((parameters.BlueCb * cb) + parameters.RoundingBias) >> CoefficientShift), default, parameters.Maximum);
        }
    }
}

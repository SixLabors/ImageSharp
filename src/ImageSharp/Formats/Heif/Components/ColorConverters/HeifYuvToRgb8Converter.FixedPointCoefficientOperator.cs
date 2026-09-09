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
internal static partial class HeifYuvToRgb8Converter
{
    /// <summary>
    /// Implements full-range coefficient-based YCbCr conversion for scalar and SIMD lanes.
    /// </summary>
    private readonly struct FixedPointCoefficientOperator : IHeifYuvToRgb8Operator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            Vector512<int> y,
            Vector512<int> cb,
            Vector512<int> cr,
            in ConversionParameters parameters,
            out Vector512<int> r,
            out Vector512<int> g,
            out Vector512<int> b)
        {
            Vector512Parameters fixedPoint = parameters.FixedPointSixteenLane;
            cb -= fixedPoint.ChromaMidpoint;
            cr -= fixedPoint.ChromaMidpoint;
            r = Vector512.Clamp(y + (((fixedPoint.RedCr * cr) + fixedPoint.RoundingBias) >> CoefficientShift), default, fixedPoint.Maximum);
            g = Vector512.Clamp(y + (((fixedPoint.GreenCb * cb) + (fixedPoint.GreenCr * cr) + fixedPoint.RoundingBias) >> CoefficientShift), default, fixedPoint.Maximum);
            b = Vector512.Clamp(y + (((fixedPoint.BlueCb * cb) + fixedPoint.RoundingBias) >> CoefficientShift), default, fixedPoint.Maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            Vector256<int> y,
            Vector256<int> cb,
            Vector256<int> cr,
            in ConversionParameters parameters,
            out Vector256<int> r,
            out Vector256<int> g,
            out Vector256<int> b)
        {
            Vector256Parameters fixedPoint = parameters.FixedPointEightLane;
            cb -= fixedPoint.ChromaMidpoint;
            cr -= fixedPoint.ChromaMidpoint;
            r = Vector256.Clamp(y + (((fixedPoint.RedCr * cr) + fixedPoint.RoundingBias) >> CoefficientShift), default, fixedPoint.Maximum);
            g = Vector256.Clamp(y + (((fixedPoint.GreenCb * cb) + (fixedPoint.GreenCr * cr) + fixedPoint.RoundingBias) >> CoefficientShift), default, fixedPoint.Maximum);
            b = Vector256.Clamp(y + (((fixedPoint.BlueCb * cb) + fixedPoint.RoundingBias) >> CoefficientShift), default, fixedPoint.Maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            Vector128<int> y,
            Vector128<int> cb,
            Vector128<int> cr,
            in ConversionParameters parameters,
            out Vector128<int> r,
            out Vector128<int> g,
            out Vector128<int> b)
        {
            Vector128Parameters fixedPoint = parameters.FixedPointFourLane;
            cb -= fixedPoint.ChromaMidpoint;
            cr -= fixedPoint.ChromaMidpoint;
            r = Vector128.Clamp(y + (((fixedPoint.RedCr * cr) + fixedPoint.RoundingBias) >> CoefficientShift), default, fixedPoint.Maximum);
            g = Vector128.Clamp(y + (((fixedPoint.GreenCb * cb) + (fixedPoint.GreenCr * cr) + fixedPoint.RoundingBias) >> CoefficientShift), default, fixedPoint.Maximum);
            b = Vector128.Clamp(y + (((fixedPoint.BlueCb * cb) + fixedPoint.RoundingBias) >> CoefficientShift), default, fixedPoint.Maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(ushort y, ushort cb, ushort cr, in ConversionParameters parameters, out byte r, out byte g, out byte b)
        {
            FixedPointParameters fixedPoint = parameters.FixedPointScalar;
            int centeredBlue = cb - ChromaMidpoint;
            int centeredRed = cr - ChromaMidpoint;

            // All overloads preserve this term grouping and round once after the complete contribution for a
            // component has been accumulated, so vector width cannot change an output code value.
            int red = y + (((fixedPoint.RedCr * centeredRed) + RoundingBias) >> CoefficientShift);
            int green = y + (((fixedPoint.GreenCb * centeredBlue) + (fixedPoint.GreenCr * centeredRed) + RoundingBias) >> CoefficientShift);
            int blue = y + (((fixedPoint.BlueCb * centeredBlue) + RoundingBias) >> CoefficientShift);

            r = (byte)Numerics.Clamp(red, 0, byte.MaxValue);
            g = (byte)Numerics.Clamp(green, 0, byte.MaxValue);
            b = (byte)Numerics.Clamp(blue, 0, byte.MaxValue);
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides the coefficient conversion executed by pinned libheif 1.23.1. Each SIMD lane carries one output pixel.
/// Range expansion and matrix arithmetic remain in single precision, RGB is rounded and clipped at the coded
/// precision, and the final integer shift reproduces libheif's separate high-bit-depth-to-eight-bit operation.
/// </content>
internal static partial class HeifYuvToRgb8Converter
{
    /// <summary>
    /// Implements pinned-libheif coefficient conversion for scalar and SIMD lanes.
    /// </summary>
    private readonly struct LibheifCoefficientOperator : IHeifYuvToRgb8Operator
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
            LibheifVector512Parameters values = parameters.LibheifSixteenLane;
            Vector512<float> luma = (Vector512.ConvertToSingle(y) - values.LumaOffset) * values.LumaScale;
            Vector512<float> blueDifference = (Vector512.ConvertToSingle(cb) - values.ChromaMidpoint) * values.ChromaScale;
            Vector512<float> redDifference = (Vector512.ConvertToSingle(cr) - values.ChromaMidpoint) * values.ChromaScale;
            Vector512<float> half = Vector512.Create(0.5F);

            // Sixteen independent samples use the same float32 ordering as the narrower paths. The closed operator
            // keeps this compatibility arithmetic outside the row dispatch while allowing an exact scalar fallback.
            Vector512<int> red = Vector512.ConvertToInt32(Vector512.Truncate(luma + (values.RedCr * redDifference) + half));
            Vector512<int> green = Vector512.ConvertToInt32(Vector512.Truncate(luma + (values.GreenCb * blueDifference) + (values.GreenCr * redDifference) + half));
            Vector512<int> blue = Vector512.ConvertToInt32(Vector512.Truncate(luma + (values.BlueCb * blueDifference) + half));

            r = Vector512.Clamp(red, default, values.Maximum) >> values.OutputShift;
            g = Vector512.Clamp(green, default, values.Maximum) >> values.OutputShift;
            b = Vector512.Clamp(blue, default, values.Maximum) >> values.OutputShift;
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
            LibheifVector256Parameters values = parameters.LibheifEightLane;
            Vector256<float> luma = (Vector256.ConvertToSingle(y) - values.LumaOffset) * values.LumaScale;
            Vector256<float> blueDifference = (Vector256.ConvertToSingle(cb) - values.ChromaMidpoint) * values.ChromaScale;
            Vector256<float> redDifference = (Vector256.ConvertToSingle(cr) - values.ChromaMidpoint) * values.ChromaScale;
            Vector256<float> half = Vector256.Create(0.5F);

            // Eight YUV tuples remain planar across the YMM arithmetic. The expression association matches the
            // pinned scalar source, including the two successive green additions before truncation.
            Vector256<int> red = Vector256.ConvertToInt32(Vector256.Truncate(luma + (values.RedCr * redDifference) + half));
            Vector256<int> green = Vector256.ConvertToInt32(Vector256.Truncate(luma + (values.GreenCb * blueDifference) + (values.GreenCr * redDifference) + half));
            Vector256<int> blue = Vector256.ConvertToInt32(Vector256.Truncate(luma + (values.BlueCb * blueDifference) + half));

            r = Vector256.Clamp(red, default, values.Maximum) >> values.OutputShift;
            g = Vector256.Clamp(green, default, values.Maximum) >> values.OutputShift;
            b = Vector256.Clamp(blue, default, values.Maximum) >> values.OutputShift;
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
            LibheifVector128Parameters values = parameters.LibheifFourLane;
            Vector128<float> luma = (Vector128.ConvertToSingle(y) - values.LumaOffset) * values.LumaScale;
            Vector128<float> blueDifference = (Vector128.ConvertToSingle(cb) - values.ChromaMidpoint) * values.ChromaScale;
            Vector128<float> redDifference = (Vector128.ConvertToSingle(cr) - values.ChromaMidpoint) * values.ChromaScale;
            Vector128<float> half = Vector128.Create(0.5F);

            // Truncate after the explicit half-unit bias to mirror C++ float-to-int conversion. Clipping in integer
            // lanes then preserves the source-precision boundary before the common eight-bit reduction shift.
            Vector128<int> red = Vector128.ConvertToInt32(Vector128.Truncate(luma + (values.RedCr * redDifference) + half));
            Vector128<int> green = Vector128.ConvertToInt32(Vector128.Truncate(luma + (values.GreenCb * blueDifference) + (values.GreenCr * redDifference) + half));
            Vector128<int> blue = Vector128.ConvertToInt32(Vector128.Truncate(luma + (values.BlueCb * blueDifference) + half));

            r = Vector128.Clamp(red, default, values.Maximum) >> values.OutputShift;
            g = Vector128.Clamp(green, default, values.Maximum) >> values.OutputShift;
            b = Vector128.Clamp(blue, default, values.Maximum) >> values.OutputShift;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(ushort y, ushort cb, ushort cr, in ConversionParameters parameters, out byte r, out byte g, out byte b)
        {
            LibheifParameters values = parameters.LibheifScalar;
            float luma = (y - values.LumaOffset) * values.LumaScale;
            float blueDifference = (cb - values.ChromaMidpoint) * values.ChromaScale;
            float redDifference = (cr - values.ChromaMidpoint) * values.ChromaScale;

            // libheif's clip_f_u16 adds one half, truncates toward zero, and then clips. RGB is rounded before
            // the high-bit-depth plane is reduced, so moving the shift into the floating-point scale changes bytes.
            int red = (int)(luma + (values.RedCr * redDifference) + 0.5F);
            int green = (int)(luma + (values.GreenCb * blueDifference) + (values.GreenCr * redDifference) + 0.5F);
            int blue = (int)(luma + (values.BlueCb * blueDifference) + 0.5F);

            r = (byte)(Numerics.Clamp(red, 0, values.Maximum) >> values.OutputShift);
            g = (byte)(Numerics.Clamp(green, 0, values.Maximum) >> values.OutputShift);
            b = (byte)(Numerics.Clamp(blue, 0, values.Maximum) >> values.OutputShift);
        }
    }
}

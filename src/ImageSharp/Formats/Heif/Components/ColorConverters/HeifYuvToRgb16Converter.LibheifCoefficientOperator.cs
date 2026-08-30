// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides pinned-libheif high-bit-depth coefficient conversion at the source RGB precision.
/// </content>
internal static partial class HeifYuvToRgb16Converter
{
    /// <summary>
    /// Implements pinned-libheif coefficient conversion for scalar and SIMD lanes.
    /// </summary>
    private readonly struct LibheifCoefficientOperator : IHeifYuvToRgb16Operator
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
            Vector512Parameters values = parameters.SixteenLane;
            Vector512<float> luma = (Vector512.ConvertToSingle(y) - values.LumaOffset) * values.LumaScale;
            Vector512<float> blueDifference = (Vector512.ConvertToSingle(cb) - values.ChromaMidpoint) * values.ChromaScale;
            Vector512<float> redDifference = (Vector512.ConvertToSingle(cr) - values.ChromaMidpoint) * values.ChromaScale;
            Vector512<float> half = Vector512.Create(0.5F);

            // libheif evaluates these as distinct float32 multiplies and adds; FMA changes some 12-bit results by one.
            Vector512<float> redValue = Vector512.Multiply(values.RedCr, redDifference);
            redValue = Vector512.Add(luma, redValue);
            Vector512<float> greenValue = Vector512.Multiply(values.GreenCb, blueDifference);
            greenValue = Vector512.Add(luma, greenValue);
            Vector512<float> greenRedValue = Vector512.Multiply(values.GreenCr, redDifference);
            greenValue = Vector512.Add(greenValue, greenRedValue);
            Vector512<float> blueValue = Vector512.Multiply(values.BlueCb, blueDifference);
            blueValue = Vector512.Add(luma, blueValue);
            Vector512<int> red = Vector512.ConvertToInt32(Vector512.Truncate(Vector512.Add(redValue, half)));
            Vector512<int> green = Vector512.ConvertToInt32(Vector512.Truncate(Vector512.Add(greenValue, half)));
            Vector512<int> blue = Vector512.ConvertToInt32(Vector512.Truncate(Vector512.Add(blueValue, half)));

            r = Vector512.Clamp(red, default, values.Maximum);
            g = Vector512.Clamp(green, default, values.Maximum);
            b = Vector512.Clamp(blue, default, values.Maximum);
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
            Vector256Parameters values = parameters.EightLane;
            Vector256<float> luma = (Vector256.ConvertToSingle(y) - values.LumaOffset) * values.LumaScale;
            Vector256<float> blueDifference = (Vector256.ConvertToSingle(cb) - values.ChromaMidpoint) * values.ChromaScale;
            Vector256<float> redDifference = (Vector256.ConvertToSingle(cr) - values.ChromaMidpoint) * values.ChromaScale;
            Vector256<float> half = Vector256.Create(0.5F);
            Vector256<float> redValue = Vector256.Multiply(values.RedCr, redDifference);
            redValue = Vector256.Add(luma, redValue);
            Vector256<float> greenValue = Vector256.Multiply(values.GreenCb, blueDifference);
            greenValue = Vector256.Add(luma, greenValue);
            Vector256<float> greenRedValue = Vector256.Multiply(values.GreenCr, redDifference);
            greenValue = Vector256.Add(greenValue, greenRedValue);
            Vector256<float> blueValue = Vector256.Multiply(values.BlueCb, blueDifference);
            blueValue = Vector256.Add(luma, blueValue);
            Vector256<int> red = Vector256.ConvertToInt32(Vector256.Truncate(Vector256.Add(redValue, half)));
            Vector256<int> green = Vector256.ConvertToInt32(Vector256.Truncate(Vector256.Add(greenValue, half)));
            Vector256<int> blue = Vector256.ConvertToInt32(Vector256.Truncate(Vector256.Add(blueValue, half)));

            r = Vector256.Clamp(red, default, values.Maximum);
            g = Vector256.Clamp(green, default, values.Maximum);
            b = Vector256.Clamp(blue, default, values.Maximum);
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
            Vector128Parameters values = parameters.FourLane;
            Vector128<float> luma = (Vector128.ConvertToSingle(y) - values.LumaOffset) * values.LumaScale;
            Vector128<float> blueDifference = (Vector128.ConvertToSingle(cb) - values.ChromaMidpoint) * values.ChromaScale;
            Vector128<float> redDifference = (Vector128.ConvertToSingle(cr) - values.ChromaMidpoint) * values.ChromaScale;
            Vector128<float> half = Vector128.Create(0.5F);
            Vector128<float> redValue = Vector128.Multiply(values.RedCr, redDifference);
            redValue = Vector128.Add(luma, redValue);
            Vector128<float> greenValue = Vector128.Multiply(values.GreenCb, blueDifference);
            greenValue = Vector128.Add(luma, greenValue);
            Vector128<float> greenRedValue = Vector128.Multiply(values.GreenCr, redDifference);
            greenValue = Vector128.Add(greenValue, greenRedValue);
            Vector128<float> blueValue = Vector128.Multiply(values.BlueCb, blueDifference);
            blueValue = Vector128.Add(luma, blueValue);
            Vector128<int> red = Vector128.ConvertToInt32(Vector128.Truncate(Vector128.Add(redValue, half)));
            Vector128<int> green = Vector128.ConvertToInt32(Vector128.Truncate(Vector128.Add(greenValue, half)));
            Vector128<int> blue = Vector128.ConvertToInt32(Vector128.Truncate(Vector128.Add(blueValue, half)));

            r = Vector128.Clamp(red, default, values.Maximum);
            g = Vector128.Clamp(green, default, values.Maximum);
            b = Vector128.Clamp(blue, default, values.Maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            ushort y,
            ushort cb,
            ushort cr,
            in ConversionParameters parameters,
            out int r,
            out int g,
            out int b)
        {
            ScalarParameters values = parameters.Scalar;
            float luma = (y - values.LumaOffset) * values.LumaScale;
            float blueDifference = (cb - values.ChromaMidpoint) * values.ChromaScale;
            float redDifference = (cr - values.ChromaMidpoint) * values.ChromaScale;

            // Keep each assignment separate so the JIT cannot fuse the reference float32 operations.
            float redValue = values.RedCr * redDifference;
            redValue = luma + redValue;
            float greenValue = values.GreenCb * blueDifference;
            greenValue = luma + greenValue;
            float greenRedValue = values.GreenCr * redDifference;
            greenValue += greenRedValue;
            float blueValue = values.BlueCb * blueDifference;
            blueValue = luma + blueValue;

            r = Numerics.Clamp((int)(redValue + 0.5F), 0, values.Maximum);
            g = Numerics.Clamp((int)(greenValue + 0.5F), 0, values.Maximum);
            b = Numerics.Clamp((int)(blueValue + 0.5F), 0, values.Maximum);
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

internal static partial class Av1ForwardQuantizer
{
    /// <summary>
    /// Quantizes high-bit-depth magnitudes without the sixteen-bit saturation used for byte samples.
    /// </summary>
    public readonly struct HighBitDepthRegularQuantizationOperator : IRegularQuantizationOperator
    {
        /// <inheritdoc/>
        public static Vector128<int> Quantize(
            Vector128<int> coefficients,
            Vector128<int> zeroBin,
            Vector128<int> rounding,
            Vector128<int> quantizer,
            Vector128<int> shift,
            Vector128<int> dequantizer,
            int logScale,
            out Vector128<int> dequantizedCoefficients)
        {
            Vector128<int> sign = coefficients >> 31;
            Vector128<int> magnitude = Vector128.Abs(coefficients);
            Vector128<int> mask = ~Vector128.GreaterThan(zeroBin, magnitude);
            Vector128<int> rounded = magnitude + rounding;

            // Preserve signed products in two widened halves. The reciprocal correction can be negative;
            // an arithmetic Q16 shift restores its implicit leading bit before the second multiplication.
            Vector128<long> lower = Vector128.WidenLower(rounded);
            Vector128<long> upper = Vector128.WidenUpper(rounded);
            lower += (lower * Vector128.WidenLower(quantizer)) >> 16;
            upper += (upper * Vector128.WidenUpper(quantizer)) >> 16;
            lower = (lower * Vector128.WidenLower(shift)) >> (16 - logScale);
            upper = (upper * Vector128.WidenUpper(shift)) >> (16 - logScale);
            Vector128<int> quantizedMagnitude = Vector128.Narrow(lower, upper) & mask;
            Vector128<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }

        /// <inheritdoc/>
        public static Vector256<int> Quantize(
            Vector256<int> coefficients,
            Vector256<int> zeroBin,
            Vector256<int> rounding,
            Vector256<int> quantizer,
            Vector256<int> shift,
            Vector256<int> dequantizer,
            int logScale,
            out Vector256<int> dequantizedCoefficients)
        {
            Vector256<int> sign = coefficients >> 31;
            Vector256<int> magnitude = Vector256.Abs(coefficients);
            Vector256<int> mask = ~Vector256.GreaterThan(zeroBin, magnitude);
            Vector256<int> rounded = magnitude + rounding;

            // Preserve signed products in two widened halves. The reciprocal correction can be negative;
            // an arithmetic Q16 shift restores its implicit leading bit before the second multiplication.
            Vector256<long> lower = Vector256.WidenLower(rounded);
            Vector256<long> upper = Vector256.WidenUpper(rounded);
            lower += (lower * Vector256.WidenLower(quantizer)) >> 16;
            upper += (upper * Vector256.WidenUpper(quantizer)) >> 16;
            lower = (lower * Vector256.WidenLower(shift)) >> (16 - logScale);
            upper = (upper * Vector256.WidenUpper(shift)) >> (16 - logScale);
            Vector256<int> quantizedMagnitude = Vector256.Narrow(lower, upper) & mask;
            Vector256<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }

        /// <inheritdoc/>
        public static Vector512<int> Quantize(
            Vector512<int> coefficients,
            Vector512<int> zeroBin,
            Vector512<int> rounding,
            Vector512<int> quantizer,
            Vector512<int> shift,
            Vector512<int> dequantizer,
            int logScale,
            out Vector512<int> dequantizedCoefficients)
        {
            Vector512<int> sign = coefficients >> 31;
            Vector512<int> magnitude = Vector512.Abs(coefficients);
            Vector512<int> mask = ~Vector512.GreaterThan(zeroBin, magnitude);
            Vector512<int> rounded = magnitude + rounding;

            // Preserve signed products in two widened halves. The reciprocal correction can be negative;
            // an arithmetic Q16 shift restores its implicit leading bit before the second multiplication.
            Vector512<long> lower = Vector512.WidenLower(rounded);
            Vector512<long> upper = Vector512.WidenUpper(rounded);
            lower += (lower * Vector512.WidenLower(quantizer)) >> 16;
            upper += (upper * Vector512.WidenUpper(quantizer)) >> 16;
            lower = (lower * Vector512.WidenLower(shift)) >> (16 - logScale);
            upper = (upper * Vector512.WidenUpper(shift)) >> (16 - logScale);
            Vector512<int> quantizedMagnitude = Vector512.Narrow(lower, upper) & mask;
            Vector512<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }

        /// <inheritdoc/>
        public static int Quantize(
            int coefficients,
            int zeroBin,
            int rounding,
            int quantizer,
            int shift,
            int dequantizer,
            int logScale,
            out int dequantizedCoefficients)
        {
            int sign = coefficients >> 31;
            int magnitude = (coefficients ^ sign) - sign;
            int quantizedMagnitude = 0;
            if (magnitude >= zeroBin)
            {
                long rounded = (long)magnitude + rounding;
                long corrected = rounded + ((rounded * quantizer) >> 16);
                quantizedMagnitude = (int)((corrected * shift) >> (16 - logScale));
            }

            int dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }
    }
}

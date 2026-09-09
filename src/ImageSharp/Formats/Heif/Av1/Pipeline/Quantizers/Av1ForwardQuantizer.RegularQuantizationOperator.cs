// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

internal static partial class Av1ForwardQuantizer
{
    /// <summary>
    /// Quantizes byte-source coefficients with saturated sixteen-bit rounded magnitudes.
    /// </summary>
    public readonly struct RegularQuantizationOperator : IRegularQuantizationOperator
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
            Vector128<int> rounded = Vector128.Min(magnitude + rounding, Vector128.Create((int)short.MaxValue));

            // Saturation bounds both signed products to 32-bit lanes. The first Q16 multiplication
            // restores the reciprocal's implicit leading bit; the second removes its power-of-two scale.
            Vector128<int> corrected = rounded + ((rounded * quantizer) >> 16);
            Vector128<int> quantizedMagnitude = ((corrected * shift) >> (16 - logScale)) & mask;
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
            Vector256<int> rounded = Vector256.Min(magnitude + rounding, Vector256.Create((int)short.MaxValue));

            // Saturation bounds both signed products to 32-bit lanes. The first Q16 multiplication
            // restores the reciprocal's implicit leading bit; the second removes its power-of-two scale.
            Vector256<int> corrected = rounded + ((rounded * quantizer) >> 16);
            Vector256<int> quantizedMagnitude = ((corrected * shift) >> (16 - logScale)) & mask;
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
            Vector512<int> rounded = Vector512.Min(magnitude + rounding, Vector512.Create((int)short.MaxValue));

            // Saturation bounds both signed products to 32-bit lanes. The first Q16 multiplication
            // restores the reciprocal's implicit leading bit; the second removes its power-of-two scale.
            Vector512<int> corrected = rounded + ((rounded * quantizer) >> 16);
            Vector512<int> quantizedMagnitude = ((corrected * shift) >> (16 - logScale)) & mask;
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
                int rounded = Math.Min(magnitude + rounding, short.MaxValue);
                int corrected = rounded + ((rounded * quantizer) >> 16);
                quantizedMagnitude = (corrected * shift) >> (16 - logScale);
            }

            int dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }
    }
}

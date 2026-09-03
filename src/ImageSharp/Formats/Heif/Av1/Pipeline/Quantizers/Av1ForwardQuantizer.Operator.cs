// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

/// <content>
/// Defines the arithmetic operators used by <see cref="Av1ForwardQuantizer"/>.
/// </content>
internal static partial class Av1ForwardQuantizer
{
    /// <summary>
    /// Defines one AV1 forward-quantization arithmetic contract across hardware widths.
    /// </summary>
    internal interface IForwardQuantizationOperator
    {
        /// <summary>
        /// Quantizes four raster-order transform coefficients.
        /// </summary>
        /// <param name="coefficients">The signed transform coefficients.</param>
        /// <param name="rounding">The positive rounding constant after transform-size scaling.</param>
        /// <param name="quantizer">The Q16 reciprocal quantizer.</param>
        /// <param name="dequantizer">The Q3 reconstruction quantizer.</param>
        /// <param name="logScale">The transform-size quantization scale.</param>
        /// <param name="dequantizedCoefficients">The signed reconstruction coefficients.</param>
        /// <returns>The signed entropy-coding coefficients.</returns>
        public static abstract Vector128<int> Quantize(
            Vector128<int> coefficients,
            Vector128<int> rounding,
            Vector128<int> quantizer,
            Vector128<int> dequantizer,
            int logScale,
            out Vector128<int> dequantizedCoefficients);

        /// <summary>
        /// Quantizes eight raster-order transform coefficients.
        /// </summary>
        /// <param name="coefficients">The signed transform coefficients.</param>
        /// <param name="rounding">The positive rounding constant after transform-size scaling.</param>
        /// <param name="quantizer">The Q16 reciprocal quantizer.</param>
        /// <param name="dequantizer">The Q3 reconstruction quantizer.</param>
        /// <param name="logScale">The transform-size quantization scale.</param>
        /// <param name="dequantizedCoefficients">The signed reconstruction coefficients.</param>
        /// <returns>The signed entropy-coding coefficients.</returns>
        public static abstract Vector256<int> Quantize(
            Vector256<int> coefficients,
            Vector256<int> rounding,
            Vector256<int> quantizer,
            Vector256<int> dequantizer,
            int logScale,
            out Vector256<int> dequantizedCoefficients);

        /// <summary>
        /// Quantizes sixteen raster-order transform coefficients.
        /// </summary>
        /// <param name="coefficients">The signed transform coefficients.</param>
        /// <param name="rounding">The positive rounding constant after transform-size scaling.</param>
        /// <param name="quantizer">The Q16 reciprocal quantizer.</param>
        /// <param name="dequantizer">The Q3 reconstruction quantizer.</param>
        /// <param name="logScale">The transform-size quantization scale.</param>
        /// <param name="dequantizedCoefficients">The signed reconstruction coefficients.</param>
        /// <returns>The signed entropy-coding coefficients.</returns>
        public static abstract Vector512<int> Quantize(
            Vector512<int> coefficients,
            Vector512<int> rounding,
            Vector512<int> quantizer,
            Vector512<int> dequantizer,
            int logScale,
            out Vector512<int> dequantizedCoefficients);

        /// <summary>
        /// Quantizes one transform coefficient.
        /// </summary>
        /// <param name="coefficient">The signed transform coefficient.</param>
        /// <param name="rounding">The positive rounding constant after transform-size scaling.</param>
        /// <param name="quantizer">The Q16 reciprocal quantizer.</param>
        /// <param name="dequantizer">The Q3 reconstruction quantizer.</param>
        /// <param name="logScale">The transform-size quantization scale.</param>
        /// <param name="dequantizedCoefficient">The signed reconstruction coefficient.</param>
        /// <returns>The signed entropy-coding coefficient.</returns>
        public static abstract int Quantize(
            int coefficient,
            int rounding,
            int quantizer,
            int dequantizer,
            int logScale,
            out int dequantizedCoefficient);
    }

    /// <summary>
    /// Implements libaom's fast no-matrix quantizer for lossy transform blocks.
    /// </summary>
    /// <remarks>
    /// Every SIMD overload preserves the scalar operation order: magnitude threshold, saturating round, Q16 reciprocal
    /// multiply, transform-size shift, dequantization, and sign restoration. Each lane owns one raster-order coefficient.
    /// </remarks>
    internal readonly struct FastQuantizationOperator : IForwardQuantizationOperator
    {
        /// <inheritdoc/>
        public static Vector128<int> Quantize(
            Vector128<int> coefficients,
            Vector128<int> rounding,
            Vector128<int> quantizer,
            Vector128<int> dequantizer,
            int logScale,
            out Vector128<int> dequantizedCoefficients)
        {
            Vector128<int> coefficientSign = Vector128.ShiftRightArithmetic(coefficients, 31);
            Vector128<int> magnitude = Vector128.Abs(coefficients);

            // libaom retains equality at the dequantizer threshold. Reversing the comparison and complementing its mask
            // expresses scaledMagnitude >= dequantizer with the vector operations available for every supported ISA.
            Vector128<int> thresholdMask = ~Vector128.GreaterThan(dequantizer, magnitude << (1 + logScale));

            // Clamp the rounded magnitude to 32,767 so the following Q16 product remains within a signed 32-bit lane.
            Vector128<int> rounded = Vector128.Min(magnitude + rounding, Vector128.Create((int)short.MaxValue));
            Vector128<int> quantizedMagnitude = ((rounded * quantizer) >> (16 - logScale)) & thresholdMask;

            // XOR followed by subtraction restores the original sign without a lane-wise branch.
            Vector128<int> quantized = (quantizedMagnitude ^ coefficientSign) - coefficientSign;
            Vector128<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ coefficientSign) - coefficientSign;
            return quantized;
        }

        /// <inheritdoc/>
        public static Vector256<int> Quantize(
            Vector256<int> coefficients,
            Vector256<int> rounding,
            Vector256<int> quantizer,
            Vector256<int> dequantizer,
            int logScale,
            out Vector256<int> dequantizedCoefficients)
        {
            Vector256<int> coefficientSign = Vector256.ShiftRightArithmetic(coefficients, 31);
            Vector256<int> magnitude = Vector256.Abs(coefficients);

            // Preserve threshold equality by complementing dequantizer > scaledMagnitude.
            Vector256<int> thresholdMask = ~Vector256.GreaterThan(dequantizer, magnitude << (1 + logScale));

            // Clamp the rounded magnitude to 32,767 so the following Q16 product remains within a signed 32-bit lane.
            Vector256<int> rounded = Vector256.Min(magnitude + rounding, Vector256.Create((int)short.MaxValue));
            Vector256<int> quantizedMagnitude = ((rounded * quantizer) >> (16 - logScale)) & thresholdMask;

            // Apply the input sign to both coded and reconstructed magnitudes without branching.
            Vector256<int> quantized = (quantizedMagnitude ^ coefficientSign) - coefficientSign;
            Vector256<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ coefficientSign) - coefficientSign;
            return quantized;
        }

        /// <inheritdoc/>
        public static Vector512<int> Quantize(
            Vector512<int> coefficients,
            Vector512<int> rounding,
            Vector512<int> quantizer,
            Vector512<int> dequantizer,
            int logScale,
            out Vector512<int> dequantizedCoefficients)
        {
            Vector512<int> coefficientSign = Vector512.ShiftRightArithmetic(coefficients, 31);
            Vector512<int> magnitude = Vector512.Abs(coefficients);

            // Preserve threshold equality by complementing dequantizer > scaledMagnitude.
            Vector512<int> thresholdMask = ~Vector512.GreaterThan(dequantizer, magnitude << (1 + logScale));

            // Clamp the rounded magnitude to 32,767 so the following Q16 product remains within a signed 32-bit lane.
            Vector512<int> rounded = Vector512.Min(magnitude + rounding, Vector512.Create((int)short.MaxValue));
            Vector512<int> quantizedMagnitude = ((rounded * quantizer) >> (16 - logScale)) & thresholdMask;

            // Apply the input sign to both coded and reconstructed magnitudes without branching.
            Vector512<int> quantized = (quantizedMagnitude ^ coefficientSign) - coefficientSign;
            Vector512<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ coefficientSign) - coefficientSign;
            return quantized;
        }

        /// <inheritdoc/>
        public static int Quantize(
            int coefficient,
            int rounding,
            int quantizer,
            int dequantizer,
            int logScale,
            out int dequantizedCoefficient)
        {
            int coefficientSign = coefficient >> 31;
            int magnitude = (coefficient ^ coefficientSign) - coefficientSign;
            int quantizedMagnitude = 0;

            // The scalar tail keeps the same threshold, clamp, and fixed-point operation order as every vector lane.
            if (((long)magnitude << (1 + logScale)) >= dequantizer)
            {
                int rounded = Math.Min(magnitude + rounding, short.MaxValue);
                quantizedMagnitude = (rounded * quantizer) >> (16 - logScale);
            }

            int quantized = (quantizedMagnitude ^ coefficientSign) - coefficientSign;
            int dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficient = (dequantizedMagnitude ^ coefficientSign) - coefficientSign;
            return quantized;
        }
    }

    /// <summary>
    /// Implements fast no-matrix quantization without truncating high-bit-depth transform magnitudes.
    /// </summary>
    /// <remarks>
    /// The reciprocal product is widened to 64 bits because 10-bit and 12-bit transforms can exceed the signed
    /// 16-bit range. Each SIMD overload preserves the scalar fixed-point operation order in every lane.
    /// </remarks>
    internal readonly struct HighBitDepthFastQuantizationOperator : IForwardQuantizationOperator
    {
        /// <inheritdoc/>
        public static Vector128<int> Quantize(
            Vector128<int> coefficients,
            Vector128<int> rounding,
            Vector128<int> quantizer,
            Vector128<int> dequantizer,
            int logScale,
            out Vector128<int> dequantizedCoefficients)
        {
            Vector128<int> coefficientSign = Vector128.ShiftRightArithmetic(coefficients, 31);
            Vector128<int> magnitude = Vector128.Abs(coefficients);
            Vector128<int> thresholdMask = ~Vector128.GreaterThan(dequantizer, magnitude << (1 + logScale));
            Vector128<int> rounded = magnitude + rounding;

            // Widen before multiplying so transform magnitudes above 32,767 retain their full precision.
            Vector128<long> lowerProduct = Vector128.WidenLower(rounded) * Vector128.WidenLower(quantizer);
            Vector128<long> upperProduct = Vector128.WidenUpper(rounded) * Vector128.WidenUpper(quantizer);
            Vector128<int> quantizedMagnitude =
                Vector128.Narrow(lowerProduct >> (16 - logScale), upperProduct >> (16 - logScale)) & thresholdMask;

            Vector128<int> quantized = (quantizedMagnitude ^ coefficientSign) - coefficientSign;
            Vector128<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ coefficientSign) - coefficientSign;
            return quantized;
        }

        /// <inheritdoc/>
        public static Vector256<int> Quantize(
            Vector256<int> coefficients,
            Vector256<int> rounding,
            Vector256<int> quantizer,
            Vector256<int> dequantizer,
            int logScale,
            out Vector256<int> dequantizedCoefficients)
        {
            Vector256<int> coefficientSign = Vector256.ShiftRightArithmetic(coefficients, 31);
            Vector256<int> magnitude = Vector256.Abs(coefficients);
            Vector256<int> thresholdMask = ~Vector256.GreaterThan(dequantizer, magnitude << (1 + logScale));
            Vector256<int> rounded = magnitude + rounding;

            // Widen before multiplying so transform magnitudes above 32,767 retain their full precision.
            Vector256<long> lowerProduct = Vector256.WidenLower(rounded) * Vector256.WidenLower(quantizer);
            Vector256<long> upperProduct = Vector256.WidenUpper(rounded) * Vector256.WidenUpper(quantizer);
            Vector256<int> quantizedMagnitude =
                Vector256.Narrow(lowerProduct >> (16 - logScale), upperProduct >> (16 - logScale)) & thresholdMask;

            Vector256<int> quantized = (quantizedMagnitude ^ coefficientSign) - coefficientSign;
            Vector256<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ coefficientSign) - coefficientSign;
            return quantized;
        }

        /// <inheritdoc/>
        public static Vector512<int> Quantize(
            Vector512<int> coefficients,
            Vector512<int> rounding,
            Vector512<int> quantizer,
            Vector512<int> dequantizer,
            int logScale,
            out Vector512<int> dequantizedCoefficients)
        {
            Vector512<int> coefficientSign = Vector512.ShiftRightArithmetic(coefficients, 31);
            Vector512<int> magnitude = Vector512.Abs(coefficients);
            Vector512<int> thresholdMask = ~Vector512.GreaterThan(dequantizer, magnitude << (1 + logScale));
            Vector512<int> rounded = magnitude + rounding;

            // Widen before multiplying so transform magnitudes above 32,767 retain their full precision.
            Vector512<long> lowerProduct = Vector512.WidenLower(rounded) * Vector512.WidenLower(quantizer);
            Vector512<long> upperProduct = Vector512.WidenUpper(rounded) * Vector512.WidenUpper(quantizer);
            Vector512<int> quantizedMagnitude =
                Vector512.Narrow(lowerProduct >> (16 - logScale), upperProduct >> (16 - logScale)) & thresholdMask;

            Vector512<int> quantized = (quantizedMagnitude ^ coefficientSign) - coefficientSign;
            Vector512<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ coefficientSign) - coefficientSign;
            return quantized;
        }

        /// <inheritdoc/>
        public static int Quantize(
            int coefficient,
            int rounding,
            int quantizer,
            int dequantizer,
            int logScale,
            out int dequantizedCoefficient)
        {
            int coefficientSign = coefficient >> 31;
            int magnitude = (coefficient ^ coefficientSign) - coefficientSign;
            int quantizedMagnitude = 0;

            if (((long)magnitude << (1 + logScale)) >= dequantizer)
            {
                quantizedMagnitude = (int)((((long)magnitude + rounding) * quantizer) >> (16 - logScale));
            }

            int quantized = (quantizedMagnitude ^ coefficientSign) - coefficientSign;
            int dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
            dequantizedCoefficient = (dequantizedMagnitude ^ coefficientSign) - coefficientSign;
            return quantized;
        }
    }

    /// <summary>
    /// Removes the reversible transform's fixed scale for entropy coding while retaining exact reconstruction coefficients.
    /// </summary>
    internal readonly struct LosslessQuantizationOperator : IForwardQuantizationOperator
    {
        /// <inheritdoc/>
        public static Vector128<int> Quantize(
            Vector128<int> coefficients,
            Vector128<int> rounding,
            Vector128<int> quantizer,
            Vector128<int> dequantizer,
            int logScale,
            out Vector128<int> dequantizedCoefficients)
        {
            dequantizedCoefficients = coefficients;
            return coefficients >> 2;
        }

        /// <inheritdoc/>
        public static Vector256<int> Quantize(
            Vector256<int> coefficients,
            Vector256<int> rounding,
            Vector256<int> quantizer,
            Vector256<int> dequantizer,
            int logScale,
            out Vector256<int> dequantizedCoefficients)
        {
            dequantizedCoefficients = coefficients;
            return coefficients >> 2;
        }

        /// <inheritdoc/>
        public static Vector512<int> Quantize(
            Vector512<int> coefficients,
            Vector512<int> rounding,
            Vector512<int> quantizer,
            Vector512<int> dequantizer,
            int logScale,
            out Vector512<int> dequantizedCoefficients)
        {
            dequantizedCoefficients = coefficients;
            return coefficients >> 2;
        }

        /// <inheritdoc/>
        public static int Quantize(
            int coefficient,
            int rounding,
            int quantizer,
            int dequantizer,
            int logScale,
            out int dequantizedCoefficient)
        {
            dequantizedCoefficient = coefficient;
            return coefficient >> 2;
        }
    }
}

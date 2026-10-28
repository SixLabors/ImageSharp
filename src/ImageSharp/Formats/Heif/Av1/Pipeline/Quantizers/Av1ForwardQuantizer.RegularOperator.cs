// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

internal static partial class Av1ForwardQuantizer
{
    /// <summary>
    /// Applies regular quantization while preserving the precision-specific rounded-magnitude contract.
    /// </summary>
    public interface IRegularQuantizationOperator
    {
        /// <summary>
        /// Applies the zero bin, corrected reciprocal, dequantization, and coefficient sign.
        /// </summary>
        /// <param name="coefficients">The signed transform coefficients.</param>
        /// <param name="zeroBin">The inclusive magnitude threshold after transform scaling.</param>
        /// <param name="rounding">The rounded magnitude adjustment.</param>
        /// <param name="quantizer">The signed reciprocal correction below its implicit leading bit.</param>
        /// <param name="shift">The power-of-two reciprocal scale.</param>
        /// <param name="dequantizer">The reconstruction quantizer.</param>
        /// <param name="logScale">The transform coefficient scale.</param>
        /// <param name="dequantizedCoefficients">The signed reconstruction coefficients.</param>
        /// <returns>The signed coding coefficients.</returns>
        static abstract Vector128<int> Quantize(
            Vector128<int> coefficients,
            Vector128<int> zeroBin,
            Vector128<int> rounding,
            Vector128<int> quantizer,
            Vector128<int> shift,
            Vector128<int> dequantizer,
            int logScale,
            out Vector128<int> dequantizedCoefficients);

        /// <summary>
        /// Applies the zero bin, corrected reciprocal, dequantization, and coefficient sign.
        /// </summary>
        /// <param name="coefficients">The signed transform coefficients.</param>
        /// <param name="zeroBin">The inclusive magnitude threshold after transform scaling.</param>
        /// <param name="rounding">The rounded magnitude adjustment.</param>
        /// <param name="quantizer">The signed reciprocal correction below its implicit leading bit.</param>
        /// <param name="shift">The power-of-two reciprocal scale.</param>
        /// <param name="dequantizer">The reconstruction quantizer.</param>
        /// <param name="logScale">The transform coefficient scale.</param>
        /// <param name="dequantizedCoefficients">The signed reconstruction coefficients.</param>
        /// <returns>The signed coding coefficients.</returns>
        static abstract Vector256<int> Quantize(
            Vector256<int> coefficients,
            Vector256<int> zeroBin,
            Vector256<int> rounding,
            Vector256<int> quantizer,
            Vector256<int> shift,
            Vector256<int> dequantizer,
            int logScale,
            out Vector256<int> dequantizedCoefficients);

        /// <summary>
        /// Applies the zero bin, corrected reciprocal, dequantization, and coefficient sign.
        /// </summary>
        /// <param name="coefficients">The signed transform coefficients.</param>
        /// <param name="zeroBin">The inclusive magnitude threshold after transform scaling.</param>
        /// <param name="rounding">The rounded magnitude adjustment.</param>
        /// <param name="quantizer">The signed reciprocal correction below its implicit leading bit.</param>
        /// <param name="shift">The power-of-two reciprocal scale.</param>
        /// <param name="dequantizer">The reconstruction quantizer.</param>
        /// <param name="logScale">The transform coefficient scale.</param>
        /// <param name="dequantizedCoefficients">The signed reconstruction coefficients.</param>
        /// <returns>The signed coding coefficients.</returns>
        static abstract Vector512<int> Quantize(
            Vector512<int> coefficients,
            Vector512<int> zeroBin,
            Vector512<int> rounding,
            Vector512<int> quantizer,
            Vector512<int> shift,
            Vector512<int> dequantizer,
            int logScale,
            out Vector512<int> dequantizedCoefficients);

        /// <summary>
        /// Applies the zero bin, corrected reciprocal, dequantization, and coefficient sign.
        /// </summary>
        /// <param name="coefficients">The signed transform coefficients.</param>
        /// <param name="zeroBin">The inclusive magnitude threshold after transform scaling.</param>
        /// <param name="rounding">The rounded magnitude adjustment.</param>
        /// <param name="quantizer">The signed reciprocal correction below its implicit leading bit.</param>
        /// <param name="shift">The power-of-two reciprocal scale.</param>
        /// <param name="dequantizer">The reconstruction quantizer.</param>
        /// <param name="logScale">The transform coefficient scale.</param>
        /// <param name="dequantizedCoefficients">The signed reconstruction coefficients.</param>
        /// <returns>The signed coding coefficients.</returns>
        static abstract int Quantize(
            int coefficients,
            int zeroBin,
            int rounding,
            int quantizer,
            int shift,
            int dequantizer,
            int logScale,
            out int dequantizedCoefficients);
    }
}

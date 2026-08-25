// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal abstract partial class Av1ColorConverterBase
{
    /// <summary>
    /// Implements H.273 constant-luminance conversion for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct Av1ConstantLuminanceColorOperator : IAv1ColorOperator
    {
        /// <inheritdoc/>
        public static bool ChromaUsesLumaRange => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float y, ref float cb, ref float cr, in Av1ColorConversionParameters parameters)
        {
            Av1ConstantLuminanceScales scales = parameters.ConstantLuminanceScales;

            // Constant-luminance chroma has different positive and negative divisors. Reconstruct nonlinear
            // red and blue first, then solve for green in linear light using the signaled transfer curve.
            float nonlinearBlue = y + (2F * (cb <= 0F ? scales.NegativeBlue : scales.PositiveBlue) * cb);
            float nonlinearRed = y + (2F * (cr <= 0F ? scales.NegativeRed : scales.PositiveRed) * cr);
            float linearY = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, y);
            float linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearBlue);
            float linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearRed);
            float linearGreen = (linearY - (parameters.Kr * linearRed) - (parameters.Kb * linearBlue)) / parameters.Kg;
            y = nonlinearRed;
            cb = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cr = nonlinearBlue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in Av1ColorConversionParameters parameters)
        {
            Av1ConstantLuminanceScales scales = parameters.ConstantLuminanceScales;
            Vector128<float> blueScale = Vector128.ConditionalSelect(
                Vector128.LessThanOrEqual(cb, Vector128<float>.Zero),
                Vector128.Create(scales.NegativeBlue),
                Vector128.Create(scales.PositiveBlue));
            Vector128<float> redScale = Vector128.ConditionalSelect(
                Vector128.LessThanOrEqual(cr, Vector128<float>.Zero),
                Vector128.Create(scales.NegativeRed),
                Vector128.Create(scales.PositiveRed));
            Vector128<float> nonlinearBlue = Vector128.MultiplyAddEstimate(Vector128.Create(2F) * blueScale, cb, y);
            Vector128<float> nonlinearRed = Vector128.MultiplyAddEstimate(Vector128.Create(2F) * redScale, cr, y);
            Vector128<float> linearY = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, y);
            Vector128<float> linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearBlue);
            Vector128<float> linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearRed);
            Vector128<float> linearGreen = (
                linearY - (Vector128.Create(parameters.Kr) * linearRed) - (Vector128.Create(parameters.Kb) * linearBlue))
                / Vector128.Create(parameters.Kg);

            y = nonlinearRed;
            cb = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cr = nonlinearBlue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in Av1ColorConversionParameters parameters)
        {
            Av1ConstantLuminanceScales scales = parameters.ConstantLuminanceScales;
            Vector256<float> blueScale = Vector256.ConditionalSelect(
                Vector256.LessThanOrEqual(cb, Vector256<float>.Zero),
                Vector256.Create(scales.NegativeBlue),
                Vector256.Create(scales.PositiveBlue));
            Vector256<float> redScale = Vector256.ConditionalSelect(
                Vector256.LessThanOrEqual(cr, Vector256<float>.Zero),
                Vector256.Create(scales.NegativeRed),
                Vector256.Create(scales.PositiveRed));
            Vector256<float> nonlinearBlue = Vector256.MultiplyAddEstimate(Vector256.Create(2F) * blueScale, cb, y);
            Vector256<float> nonlinearRed = Vector256.MultiplyAddEstimate(Vector256.Create(2F) * redScale, cr, y);
            Vector256<float> linearY = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, y);
            Vector256<float> linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearBlue);
            Vector256<float> linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearRed);
            Vector256<float> linearGreen = (
                linearY - (Vector256.Create(parameters.Kr) * linearRed) - (Vector256.Create(parameters.Kb) * linearBlue))
                / Vector256.Create(parameters.Kg);

            y = nonlinearRed;
            cb = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cr = nonlinearBlue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in Av1ColorConversionParameters parameters)
        {
            Av1ConstantLuminanceScales scales = parameters.ConstantLuminanceScales;
            Vector512<float> blueScale = Vector512.ConditionalSelect(
                Vector512.LessThanOrEqual(cb, Vector512<float>.Zero),
                Vector512.Create(scales.NegativeBlue),
                Vector512.Create(scales.PositiveBlue));
            Vector512<float> redScale = Vector512.ConditionalSelect(
                Vector512.LessThanOrEqual(cr, Vector512<float>.Zero),
                Vector512.Create(scales.NegativeRed),
                Vector512.Create(scales.PositiveRed));
            Vector512<float> nonlinearBlue = Vector512.MultiplyAddEstimate(Vector512.Create(2F) * blueScale, cb, y);
            Vector512<float> nonlinearRed = Vector512.MultiplyAddEstimate(Vector512.Create(2F) * redScale, cr, y);
            Vector512<float> linearY = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, y);
            Vector512<float> linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearBlue);
            Vector512<float> linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearRed);
            Vector512<float> linearGreen = (
                linearY - (Vector512.Create(parameters.Kr) * linearRed) - (Vector512.Create(parameters.Kb) * linearBlue))
                / Vector512.Create(parameters.Kg);

            y = nonlinearRed;
            cb = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cr = nonlinearBlue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            float r,
            float g,
            float b,
            in Av1ColorConversionParameters parameters,
            out float y,
            out float cb,
            out float cr)
        {
            // Luma is formed in linear light. The nonlinear red and blue differences then choose the
            // sign-dependent denominators that define constant-luminance Cb and Cr.
            float linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, r);
            float linearGreen = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, g);
            float linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, b);
            float linearY = (parameters.Kr * linearRed) + (parameters.Kg * linearGreen) + (parameters.Kb * linearBlue);
            y = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearY);
            float blueDifference = b - y;
            float redDifference = r - y;
            Av1ConstantLuminanceScales scales = parameters.ConstantLuminanceScales;
            cb = blueDifference / (2F * (blueDifference <= 0F ? scales.NegativeBlue : scales.PositiveBlue));
            cr = redDifference / (2F * (redDifference <= 0F ? scales.NegativeRed : scales.PositiveRed));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<float> r,
            Vector128<float> g,
            Vector128<float> b,
            in Av1ColorConversionParameters parameters,
            out Vector128<float> y,
            out Vector128<float> cb,
            out Vector128<float> cr)
        {
            Vector128<float> linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, r);
            Vector128<float> linearGreen = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, g);
            Vector128<float> linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, b);
            Vector128<float> linearY = Vector128.MultiplyAddEstimate(
                Vector128.Create(parameters.Kr),
                linearRed,
                Vector128.MultiplyAddEstimate(Vector128.Create(parameters.Kg), linearGreen, Vector128.Create(parameters.Kb) * linearBlue));
            y = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearY);
            Vector128<float> blueDifference = b - y;
            Vector128<float> redDifference = r - y;
            Av1ConstantLuminanceScales scales = parameters.ConstantLuminanceScales;
            Vector128<float> blueScale = Vector128.ConditionalSelect(
                Vector128.LessThanOrEqual(blueDifference, Vector128<float>.Zero),
                Vector128.Create(scales.NegativeBlue),
                Vector128.Create(scales.PositiveBlue));
            Vector128<float> redScale = Vector128.ConditionalSelect(
                Vector128.LessThanOrEqual(redDifference, Vector128<float>.Zero),
                Vector128.Create(scales.NegativeRed),
                Vector128.Create(scales.PositiveRed));

            cb = blueDifference / (Vector128.Create(2F) * blueScale);
            cr = redDifference / (Vector128.Create(2F) * redScale);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<float> r,
            Vector256<float> g,
            Vector256<float> b,
            in Av1ColorConversionParameters parameters,
            out Vector256<float> y,
            out Vector256<float> cb,
            out Vector256<float> cr)
        {
            Vector256<float> linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, r);
            Vector256<float> linearGreen = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, g);
            Vector256<float> linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, b);
            Vector256<float> linearY = Vector256.MultiplyAddEstimate(
                Vector256.Create(parameters.Kr),
                linearRed,
                Vector256.MultiplyAddEstimate(Vector256.Create(parameters.Kg), linearGreen, Vector256.Create(parameters.Kb) * linearBlue));
            y = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearY);
            Vector256<float> blueDifference = b - y;
            Vector256<float> redDifference = r - y;
            Av1ConstantLuminanceScales scales = parameters.ConstantLuminanceScales;
            Vector256<float> blueScale = Vector256.ConditionalSelect(
                Vector256.LessThanOrEqual(blueDifference, Vector256<float>.Zero),
                Vector256.Create(scales.NegativeBlue),
                Vector256.Create(scales.PositiveBlue));
            Vector256<float> redScale = Vector256.ConditionalSelect(
                Vector256.LessThanOrEqual(redDifference, Vector256<float>.Zero),
                Vector256.Create(scales.NegativeRed),
                Vector256.Create(scales.PositiveRed));

            cb = blueDifference / (Vector256.Create(2F) * blueScale);
            cr = redDifference / (Vector256.Create(2F) * redScale);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<float> r,
            Vector512<float> g,
            Vector512<float> b,
            in Av1ColorConversionParameters parameters,
            out Vector512<float> y,
            out Vector512<float> cb,
            out Vector512<float> cr)
        {
            Vector512<float> linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, r);
            Vector512<float> linearGreen = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, g);
            Vector512<float> linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, b);
            Vector512<float> linearY = Vector512.MultiplyAddEstimate(
                Vector512.Create(parameters.Kr),
                linearRed,
                Vector512.MultiplyAddEstimate(Vector512.Create(parameters.Kg), linearGreen, Vector512.Create(parameters.Kb) * linearBlue));
            y = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearY);
            Vector512<float> blueDifference = b - y;
            Vector512<float> redDifference = r - y;
            Av1ConstantLuminanceScales scales = parameters.ConstantLuminanceScales;
            Vector512<float> blueScale = Vector512.ConditionalSelect(
                Vector512.LessThanOrEqual(blueDifference, Vector512<float>.Zero),
                Vector512.Create(scales.NegativeBlue),
                Vector512.Create(scales.PositiveBlue));
            Vector512<float> redScale = Vector512.ConditionalSelect(
                Vector512.LessThanOrEqual(redDifference, Vector512<float>.Zero),
                Vector512.Create(scales.NegativeRed),
                Vector512.Create(scales.PositiveRed));

            cb = blueDifference / (Vector512.Create(2F) * blueScale);
            cr = redDifference / (Vector512.Create(2F) * redScale);
        }
    }
}

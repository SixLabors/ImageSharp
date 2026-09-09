// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides reversible YCgCo integer lifting for scalar and SIMD lanes.
/// </content>
internal abstract partial class HeifColorConverterBase
{
    /// <summary>
    /// Implements the YCgCo-Re and YCgCo-Ro integer lifting transforms for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct HeifYCgCoReversibleColorOperator : IHeifColorOperator
    {
        /// <inheritdoc/>
        public static bool ChromaUsesLumaRange => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float y, ref float cg, ref float co, in HeifColorConversionParameters parameters)
        {
            int yCode = (int)MathF.Floor((y * parameters.LumaSampleMaximum) + 0.5F);
            int cgCode = (int)MathF.Floor((cg * parameters.LumaSampleMaximum) + 0.5F);
            int coCode = (int)MathF.Floor((co * parameters.LumaSampleMaximum) + 0.5F);

            // Signed arithmetic shifts are part of the reversible lifting definition. In particular, they preserve
            // the specified floor division for negative odd Cg and Co values instead of truncating toward zero.
            int temporary = yCode - (cgCode >> 1);
            int greenCode = Numerics.Clamp(temporary + cgCode, 0, (int)parameters.RgbSampleMaximum);
            int blueCode = Numerics.Clamp(temporary - (coCode >> 1), 0, (int)parameters.RgbSampleMaximum);
            int redCode = Numerics.Clamp(blueCode + coCode, 0, (int)parameters.RgbSampleMaximum);
            float inverseRgbScale = 1F / parameters.RgbScale;
            y = (redCode - parameters.RgbBias) * inverseRgbScale;
            cg = (greenCode - parameters.RgbBias) * inverseRgbScale;
            co = (blueCode - parameters.RgbBias) * inverseRgbScale;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector128<float> y,
            ref Vector128<float> cg,
            ref Vector128<float> co,
            in HeifColorConversionParameters parameters)
        {
            Vector128<float> encodedMaximum = Vector128.Create(parameters.LumaSampleMaximum);
            Vector128<float> half = Vector128.Create(0.5F);
            Vector128<int> yCode = Vector128.ConvertToInt32(Vector128.Floor((y * encodedMaximum) + half));
            Vector128<int> cgCode = Vector128.ConvertToInt32(Vector128.Floor((cg * encodedMaximum) + half));
            Vector128<int> coCode = Vector128.ConvertToInt32(Vector128.Floor((co * encodedMaximum) + half));

            // Integer lanes preserve the normative arithmetic shifts; converting the lifting stages back to
            // floating point would change negative odd Cg and Co values and break reversibility.
            Vector128<int> temporary = yCode - Vector128.ShiftRightArithmetic(cgCode, 1);
            Vector128<int> zero = Vector128<int>.Zero;
            Vector128<int> rgbMaximum = Vector128.Create((int)parameters.RgbSampleMaximum);
            Vector128<int> greenCode = Vector128.Min(Vector128.Max(temporary + cgCode, zero), rgbMaximum);
            Vector128<int> blueCode = Vector128.Min(Vector128.Max(temporary - Vector128.ShiftRightArithmetic(coCode, 1), zero), rgbMaximum);
            Vector128<int> redCode = Vector128.Min(Vector128.Max(blueCode + coCode, zero), rgbMaximum);
            Vector128<float> rgbBias = Vector128.Create(parameters.RgbBias);
            Vector128<float> inverseRgbScale = Vector128.Create(1F / parameters.RgbScale);
            y = (Vector128.ConvertToSingle(redCode) - rgbBias) * inverseRgbScale;
            cg = (Vector128.ConvertToSingle(greenCode) - rgbBias) * inverseRgbScale;
            co = (Vector128.ConvertToSingle(blueCode) - rgbBias) * inverseRgbScale;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector256<float> y,
            ref Vector256<float> cg,
            ref Vector256<float> co,
            in HeifColorConversionParameters parameters)
        {
            Vector256<float> encodedMaximum = Vector256.Create(parameters.LumaSampleMaximum);
            Vector256<float> half = Vector256.Create(0.5F);
            Vector256<int> yCode = Vector256.ConvertToInt32(Vector256.Floor((y * encodedMaximum) + half));
            Vector256<int> cgCode = Vector256.ConvertToInt32(Vector256.Floor((cg * encodedMaximum) + half));
            Vector256<int> coCode = Vector256.ConvertToInt32(Vector256.Floor((co * encodedMaximum) + half));
            Vector256<int> temporary = yCode - Vector256.ShiftRightArithmetic(cgCode, 1);
            Vector256<int> zero = Vector256<int>.Zero;
            Vector256<int> rgbMaximum = Vector256.Create((int)parameters.RgbSampleMaximum);
            Vector256<int> greenCode = Vector256.Min(Vector256.Max(temporary + cgCode, zero), rgbMaximum);
            Vector256<int> blueCode = Vector256.Min(Vector256.Max(temporary - Vector256.ShiftRightArithmetic(coCode, 1), zero), rgbMaximum);
            Vector256<int> redCode = Vector256.Min(Vector256.Max(blueCode + coCode, zero), rgbMaximum);
            Vector256<float> rgbBias = Vector256.Create(parameters.RgbBias);
            Vector256<float> inverseRgbScale = Vector256.Create(1F / parameters.RgbScale);
            y = (Vector256.ConvertToSingle(redCode) - rgbBias) * inverseRgbScale;
            cg = (Vector256.ConvertToSingle(greenCode) - rgbBias) * inverseRgbScale;
            co = (Vector256.ConvertToSingle(blueCode) - rgbBias) * inverseRgbScale;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector512<float> y,
            ref Vector512<float> cg,
            ref Vector512<float> co,
            in HeifColorConversionParameters parameters)
        {
            Vector512<float> encodedMaximum = Vector512.Create(parameters.LumaSampleMaximum);
            Vector512<float> half = Vector512.Create(0.5F);
            Vector512<int> yCode = Vector512.ConvertToInt32(Vector512.Floor((y * encodedMaximum) + half));
            Vector512<int> cgCode = Vector512.ConvertToInt32(Vector512.Floor((cg * encodedMaximum) + half));
            Vector512<int> coCode = Vector512.ConvertToInt32(Vector512.Floor((co * encodedMaximum) + half));
            Vector512<int> temporary = yCode - Vector512.ShiftRightArithmetic(cgCode, 1);
            Vector512<int> zero = Vector512<int>.Zero;
            Vector512<int> rgbMaximum = Vector512.Create((int)parameters.RgbSampleMaximum);
            Vector512<int> greenCode = Vector512.Min(Vector512.Max(temporary + cgCode, zero), rgbMaximum);
            Vector512<int> blueCode = Vector512.Min(Vector512.Max(temporary - Vector512.ShiftRightArithmetic(coCode, 1), zero), rgbMaximum);
            Vector512<int> redCode = Vector512.Min(Vector512.Max(blueCode + coCode, zero), rgbMaximum);
            Vector512<float> rgbBias = Vector512.Create(parameters.RgbBias);
            Vector512<float> inverseRgbScale = Vector512.Create(1F / parameters.RgbScale);
            y = (Vector512.ConvertToSingle(redCode) - rgbBias) * inverseRgbScale;
            cg = (Vector512.ConvertToSingle(greenCode) - rgbBias) * inverseRgbScale;
            co = (Vector512.ConvertToSingle(blueCode) - rgbBias) * inverseRgbScale;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            float red,
            float green,
            float blue,
            in HeifColorConversionParameters parameters,
            out float y,
            out float cg,
            out float co)
        {
            int redCode = (int)MathF.Floor((red * parameters.RgbScale) + parameters.RgbBias + 0.5F);
            int greenCode = (int)MathF.Floor((green * parameters.RgbScale) + parameters.RgbBias + 0.5F);
            int blueCode = (int)MathF.Floor((blue * parameters.RgbScale) + parameters.RgbBias + 0.5F);
            int coCode = redCode - blueCode;
            int temporary = blueCode + (coCode >> 1);
            int cgCode = greenCode - temporary;
            int yCode = temporary + (cgCode >> 1);
            float inverseEncodedMaximum = 1F / parameters.LumaSampleMaximum;
            y = yCode * inverseEncodedMaximum;
            cg = cgCode * inverseEncodedMaximum;
            co = coCode * inverseEncodedMaximum;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<float> red,
            Vector128<float> green,
            Vector128<float> blue,
            in HeifColorConversionParameters parameters,
            out Vector128<float> y,
            out Vector128<float> cg,
            out Vector128<float> co)
        {
            Vector128<float> rgbScale = Vector128.Create(parameters.RgbScale);
            Vector128<float> quantizationOffset = Vector128.Create(parameters.RgbBias + 0.5F);
            Vector128<int> redCode = Vector128.ConvertToInt32(Vector128.Floor((red * rgbScale) + quantizationOffset));
            Vector128<int> greenCode = Vector128.ConvertToInt32(Vector128.Floor((green * rgbScale) + quantizationOffset));
            Vector128<int> blueCode = Vector128.ConvertToInt32(Vector128.Floor((blue * rgbScale) + quantizationOffset));
            Vector128<int> coCode = redCode - blueCode;
            Vector128<int> temporary = blueCode + Vector128.ShiftRightArithmetic(coCode, 1);
            Vector128<int> cgCode = greenCode - temporary;
            Vector128<int> yCode = temporary + Vector128.ShiftRightArithmetic(cgCode, 1);
            Vector128<float> inverseEncodedMaximum = Vector128.Create(1F / parameters.LumaSampleMaximum);
            y = Vector128.ConvertToSingle(yCode) * inverseEncodedMaximum;
            cg = Vector128.ConvertToSingle(cgCode) * inverseEncodedMaximum;
            co = Vector128.ConvertToSingle(coCode) * inverseEncodedMaximum;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<float> red,
            Vector256<float> green,
            Vector256<float> blue,
            in HeifColorConversionParameters parameters,
            out Vector256<float> y,
            out Vector256<float> cg,
            out Vector256<float> co)
        {
            Vector256<float> rgbScale = Vector256.Create(parameters.RgbScale);
            Vector256<float> quantizationOffset = Vector256.Create(parameters.RgbBias + 0.5F);
            Vector256<int> redCode = Vector256.ConvertToInt32(Vector256.Floor((red * rgbScale) + quantizationOffset));
            Vector256<int> greenCode = Vector256.ConvertToInt32(Vector256.Floor((green * rgbScale) + quantizationOffset));
            Vector256<int> blueCode = Vector256.ConvertToInt32(Vector256.Floor((blue * rgbScale) + quantizationOffset));
            Vector256<int> coCode = redCode - blueCode;
            Vector256<int> temporary = blueCode + Vector256.ShiftRightArithmetic(coCode, 1);
            Vector256<int> cgCode = greenCode - temporary;
            Vector256<int> yCode = temporary + Vector256.ShiftRightArithmetic(cgCode, 1);
            Vector256<float> inverseEncodedMaximum = Vector256.Create(1F / parameters.LumaSampleMaximum);
            y = Vector256.ConvertToSingle(yCode) * inverseEncodedMaximum;
            cg = Vector256.ConvertToSingle(cgCode) * inverseEncodedMaximum;
            co = Vector256.ConvertToSingle(coCode) * inverseEncodedMaximum;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<float> red,
            Vector512<float> green,
            Vector512<float> blue,
            in HeifColorConversionParameters parameters,
            out Vector512<float> y,
            out Vector512<float> cg,
            out Vector512<float> co)
        {
            Vector512<float> rgbScale = Vector512.Create(parameters.RgbScale);
            Vector512<float> quantizationOffset = Vector512.Create(parameters.RgbBias + 0.5F);
            Vector512<int> redCode = Vector512.ConvertToInt32(Vector512.Floor((red * rgbScale) + quantizationOffset));
            Vector512<int> greenCode = Vector512.ConvertToInt32(Vector512.Floor((green * rgbScale) + quantizationOffset));
            Vector512<int> blueCode = Vector512.ConvertToInt32(Vector512.Floor((blue * rgbScale) + quantizationOffset));
            Vector512<int> coCode = redCode - blueCode;
            Vector512<int> temporary = blueCode + Vector512.ShiftRightArithmetic(coCode, 1);
            Vector512<int> cgCode = greenCode - temporary;
            Vector512<int> yCode = temporary + Vector512.ShiftRightArithmetic(cgCode, 1);
            Vector512<float> inverseEncodedMaximum = Vector512.Create(1F / parameters.LumaSampleMaximum);
            y = Vector512.ConvertToSingle(yCode) * inverseEncodedMaximum;
            cg = Vector512.ConvertToSingle(cgCode) * inverseEncodedMaximum;
            co = Vector512.ConvertToSingle(coCode) * inverseEncodedMaximum;
        }
    }
}

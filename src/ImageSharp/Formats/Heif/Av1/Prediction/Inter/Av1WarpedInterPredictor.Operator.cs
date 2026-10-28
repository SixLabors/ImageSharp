// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines affine warped-motion prediction arithmetic.
/// </content>
internal static partial class Av1WarpedInterPredictor
{
    /// <summary>
    /// Defines the affine warped-motion dot product for scalar and SIMD lane groups.
    /// </summary>
    /// <remarks>
    /// Each SIMD overload holds consecutive independent eight-tap filters.
    /// The generic warped traversal gathers the source windows and the coefficient phases. The closed operator does the exact multiply and sum.
    /// </remarks>
    private interface IAv1WarpedPredictionOperator
    {
        /// <summary>
        /// Convolves one eight-sample window without hardware intrinsics.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <param name="sourceStride">The distance between source samples.</param>
        /// <param name="coefficients">The first signed Q7 coefficient.</param>
        /// <returns>The exact dot product.</returns>
        public static abstract int Convolve(ref byte source, int sourceStride, ref short coefficients);

        /// <summary>
        /// Convolves one high-bit-depth eight-sample window without hardware intrinsics.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <param name="sourceStride">The distance between source samples.</param>
        /// <param name="coefficients">The first signed Q7 coefficient.</param>
        /// <returns>The exact dot product.</returns>
        public static abstract int Convolve(ref ushort source, int sourceStride, ref short coefficients);

        /// <summary>
        /// Convolves one packed eight-sample window.
        /// </summary>
        /// <param name="samples">The unsigned samples.</param>
        /// <param name="coefficients">The signed Q7 coefficients.</param>
        /// <returns>The exact dot product.</returns>
        public static abstract int Convolve(Vector128<ushort> samples, Vector128<short> coefficients);

        /// <summary>
        /// Convolves two packed eight-sample windows.
        /// </summary>
        /// <param name="samples">The two unsigned sample windows.</param>
        /// <param name="coefficients">The two signed Q7 coefficient windows.</param>
        /// <returns>The two exact dot products in the low lanes.</returns>
        public static abstract Vector128<int> Convolve(Vector256<ushort> samples, Vector256<short> coefficients);

        /// <summary>
        /// Convolves four packed eight-sample windows.
        /// </summary>
        /// <param name="samples">The four unsigned sample windows.</param>
        /// <param name="coefficients">The four signed Q7 coefficient windows.</param>
        /// <returns>The four exact dot products.</returns>
        public static abstract Vector128<int> Convolve(Vector512<ushort> samples, Vector512<short> coefficients);
    }

    /// <summary>
    /// Implements the affine warped-motion dot product for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct WarpedOperator : IAv1WarpedPredictionOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Convolve(ref byte source, int sourceStride, ref short coefficients)
        {
            int sum = 0;
            for (int index = 0; index < FilterCoefficientCount; index++)
            {
                sum += Unsafe.Add(ref source, index * sourceStride) * Unsafe.Add(ref coefficients, index);
            }

            return sum;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Convolve(ref ushort source, int sourceStride, ref short coefficients)
        {
            int sum = 0;
            for (int index = 0; index < FilterCoefficientCount; index++)
            {
                sum += Unsafe.Add(ref source, index * sourceStride) * Unsafe.Add(ref coefficients, index);
            }

            return sum;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Convolve(Vector128<ushort> samples, Vector128<short> coefficients)
        {
            (Vector128<uint> sampleLower, Vector128<uint> sampleUpper) = Vector128.Widen(samples);
            (Vector128<int> coefficientLower, Vector128<int> coefficientUpper) = Vector128.Widen(coefficients);
            return Vector128.Sum(sampleLower.AsInt32() * coefficientLower) +
                Vector128.Sum(sampleUpper.AsInt32() * coefficientUpper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Convolve(Vector256<ushort> samples, Vector256<short> coefficients)
        {
            // Widen puts each eight-tap window into its own 256-bit vector.
            // Thus the sum of each product vector gives one prediction, without lane shuffles.
            (Vector256<uint> sample0, Vector256<uint> sample1) = Vector256.Widen(samples);
            (Vector256<int> coefficient0, Vector256<int> coefficient1) = Vector256.Widen(coefficients);
            return Vector128.Create(
                Vector256.Sum(sample0.AsInt32() * coefficient0),
                Vector256.Sum(sample1.AsInt32() * coefficient1),
                0,
                0);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Convolve(Vector512<ushort> samples, Vector512<short> coefficients)
        {
            // After the widen, the four eight-tap windows fill four consecutive 256-bit quarters, and the multiply stays 512-bit.
            // The sum of each quarter gives one dot product, in output order.
            (Vector512<uint> sampleLower, Vector512<uint> sampleUpper) = Vector512.Widen(samples);
            (Vector512<int> coefficientLower, Vector512<int> coefficientUpper) = Vector512.Widen(coefficients);
            Vector512<int> productLower = sampleLower.AsInt32() * coefficientLower;
            Vector512<int> productUpper = sampleUpper.AsInt32() * coefficientUpper;
            return Vector128.Create(
                Vector256.Sum(productLower.GetLower()),
                Vector256.Sum(productLower.GetUpper()),
                Vector256.Sum(productUpper.GetLower()),
                Vector256.Sum(productUpper.GetUpper()));
        }
    }
}

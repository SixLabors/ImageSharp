// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

internal static partial class HevcResidualReconstructor
{
    /// <summary>
    /// Defines a closed transform-skip normalization operator for every SIMD width and the scalar tail.
    /// </summary>
    private interface ITransformSkipOperator
    {
        /// <summary>
        /// Normalizes sixteen transform-skipped coefficients.
        /// </summary>
        /// <param name="values">The dequantized coefficients.</param>
        /// <param name="shift">The nonnegative shift magnitude.</param>
        /// <returns>The reconstructed residuals.</returns>
        static abstract Vector512<int> Invoke(Vector512<int> values, int shift);

        /// <summary>
        /// Normalizes eight transform-skipped coefficients.
        /// </summary>
        /// <param name="values">The dequantized coefficients.</param>
        /// <param name="shift">The nonnegative shift magnitude.</param>
        /// <returns>The reconstructed residuals.</returns>
        static abstract Vector256<int> Invoke(Vector256<int> values, int shift);

        /// <summary>
        /// Normalizes four transform-skipped coefficients.
        /// </summary>
        /// <param name="values">The dequantized coefficients.</param>
        /// <param name="shift">The nonnegative shift magnitude.</param>
        /// <returns>The reconstructed residuals.</returns>
        static abstract Vector128<int> Invoke(Vector128<int> values, int shift);

        /// <summary>
        /// Normalizes one transform-skipped coefficient.
        /// </summary>
        /// <param name="value">The dequantized coefficient.</param>
        /// <param name="shift">The nonnegative shift magnitude.</param>
        /// <returns>The reconstructed residual.</returns>
        static abstract int Invoke(int value, int shift);
    }
}

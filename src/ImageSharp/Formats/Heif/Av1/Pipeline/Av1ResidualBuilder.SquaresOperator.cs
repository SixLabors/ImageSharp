// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the arithmetic of the signed residual sums.
/// </content>
internal static partial class Av1ResidualBuilder
{
    /// <summary>
    /// Defines the sum and the square sum of signed sixteen-bit residuals across hardware widths.
    /// </summary>
    /// <remarks>
    /// Every total is held in sixty-four-bit lanes, so it stays exact for a span of any length. The spread
    /// of a total across the lanes is not defined; only the sum of all lanes is.
    /// </remarks>
    internal interface IResidualSquaresOperator
    {
        /// <summary>
        /// Adds the squares of eight residuals to a running total.
        /// </summary>
        /// <param name="values">The residuals.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector128<long> AccumulateSquares(Vector128<short> values, Vector128<long> total);

        /// <summary>
        /// Adds the squares of sixteen residuals to a running total.
        /// </summary>
        /// <param name="values">The residuals.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector256<long> AccumulateSquares(Vector256<short> values, Vector256<long> total);

        /// <summary>
        /// Adds the squares of thirty-two residuals to a running total.
        /// </summary>
        /// <param name="values">The residuals.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector512<long> AccumulateSquares(Vector512<short> values, Vector512<long> total);

        /// <summary>
        /// Adds the square of one residual to a running total.
        /// </summary>
        /// <param name="value">The residual.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract long AccumulateSquares(short value, long total);

        /// <summary>
        /// Adds eight residuals to a running total.
        /// </summary>
        /// <param name="values">The residuals.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector128<long> AccumulateSum(Vector128<short> values, Vector128<long> total);

        /// <summary>
        /// Adds sixteen residuals to a running total.
        /// </summary>
        /// <param name="values">The residuals.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector256<long> AccumulateSum(Vector256<short> values, Vector256<long> total);

        /// <summary>
        /// Adds thirty-two residuals to a running total.
        /// </summary>
        /// <param name="values">The residuals.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector512<long> AccumulateSum(Vector512<short> values, Vector512<long> total);

        /// <summary>
        /// Adds one residual to a running total.
        /// </summary>
        /// <param name="value">The residual.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract long AccumulateSum(short value, long total);

        /// <summary>
        /// Adds the products of eight residual pairs to a running total.
        /// </summary>
        /// <param name="first">The first residuals.</param>
        /// <param name="second">The second residuals.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector128<long> AccumulateProducts(Vector128<short> first, Vector128<short> second, Vector128<long> total);

        /// <summary>
        /// Adds the products of sixteen residual pairs to a running total.
        /// </summary>
        /// <param name="first">The first residuals.</param>
        /// <param name="second">The second residuals.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector256<long> AccumulateProducts(Vector256<short> first, Vector256<short> second, Vector256<long> total);

        /// <summary>
        /// Adds the products of thirty-two residual pairs to a running total.
        /// </summary>
        /// <param name="first">The first residuals.</param>
        /// <param name="second">The second residuals.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector512<long> AccumulateProducts(Vector512<short> first, Vector512<short> second, Vector512<long> total);

        /// <summary>
        /// Adds the product of one residual pair to a running total.
        /// </summary>
        /// <param name="first">The first residual.</param>
        /// <param name="second">The second residual.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract long AccumulateProducts(short first, short second, long total);
    }

    /// <summary>
    /// Sums signed residuals and their squares with the pairwise multiply-add.
    /// </summary>
    /// <remarks>
    /// Reference: aom_sum_squares_i16, aom_sum_squares_2d_i16 and aom_sum_sse_2d_i16. The pairwise multiply-add
    /// squares every lane and adds the pairs in one instruction, and multiplying by one reuses it to fold eight
    /// lanes into four for the plain sum. Widening the pairs keeps both totals exact.
    /// </remarks>
    internal readonly struct ResidualSquaresOperator : IResidualSquaresOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<long> AccumulateSquares(Vector128<short> values, Vector128<long> total)
        {
            (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(Vector128_.MultiplyAddAdjacent(values, values));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<long> AccumulateSquares(Vector256<short> values, Vector256<long> total)
        {
            (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(Vector256_.MultiplyAddAdjacent(values, values));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<long> AccumulateSquares(Vector512<short> values, Vector512<long> total)
        {
            (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(Vector512_.MultiplyAddAdjacent(values, values));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AccumulateSquares(short value, long total) => total + (value * value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<long> AccumulateSum(Vector128<short> values, Vector128<long> total)
        {
            (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(Vector128_.MultiplyAddAdjacent(values, Vector128.Create((short)1)));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<long> AccumulateSum(Vector256<short> values, Vector256<long> total)
        {
            (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(Vector256_.MultiplyAddAdjacent(values, Vector256.Create((short)1)));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<long> AccumulateSum(Vector512<short> values, Vector512<long> total)
        {
            (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(Vector512_.MultiplyAddAdjacent(values, Vector512.Create((short)1)));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AccumulateSum(short value, long total) => total + value;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<long> AccumulateProducts(Vector128<short> first, Vector128<short> second, Vector128<long> total)
        {
            // A residual has at most thirteen bits, so a pairwise sum of products is far below 2^31.
            (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(Vector128_.MultiplyAddAdjacent(first, second));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<long> AccumulateProducts(Vector256<short> first, Vector256<short> second, Vector256<long> total)
        {
            (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(Vector256_.MultiplyAddAdjacent(first, second));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<long> AccumulateProducts(Vector512<short> first, Vector512<short> second, Vector512<long> total)
        {
            (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(Vector512_.MultiplyAddAdjacent(first, second));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AccumulateProducts(short first, short second, long total) => total + (first * second);
    }
}

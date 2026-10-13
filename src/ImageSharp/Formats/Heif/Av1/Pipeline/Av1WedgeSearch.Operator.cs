// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the lane arithmetic of the <see cref="Av1WedgeSearch"/> traversals.
/// </content>
internal static partial class Av1WedgeSearch
{
    /// <summary>
    /// Defines the wedge search arithmetic across hardware widths.
    /// </summary>
    /// <remarks>
    /// One lane is one sample of a block, in raster order. The residuals are signed sixteen-bit values, and the mask weights
    /// are values from 0 to 64. A measure returns a sixty-four-bit total in vector lanes. The split of the total across the
    /// lanes is not defined. Only the sum of all lanes is defined.
    /// </remarks>
    internal interface IWedgeOperator
    {
        /// <summary>
        /// Loads eight mask weights into sixteen-bit lanes.
        /// </summary>
        /// <param name="mask">The first weight of the mask.</param>
        /// <param name="offset">The sample offset.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The weights in increasing sample order.</returns>
        public static abstract Vector128<short> LoadWeights(ref byte mask, nuint offset, Vector128<short> lanes);

        /// <summary>
        /// Loads sixteen mask weights into sixteen-bit lanes.
        /// </summary>
        /// <param name="mask">The first weight of the mask.</param>
        /// <param name="offset">The sample offset.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The weights in increasing sample order.</returns>
        public static abstract Vector256<short> LoadWeights(ref byte mask, nuint offset, Vector256<short> lanes);

        /// <summary>
        /// Loads thirty-two mask weights into sixteen-bit lanes.
        /// </summary>
        /// <param name="mask">The first weight of the mask.</param>
        /// <param name="offset">The sample offset.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The weights in increasing sample order.</returns>
        public static abstract Vector512<short> LoadWeights(ref byte mask, nuint offset, Vector512<short> lanes);

        /// <summary>
        /// Returns the saturated difference of the squares of eight residual pairs.
        /// </summary>
        /// <param name="first">The first residuals.</param>
        /// <param name="second">The second residuals.</param>
        /// <returns>The saturated differences in increasing sample order.</returns>
        public static abstract Vector128<short> DeltaSquares(Vector128<short> first, Vector128<short> second);

        /// <summary>
        /// Returns the saturated difference of the squares of sixteen residual pairs.
        /// </summary>
        /// <param name="first">The first residuals.</param>
        /// <param name="second">The second residuals.</param>
        /// <returns>The saturated differences in increasing sample order.</returns>
        public static abstract Vector256<short> DeltaSquares(Vector256<short> first, Vector256<short> second);

        /// <summary>
        /// Returns the saturated difference of the squares of thirty-two residual pairs.
        /// </summary>
        /// <param name="first">The first residuals.</param>
        /// <param name="second">The second residuals.</param>
        /// <returns>The saturated differences in increasing sample order.</returns>
        public static abstract Vector512<short> DeltaSquares(Vector512<short> first, Vector512<short> second);

        /// <summary>
        /// Returns the saturated difference of the squares of one residual pair.
        /// </summary>
        /// <param name="first">The first residual.</param>
        /// <param name="second">The second residual.</param>
        /// <returns>The saturated difference.</returns>
        public static abstract short DeltaSquares(short first, short second);

        /// <summary>
        /// Adds the mask-weighted differences of squares of eight samples to a running total.
        /// </summary>
        /// <param name="deltaSquares">The saturated differences of squares.</param>
        /// <param name="weights">The mask weights.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector128<long> AccumulateWeightedDelta(Vector128<short> deltaSquares, Vector128<short> weights, Vector128<long> total);

        /// <summary>
        /// Adds the mask-weighted differences of squares of sixteen samples to a running total.
        /// </summary>
        /// <param name="deltaSquares">The saturated differences of squares.</param>
        /// <param name="weights">The mask weights.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector256<long> AccumulateWeightedDelta(Vector256<short> deltaSquares, Vector256<short> weights, Vector256<long> total);

        /// <summary>
        /// Adds the mask-weighted differences of squares of thirty-two samples to a running total.
        /// </summary>
        /// <param name="deltaSquares">The saturated differences of squares.</param>
        /// <param name="weights">The mask weights.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector512<long> AccumulateWeightedDelta(Vector512<short> deltaSquares, Vector512<short> weights, Vector512<long> total);

        /// <summary>
        /// Adds the mask-weighted difference of squares of one sample to a running total.
        /// </summary>
        /// <param name="deltaSquares">The saturated difference of squares.</param>
        /// <param name="weight">The mask weight.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract long AccumulateWeightedDelta(short deltaSquares, int weight, long total);

        /// <summary>
        /// Adds the squared saturated wedge residuals of eight samples to a running total.
        /// </summary>
        /// <param name="residual">The residuals of the second prediction.</param>
        /// <param name="difference">The second prediction minus the first.</param>
        /// <param name="weights">The mask weights of the first prediction.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector128<ulong> AccumulateSquaredErrors(Vector128<short> residual, Vector128<short> difference, Vector128<short> weights, Vector128<ulong> total);

        /// <summary>
        /// Adds the squared saturated wedge residuals of sixteen samples to a running total.
        /// </summary>
        /// <param name="residual">The residuals of the second prediction.</param>
        /// <param name="difference">The second prediction minus the first.</param>
        /// <param name="weights">The mask weights of the first prediction.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector256<ulong> AccumulateSquaredErrors(Vector256<short> residual, Vector256<short> difference, Vector256<short> weights, Vector256<ulong> total);

        /// <summary>
        /// Adds the squared saturated wedge residuals of thirty-two samples to a running total.
        /// </summary>
        /// <param name="residual">The residuals of the second prediction.</param>
        /// <param name="difference">The second prediction minus the first.</param>
        /// <param name="weights">The mask weights of the first prediction.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector512<ulong> AccumulateSquaredErrors(Vector512<short> residual, Vector512<short> difference, Vector512<short> weights, Vector512<ulong> total);

        /// <summary>
        /// Adds the squared saturated wedge residual of one sample to a running total.
        /// </summary>
        /// <param name="residual">The residual of the second prediction.</param>
        /// <param name="difference">The second prediction minus the first.</param>
        /// <param name="weight">The mask weight of the first prediction.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract ulong AccumulateSquaredErrors(short residual, short difference, int weight, ulong total);
    }

    /// <summary>
    /// Applies the wedge search arithmetic lane by lane, with sixteen-bit saturation at the same points as the scalar overloads.
    /// </summary>
    internal readonly struct WedgeOperator : IWedgeOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> LoadWeights(ref byte mask, nuint offset, Vector128<short> lanes)
            => Vector128.WidenLower(Vector64.LoadUnsafe(ref mask, offset).ToVector128()).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> LoadWeights(ref byte mask, nuint offset, Vector256<short> lanes)
            => Vector256.WidenLower(Vector128.LoadUnsafe(ref mask, offset).ToVector256Unsafe()).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> LoadWeights(ref byte mask, nuint offset, Vector512<short> lanes)
            => Vector512.WidenLower(Vector256.LoadUnsafe(ref mask, offset).ToVector512Unsafe()).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> DeltaSquares(Vector128<short> first, Vector128<short> second)
        {
            // Each half widens to thirty-two-bit lanes. A square of a sixteen-bit value is at most 2^30, so the difference
            // of two squares fits a thirty-two-bit lane. The clamp saturates the difference to sixteen bits. Then the
            // narrowing of the lower and the upper half keeps the sample order.
            (Vector128<int> firstLower, Vector128<int> firstUpper) = Vector128.Widen(first);
            (Vector128<int> secondLower, Vector128<int> secondUpper) = Vector128.Widen(second);
            Vector128<int> minimum = Vector128.Create((int)short.MinValue);
            Vector128<int> maximum = Vector128.Create((int)short.MaxValue);
            Vector128<int> lower = Vector128.Clamp((firstLower * firstLower) - (secondLower * secondLower), minimum, maximum);
            Vector128<int> upper = Vector128.Clamp((firstUpper * firstUpper) - (secondUpper * secondUpper), minimum, maximum);
            return Vector128.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> DeltaSquares(Vector256<short> first, Vector256<short> second)
        {
            (Vector256<int> firstLower, Vector256<int> firstUpper) = Vector256.Widen(first);
            (Vector256<int> secondLower, Vector256<int> secondUpper) = Vector256.Widen(second);
            Vector256<int> minimum = Vector256.Create((int)short.MinValue);
            Vector256<int> maximum = Vector256.Create((int)short.MaxValue);
            Vector256<int> lower = Vector256.Clamp((firstLower * firstLower) - (secondLower * secondLower), minimum, maximum);
            Vector256<int> upper = Vector256.Clamp((firstUpper * firstUpper) - (secondUpper * secondUpper), minimum, maximum);
            return Vector256.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> DeltaSquares(Vector512<short> first, Vector512<short> second)
        {
            (Vector512<int> firstLower, Vector512<int> firstUpper) = Vector512.Widen(first);
            (Vector512<int> secondLower, Vector512<int> secondUpper) = Vector512.Widen(second);
            Vector512<int> minimum = Vector512.Create((int)short.MinValue);
            Vector512<int> maximum = Vector512.Create((int)short.MaxValue);
            Vector512<int> lower = Vector512.Clamp((firstLower * firstLower) - (secondLower * secondLower), minimum, maximum);
            Vector512<int> upper = Vector512.Clamp((firstUpper * firstUpper) - (secondUpper * secondUpper), minimum, maximum);
            return Vector512.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short DeltaSquares(short first, short second)
            => (short)Math.Clamp((first * first) - (second * second), short.MinValue, short.MaxValue);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<long> AccumulateWeightedDelta(Vector128<short> deltaSquares, Vector128<short> weights, Vector128<long> total)
        {
            // The multiply-add gives the sum of two adjacent weighted values in one thirty-two-bit lane. That sum is at most
            // 2 * 64 * 32768 in magnitude, so it fits. The sum widens to sixty-four bits before it adds to the total,
            // because the total can exceed the thirty-two-bit range.
            (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(Vector128_.MultiplyAddAdjacent(deltaSquares, weights));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<long> AccumulateWeightedDelta(Vector256<short> deltaSquares, Vector256<short> weights, Vector256<long> total)
        {
            (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(Vector256_.MultiplyAddAdjacent(deltaSquares, weights));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<long> AccumulateWeightedDelta(Vector512<short> deltaSquares, Vector512<short> weights, Vector512<long> total)
        {
            (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(Vector512_.MultiplyAddAdjacent(deltaSquares, weights));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AccumulateWeightedDelta(short deltaSquares, int weight, long total)
            => total + (deltaSquares * weight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> AccumulateSquaredErrors(Vector128<short> residual, Vector128<short> difference, Vector128<short> weights, Vector128<ulong> total)
        {
            // The weighted residual 64 * r + m * d needs a thirty-two-bit lane before its saturation to sixteen bits.
            // The multiply-add of the saturated values with themselves gives the sum of two adjacent squares. That sum is
            // at most 2^31. The signed lane wraps at 2^31, but the unsigned view of the same bits is exact.
            Vector128<short> saturated = Saturate(residual, difference, weights);
            (Vector128<ulong> lower, Vector128<ulong> upper) = Vector128.Widen(Vector128_.MultiplyAddAdjacent(saturated, saturated).AsUInt32());
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> AccumulateSquaredErrors(Vector256<short> residual, Vector256<short> difference, Vector256<short> weights, Vector256<ulong> total)
        {
            Vector256<short> saturated = Saturate(residual, difference, weights);
            (Vector256<ulong> lower, Vector256<ulong> upper) = Vector256.Widen(Vector256_.MultiplyAddAdjacent(saturated, saturated).AsUInt32());
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ulong> AccumulateSquaredErrors(Vector512<short> residual, Vector512<short> difference, Vector512<short> weights, Vector512<ulong> total)
        {
            Vector512<short> saturated = Saturate(residual, difference, weights);
            (Vector512<ulong> lower, Vector512<ulong> upper) = Vector512.Widen(Vector512_.MultiplyAddAdjacent(saturated, saturated).AsUInt32());
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong AccumulateSquaredErrors(short residual, short difference, int weight, ulong total)
        {
            int weighted = Math.Clamp((MaximumMaskValue * residual) + (weight * difference), short.MinValue, short.MaxValue);
            return total + (ulong)(weighted * weighted);
        }

        /// <summary>
        /// Returns 64 * r + m * d of eight samples, saturated to sixteen bits.
        /// </summary>
        /// <param name="residual">The residuals of the second prediction.</param>
        /// <param name="difference">The second prediction minus the first.</param>
        /// <param name="weights">The mask weights of the first prediction.</param>
        /// <returns>The saturated weighted residuals in increasing sample order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<short> Saturate(Vector128<short> residual, Vector128<short> difference, Vector128<short> weights)
        {
            (Vector128<int> residualLower, Vector128<int> residualUpper) = Vector128.Widen(residual);
            (Vector128<int> differenceLower, Vector128<int> differenceUpper) = Vector128.Widen(difference);
            (Vector128<int> weightLower, Vector128<int> weightUpper) = Vector128.Widen(weights);
            Vector128<int> minimum = Vector128.Create((int)short.MinValue);
            Vector128<int> maximum = Vector128.Create((int)short.MaxValue);
            Vector128<int> lower = Vector128.Clamp(Vector128.ShiftLeft(residualLower, MaximumMaskBits) + (weightLower * differenceLower), minimum, maximum);
            Vector128<int> upper = Vector128.Clamp(Vector128.ShiftLeft(residualUpper, MaximumMaskBits) + (weightUpper * differenceUpper), minimum, maximum);
            return Vector128.Narrow(lower, upper);
        }

        /// <summary>
        /// Returns 64 * r + m * d of sixteen samples, saturated to sixteen bits.
        /// </summary>
        /// <param name="residual">The residuals of the second prediction.</param>
        /// <param name="difference">The second prediction minus the first.</param>
        /// <param name="weights">The mask weights of the first prediction.</param>
        /// <returns>The saturated weighted residuals in increasing sample order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<short> Saturate(Vector256<short> residual, Vector256<short> difference, Vector256<short> weights)
        {
            (Vector256<int> residualLower, Vector256<int> residualUpper) = Vector256.Widen(residual);
            (Vector256<int> differenceLower, Vector256<int> differenceUpper) = Vector256.Widen(difference);
            (Vector256<int> weightLower, Vector256<int> weightUpper) = Vector256.Widen(weights);
            Vector256<int> minimum = Vector256.Create((int)short.MinValue);
            Vector256<int> maximum = Vector256.Create((int)short.MaxValue);
            Vector256<int> lower = Vector256.Clamp(Vector256.ShiftLeft(residualLower, MaximumMaskBits) + (weightLower * differenceLower), minimum, maximum);
            Vector256<int> upper = Vector256.Clamp(Vector256.ShiftLeft(residualUpper, MaximumMaskBits) + (weightUpper * differenceUpper), minimum, maximum);
            return Vector256.Narrow(lower, upper);
        }

        /// <summary>
        /// Returns 64 * r + m * d of thirty-two samples, saturated to sixteen bits.
        /// </summary>
        /// <param name="residual">The residuals of the second prediction.</param>
        /// <param name="difference">The second prediction minus the first.</param>
        /// <param name="weights">The mask weights of the first prediction.</param>
        /// <returns>The saturated weighted residuals in increasing sample order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<short> Saturate(Vector512<short> residual, Vector512<short> difference, Vector512<short> weights)
        {
            (Vector512<int> residualLower, Vector512<int> residualUpper) = Vector512.Widen(residual);
            (Vector512<int> differenceLower, Vector512<int> differenceUpper) = Vector512.Widen(difference);
            (Vector512<int> weightLower, Vector512<int> weightUpper) = Vector512.Widen(weights);
            Vector512<int> minimum = Vector512.Create((int)short.MinValue);
            Vector512<int> maximum = Vector512.Create((int)short.MaxValue);
            Vector512<int> lower = Vector512.Clamp(Vector512.ShiftLeft(residualLower, MaximumMaskBits) + (weightLower * differenceLower), minimum, maximum);
            Vector512<int> upper = Vector512.Clamp(Vector512.ShiftLeft(residualUpper, MaximumMaskBits) + (weightUpper * differenceUpper), minimum, maximum);
            return Vector512.Narrow(lower, upper);
        }
    }
}

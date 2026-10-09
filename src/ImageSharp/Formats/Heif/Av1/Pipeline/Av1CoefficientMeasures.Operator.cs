// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the lane arithmetic of the <see cref="Av1CoefficientMeasures"/> traversals.
/// </content>
internal static partial class Av1CoefficientMeasures
{
    /// <summary>
    /// Defines the magnitude measures of transform coefficients across hardware widths.
    /// </summary>
    /// <remarks>
    /// One lane is one coefficient. A coefficient magnitude is far below 2^31, so its absolute value is
    /// exact in a thirty-two-bit lane. A total spreads across the lanes in no defined way; only its
    /// reduction is defined.
    /// </remarks>
    internal interface ICoefficientMeasureOperator
    {
        /// <summary>
        /// Adds the magnitudes of four coefficients to a running total.
        /// </summary>
        /// <param name="values">The coefficients.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector128<long> AccumulateAbsolute(Vector128<int> values, Vector128<long> total);

        /// <summary>
        /// Adds the magnitudes of eight coefficients to a running total.
        /// </summary>
        /// <param name="values">The coefficients.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector256<long> AccumulateAbsolute(Vector256<int> values, Vector256<long> total);

        /// <summary>
        /// Adds the magnitudes of sixteen coefficients to a running total.
        /// </summary>
        /// <param name="values">The coefficients.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector512<long> AccumulateAbsolute(Vector512<int> values, Vector512<long> total);

        /// <summary>
        /// Adds the magnitude of one coefficient to a running total.
        /// </summary>
        /// <param name="value">The coefficient.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract long AccumulateAbsolute(int value, long total);

        /// <summary>
        /// Keeps the largest magnitude seen in each lane over four coefficients.
        /// </summary>
        /// <param name="values">The coefficients.</param>
        /// <param name="maximum">The running lane maxima.</param>
        /// <returns>The updated lane maxima.</returns>
        public static abstract Vector128<int> AccumulateMaximumAbsolute(Vector128<int> values, Vector128<int> maximum);

        /// <summary>
        /// Keeps the largest magnitude seen in each lane over eight coefficients.
        /// </summary>
        /// <param name="values">The coefficients.</param>
        /// <param name="maximum">The running lane maxima.</param>
        /// <returns>The updated lane maxima.</returns>
        public static abstract Vector256<int> AccumulateMaximumAbsolute(Vector256<int> values, Vector256<int> maximum);

        /// <summary>
        /// Keeps the largest magnitude seen in each lane over sixteen coefficients.
        /// </summary>
        /// <param name="values">The coefficients.</param>
        /// <param name="maximum">The running lane maxima.</param>
        /// <returns>The updated lane maxima.</returns>
        public static abstract Vector512<int> AccumulateMaximumAbsolute(Vector512<int> values, Vector512<int> maximum);

        /// <summary>
        /// Keeps the larger of a running maximum and the magnitude of one coefficient.
        /// </summary>
        /// <param name="value">The coefficient.</param>
        /// <param name="maximum">The running maximum.</param>
        /// <returns>The updated maximum.</returns>
        public static abstract int AccumulateMaximumAbsolute(int value, int maximum);

        /// <summary>
        /// Adds the level bits of four coefficients to lane totals: the floored base-two logarithm of the magnitude
        /// plus one, and one more for a nonzero coefficient.
        /// </summary>
        /// <param name="values">The quantized coefficients.</param>
        /// <param name="total">The lane totals.</param>
        /// <returns>The updated lane totals.</returns>
        public static abstract Vector128<int> AccumulateLevelBits(Vector128<int> values, Vector128<int> total);

        /// <summary>
        /// Adds the level bits of eight coefficients to lane totals.
        /// </summary>
        /// <param name="values">The quantized coefficients.</param>
        /// <param name="total">The lane totals.</param>
        /// <returns>The updated lane totals.</returns>
        public static abstract Vector256<int> AccumulateLevelBits(Vector256<int> values, Vector256<int> total);

        /// <summary>
        /// Adds the level bits of sixteen coefficients to lane totals.
        /// </summary>
        /// <param name="values">The quantized coefficients.</param>
        /// <param name="total">The lane totals.</param>
        /// <returns>The updated lane totals.</returns>
        public static abstract Vector512<int> AccumulateLevelBits(Vector512<int> values, Vector512<int> total);

        /// <summary>
        /// Adds the level bits of one coefficient to a total.
        /// </summary>
        /// <param name="value">The quantized coefficient.</param>
        /// <param name="total">The total.</param>
        /// <returns>The updated total.</returns>
        public static abstract int AccumulateLevelBits(int value, int total);

        /// <summary>
        /// Keeps, in each lane, the largest one-based scan position of a nonzero coefficient over four coefficients.
        /// </summary>
        /// <param name="values">The raster-order quantized coefficients.</param>
        /// <param name="inverseScan">The first scan position of the whole block.</param>
        /// <param name="offset">The raster offset of <paramref name="values"/>.</param>
        /// <param name="maximum">The running lane maxima.</param>
        /// <returns>The updated lane maxima.</returns>
        public static abstract Vector128<int> AccumulateEndOfBlock(Vector128<int> values, ref short inverseScan, nuint offset, Vector128<int> maximum);

        /// <summary>
        /// Keeps, in each lane, the largest one-based scan position of a nonzero coefficient over eight coefficients.
        /// </summary>
        /// <param name="values">The raster-order quantized coefficients.</param>
        /// <param name="inverseScan">The first scan position of the whole block.</param>
        /// <param name="offset">The raster offset of <paramref name="values"/>.</param>
        /// <param name="maximum">The running lane maxima.</param>
        /// <returns>The updated lane maxima.</returns>
        public static abstract Vector256<int> AccumulateEndOfBlock(Vector256<int> values, ref short inverseScan, nuint offset, Vector256<int> maximum);

        /// <summary>
        /// Keeps, in each lane, the largest one-based scan position of a nonzero coefficient over sixteen coefficients.
        /// </summary>
        /// <param name="values">The raster-order quantized coefficients.</param>
        /// <param name="inverseScan">The first scan position of the whole block.</param>
        /// <param name="offset">The raster offset of <paramref name="values"/>.</param>
        /// <param name="maximum">The running lane maxima.</param>
        /// <returns>The updated lane maxima.</returns>
        public static abstract Vector512<int> AccumulateEndOfBlock(Vector512<int> values, ref short inverseScan, nuint offset, Vector512<int> maximum);

        /// <summary>
        /// Keeps the larger of a running end position and the one-based scan position of a nonzero coefficient.
        /// </summary>
        /// <param name="value">The quantized coefficient.</param>
        /// <param name="position">Its zero-based scan position.</param>
        /// <param name="maximum">The running end position.</param>
        /// <returns>The updated end position.</returns>
        public static abstract int AccumulateEndOfBlock(int value, short position, int maximum);

        /// <summary>
        /// Adds the squared differences of four coefficient pairs to a running total.
        /// </summary>
        /// <param name="coefficients">The original coefficients.</param>
        /// <param name="reconstructed">The reconstructed coefficients.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector128<long> AccumulateSquaredDifferences(Vector128<int> coefficients, Vector128<int> reconstructed, Vector128<long> total);

        /// <summary>
        /// Adds the squared differences of eight coefficient pairs to a running total.
        /// </summary>
        /// <param name="coefficients">The original coefficients.</param>
        /// <param name="reconstructed">The reconstructed coefficients.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector256<long> AccumulateSquaredDifferences(Vector256<int> coefficients, Vector256<int> reconstructed, Vector256<long> total);

        /// <summary>
        /// Adds the squared differences of sixteen coefficient pairs to a running total.
        /// </summary>
        /// <param name="coefficients">The original coefficients.</param>
        /// <param name="reconstructed">The reconstructed coefficients.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector512<long> AccumulateSquaredDifferences(Vector512<int> coefficients, Vector512<int> reconstructed, Vector512<long> total);

        /// <summary>
        /// Adds the squared difference of one coefficient pair to a running total.
        /// </summary>
        /// <param name="coefficient">The original coefficient.</param>
        /// <param name="reconstructed">The reconstructed coefficient.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract long AccumulateSquaredDifferences(int coefficient, int reconstructed, long total);

        /// <summary>
        /// Keeps the low sixteen bits of four coefficients as signed values, as an int16 store does.
        /// </summary>
        /// <param name="values">The coefficients.</param>
        /// <returns>The sign-extended low sixteen bits.</returns>
        public static abstract Vector128<int> TruncateToInt16(Vector128<int> values);

        /// <summary>
        /// Keeps the low sixteen bits of eight coefficients as signed values, as an int16 store does.
        /// </summary>
        /// <param name="values">The coefficients.</param>
        /// <returns>The sign-extended low sixteen bits.</returns>
        public static abstract Vector256<int> TruncateToInt16(Vector256<int> values);

        /// <summary>
        /// Keeps the low sixteen bits of sixteen coefficients as signed values, as an int16 store does.
        /// </summary>
        /// <param name="values">The coefficients.</param>
        /// <returns>The sign-extended low sixteen bits.</returns>
        public static abstract Vector512<int> TruncateToInt16(Vector512<int> values);
    }

    /// <summary>
    /// Measures coefficient magnitudes lane by lane.
    /// </summary>
    internal readonly struct CoefficientMeasureOperator : ICoefficientMeasureOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<long> AccumulateAbsolute(Vector128<int> values, Vector128<long> total)
        {
            // The magnitudes widen before they join the total, so the total stays exact for any block.
            (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(Vector128.Abs(values));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<long> AccumulateAbsolute(Vector256<int> values, Vector256<long> total)
        {
            (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(Vector256.Abs(values));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<long> AccumulateAbsolute(Vector512<int> values, Vector512<long> total)
        {
            (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(Vector512.Abs(values));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AccumulateAbsolute(int value, long total) => total + Math.Abs(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> AccumulateMaximumAbsolute(Vector128<int> values, Vector128<int> maximum)
            => Vector128.Max(Vector128.Abs(values), maximum);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> AccumulateMaximumAbsolute(Vector256<int> values, Vector256<int> maximum)
            => Vector256.Max(Vector256.Abs(values), maximum);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> AccumulateMaximumAbsolute(Vector512<int> values, Vector512<int> maximum)
            => Vector512.Max(Vector512.Abs(values), maximum);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AccumulateMaximumAbsolute(int value, int maximum) => Math.Max(Math.Abs(value), maximum);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> AccumulateLevelBits(Vector128<int> values, Vector128<int> total)
        {
            // A quantized block is mostly zero, and a zero coefficient adds nothing.
            if (values == Vector128<int>.Zero)
            {
                return total;
            }

            // The vector has no leading-zero count, so the logarithm halves the remaining range in exact steps. The
            // two widest steps run only when a lane needs them. A nonzero lane's all-ones equality complement
            // subtracts as one.
            Vector128<int> level = Vector128.Abs(values) + Vector128<int>.One;
            Vector128<int> bits = total - ~Vector128.Equals(values, Vector128<int>.Zero);
            for (int step = 16; step > 0; step >>= 1)
            {
                Vector128<int> above = Vector128.GreaterThanOrEqual(level, Vector128.Create(1 << step));
                if (step < 4 || above != Vector128<int>.Zero)
                {
                    bits += above & Vector128.Create(step);
                    level = Vector128.ConditionalSelect(above, level >>> step, level);
                }
            }

            return bits;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> AccumulateLevelBits(Vector256<int> values, Vector256<int> total)
        {
            if (values == Vector256<int>.Zero)
            {
                return total;
            }

            Vector256<int> level = Vector256.Abs(values) + Vector256<int>.One;
            Vector256<int> bits = total - ~Vector256.Equals(values, Vector256<int>.Zero);
            for (int step = 16; step > 0; step >>= 1)
            {
                Vector256<int> above = Vector256.GreaterThanOrEqual(level, Vector256.Create(1 << step));
                if (step < 4 || above != Vector256<int>.Zero)
                {
                    bits += above & Vector256.Create(step);
                    level = Vector256.ConditionalSelect(above, level >>> step, level);
                }
            }

            return bits;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> AccumulateLevelBits(Vector512<int> values, Vector512<int> total)
        {
            if (values == Vector512<int>.Zero)
            {
                return total;
            }

            Vector512<int> level = Vector512.Abs(values) + Vector512<int>.One;
            Vector512<int> bits = total - ~Vector512.Equals(values, Vector512<int>.Zero);
            for (int step = 16; step > 0; step >>= 1)
            {
                Vector512<int> above = Vector512.GreaterThanOrEqual(level, Vector512.Create(1 << step));
                if (step < 4 || above != Vector512<int>.Zero)
                {
                    bits += above & Vector512.Create(step);
                    level = Vector512.ConditionalSelect(above, level >>> step, level);
                }
            }

            return bits;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AccumulateLevelBits(int value, int total)
            => total + BitOperations.Log2((uint)(Math.Abs(value) + 1)) + (value != 0 ? 1 : 0);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> AccumulateEndOfBlock(Vector128<int> values, ref short inverseScan, nuint offset, Vector128<int> maximum)
        {
            // A zero coefficient masks its position to zero, which never raises the maximum.
            Vector128<int> positions = Vector128.WidenLower(Vector64.LoadUnsafe(ref inverseScan, offset).ToVector128()) + Vector128<int>.One;
            return Vector128.Max(maximum, positions & ~Vector128.Equals(values, Vector128<int>.Zero));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> AccumulateEndOfBlock(Vector256<int> values, ref short inverseScan, nuint offset, Vector256<int> maximum)
        {
            Vector256<int> positions = Vector256.WidenLower(Vector128.LoadUnsafe(ref inverseScan, offset).ToVector256Unsafe()) + Vector256<int>.One;
            return Vector256.Max(maximum, positions & ~Vector256.Equals(values, Vector256<int>.Zero));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> AccumulateEndOfBlock(Vector512<int> values, ref short inverseScan, nuint offset, Vector512<int> maximum)
        {
            Vector512<int> positions = Vector512.WidenLower(Vector256.LoadUnsafe(ref inverseScan, offset).ToVector512Unsafe()) + Vector512<int>.One;
            return Vector512.Max(maximum, positions & ~Vector512.Equals(values, Vector512<int>.Zero));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AccumulateEndOfBlock(int value, short position, int maximum)
            => value != 0 ? Math.Max(maximum, position + 1) : maximum;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<long> AccumulateSquaredDifferences(Vector128<int> coefficients, Vector128<int> reconstructed, Vector128<long> total)
        {
            // A high-bit-depth difference can exceed sixteen bits, so the square needs sixty-four-bit lanes.
            (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(coefficients - reconstructed);
            return total + (lower * lower) + (upper * upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<long> AccumulateSquaredDifferences(Vector256<int> coefficients, Vector256<int> reconstructed, Vector256<long> total)
        {
            (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(coefficients - reconstructed);
            return total + (lower * lower) + (upper * upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<long> AccumulateSquaredDifferences(Vector512<int> coefficients, Vector512<int> reconstructed, Vector512<long> total)
        {
            (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(coefficients - reconstructed);
            return total + (lower * lower) + (upper * upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AccumulateSquaredDifferences(int coefficient, int reconstructed, long total)
        {
            long difference = (long)coefficient - reconstructed;
            return total + (difference * difference);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> TruncateToInt16(Vector128<int> values)
            => Vector128.ShiftRightArithmetic(Vector128.ShiftLeft(values, 16), 16);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> TruncateToInt16(Vector256<int> values)
            => Vector256.ShiftRightArithmetic(Vector256.ShiftLeft(values, 16), 16);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> TruncateToInt16(Vector512<int> values)
            => Vector512.ShiftRightArithmetic(Vector512.ShiftLeft(values, 16), 16);
    }
}

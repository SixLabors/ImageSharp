// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    }
}

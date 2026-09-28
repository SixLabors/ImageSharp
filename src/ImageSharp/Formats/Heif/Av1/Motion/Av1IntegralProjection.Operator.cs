// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Defines the lane arithmetic of the <see cref="Av1IntegralProjection"/> traversals.
/// </content>
internal static partial class Av1IntegralProjection
{
    /// <summary>
    /// Defines the integral projection arithmetic of eight-bit samples across hardware widths.
    /// </summary>
    /// <remarks>
    /// A column sum of at most 128 eight-bit samples is at most 32640, so it fits a sixteen-bit lane, as the
    /// int16 buffers of aom_int_pro_row hold it. A measure spreads its total across the lanes in no defined way;
    /// only the reduction of the total is defined.
    /// </remarks>
    internal interface IIntegralProjectionOperator
    {
        /// <summary>
        /// Adds eight samples of one row to eight column sums.
        /// </summary>
        /// <param name="row">The first sample of the row.</param>
        /// <param name="offset">The column offset.</param>
        /// <param name="total">The running column sums.</param>
        /// <returns>The updated column sums.</returns>
        public static abstract Vector128<ushort> AccumulateColumns(ref byte row, nuint offset, Vector128<ushort> total);

        /// <summary>
        /// Adds sixteen samples of one row to sixteen column sums.
        /// </summary>
        /// <param name="row">The first sample of the row.</param>
        /// <param name="offset">The column offset.</param>
        /// <param name="total">The running column sums.</param>
        /// <returns>The updated column sums.</returns>
        public static abstract Vector256<ushort> AccumulateColumns(ref byte row, nuint offset, Vector256<ushort> total);

        /// <summary>
        /// Adds thirty-two samples of one row to thirty-two column sums.
        /// </summary>
        /// <param name="row">The first sample of the row.</param>
        /// <param name="offset">The column offset.</param>
        /// <param name="total">The running column sums.</param>
        /// <returns>The updated column sums.</returns>
        public static abstract Vector512<ushort> AccumulateColumns(ref byte row, nuint offset, Vector512<ushort> total);

        /// <summary>
        /// Adds one sample to a column sum.
        /// </summary>
        /// <param name="value">The sample.</param>
        /// <param name="total">The running column sum.</param>
        /// <returns>The updated column sum.</returns>
        public static abstract int AccumulateColumns(byte value, int total);

        /// <summary>
        /// Adds sixteen samples of one row to a running row sum.
        /// </summary>
        /// <param name="values">The samples.</param>
        /// <param name="total">The running row sum.</param>
        /// <returns>The updated row sum.</returns>
        public static abstract Vector128<uint> AccumulateRow(Vector128<byte> values, Vector128<uint> total);

        /// <summary>
        /// Adds thirty-two samples of one row to a running row sum.
        /// </summary>
        /// <param name="values">The samples.</param>
        /// <param name="total">The running row sum.</param>
        /// <returns>The updated row sum.</returns>
        public static abstract Vector256<uint> AccumulateRow(Vector256<byte> values, Vector256<uint> total);

        /// <summary>
        /// Adds sixty-four samples of one row to a running row sum.
        /// </summary>
        /// <param name="values">The samples.</param>
        /// <param name="total">The running row sum.</param>
        /// <returns>The updated row sum.</returns>
        public static abstract Vector512<uint> AccumulateRow(Vector512<byte> values, Vector512<uint> total);

        /// <summary>
        /// Adds the differences of eight projection pairs, and their squares, to two running totals.
        /// </summary>
        /// <param name="reference">The reference projection values.</param>
        /// <param name="source">The source projection values.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        public static abstract void AccumulateMoments(Vector128<short> reference, Vector128<short> source, ref Vector128<int> sum, ref Vector128<int> squares);

        /// <summary>
        /// Adds the differences of sixteen projection pairs, and their squares, to two running totals.
        /// </summary>
        /// <param name="reference">The reference projection values.</param>
        /// <param name="source">The source projection values.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        public static abstract void AccumulateMoments(Vector256<short> reference, Vector256<short> source, ref Vector256<int> sum, ref Vector256<int> squares);

        /// <summary>
        /// Adds the differences of thirty-two projection pairs, and their squares, to two running totals.
        /// </summary>
        /// <param name="reference">The reference projection values.</param>
        /// <param name="source">The source projection values.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        public static abstract void AccumulateMoments(Vector512<short> reference, Vector512<short> source, ref Vector512<int> sum, ref Vector512<int> squares);

        /// <summary>
        /// Adds the difference of one projection pair, and its square, to two running totals.
        /// </summary>
        /// <param name="reference">The reference projection value.</param>
        /// <param name="source">The source projection value.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        public static abstract void AccumulateMoments(short reference, short source, ref int sum, ref int squares);
    }

    /// <summary>
    /// Applies the integral projection arithmetic of libaom lane by lane.
    /// </summary>
    internal readonly struct IntegralProjectionOperator : IIntegralProjectionOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> AccumulateColumns(ref byte row, nuint offset, Vector128<ushort> total)
            => total + Vector128.WidenLower(Vector64.LoadUnsafe(ref row, offset).ToVector128());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> AccumulateColumns(ref byte row, nuint offset, Vector256<ushort> total)
            => total + Vector256.WidenLower(Vector128.LoadUnsafe(ref row, offset).ToVector256Unsafe());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> AccumulateColumns(ref byte row, nuint offset, Vector512<ushort> total)
            => total + Vector512.WidenLower(Vector256.LoadUnsafe(ref row, offset).ToVector512Unsafe());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AccumulateColumns(byte value, int total) => total + value;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> AccumulateRow(Vector128<byte> values, Vector128<uint> total)
            => Vector128_.SumAbsoluteDifferences(values, Vector128<byte>.Zero, total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> AccumulateRow(Vector256<byte> values, Vector256<uint> total)
            => Vector256_.SumAbsoluteDifferences(values, Vector256<byte>.Zero, total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> AccumulateRow(Vector512<byte> values, Vector512<uint> total)
            => Vector512_.SumAbsoluteDifferences(values, Vector512<byte>.Zero, total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector128<short> reference, Vector128<short> source, ref Vector128<int> sum, ref Vector128<int> squares)
        {
            // A projection is at most 1020, so a difference and a pairwise sum of squares both stay far inside
            // a thirty-two-bit lane. Multiplying by one reuses the pairwise instruction for the plain sum.
            Vector128<short> difference = reference - source;
            sum += Vector128_.MultiplyAddAdjacent(difference, Vector128.Create((short)1));
            squares += Vector128_.MultiplyAddAdjacent(difference, difference);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector256<short> reference, Vector256<short> source, ref Vector256<int> sum, ref Vector256<int> squares)
        {
            Vector256<short> difference = reference - source;
            sum += Vector256_.MultiplyAddAdjacent(difference, Vector256.Create((short)1));
            squares += Vector256_.MultiplyAddAdjacent(difference, difference);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector512<short> reference, Vector512<short> source, ref Vector512<int> sum, ref Vector512<int> squares)
        {
            Vector512<short> difference = reference - source;
            sum += Vector512_.MultiplyAddAdjacent(difference, Vector512.Create((short)1));
            squares += Vector512_.MultiplyAddAdjacent(difference, difference);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(short reference, short source, ref int sum, ref int squares)
        {
            int difference = reference - source;
            sum += difference;
            squares += difference * difference;
        }
    }
}

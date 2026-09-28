// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Defines the sample-width-specific arithmetic used by the <see cref="Av1ObmcSearch"/> traversals.
/// </content>
internal static partial class Av1ObmcSearch
{
    /// <summary>
    /// Defines the OBMC search arithmetic for one sample storage type across hardware widths.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <remarks>
    /// <para>
    /// One lane is one sample, widened to thirty-two bits because the weighted source and the mask are
    /// thirty-two-bit arrays. A vector overload reads as many samples as its register has thirty-two-bit
    /// lanes: four, eight or sixteen. The narrowest overload therefore covers the four-sample overlap of
    /// an eight-sample block, and no OBMC row is left to the scalar overload.
    /// </para>
    /// <para>
    /// A measure returns a lane-shaped total, not a scalar one, so that the traversal reduces each
    /// total once and never inside its loop. The spread of a total across the lanes is not defined.
    /// Only the sum of all lanes is.
    /// </para>
    /// </remarks>
    internal interface IObmcOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Adds the rounded OBMC absolute differences of four samples to a running total.
        /// </summary>
        /// <param name="prediction">The first prediction sample.</param>
        /// <param name="weightedSource">The first weighted source value.</param>
        /// <param name="mask">The first prediction weight.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector128<uint> AccumulateAbsoluteDifferences(ref TSample prediction, ref int weightedSource, ref int mask, Vector128<uint> total);

        /// <summary>
        /// Adds the rounded OBMC absolute differences of eight samples to a running total.
        /// </summary>
        /// <param name="prediction">The first prediction sample.</param>
        /// <param name="weightedSource">The first weighted source value.</param>
        /// <param name="mask">The first prediction weight.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector256<uint> AccumulateAbsoluteDifferences(ref TSample prediction, ref int weightedSource, ref int mask, Vector256<uint> total);

        /// <summary>
        /// Adds the rounded OBMC absolute differences of sixteen samples to a running total.
        /// </summary>
        /// <param name="prediction">The first prediction sample.</param>
        /// <param name="weightedSource">The first weighted source value.</param>
        /// <param name="mask">The first prediction weight.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector512<uint> AccumulateAbsoluteDifferences(ref TSample prediction, ref int weightedSource, ref int mask, Vector512<uint> total);

        /// <summary>
        /// Adds the rounded OBMC absolute difference of one sample to a running total.
        /// </summary>
        /// <param name="prediction">The prediction sample.</param>
        /// <param name="weightedSource">The weighted source value.</param>
        /// <param name="mask">The prediction weight.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        public static abstract uint AccumulateAbsoluteDifferences(TSample prediction, int weightedSource, int mask, uint total);

        /// <summary>
        /// Adds the rounded OBMC differences of four samples, and their squares, to two running totals.
        /// </summary>
        /// <param name="prediction">The first prediction sample.</param>
        /// <param name="weightedSource">The first weighted source value.</param>
        /// <param name="mask">The first prediction weight.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        public static abstract void AccumulateMoments(ref TSample prediction, ref int weightedSource, ref int mask, ref Vector128<int> sum, ref Vector128<uint> squares);

        /// <summary>
        /// Adds the rounded OBMC differences of eight samples, and their squares, to two running totals.
        /// </summary>
        /// <param name="prediction">The first prediction sample.</param>
        /// <param name="weightedSource">The first weighted source value.</param>
        /// <param name="mask">The first prediction weight.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        public static abstract void AccumulateMoments(ref TSample prediction, ref int weightedSource, ref int mask, ref Vector256<int> sum, ref Vector256<uint> squares);

        /// <summary>
        /// Adds the rounded OBMC differences of sixteen samples, and their squares, to two running totals.
        /// </summary>
        /// <param name="prediction">The first prediction sample.</param>
        /// <param name="weightedSource">The first weighted source value.</param>
        /// <param name="mask">The first prediction weight.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        public static abstract void AccumulateMoments(ref TSample prediction, ref int weightedSource, ref int mask, ref Vector512<int> sum, ref Vector512<uint> squares);

        /// <summary>
        /// Adds the rounded OBMC difference of one sample, and its square, to two running totals.
        /// </summary>
        /// <param name="prediction">The prediction sample.</param>
        /// <param name="weightedSource">The weighted source value.</param>
        /// <param name="mask">The prediction weight.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        public static abstract void AccumulateMoments(TSample prediction, int weightedSource, int mask, ref int sum, ref ulong squares);

        /// <summary>
        /// Writes the above-neighbor term of four target values, all at one row weight.
        /// </summary>
        /// <param name="prediction">The first sample of the above-neighbor prediction.</param>
        /// <param name="weight">The weight of the block's own prediction, in every lane.</param>
        /// <param name="weightedSource">The first weighted source value to write.</param>
        /// <param name="mask">The first prediction weight to write.</param>
        public static abstract void WeightAbove(ref TSample prediction, Vector128<int> weight, ref int weightedSource, ref int mask);

        /// <summary>
        /// Writes the above-neighbor term of eight target values, all at one row weight.
        /// </summary>
        /// <param name="prediction">The first sample of the above-neighbor prediction.</param>
        /// <param name="weight">The weight of the block's own prediction, in every lane.</param>
        /// <param name="weightedSource">The first weighted source value to write.</param>
        /// <param name="mask">The first prediction weight to write.</param>
        public static abstract void WeightAbove(ref TSample prediction, Vector256<int> weight, ref int weightedSource, ref int mask);

        /// <summary>
        /// Writes the above-neighbor term of sixteen target values, all at one row weight.
        /// </summary>
        /// <param name="prediction">The first sample of the above-neighbor prediction.</param>
        /// <param name="weight">The weight of the block's own prediction, in every lane.</param>
        /// <param name="weightedSource">The first weighted source value to write.</param>
        /// <param name="mask">The first prediction weight to write.</param>
        public static abstract void WeightAbove(ref TSample prediction, Vector512<int> weight, ref int weightedSource, ref int mask);

        /// <summary>
        /// Writes the above-neighbor term of one target value.
        /// </summary>
        /// <param name="prediction">The above-neighbor prediction sample.</param>
        /// <param name="weight">The weight of the block's own prediction.</param>
        /// <param name="weightedSource">The weighted source value to write.</param>
        /// <param name="mask">The prediction weight to write.</param>
        public static abstract void WeightAbove(TSample prediction, int weight, ref int weightedSource, ref int mask);

        /// <summary>
        /// Blends the left-neighbor term into four target values, one column weight per value.
        /// </summary>
        /// <param name="prediction">The first sample of the left-neighbor prediction.</param>
        /// <param name="weights">The first column weight of the block's own prediction.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void WeightLeft(ref TSample prediction, ref byte weights, ref int weightedSource, ref int mask, Vector128<int> lanes);

        /// <summary>
        /// Blends the left-neighbor term into eight target values, one column weight per value.
        /// </summary>
        /// <param name="prediction">The first sample of the left-neighbor prediction.</param>
        /// <param name="weights">The first column weight of the block's own prediction.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void WeightLeft(ref TSample prediction, ref byte weights, ref int weightedSource, ref int mask, Vector256<int> lanes);

        /// <summary>
        /// Blends the left-neighbor term into sixteen target values, one column weight per value.
        /// </summary>
        /// <param name="prediction">The first sample of the left-neighbor prediction.</param>
        /// <param name="weights">The first column weight of the block's own prediction.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void WeightLeft(ref TSample prediction, ref byte weights, ref int weightedSource, ref int mask, Vector512<int> lanes);

        /// <summary>
        /// Blends the left-neighbor term into one target value.
        /// </summary>
        /// <param name="prediction">The left-neighbor prediction sample.</param>
        /// <param name="weight">The column weight of the block's own prediction.</param>
        /// <param name="weightedSource">The weighted source value to update.</param>
        /// <param name="mask">The prediction weight to update.</param>
        public static abstract void WeightLeft(TSample prediction, int weight, ref int weightedSource, ref int mask);

        /// <summary>
        /// Scales four weighted source values and prediction weights by the maximum blend weight.
        /// </summary>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void Scale(ref int weightedSource, ref int mask, Vector128<int> lanes);

        /// <summary>
        /// Scales eight weighted source values and prediction weights by the maximum blend weight.
        /// </summary>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void Scale(ref int weightedSource, ref int mask, Vector256<int> lanes);

        /// <summary>
        /// Scales sixteen weighted source values and prediction weights by the maximum blend weight.
        /// </summary>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void Scale(ref int weightedSource, ref int mask, Vector512<int> lanes);

        /// <summary>
        /// Scales one weighted source value and prediction weight by the maximum blend weight.
        /// </summary>
        /// <param name="weightedSource">The weighted source value to update.</param>
        /// <param name="mask">The prediction weight to update.</param>
        public static abstract void Scale(ref int weightedSource, ref int mask);

        /// <summary>
        /// Replaces four neighbor terms with the scaled source minus the neighbor term.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SubtractFromSource(ref TSample source, ref int weightedSource, Vector128<int> lanes);

        /// <summary>
        /// Replaces eight neighbor terms with the scaled source minus the neighbor term.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SubtractFromSource(ref TSample source, ref int weightedSource, Vector256<int> lanes);

        /// <summary>
        /// Replaces sixteen neighbor terms with the scaled source minus the neighbor term.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SubtractFromSource(ref TSample source, ref int weightedSource, Vector512<int> lanes);

        /// <summary>
        /// Replaces one neighbor term with the scaled source minus the neighbor term.
        /// </summary>
        /// <param name="source">The source sample.</param>
        /// <param name="weightedSource">The weighted source value to update.</param>
        public static abstract void SubtractFromSource(TSample source, ref int weightedSource);
    }

    /// <summary>
    /// Widens eight-bit samples to thirty-two-bit lanes and applies the shared OBMC lane arithmetic.
    /// </summary>
    internal readonly struct ByteOperator : IObmcOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> AccumulateAbsoluteDifferences(ref byte prediction, ref int weightedSource, ref int mask, Vector128<uint> total)
            => ObmcLanes.AccumulateAbsoluteDifferences(ObmcLanes.LoadBytes(ref prediction, default(Vector128<int>)), Vector128.LoadUnsafe(ref weightedSource), Vector128.LoadUnsafe(ref mask), total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> AccumulateAbsoluteDifferences(ref byte prediction, ref int weightedSource, ref int mask, Vector256<uint> total)
            => ObmcLanes.AccumulateAbsoluteDifferences(ObmcLanes.LoadBytes(ref prediction, default(Vector256<int>)), Vector256.LoadUnsafe(ref weightedSource), Vector256.LoadUnsafe(ref mask), total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> AccumulateAbsoluteDifferences(ref byte prediction, ref int weightedSource, ref int mask, Vector512<uint> total)
            => ObmcLanes.AccumulateAbsoluteDifferences(ObmcLanes.LoadBytes(ref prediction, default(Vector512<int>)), Vector512.LoadUnsafe(ref weightedSource), Vector512.LoadUnsafe(ref mask), total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint AccumulateAbsoluteDifferences(byte prediction, int weightedSource, int mask, uint total)
            => ObmcLanes.AccumulateAbsoluteDifferences(prediction, weightedSource, mask, total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(ref byte prediction, ref int weightedSource, ref int mask, ref Vector128<int> sum, ref Vector128<uint> squares)
            => ObmcLanes.AccumulateMoments(ObmcLanes.LoadBytes(ref prediction, default(Vector128<int>)), Vector128.LoadUnsafe(ref weightedSource), Vector128.LoadUnsafe(ref mask), ref sum, ref squares);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(ref byte prediction, ref int weightedSource, ref int mask, ref Vector256<int> sum, ref Vector256<uint> squares)
            => ObmcLanes.AccumulateMoments(ObmcLanes.LoadBytes(ref prediction, default(Vector256<int>)), Vector256.LoadUnsafe(ref weightedSource), Vector256.LoadUnsafe(ref mask), ref sum, ref squares);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(ref byte prediction, ref int weightedSource, ref int mask, ref Vector512<int> sum, ref Vector512<uint> squares)
            => ObmcLanes.AccumulateMoments(ObmcLanes.LoadBytes(ref prediction, default(Vector512<int>)), Vector512.LoadUnsafe(ref weightedSource), Vector512.LoadUnsafe(ref mask), ref sum, ref squares);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(byte prediction, int weightedSource, int mask, ref int sum, ref ulong squares)
            => ObmcLanes.AccumulateMoments(prediction, weightedSource, mask, ref sum, ref squares);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(ref byte prediction, Vector128<int> weight, ref int weightedSource, ref int mask)
            => ObmcLanes.WeightAbove(ObmcLanes.LoadBytes(ref prediction, default(Vector128<int>)), weight, ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(ref byte prediction, Vector256<int> weight, ref int weightedSource, ref int mask)
            => ObmcLanes.WeightAbove(ObmcLanes.LoadBytes(ref prediction, default(Vector256<int>)), weight, ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(ref byte prediction, Vector512<int> weight, ref int weightedSource, ref int mask)
            => ObmcLanes.WeightAbove(ObmcLanes.LoadBytes(ref prediction, default(Vector512<int>)), weight, ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(byte prediction, int weight, ref int weightedSource, ref int mask)
            => ObmcLanes.WeightAbove(prediction, weight, ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(ref byte prediction, ref byte weights, ref int weightedSource, ref int mask, Vector128<int> lanes)
            => ObmcLanes.WeightLeft(ObmcLanes.LoadBytes(ref prediction, lanes), ObmcLanes.LoadBytes(ref weights, lanes), ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(ref byte prediction, ref byte weights, ref int weightedSource, ref int mask, Vector256<int> lanes)
            => ObmcLanes.WeightLeft(ObmcLanes.LoadBytes(ref prediction, lanes), ObmcLanes.LoadBytes(ref weights, lanes), ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(ref byte prediction, ref byte weights, ref int weightedSource, ref int mask, Vector512<int> lanes)
            => ObmcLanes.WeightLeft(ObmcLanes.LoadBytes(ref prediction, lanes), ObmcLanes.LoadBytes(ref weights, lanes), ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(byte prediction, int weight, ref int weightedSource, ref int mask)
            => ObmcLanes.WeightLeft(prediction, weight, ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask, Vector128<int> lanes)
            => ObmcLanes.Scale(ref weightedSource, ref mask, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask, Vector256<int> lanes)
            => ObmcLanes.Scale(ref weightedSource, ref mask, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask, Vector512<int> lanes)
            => ObmcLanes.Scale(ref weightedSource, ref mask, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask)
            => ObmcLanes.Scale(ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(ref byte source, ref int weightedSource, Vector128<int> lanes)
            => ObmcLanes.SubtractFromSource(ObmcLanes.LoadBytes(ref source, lanes), ref weightedSource);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(ref byte source, ref int weightedSource, Vector256<int> lanes)
            => ObmcLanes.SubtractFromSource(ObmcLanes.LoadBytes(ref source, lanes), ref weightedSource);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(ref byte source, ref int weightedSource, Vector512<int> lanes)
            => ObmcLanes.SubtractFromSource(ObmcLanes.LoadBytes(ref source, lanes), ref weightedSource);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(byte source, ref int weightedSource)
            => ObmcLanes.SubtractFromSource(source, ref weightedSource);
    }

    /// <summary>
    /// Widens high-bit-depth samples to thirty-two-bit lanes and applies the shared OBMC lane arithmetic.
    /// </summary>
    internal readonly struct UInt16Operator : IObmcOperator<ushort>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> AccumulateAbsoluteDifferences(ref ushort prediction, ref int weightedSource, ref int mask, Vector128<uint> total)
            => ObmcLanes.AccumulateAbsoluteDifferences(ObmcLanes.LoadWords(ref prediction, default(Vector128<int>)), Vector128.LoadUnsafe(ref weightedSource), Vector128.LoadUnsafe(ref mask), total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> AccumulateAbsoluteDifferences(ref ushort prediction, ref int weightedSource, ref int mask, Vector256<uint> total)
            => ObmcLanes.AccumulateAbsoluteDifferences(ObmcLanes.LoadWords(ref prediction, default(Vector256<int>)), Vector256.LoadUnsafe(ref weightedSource), Vector256.LoadUnsafe(ref mask), total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> AccumulateAbsoluteDifferences(ref ushort prediction, ref int weightedSource, ref int mask, Vector512<uint> total)
            => ObmcLanes.AccumulateAbsoluteDifferences(ObmcLanes.LoadWords(ref prediction, default(Vector512<int>)), Vector512.LoadUnsafe(ref weightedSource), Vector512.LoadUnsafe(ref mask), total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint AccumulateAbsoluteDifferences(ushort prediction, int weightedSource, int mask, uint total)
            => ObmcLanes.AccumulateAbsoluteDifferences(prediction, weightedSource, mask, total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(ref ushort prediction, ref int weightedSource, ref int mask, ref Vector128<int> sum, ref Vector128<uint> squares)
            => ObmcLanes.AccumulateMoments(ObmcLanes.LoadWords(ref prediction, default(Vector128<int>)), Vector128.LoadUnsafe(ref weightedSource), Vector128.LoadUnsafe(ref mask), ref sum, ref squares);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(ref ushort prediction, ref int weightedSource, ref int mask, ref Vector256<int> sum, ref Vector256<uint> squares)
            => ObmcLanes.AccumulateMoments(ObmcLanes.LoadWords(ref prediction, default(Vector256<int>)), Vector256.LoadUnsafe(ref weightedSource), Vector256.LoadUnsafe(ref mask), ref sum, ref squares);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(ref ushort prediction, ref int weightedSource, ref int mask, ref Vector512<int> sum, ref Vector512<uint> squares)
            => ObmcLanes.AccumulateMoments(ObmcLanes.LoadWords(ref prediction, default(Vector512<int>)), Vector512.LoadUnsafe(ref weightedSource), Vector512.LoadUnsafe(ref mask), ref sum, ref squares);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(ushort prediction, int weightedSource, int mask, ref int sum, ref ulong squares)
            => ObmcLanes.AccumulateMoments(prediction, weightedSource, mask, ref sum, ref squares);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(ref ushort prediction, Vector128<int> weight, ref int weightedSource, ref int mask)
            => ObmcLanes.WeightAbove(ObmcLanes.LoadWords(ref prediction, default(Vector128<int>)), weight, ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(ref ushort prediction, Vector256<int> weight, ref int weightedSource, ref int mask)
            => ObmcLanes.WeightAbove(ObmcLanes.LoadWords(ref prediction, default(Vector256<int>)), weight, ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(ref ushort prediction, Vector512<int> weight, ref int weightedSource, ref int mask)
            => ObmcLanes.WeightAbove(ObmcLanes.LoadWords(ref prediction, default(Vector512<int>)), weight, ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(ushort prediction, int weight, ref int weightedSource, ref int mask)
            => ObmcLanes.WeightAbove(prediction, weight, ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(ref ushort prediction, ref byte weights, ref int weightedSource, ref int mask, Vector128<int> lanes)
            => ObmcLanes.WeightLeft(ObmcLanes.LoadWords(ref prediction, lanes), ObmcLanes.LoadBytes(ref weights, lanes), ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(ref ushort prediction, ref byte weights, ref int weightedSource, ref int mask, Vector256<int> lanes)
            => ObmcLanes.WeightLeft(ObmcLanes.LoadWords(ref prediction, lanes), ObmcLanes.LoadBytes(ref weights, lanes), ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(ref ushort prediction, ref byte weights, ref int weightedSource, ref int mask, Vector512<int> lanes)
            => ObmcLanes.WeightLeft(ObmcLanes.LoadWords(ref prediction, lanes), ObmcLanes.LoadBytes(ref weights, lanes), ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(ushort prediction, int weight, ref int weightedSource, ref int mask)
            => ObmcLanes.WeightLeft(prediction, weight, ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask, Vector128<int> lanes)
            => ObmcLanes.Scale(ref weightedSource, ref mask, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask, Vector256<int> lanes)
            => ObmcLanes.Scale(ref weightedSource, ref mask, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask, Vector512<int> lanes)
            => ObmcLanes.Scale(ref weightedSource, ref mask, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask)
            => ObmcLanes.Scale(ref weightedSource, ref mask);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(ref ushort source, ref int weightedSource, Vector128<int> lanes)
            => ObmcLanes.SubtractFromSource(ObmcLanes.LoadWords(ref source, lanes), ref weightedSource);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(ref ushort source, ref int weightedSource, Vector256<int> lanes)
            => ObmcLanes.SubtractFromSource(ObmcLanes.LoadWords(ref source, lanes), ref weightedSource);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(ref ushort source, ref int weightedSource, Vector512<int> lanes)
            => ObmcLanes.SubtractFromSource(ObmcLanes.LoadWords(ref source, lanes), ref weightedSource);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(ushort source, ref int weightedSource)
            => ObmcLanes.SubtractFromSource(source, ref weightedSource);
    }

    /// <summary>
    /// Holds the OBMC lane arithmetic that both sample operators share once their samples are widened.
    /// </summary>
    /// <remarks>
    /// Every lane is an independent sample, so each width repeats the arithmetic of the scalar form
    /// lane by lane. The products fit a thirty-two-bit lane: a twelve-bit sample times a weight of at
    /// most 64 * 64 is below 2^24.
    /// </remarks>
    private static class ObmcLanes
    {
        /// <summary>
        /// The rounding shift of an OBMC difference: the weighted source carries two blend weights of 64.
        /// </summary>
        private const int DifferenceShift = 12;

        /// <summary>
        /// The half of the rounding divisor.
        /// </summary>
        private const int DifferenceBias = 1 << (DifferenceShift - 1);

        /// <summary>
        /// The shift of the maximum blend weight, AOM_BLEND_A64_MAX_ALPHA.
        /// </summary>
        private const int AlphaShift = 6;

        /// <summary>
        /// The maximum blend weight, AOM_BLEND_A64_MAX_ALPHA.
        /// </summary>
        private const int MaximumAlpha = 1 << AlphaShift;

        /// <summary>
        /// Loads four bytes and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadBytes(ref byte source, Vector128<int> lanes)
        {
            // Exactly four bytes are read, so the last group of a row never touches the row that
            // follows it or the end of the plane.
            Vector128<byte> bytes = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<uint>(ref source)).AsByte();
            return Vector128.WidenLower(Vector128.WidenLower(bytes)).AsInt32();
        }

        /// <summary>
        /// Loads eight bytes and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> LoadBytes(ref byte source, Vector256<int> lanes)
        {
            Vector128<ushort> words = Vector128.WidenLower(Vector64.LoadUnsafe(ref source).ToVector128());
            return Vector256.WidenLower(words.ToVector256Unsafe()).AsInt32();
        }

        /// <summary>
        /// Loads sixteen bytes and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> LoadBytes(ref byte source, Vector512<int> lanes)
        {
            Vector256<ushort> words = Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe());
            return Vector512.WidenLower(words.ToVector512Unsafe()).AsInt32();
        }

        /// <summary>
        /// Loads four words and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first word.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadWords(ref ushort source, Vector128<int> lanes)
            => Vector128.WidenLower(Vector64.LoadUnsafe(ref source).ToVector128()).AsInt32();

        /// <summary>
        /// Loads eight words and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first word.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> LoadWords(ref ushort source, Vector256<int> lanes)
            => Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe()).AsInt32();

        /// <summary>
        /// Loads sixteen words and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first word.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> LoadWords(ref ushort source, Vector512<int> lanes)
            => Vector512.WidenLower(Vector256.LoadUnsafe(ref source).ToVector512Unsafe()).AsInt32();

        /// <summary>
        /// Adds the rounded absolute differences of four lanes. Reference: obmc_sad_w8n().
        /// </summary>
        /// <param name="prediction">The widened prediction samples.</param>
        /// <param name="weightedSource">The weighted source values.</param>
        /// <param name="mask">The prediction weights.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> AccumulateAbsoluteDifferences(Vector128<int> prediction, Vector128<int> weightedSource, Vector128<int> mask, Vector128<uint> total)
        {
            // The absolute difference is below 2^24, so the biased value stays positive and the
            // logical shift is the rounding of xx_roundn_epu32().
            Vector128<uint> difference = Vector128.Abs(weightedSource - (prediction * mask)).AsUInt32();
            return total + Vector128.ShiftRightLogical(difference + Vector128.Create((uint)DifferenceBias), DifferenceShift);
        }

        /// <summary>
        /// Adds the rounded absolute differences of eight lanes. Reference: obmc_sad_w8n().
        /// </summary>
        /// <param name="prediction">The widened prediction samples.</param>
        /// <param name="weightedSource">The weighted source values.</param>
        /// <param name="mask">The prediction weights.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> AccumulateAbsoluteDifferences(Vector256<int> prediction, Vector256<int> weightedSource, Vector256<int> mask, Vector256<uint> total)
        {
            Vector256<uint> difference = Vector256.Abs(weightedSource - (prediction * mask)).AsUInt32();
            return total + Vector256.ShiftRightLogical(difference + Vector256.Create((uint)DifferenceBias), DifferenceShift);
        }

        /// <summary>
        /// Adds the rounded absolute differences of sixteen lanes. Reference: obmc_sad_w8n().
        /// </summary>
        /// <param name="prediction">The widened prediction samples.</param>
        /// <param name="weightedSource">The weighted source values.</param>
        /// <param name="mask">The prediction weights.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> AccumulateAbsoluteDifferences(Vector512<int> prediction, Vector512<int> weightedSource, Vector512<int> mask, Vector512<uint> total)
        {
            Vector512<uint> difference = Vector512.Abs(weightedSource - (prediction * mask)).AsUInt32();
            return total + Vector512.ShiftRightLogical(difference + Vector512.Create((uint)DifferenceBias), DifferenceShift);
        }

        /// <summary>
        /// Adds the rounded absolute difference of one sample. Reference: obmc_sad().
        /// </summary>
        /// <param name="prediction">The prediction sample.</param>
        /// <param name="weightedSource">The weighted source value.</param>
        /// <param name="mask">The prediction weight.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint AccumulateAbsoluteDifferences(int prediction, int weightedSource, int mask, uint total)
            => total + (uint)((Math.Abs(weightedSource - (prediction * mask)) + DifferenceBias) >> DifferenceShift);

        /// <summary>
        /// Adds the rounded differences of four lanes and their squares. Reference: obmc_variance_w8n().
        /// </summary>
        /// <param name="prediction">The widened prediction samples.</param>
        /// <param name="weightedSource">The weighted source values.</param>
        /// <param name="mask">The prediction weights.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector128<int> prediction, Vector128<int> weightedSource, Vector128<int> mask, ref Vector128<int> sum, ref Vector128<uint> squares)
        {
            // The sign lane is -1 for a negative value, which lowers the bias by one. The arithmetic
            // shift then rounds half away from zero, as ROUND_POWER_OF_TWO_SIGNED() does. This is the
            // rounding of xx_roundn_epi32().
            Vector128<int> value = weightedSource - (prediction * mask);
            Vector128<int> difference = Vector128.ShiftRightArithmetic(
                value + Vector128.Create(DifferenceBias) + Vector128.ShiftRightArithmetic(value, 31),
                DifferenceShift);
            sum += difference;
            squares += (difference * difference).AsUInt32();
        }

        /// <summary>
        /// Adds the rounded differences of eight lanes and their squares. Reference: obmc_variance_w8n().
        /// </summary>
        /// <param name="prediction">The widened prediction samples.</param>
        /// <param name="weightedSource">The weighted source values.</param>
        /// <param name="mask">The prediction weights.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector256<int> prediction, Vector256<int> weightedSource, Vector256<int> mask, ref Vector256<int> sum, ref Vector256<uint> squares)
        {
            Vector256<int> value = weightedSource - (prediction * mask);
            Vector256<int> difference = Vector256.ShiftRightArithmetic(
                value + Vector256.Create(DifferenceBias) + Vector256.ShiftRightArithmetic(value, 31),
                DifferenceShift);
            sum += difference;
            squares += (difference * difference).AsUInt32();
        }

        /// <summary>
        /// Adds the rounded differences of sixteen lanes and their squares. Reference: obmc_variance_w8n().
        /// </summary>
        /// <param name="prediction">The widened prediction samples.</param>
        /// <param name="weightedSource">The weighted source values.</param>
        /// <param name="mask">The prediction weights.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector512<int> prediction, Vector512<int> weightedSource, Vector512<int> mask, ref Vector512<int> sum, ref Vector512<uint> squares)
        {
            Vector512<int> value = weightedSource - (prediction * mask);
            Vector512<int> difference = Vector512.ShiftRightArithmetic(
                value + Vector512.Create(DifferenceBias) + Vector512.ShiftRightArithmetic(value, 31),
                DifferenceShift);
            sum += difference;
            squares += (difference * difference).AsUInt32();
        }

        /// <summary>
        /// Adds the rounded difference of one sample and its square. Reference: obmc_variance().
        /// </summary>
        /// <param name="prediction">The prediction sample.</param>
        /// <param name="weightedSource">The weighted source value.</param>
        /// <param name="mask">The prediction weight.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(int prediction, int weightedSource, int mask, ref int sum, ref ulong squares)
        {
            int value = weightedSource - (prediction * mask);
            int difference = (value + DifferenceBias + (value >> 31)) >> DifferenceShift;
            sum += difference;
            squares += (uint)(difference * difference);
        }

        /// <summary>
        /// Writes the above-neighbor term of four lanes. Reference: calc_target_weighted_pred_above().
        /// </summary>
        /// <param name="prediction">The widened above-neighbor prediction.</param>
        /// <param name="weight">The weight of the block's own prediction.</param>
        /// <param name="weightedSource">The first weighted source value to write.</param>
        /// <param name="mask">The first prediction weight to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(Vector128<int> prediction, Vector128<int> weight, ref int weightedSource, ref int mask)
        {
            ((Vector128.Create(MaximumAlpha) - weight) * prediction).StoreUnsafe(ref weightedSource);
            weight.StoreUnsafe(ref mask);
        }

        /// <summary>
        /// Writes the above-neighbor term of eight lanes. Reference: calc_target_weighted_pred_above().
        /// </summary>
        /// <param name="prediction">The widened above-neighbor prediction.</param>
        /// <param name="weight">The weight of the block's own prediction.</param>
        /// <param name="weightedSource">The first weighted source value to write.</param>
        /// <param name="mask">The first prediction weight to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(Vector256<int> prediction, Vector256<int> weight, ref int weightedSource, ref int mask)
        {
            ((Vector256.Create(MaximumAlpha) - weight) * prediction).StoreUnsafe(ref weightedSource);
            weight.StoreUnsafe(ref mask);
        }

        /// <summary>
        /// Writes the above-neighbor term of sixteen lanes. Reference: calc_target_weighted_pred_above().
        /// </summary>
        /// <param name="prediction">The widened above-neighbor prediction.</param>
        /// <param name="weight">The weight of the block's own prediction.</param>
        /// <param name="weightedSource">The first weighted source value to write.</param>
        /// <param name="mask">The first prediction weight to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(Vector512<int> prediction, Vector512<int> weight, ref int weightedSource, ref int mask)
        {
            ((Vector512.Create(MaximumAlpha) - weight) * prediction).StoreUnsafe(ref weightedSource);
            weight.StoreUnsafe(ref mask);
        }

        /// <summary>
        /// Writes the above-neighbor term of one sample. Reference: calc_target_weighted_pred_above().
        /// </summary>
        /// <param name="prediction">The above-neighbor prediction sample.</param>
        /// <param name="weight">The weight of the block's own prediction.</param>
        /// <param name="weightedSource">The weighted source value to write.</param>
        /// <param name="mask">The prediction weight to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightAbove(int prediction, int weight, ref int weightedSource, ref int mask)
        {
            weightedSource = (MaximumAlpha - weight) * prediction;
            mask = weight;
        }

        /// <summary>
        /// Blends the left-neighbor term into four lanes. Reference: calc_target_weighted_pred_left().
        /// </summary>
        /// <param name="prediction">The widened left-neighbor prediction.</param>
        /// <param name="weight">The widened column weights of the block's own prediction.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(Vector128<int> prediction, Vector128<int> weight, ref int weightedSource, ref int mask)
        {
            // The stored values already carry one factor of 64 from the scale pass. The shift removes
            // it exactly, because the scale pass multiplied every value by 64.
            Vector128<int> source = Vector128.LoadUnsafe(ref weightedSource);
            Vector128<int> weights = Vector128.LoadUnsafe(ref mask);
            ((Vector128.ShiftRightArithmetic(source, AlphaShift) * weight) +
                (Vector128.ShiftLeft(prediction, AlphaShift) * (Vector128.Create(MaximumAlpha) - weight))).StoreUnsafe(ref weightedSource);
            (Vector128.ShiftRightArithmetic(weights, AlphaShift) * weight).StoreUnsafe(ref mask);
        }

        /// <summary>
        /// Blends the left-neighbor term into eight lanes. Reference: calc_target_weighted_pred_left().
        /// </summary>
        /// <param name="prediction">The widened left-neighbor prediction.</param>
        /// <param name="weight">The widened column weights of the block's own prediction.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(Vector256<int> prediction, Vector256<int> weight, ref int weightedSource, ref int mask)
        {
            Vector256<int> source = Vector256.LoadUnsafe(ref weightedSource);
            Vector256<int> weights = Vector256.LoadUnsafe(ref mask);
            ((Vector256.ShiftRightArithmetic(source, AlphaShift) * weight) +
                (Vector256.ShiftLeft(prediction, AlphaShift) * (Vector256.Create(MaximumAlpha) - weight))).StoreUnsafe(ref weightedSource);
            (Vector256.ShiftRightArithmetic(weights, AlphaShift) * weight).StoreUnsafe(ref mask);
        }

        /// <summary>
        /// Blends the left-neighbor term into sixteen lanes. Reference: calc_target_weighted_pred_left().
        /// </summary>
        /// <param name="prediction">The widened left-neighbor prediction.</param>
        /// <param name="weight">The widened column weights of the block's own prediction.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(Vector512<int> prediction, Vector512<int> weight, ref int weightedSource, ref int mask)
        {
            Vector512<int> source = Vector512.LoadUnsafe(ref weightedSource);
            Vector512<int> weights = Vector512.LoadUnsafe(ref mask);
            ((Vector512.ShiftRightArithmetic(source, AlphaShift) * weight) +
                (Vector512.ShiftLeft(prediction, AlphaShift) * (Vector512.Create(MaximumAlpha) - weight))).StoreUnsafe(ref weightedSource);
            (Vector512.ShiftRightArithmetic(weights, AlphaShift) * weight).StoreUnsafe(ref mask);
        }

        /// <summary>
        /// Blends the left-neighbor term into one sample. Reference: calc_target_weighted_pred_left().
        /// </summary>
        /// <param name="prediction">The left-neighbor prediction sample.</param>
        /// <param name="weight">The column weight of the block's own prediction.</param>
        /// <param name="weightedSource">The weighted source value to update.</param>
        /// <param name="mask">The prediction weight to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(int prediction, int weight, ref int weightedSource, ref int mask)
        {
            weightedSource = ((weightedSource >> AlphaShift) * weight) + ((prediction << AlphaShift) * (MaximumAlpha - weight));
            mask = (mask >> AlphaShift) * weight;
        }

        /// <summary>
        /// Scales four lanes by the maximum blend weight. Reference: calc_target_weighted_pred().
        /// </summary>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask, Vector128<int> lanes)
        {
            // A left shift of a two's complement lane by six is its product with 64.
            Vector128.ShiftLeft(Vector128.LoadUnsafe(ref weightedSource), AlphaShift).StoreUnsafe(ref weightedSource);
            Vector128.ShiftLeft(Vector128.LoadUnsafe(ref mask), AlphaShift).StoreUnsafe(ref mask);
        }

        /// <summary>
        /// Scales eight lanes by the maximum blend weight. Reference: calc_target_weighted_pred().
        /// </summary>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask, Vector256<int> lanes)
        {
            Vector256.ShiftLeft(Vector256.LoadUnsafe(ref weightedSource), AlphaShift).StoreUnsafe(ref weightedSource);
            Vector256.ShiftLeft(Vector256.LoadUnsafe(ref mask), AlphaShift).StoreUnsafe(ref mask);
        }

        /// <summary>
        /// Scales sixteen lanes by the maximum blend weight. Reference: calc_target_weighted_pred().
        /// </summary>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask, Vector512<int> lanes)
        {
            Vector512.ShiftLeft(Vector512.LoadUnsafe(ref weightedSource), AlphaShift).StoreUnsafe(ref weightedSource);
            Vector512.ShiftLeft(Vector512.LoadUnsafe(ref mask), AlphaShift).StoreUnsafe(ref mask);
        }

        /// <summary>
        /// Scales one value and weight by the maximum blend weight. Reference: calc_target_weighted_pred().
        /// </summary>
        /// <param name="weightedSource">The weighted source value to update.</param>
        /// <param name="mask">The prediction weight to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref int weightedSource, ref int mask)
        {
            weightedSource <<= AlphaShift;
            mask <<= AlphaShift;
        }

        /// <summary>
        /// Replaces four neighbor terms with the scaled source minus the term. Reference: calc_target_weighted_pred().
        /// </summary>
        /// <param name="source">The widened source samples.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(Vector128<int> source, ref int weightedSource)
            => (Vector128.ShiftLeft(source, 2 * AlphaShift) - Vector128.LoadUnsafe(ref weightedSource)).StoreUnsafe(ref weightedSource);

        /// <summary>
        /// Replaces eight neighbor terms with the scaled source minus the term. Reference: calc_target_weighted_pred().
        /// </summary>
        /// <param name="source">The widened source samples.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(Vector256<int> source, ref int weightedSource)
            => (Vector256.ShiftLeft(source, 2 * AlphaShift) - Vector256.LoadUnsafe(ref weightedSource)).StoreUnsafe(ref weightedSource);

        /// <summary>
        /// Replaces sixteen neighbor terms with the scaled source minus the term. Reference: calc_target_weighted_pred().
        /// </summary>
        /// <param name="source">The widened source samples.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(Vector512<int> source, ref int weightedSource)
            => (Vector512.ShiftLeft(source, 2 * AlphaShift) - Vector512.LoadUnsafe(ref weightedSource)).StoreUnsafe(ref weightedSource);

        /// <summary>
        /// Replaces one neighbor term with the scaled source minus the term. Reference: calc_target_weighted_pred().
        /// </summary>
        /// <param name="source">The source sample.</param>
        /// <param name="weightedSource">The weighted source value to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(int source, ref int weightedSource)
            => weightedSource = (source << (2 * AlphaShift)) - weightedSource;
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Defines the sample-width-specific loads used by the <see cref="Av1ObmcSearch"/> traversals.
/// </content>
internal static partial class Av1ObmcSearch
{
    /// <summary>
    /// Defines how the OBMC search reads one sample storage type across hardware widths.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <remarks>
    /// <para>
    /// One lane is one sample, widened to thirty-two bits because the weighted source and the mask are thirty-two-bit arrays.
    /// A load reads as many samples as its register has thirty-two-bit lanes: four, eight or sixteen.
    /// The narrowest load therefore covers the four-sample overlap of an eight-sample block, and no OBMC row is left to the scalar form.
    /// </para>
    /// <para>
    /// The OBMC arithmetic does not depend on the storage type once the samples are widened, so <see cref="Av1ObmcSearch"/>
    /// writes it once. This operator supplies only the widening loads and the scalar conversion.
    /// </para>
    /// </remarks>
    internal interface IObmcOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Loads four samples and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened samples in increasing order.</returns>
        public static abstract Vector128<int> Load(ref TSample source, Vector128<int> lanes);

        /// <summary>
        /// Loads eight samples and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened samples in increasing order.</returns>
        public static abstract Vector256<int> Load(ref TSample source, Vector256<int> lanes);

        /// <summary>
        /// Loads sixteen samples and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened samples in increasing order.</returns>
        public static abstract Vector512<int> Load(ref TSample source, Vector512<int> lanes);

        /// <summary>
        /// Returns the value of one sample.
        /// </summary>
        /// <param name="sample">The sample.</param>
        /// <returns>The sample value.</returns>
        public static abstract int ToInt32(TSample sample);
    }

    /// <summary>
    /// Widens eight-bit samples to thirty-two-bit lanes for the shared OBMC lane arithmetic.
    /// </summary>
    internal readonly struct ByteOperator : IObmcOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Load(ref byte source, Vector128<int> lanes) => ObmcLanes.LoadBytes(ref source, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Load(ref byte source, Vector256<int> lanes) => ObmcLanes.LoadBytes(ref source, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Load(ref byte source, Vector512<int> lanes) => ObmcLanes.LoadBytes(ref source, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32(byte sample) => sample;
    }

    /// <summary>
    /// Widens high-bit-depth samples to thirty-two-bit lanes for the shared OBMC lane arithmetic.
    /// </summary>
    internal readonly struct UInt16Operator : IObmcOperator<ushort>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Load(ref ushort source, Vector128<int> lanes)
            => Vector128.WidenLower(Vector64.LoadUnsafe(ref source).ToVector128()).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Load(ref ushort source, Vector256<int> lanes)
            => Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe()).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Load(ref ushort source, Vector512<int> lanes)
            => Vector512.WidenLower(Vector256.LoadUnsafe(ref source).ToVector512Unsafe()).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32(ushort sample) => sample;
    }

    /// <summary>
    /// Holds the OBMC lane arithmetic that both sample operators share once their samples are widened.
    /// </summary>
    /// <remarks>
    /// Every lane is an independent sample, so each width repeats the arithmetic of the scalar form lane by lane.
    /// The products fit a thirty-two-bit lane: a twelve-bit sample times a weight of at most 64 * 64 is below 2^24.
    /// </remarks>
    private static class ObmcLanes
    {
        /// <summary>
        /// The rounding shift of an OBMC difference: the weighted source carries two blend weights of 64.
        /// </summary>
        private const int DifferenceShift = 12;

        /// <summary>
        /// Half of the rounding divisor. Added before the shift, it makes the shift round to the nearest value.
        /// </summary>
        private const int DifferenceBias = 1 << (DifferenceShift - 1);

        /// <summary>
        /// The shift of the maximum blend weight.
        /// </summary>
        private const int AlphaShift = 6;

        /// <summary>
        /// The maximum blend weight of a six-bit blend mask.
        /// </summary>
        private const int MaximumAlpha = 1 << AlphaShift;

        /// <summary>
        /// Loads four bytes and widens them to thirty-two-bit lanes. Eight-bit samples and the column weights use this load.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadBytes(ref byte source, Vector128<int> lanes)
        {
            // The load reads exactly four bytes. Thus the last group of a row never reads the next row or past the end of the plane.
            Vector128<byte> bytes = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<uint>(ref source)).AsByte();
            return Vector128.WidenLower(Vector128.WidenLower(bytes)).AsInt32();
        }

        /// <summary>
        /// Loads eight bytes and widens them to thirty-two-bit lanes. Eight-bit samples and the column weights use this load.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> LoadBytes(ref byte source, Vector256<int> lanes)
        {
            // The load reads exactly eight bytes. The first widening makes eight words, and the second makes eight thirty-two-bit lanes.
            Vector128<ushort> words = Vector128.WidenLower(Vector64.LoadUnsafe(ref source).ToVector128());
            return Vector256.WidenLower(words.ToVector256Unsafe()).AsInt32();
        }

        /// <summary>
        /// Loads sixteen bytes and widens them to thirty-two-bit lanes. Eight-bit samples and the column weights use this load.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> LoadBytes(ref byte source, Vector512<int> lanes)
        {
            // The load reads exactly sixteen bytes. The first widening makes sixteen words, and the second makes sixteen thirty-two-bit lanes.
            Vector256<ushort> words = Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe());
            return Vector512.WidenLower(words.ToVector512Unsafe()).AsInt32();
        }

        /// <summary>
        /// Adds the rounded absolute differences of four lanes.
        /// </summary>
        /// <param name="prediction">The widened prediction samples.</param>
        /// <param name="weightedSource">The weighted source values.</param>
        /// <param name="mask">The prediction weights.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The updated running total.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> AccumulateAbsoluteDifferences(Vector128<int> prediction, Vector128<int> weightedSource, Vector128<int> mask, Vector128<uint> total)
        {
            // The absolute difference is below 2^24, so the biased value stays far below 2^31.
            // Thus the logical shift gives the same result as the signed shift of the scalar form and rounds half up.
            Vector128<uint> difference = Vector128.Abs(weightedSource - (prediction * mask)).AsUInt32();
            return total + Vector128.ShiftRightLogical(difference + Vector128.Create((uint)DifferenceBias), DifferenceShift);
        }

        /// <summary>
        /// Adds the rounded absolute differences of eight lanes.
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
        /// Adds the rounded absolute differences of sixteen lanes.
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
        /// Adds the rounded absolute difference of one sample.
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
        /// Adds the rounded differences of four lanes and their squares.
        /// </summary>
        /// <param name="prediction">The widened prediction samples.</param>
        /// <param name="weightedSource">The weighted source values.</param>
        /// <param name="mask">The prediction weights.</param>
        /// <param name="sum">The running signed total.</param>
        /// <param name="squares">The running squared total.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector128<int> prediction, Vector128<int> weightedSource, Vector128<int> mask, ref Vector128<int> sum, ref Vector128<uint> squares)
        {
            // The sign lane is -1 for a negative value, which lowers the bias by one.
            // Then the arithmetic shift rounds half away from zero, the same as the signed rounding of the scalar form.
            Vector128<int> value = weightedSource - (prediction * mask);
            Vector128<int> difference = Vector128.ShiftRightArithmetic(
                value + Vector128.Create(DifferenceBias) + Vector128.ShiftRightArithmetic(value, 31),
                DifferenceShift);

            sum += difference;
            squares += (difference * difference).AsUInt32();
        }

        /// <summary>
        /// Adds the rounded differences of eight lanes and their squares.
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
        /// Adds the rounded differences of sixteen lanes and their squares.
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
        /// Adds the rounded difference of one sample and its square.
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
        /// Writes the above-neighbor term of four lanes.
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
        /// Writes the above-neighbor term of eight lanes.
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
        /// Writes the above-neighbor term of sixteen lanes.
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
        /// Writes the above-neighbor term of one sample.
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
        /// Blends the left-neighbor term into four lanes.
        /// </summary>
        /// <param name="prediction">The widened left-neighbor prediction.</param>
        /// <param name="weight">The widened column weights of the block's own prediction.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        /// <param name="mask">The first prediction weight to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeightLeft(Vector128<int> prediction, Vector128<int> weight, ref int weightedSource, ref int mask)
        {
            // The stored values already carry one factor of 64 from the scale pass.
            // The shift removes it exactly, because the scale pass multiplied every value by 64.
            Vector128<int> source = Vector128.LoadUnsafe(ref weightedSource);
            Vector128<int> weights = Vector128.LoadUnsafe(ref mask);
            ((Vector128.ShiftRightArithmetic(source, AlphaShift) * weight) +
                (Vector128.ShiftLeft(prediction, AlphaShift) * (Vector128.Create(MaximumAlpha) - weight))).StoreUnsafe(ref weightedSource);

            (Vector128.ShiftRightArithmetic(weights, AlphaShift) * weight).StoreUnsafe(ref mask);
        }

        /// <summary>
        /// Blends the left-neighbor term into eight lanes.
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
        /// Blends the left-neighbor term into sixteen lanes.
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
        /// Blends the left-neighbor term into one sample.
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
        /// Scales four lanes by the maximum blend weight.
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
        /// Scales eight lanes by the maximum blend weight.
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
        /// Scales sixteen lanes by the maximum blend weight.
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
        /// Scales one value and weight by the maximum blend weight.
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
        /// Replaces four neighbor terms with the scaled source minus the term.
        /// </summary>
        /// <param name="source">The widened source samples.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(Vector128<int> source, ref int weightedSource)
            => (Vector128.ShiftLeft(source, 2 * AlphaShift) - Vector128.LoadUnsafe(ref weightedSource)).StoreUnsafe(ref weightedSource);

        /// <summary>
        /// Replaces eight neighbor terms with the scaled source minus the term.
        /// </summary>
        /// <param name="source">The widened source samples.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(Vector256<int> source, ref int weightedSource)
            => (Vector256.ShiftLeft(source, 2 * AlphaShift) - Vector256.LoadUnsafe(ref weightedSource)).StoreUnsafe(ref weightedSource);

        /// <summary>
        /// Replaces sixteen neighbor terms with the scaled source minus the term.
        /// </summary>
        /// <param name="source">The widened source samples.</param>
        /// <param name="weightedSource">The first weighted source value to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(Vector512<int> source, ref int weightedSource)
            => (Vector512.ShiftLeft(source, 2 * AlphaShift) - Vector512.LoadUnsafe(ref weightedSource)).StoreUnsafe(ref weightedSource);

        /// <summary>
        /// Replaces one neighbor term with the scaled source minus the term.
        /// </summary>
        /// <param name="source">The source sample.</param>
        /// <param name="weightedSource">The weighted source value to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SubtractFromSource(int source, ref int weightedSource)
            => weightedSource = (source << (2 * AlphaShift)) - weightedSource;
    }
}

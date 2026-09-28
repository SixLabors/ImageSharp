// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the sample-width-specific arithmetic used by <see cref="Av1ResidualBuilder"/>.
/// </content>
internal static partial class Av1ResidualBuilder
{
    /// <summary>
    /// The maximum blend weight, AOM_BLEND_A64_MAX_ALPHA.
    /// </summary>
    private const int BlendMaximumAlpha = 64;

    /// <summary>
    /// The rounding shift of a six-bit blend, AOM_BLEND_A64_ROUND_BITS.
    /// </summary>
    private const int BlendShift = 6;

    /// <summary>
    /// The rounding bias of a six-bit blend.
    /// </summary>
    private const int BlendRoundingBias = 1 << (BlendShift - 1);

    /// <summary>
    /// Defines one AV1 source-minus-prediction operation across hardware widths.
    /// </summary>
    /// <typeparam name="TSample">The source and prediction sample type.</typeparam>
    /// <remarks>
    /// Every measure this interface declares returns a lane-shaped total rather than a scalar one. A
    /// scalar return would put a horizontal sum inside the traversal loop, and a horizontal sum is a
    /// chain of shuffles and adds that costs far more than the lane work it reduces. The traversal
    /// therefore carries the total in lanes and reduces it once, which is what a hand-written
    /// specialization of the same measure does.
    /// </remarks>
    internal interface IResidualOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Adds the absolute differences of sixteen bytes, or eight words, to a running total.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="total">The running total in 32-bit lanes.</param>
        /// <returns>The updated running total.</returns>
        /// <remarks>
        /// The spread of the total across the lanes is not defined. Only the sum of all lanes is.
        /// </remarks>
        public static abstract Vector128<uint> AccumulateAbsoluteDifferences(Vector128<TSample> source, Vector128<TSample> prediction, Vector128<uint> total);

        /// <summary>
        /// Adds the absolute differences of thirty-two bytes, or sixteen words, to a running total.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="total">The running total in 32-bit lanes.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector256<uint> AccumulateAbsoluteDifferences(Vector256<TSample> source, Vector256<TSample> prediction, Vector256<uint> total);

        /// <summary>
        /// Adds the absolute differences of sixty-four bytes, or thirty-two words, to a running total.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="total">The running total in 32-bit lanes.</param>
        /// <returns>The updated running total.</returns>
        public static abstract Vector512<uint> AccumulateAbsoluteDifferences(Vector512<TSample> source, Vector512<TSample> prediction, Vector512<uint> total);

        /// <summary>
        /// Measures one scalar absolute sample difference.
        /// </summary>
        /// <param name="source">The source sample.</param>
        /// <param name="prediction">The prediction sample.</param>
        /// <returns>The absolute difference.</returns>
        public static abstract int SumAbsoluteDifferences(TSample source, TSample prediction);

        /// <summary>
        /// Adds the signed differences of sixteen bytes, or eight words, and their squares, to two running totals.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="sum">The running signed total in 32-bit lanes.</param>
        /// <param name="squares">The running squared total in 32-bit lanes.</param>
        /// <remarks>
        /// The spread of either total across the lanes is not defined. Only the sum of all lanes is.
        /// </remarks>
        public static abstract void AccumulateMoments(Vector128<TSample> source, Vector128<TSample> prediction, ref Vector128<int> sum, ref Vector128<int> squares);

        /// <summary>
        /// Adds the signed differences of thirty-two bytes, or sixteen words, and their squares, to two running totals.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="sum">The running signed total in 32-bit lanes.</param>
        /// <param name="squares">The running squared total in 32-bit lanes.</param>
        public static abstract void AccumulateMoments(Vector256<TSample> source, Vector256<TSample> prediction, ref Vector256<int> sum, ref Vector256<int> squares);

        /// <summary>
        /// Adds the signed differences of sixty-four bytes, or thirty-two words, and their squares, to two running totals.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="sum">The running signed total in 32-bit lanes.</param>
        /// <param name="squares">The running squared total in 32-bit lanes.</param>
        public static abstract void AccumulateMoments(Vector512<TSample> source, Vector512<TSample> prediction, ref Vector512<int> sum, ref Vector512<int> squares);

        /// <summary>
        /// Loads eight source and prediction samples and subtracts them.
        /// </summary>
        /// <param name="source">The first sample of the source row.</param>
        /// <param name="prediction">The first sample of the prediction row.</param>
        /// <param name="offset">The column offset shared by both rows.</param>
        /// <param name="width">The overload-selection value.</param>
        /// <returns>The eight residuals in increasing column order.</returns>
        /// <remarks>
        /// The residual of either sample depth is a signed sixteen-bit value, so one vector of
        /// sixteen-bit lanes is eight samples whatever the depth. Loading at that width is what lets
        /// a transform row of eight samples fill a vector: loading at the width of the sample type
        /// would need sixteen eight-bit samples and would leave every narrow row to the scalar loop.
        /// </remarks>
        public static abstract Vector128<short> LoadDifference(ref TSample source, ref TSample prediction, int offset, Vector128<short> width);

        /// <summary>
        /// Loads sixteen source and prediction samples and subtracts them.
        /// </summary>
        /// <param name="source">The first sample of the source row.</param>
        /// <param name="prediction">The first sample of the prediction row.</param>
        /// <param name="offset">The column offset shared by both rows.</param>
        /// <param name="width">The overload-selection value.</param>
        /// <returns>The sixteen residuals in increasing column order.</returns>
        public static abstract Vector256<short> LoadDifference(ref TSample source, ref TSample prediction, int offset, Vector256<short> width);

        /// <summary>
        /// Loads thirty-two source and prediction samples and subtracts them.
        /// </summary>
        /// <param name="source">The first sample of the source row.</param>
        /// <param name="prediction">The first sample of the prediction row.</param>
        /// <param name="offset">The column offset shared by both rows.</param>
        /// <param name="width">The overload-selection value.</param>
        /// <returns>The thirty-two residuals in increasing column order.</returns>
        public static abstract Vector512<short> LoadDifference(ref TSample source, ref TSample prediction, int offset, Vector512<short> width);

        /// <summary>
        /// Subtracts one source and prediction sample.
        /// </summary>
        /// <param name="source">The source sample.</param>
        /// <param name="prediction">The prediction sample.</param>
        /// <returns>The signed residual.</returns>
        public static abstract short Subtract(TSample source, TSample prediction);

        /// <summary>
        /// Measures four eight-sample predictions, returning their costs in candidate order.
        /// Byte inputs occupy only the lower eight lanes.
        /// </summary>
        /// <param name="source">The eight source samples.</param>
        /// <param name="prediction0">The eight samples for candidate 0.</param>
        /// <param name="prediction1">The eight samples for candidate 1.</param>
        /// <param name="prediction2">The eight samples for candidate 2.</param>
        /// <param name="prediction3">The eight samples for candidate 3.</param>
        /// <returns>Four absolute-difference sums in increasing candidate order.</returns>
        public static abstract Vector128<int> SumFourAbsoluteDifferences(
            Vector128<TSample> source,
            Vector128<TSample> prediction0,
            Vector128<TSample> prediction1,
            Vector128<TSample> prediction2,
            Vector128<TSample> prediction3);

        /// <summary>
        /// Calculates one squared difference and returns its signed difference for the first moment.
        /// </summary>
        /// <param name="source">The source sample.</param>
        /// <param name="prediction">The prediction sample.</param>
        /// <param name="sum">The signed source-minus-prediction difference.</param>
        /// <returns>The squared difference.</returns>
        public static abstract int SumSquaredDifferences(TSample source, TSample prediction, out int sum);

        /// <summary>
        /// Averages sixteen bytes, or eight words, of two predictions with upward rounding.
        /// </summary>
        /// <param name="first">The first prediction.</param>
        /// <param name="second">The second prediction.</param>
        /// <returns>The equal-weight compound prediction.</returns>
        public static abstract Vector128<TSample> Average(Vector128<TSample> first, Vector128<TSample> second);

        /// <summary>
        /// Averages thirty-two bytes, or sixteen words, of two predictions with upward rounding.
        /// </summary>
        /// <param name="first">The first prediction.</param>
        /// <param name="second">The second prediction.</param>
        /// <returns>The equal-weight compound prediction.</returns>
        public static abstract Vector256<TSample> Average(Vector256<TSample> first, Vector256<TSample> second);

        /// <summary>
        /// Averages sixty-four bytes, or thirty-two words, of two predictions with upward rounding.
        /// </summary>
        /// <param name="first">The first prediction.</param>
        /// <param name="second">The second prediction.</param>
        /// <returns>The equal-weight compound prediction.</returns>
        public static abstract Vector512<TSample> Average(Vector512<TSample> first, Vector512<TSample> second);

        /// <summary>
        /// Averages one sample of two predictions with upward rounding.
        /// </summary>
        /// <param name="first">The first prediction sample.</param>
        /// <param name="second">The second prediction sample.</param>
        /// <returns>The equal-weight compound sample.</returns>
        public static abstract TSample Average(TSample first, TSample second);

        /// <summary>
        /// Loads the six-bit blend weights of sixteen bytes, or eight words, in the lanes of the sample type.
        /// </summary>
        /// <param name="mask">The first weight of the mask row.</param>
        /// <param name="offset">The column offset.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The weights in increasing column order.</returns>
        public static abstract Vector128<TSample> LoadMask(ref byte mask, nuint offset, Vector128<TSample> lanes);

        /// <summary>
        /// Loads the six-bit blend weights of thirty-two bytes, or sixteen words, in the lanes of the sample type.
        /// </summary>
        /// <param name="mask">The first weight of the mask row.</param>
        /// <param name="offset">The column offset.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The weights in increasing column order.</returns>
        public static abstract Vector256<TSample> LoadMask(ref byte mask, nuint offset, Vector256<TSample> lanes);

        /// <summary>
        /// Loads the six-bit blend weights of sixty-four bytes, or thirty-two words, in the lanes of the sample type.
        /// </summary>
        /// <param name="mask">The first weight of the mask row.</param>
        /// <param name="offset">The column offset.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The weights in increasing column order.</returns>
        public static abstract Vector512<TSample> LoadMask(ref byte mask, nuint offset, Vector512<TSample> lanes);

        /// <summary>
        /// Loads exactly eight six-bit blend weights; byte weights occupy the lower half of the returned vector.
        /// </summary>
        /// <param name="mask">The mask row at the first weight.</param>
        /// <returns>The eight weights, with zero padding for byte samples.</returns>
        public static abstract Vector128<TSample> LoadSearchMask(ReadOnlySpan<byte> mask);

        /// <summary>
        /// Blends sixteen bytes, or eight words, of two predictions with six-bit weights of the first.
        /// </summary>
        /// <param name="first">The weighted prediction.</param>
        /// <param name="second">The complementary prediction.</param>
        /// <param name="weights">The weights of <paramref name="first"/>, from 0 to 64.</param>
        /// <returns>The rounded blend.</returns>
        public static abstract Vector128<TSample> Blend(Vector128<TSample> first, Vector128<TSample> second, Vector128<TSample> weights);

        /// <summary>
        /// Blends thirty-two bytes, or sixteen words, of two predictions with six-bit weights of the first.
        /// </summary>
        /// <param name="first">The weighted prediction.</param>
        /// <param name="second">The complementary prediction.</param>
        /// <param name="weights">The weights of <paramref name="first"/>, from 0 to 64.</param>
        /// <returns>The rounded blend.</returns>
        public static abstract Vector256<TSample> Blend(Vector256<TSample> first, Vector256<TSample> second, Vector256<TSample> weights);

        /// <summary>
        /// Blends sixty-four bytes, or thirty-two words, of two predictions with six-bit weights of the first.
        /// </summary>
        /// <param name="first">The weighted prediction.</param>
        /// <param name="second">The complementary prediction.</param>
        /// <param name="weights">The weights of <paramref name="first"/>, from 0 to 64.</param>
        /// <returns>The rounded blend.</returns>
        public static abstract Vector512<TSample> Blend(Vector512<TSample> first, Vector512<TSample> second, Vector512<TSample> weights);

        /// <summary>
        /// Blends one sample of two predictions with a six-bit weight of the first.
        /// </summary>
        /// <param name="first">The weighted prediction sample.</param>
        /// <param name="second">The complementary prediction sample.</param>
        /// <param name="weight">The weight of <paramref name="first"/>, from 0 to 64.</param>
        /// <returns>The rounded blend.</returns>
        public static abstract TSample Blend(TSample first, TSample second, int weight);
    }

    /// <summary>
    /// Widens 8-bit samples before subtraction so every residual is represented without precision loss.
    /// </summary>
    internal readonly struct ByteOperator : IResidualOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> AccumulateAbsoluteDifferences(Vector128<byte> source, Vector128<byte> prediction, Vector128<uint> total)
            => Vector128_.SumAbsoluteDifferences(source, prediction, total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> AccumulateAbsoluteDifferences(Vector256<byte> source, Vector256<byte> prediction, Vector256<uint> total)
            => Vector256_.SumAbsoluteDifferences(source, prediction, total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> AccumulateAbsoluteDifferences(Vector512<byte> source, Vector512<byte> prediction, Vector512<uint> total)
            => Vector512_.SumAbsoluteDifferences(source, prediction, total);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SumAbsoluteDifferences(byte source, byte prediction) => Math.Abs(Subtract(source, prediction));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector128<byte> source, Vector128<byte> prediction, ref Vector128<int> sum, ref Vector128<int> squares)
        {
            // A byte vector holds twice as many samples as a 16-bit lane can take, so both halves are
            // widened and folded apart. Multiplying a difference by one, and by itself, gives the
            // signed total and the squared total from the same pairwise instruction.
            Vector128<short> lower = Vector128.WidenLower(source).AsInt16() - Vector128.WidenLower(prediction).AsInt16();
            Vector128<short> upper = Vector128.WidenUpper(source).AsInt16() - Vector128.WidenUpper(prediction).AsInt16();
            Vector128<short> ones = Vector128.Create((short)1);
            sum += Vector128_.MultiplyAddAdjacent(lower, ones) + Vector128_.MultiplyAddAdjacent(upper, ones);
            squares += Vector128_.MultiplyAddAdjacent(lower, lower) + Vector128_.MultiplyAddAdjacent(upper, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector256<byte> source, Vector256<byte> prediction, ref Vector256<int> sum, ref Vector256<int> squares)
        {
            Vector256<short> lower = Vector256.WidenLower(source).AsInt16() - Vector256.WidenLower(prediction).AsInt16();
            Vector256<short> upper = Vector256.WidenUpper(source).AsInt16() - Vector256.WidenUpper(prediction).AsInt16();
            Vector256<short> ones = Vector256.Create((short)1);
            sum += Vector256_.MultiplyAddAdjacent(lower, ones) + Vector256_.MultiplyAddAdjacent(upper, ones);
            squares += Vector256_.MultiplyAddAdjacent(lower, lower) + Vector256_.MultiplyAddAdjacent(upper, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector512<byte> source, Vector512<byte> prediction, ref Vector512<int> sum, ref Vector512<int> squares)
        {
            Vector512<short> lower = Vector512.WidenLower(source).AsInt16() - Vector512.WidenLower(prediction).AsInt16();
            Vector512<short> upper = Vector512.WidenUpper(source).AsInt16() - Vector512.WidenUpper(prediction).AsInt16();
            Vector512<short> ones = Vector512.Create((short)1);
            sum += Vector512_.MultiplyAddAdjacent(lower, ones) + Vector512_.MultiplyAddAdjacent(upper, ones);
            squares += Vector512_.MultiplyAddAdjacent(lower, lower) + Vector512_.MultiplyAddAdjacent(upper, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> LoadDifference(ref byte source, ref byte prediction, int offset, Vector128<short> width)
        {
            // Exactly eight bytes are read from each row, so a row of eight samples is covered
            // without touching the row that follows it. The half-width load zero-extends into the
            // register, which the move already does, so the defined form costs nothing over the
            // undefined one.
            Vector128<ushort> s = Vector128.WidenLower(Vector64.LoadUnsafe(ref source, (nuint)offset).ToVector128());
            Vector128<ushort> p = Vector128.WidenLower(Vector64.LoadUnsafe(ref prediction, (nuint)offset).ToVector128());

            // Both operands are below 256, so the wrapped unsigned subtraction reinterprets as the
            // signed difference the residual is defined to be.
            return (s - p).AsInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> LoadDifference(ref byte source, ref byte prediction, int offset, Vector256<short> width)
        {
            Vector256<ushort> s = Vector256.WidenLower(Vector128.LoadUnsafe(ref source, (nuint)offset).ToVector256());
            Vector256<ushort> p = Vector256.WidenLower(Vector128.LoadUnsafe(ref prediction, (nuint)offset).ToVector256());

            return (s - p).AsInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> LoadDifference(ref byte source, ref byte prediction, int offset, Vector512<short> width)
        {
            Vector512<ushort> s = Vector512.WidenLower(Vector256.LoadUnsafe(ref source, (nuint)offset).ToVector512());
            Vector512<ushort> p = Vector512.WidenLower(Vector256.LoadUnsafe(ref prediction, (nuint)offset).ToVector512());

            return (s - p).AsInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> SumFourAbsoluteDifferences(
            Vector128<byte> source,
            Vector128<byte> prediction0,
            Vector128<byte> prediction1,
            Vector128<byte> prediction2,
            Vector128<byte> prediction3)
        {
            // Reuse the source conversion across all four candidates. Each reduction contributes one independent
            // 32-bit lane, allowing the traversal to accumulate all eight rows without extracting candidate costs.
            Vector128<short> sourceSamples = Vector128.WidenLower(source).AsInt16();
            return Vector128.Create(
                (int)Vector128.Sum(Vector128.Abs(sourceSamples - Vector128.WidenLower(prediction0).AsInt16())),
                (int)Vector128.Sum(Vector128.Abs(sourceSamples - Vector128.WidenLower(prediction1).AsInt16())),
                (int)Vector128.Sum(Vector128.Abs(sourceSamples - Vector128.WidenLower(prediction2).AsInt16())),
                (int)Vector128.Sum(Vector128.Abs(sourceSamples - Vector128.WidenLower(prediction3).AsInt16())));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SumSquaredDifferences(byte source, byte prediction, out int sum)
        {
            sum = Subtract(source, prediction);
            return sum * sum;
        }

        /// <inheritdoc/>
        public static short Subtract(byte source, byte prediction) => (short)(source - prediction);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Average(Vector128<byte> first, Vector128<byte> second) => Vector128_.Average(first, second);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Average(Vector256<byte> first, Vector256<byte> second) => Vector256_.Average(first, second);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Average(Vector512<byte> first, Vector512<byte> second) => Vector512_.Average(first, second);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Average(byte first, byte second) => (byte)((first + second + 1) >> 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> LoadMask(ref byte mask, nuint offset, Vector128<byte> lanes) => Vector128.LoadUnsafe(ref mask, offset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> LoadMask(ref byte mask, nuint offset, Vector256<byte> lanes) => Vector256.LoadUnsafe(ref mask, offset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> LoadMask(ref byte mask, nuint offset, Vector512<byte> lanes) => Vector512.LoadUnsafe(ref mask, offset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> LoadSearchMask(ReadOnlySpan<byte> mask) => Vector128.Create(Vector64.Create(mask), Vector64<byte>.Zero);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Blend(Vector128<byte> first, Vector128<byte> second, Vector128<byte> weights)
        {
            // A weighted byte is at most 64 * 255, so the whole blend, its complement and the rounding
            // bias fit a sixteen-bit lane, and the result fits a byte again.
            (Vector128<ushort> weightLower, Vector128<ushort> weightUpper) = Vector128.Widen(weights);
            (Vector128<ushort> firstLower, Vector128<ushort> firstUpper) = Vector128.Widen(first);
            (Vector128<ushort> secondLower, Vector128<ushort> secondUpper) = Vector128.Widen(second);
            Vector128<ushort> maximum = Vector128.Create((ushort)BlendMaximumAlpha);
            Vector128<ushort> bias = Vector128.Create((ushort)BlendRoundingBias);
            Vector128<ushort> lower = ((weightLower * firstLower) + ((maximum - weightLower) * secondLower) + bias) >> BlendShift;
            Vector128<ushort> upper = ((weightUpper * firstUpper) + ((maximum - weightUpper) * secondUpper) + bias) >> BlendShift;
            return Vector128.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Blend(Vector256<byte> first, Vector256<byte> second, Vector256<byte> weights)
        {
            (Vector256<ushort> weightLower, Vector256<ushort> weightUpper) = Vector256.Widen(weights);
            (Vector256<ushort> firstLower, Vector256<ushort> firstUpper) = Vector256.Widen(first);
            (Vector256<ushort> secondLower, Vector256<ushort> secondUpper) = Vector256.Widen(second);
            Vector256<ushort> maximum = Vector256.Create((ushort)BlendMaximumAlpha);
            Vector256<ushort> bias = Vector256.Create((ushort)BlendRoundingBias);
            Vector256<ushort> lower = ((weightLower * firstLower) + ((maximum - weightLower) * secondLower) + bias) >> BlendShift;
            Vector256<ushort> upper = ((weightUpper * firstUpper) + ((maximum - weightUpper) * secondUpper) + bias) >> BlendShift;
            return Vector256.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Blend(Vector512<byte> first, Vector512<byte> second, Vector512<byte> weights)
        {
            (Vector512<ushort> weightLower, Vector512<ushort> weightUpper) = Vector512.Widen(weights);
            (Vector512<ushort> firstLower, Vector512<ushort> firstUpper) = Vector512.Widen(first);
            (Vector512<ushort> secondLower, Vector512<ushort> secondUpper) = Vector512.Widen(second);
            Vector512<ushort> maximum = Vector512.Create((ushort)BlendMaximumAlpha);
            Vector512<ushort> bias = Vector512.Create((ushort)BlendRoundingBias);
            Vector512<ushort> lower = ((weightLower * firstLower) + ((maximum - weightLower) * secondLower) + bias) >> BlendShift;
            Vector512<ushort> upper = ((weightUpper * firstUpper) + ((maximum - weightUpper) * secondUpper) + bias) >> BlendShift;
            return Vector512.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Blend(byte first, byte second, int weight)
            => (byte)(((weight * first) + ((BlendMaximumAlpha - weight) * second) + BlendRoundingBias) >> BlendShift);
    }

    /// <summary>
    /// Subtracts high-bit-depth samples directly because the AV1 10-bit and 12-bit ranges fit signed-short lanes.
    /// </summary>
    internal readonly struct UInt16Operator : IResidualOperator<ushort>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> AccumulateAbsoluteDifferences(Vector128<ushort> source, Vector128<ushort> prediction, Vector128<uint> total)
        {
            // Unsigned lanes make the absolute difference the larger value minus the smaller one, which
            // needs no sign handling. A twelve-bit difference reaches 4095, so the pairwise multiply-add
            // by one folds eight lanes into four without leaving the signed range at any step.
            Vector128<ushort> difference = Vector128.Max(source, prediction) - Vector128.Min(source, prediction);
            return total + Vector128_.MultiplyAddAdjacent(difference.AsInt16(), Vector128.Create((short)1)).AsUInt32();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> AccumulateAbsoluteDifferences(Vector256<ushort> source, Vector256<ushort> prediction, Vector256<uint> total)
        {
            Vector256<ushort> difference = Vector256.Max(source, prediction) - Vector256.Min(source, prediction);
            return total + Vector256_.MultiplyAddAdjacent(difference.AsInt16(), Vector256.Create((short)1)).AsUInt32();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> AccumulateAbsoluteDifferences(Vector512<ushort> source, Vector512<ushort> prediction, Vector512<uint> total)
        {
            Vector512<ushort> difference = Vector512.Max(source, prediction) - Vector512.Min(source, prediction);
            return total + Vector512_.MultiplyAddAdjacent(difference.AsInt16(), Vector512.Create((short)1)).AsUInt32();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SumAbsoluteDifferences(ushort source, ushort prediction) => Math.Abs(Subtract(source, prediction));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector128<ushort> source, Vector128<ushort> prediction, ref Vector128<int> sum, ref Vector128<int> squares)
        {
            // A twelve-bit residual fits a signed 16-bit lane, so the difference needs no widening.
            // Multiplying it by one, and by itself, gives the signed total and the squared total from
            // the same pairwise instruction.
            Vector128<short> difference = source.AsInt16() - prediction.AsInt16();
            sum += Vector128_.MultiplyAddAdjacent(difference, Vector128.Create((short)1));
            squares += Vector128_.MultiplyAddAdjacent(difference, difference);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector256<ushort> source, Vector256<ushort> prediction, ref Vector256<int> sum, ref Vector256<int> squares)
        {
            Vector256<short> difference = source.AsInt16() - prediction.AsInt16();
            sum += Vector256_.MultiplyAddAdjacent(difference, Vector256.Create((short)1));
            squares += Vector256_.MultiplyAddAdjacent(difference, difference);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments(Vector512<ushort> source, Vector512<ushort> prediction, ref Vector512<int> sum, ref Vector512<int> squares)
        {
            Vector512<short> difference = source.AsInt16() - prediction.AsInt16();
            sum += Vector512_.MultiplyAddAdjacent(difference, Vector512.Create((short)1));
            squares += Vector512_.MultiplyAddAdjacent(difference, difference);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> LoadDifference(ref ushort source, ref ushort prediction, int offset, Vector128<short> width)
            => (Vector128.LoadUnsafe(ref source, (nuint)offset) - Vector128.LoadUnsafe(ref prediction, (nuint)offset)).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> LoadDifference(ref ushort source, ref ushort prediction, int offset, Vector256<short> width)
            => (Vector256.LoadUnsafe(ref source, (nuint)offset) - Vector256.LoadUnsafe(ref prediction, (nuint)offset)).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> LoadDifference(ref ushort source, ref ushort prediction, int offset, Vector512<short> width)
            => (Vector512.LoadUnsafe(ref source, (nuint)offset) - Vector512.LoadUnsafe(ref prediction, (nuint)offset)).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> SumFourAbsoluteDifferences(
            Vector128<ushort> source,
            Vector128<ushort> prediction0,
            Vector128<ushort> prediction1,
            Vector128<ushort> prediction2,
            Vector128<ushort> prediction3)
        {
            // Reuse the source conversion across all four candidates. Each reduction contributes one independent
            // 32-bit lane, allowing the traversal to accumulate all eight rows without extracting candidate costs.
            Vector128<short> sourceSamples = source.AsInt16();
            return Vector128.Create(
                (int)Vector128.Sum(Vector128.Abs(sourceSamples - prediction0.AsInt16())),
                (int)Vector128.Sum(Vector128.Abs(sourceSamples - prediction1.AsInt16())),
                (int)Vector128.Sum(Vector128.Abs(sourceSamples - prediction2.AsInt16())),
                (int)Vector128.Sum(Vector128.Abs(sourceSamples - prediction3.AsInt16())));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SumSquaredDifferences(ushort source, ushort prediction, out int sum)
        {
            sum = Subtract(source, prediction);
            return sum * sum;
        }

        /// <inheritdoc/>
        public static short Subtract(ushort source, ushort prediction) => (short)(source - prediction);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Average(Vector128<ushort> first, Vector128<ushort> second) => Vector128_.Average(first, second);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Average(Vector256<ushort> first, Vector256<ushort> second) => Vector256_.Average(first, second);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Average(Vector512<ushort> first, Vector512<ushort> second) => Vector512_.Average(first, second);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Average(ushort first, ushort second) => (ushort)((first + second + 1) >> 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> LoadMask(ref byte mask, nuint offset, Vector128<ushort> lanes)
            => Vector128.WidenLower(Vector64.LoadUnsafe(ref mask, offset).ToVector128());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> LoadMask(ref byte mask, nuint offset, Vector256<ushort> lanes)
            => Vector256.WidenLower(Vector128.LoadUnsafe(ref mask, offset).ToVector256Unsafe());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> LoadMask(ref byte mask, nuint offset, Vector512<ushort> lanes)
            => Vector512.WidenLower(Vector256.LoadUnsafe(ref mask, offset).ToVector512Unsafe());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> LoadSearchMask(ReadOnlySpan<byte> mask) => Vector128.WidenLower(Vector64.Create(mask).ToVector128());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Blend(Vector128<ushort> first, Vector128<ushort> second, Vector128<ushort> weights)
        {
            // A weighted twelve-bit sample is at most 64 * 4095, which needs a thirty-two-bit lane.
            // The rounded blend is a sample again, so it narrows back without loss.
            (Vector128<uint> weightLower, Vector128<uint> weightUpper) = Vector128.Widen(weights);
            (Vector128<uint> firstLower, Vector128<uint> firstUpper) = Vector128.Widen(first);
            (Vector128<uint> secondLower, Vector128<uint> secondUpper) = Vector128.Widen(second);
            Vector128<uint> maximum = Vector128.Create((uint)BlendMaximumAlpha);
            Vector128<uint> bias = Vector128.Create((uint)BlendRoundingBias);
            Vector128<uint> lower = ((weightLower * firstLower) + ((maximum - weightLower) * secondLower) + bias) >> BlendShift;
            Vector128<uint> upper = ((weightUpper * firstUpper) + ((maximum - weightUpper) * secondUpper) + bias) >> BlendShift;
            return Vector128.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Blend(Vector256<ushort> first, Vector256<ushort> second, Vector256<ushort> weights)
        {
            (Vector256<uint> weightLower, Vector256<uint> weightUpper) = Vector256.Widen(weights);
            (Vector256<uint> firstLower, Vector256<uint> firstUpper) = Vector256.Widen(first);
            (Vector256<uint> secondLower, Vector256<uint> secondUpper) = Vector256.Widen(second);
            Vector256<uint> maximum = Vector256.Create((uint)BlendMaximumAlpha);
            Vector256<uint> bias = Vector256.Create((uint)BlendRoundingBias);
            Vector256<uint> lower = ((weightLower * firstLower) + ((maximum - weightLower) * secondLower) + bias) >> BlendShift;
            Vector256<uint> upper = ((weightUpper * firstUpper) + ((maximum - weightUpper) * secondUpper) + bias) >> BlendShift;
            return Vector256.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Blend(Vector512<ushort> first, Vector512<ushort> second, Vector512<ushort> weights)
        {
            (Vector512<uint> weightLower, Vector512<uint> weightUpper) = Vector512.Widen(weights);
            (Vector512<uint> firstLower, Vector512<uint> firstUpper) = Vector512.Widen(first);
            (Vector512<uint> secondLower, Vector512<uint> secondUpper) = Vector512.Widen(second);
            Vector512<uint> maximum = Vector512.Create((uint)BlendMaximumAlpha);
            Vector512<uint> bias = Vector512.Create((uint)BlendRoundingBias);
            Vector512<uint> lower = ((weightLower * firstLower) + ((maximum - weightLower) * secondLower) + bias) >> BlendShift;
            Vector512<uint> upper = ((weightUpper * firstUpper) + ((maximum - weightUpper) * secondUpper) + bias) >> BlendShift;
            return Vector512.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Blend(ushort first, ushort second, int weight)
            => (ushort)(((weight * first) + ((BlendMaximumAlpha - weight) * second) + BlendRoundingBias) >> BlendShift);
    }
}

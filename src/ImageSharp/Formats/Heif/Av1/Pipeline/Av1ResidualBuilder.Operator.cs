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
    }
}

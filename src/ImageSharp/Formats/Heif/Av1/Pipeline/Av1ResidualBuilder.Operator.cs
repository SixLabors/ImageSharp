// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

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
    internal interface IResidualOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Subtracts eight or sixteen source and prediction samples.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="upper">The upper residual lanes when the inputs contain 8-bit samples.</param>
        /// <returns>The lower residual lanes.</returns>
        public static abstract Vector128<short> Subtract(Vector128<TSample> source, Vector128<TSample> prediction, out Vector128<short> upper);

        /// <summary>
        /// Subtracts sixteen or thirty-two source and prediction samples.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="upper">The upper residual lanes when the inputs contain 8-bit samples.</param>
        /// <returns>The lower residual lanes.</returns>
        public static abstract Vector256<short> Subtract(Vector256<TSample> source, Vector256<TSample> prediction, out Vector256<short> upper);

        /// <summary>
        /// Subtracts thirty-two or sixty-four source and prediction samples.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="upper">The upper residual lanes when the inputs contain 8-bit samples.</param>
        /// <returns>The lower residual lanes.</returns>
        public static abstract Vector512<short> Subtract(Vector512<TSample> source, Vector512<TSample> prediction, out Vector512<short> upper);

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
        /// Measures one scalar absolute sample difference.
        /// </summary>
        /// <param name="source">The source sample.</param>
        /// <param name="prediction">The prediction sample.</param>
        /// <returns>The sum of absolute differences.</returns>
        public static abstract int SumAbsoluteDifferences(TSample source, TSample prediction);

        /// <summary>
        /// Measures eight absolute sample differences; byte inputs occupy only the lower eight lanes.
        /// </summary>
        /// <param name="source">The eight source samples.</param>
        /// <param name="prediction">The eight prediction samples.</param>
        /// <returns>The sum of absolute differences.</returns>
        public static abstract int SumAbsoluteDifferences(Vector128<TSample> source, Vector128<TSample> prediction);

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
        /// <param name="sum">The signed sum of source-minus-prediction differences.</param>
        /// <returns>The sum of squared differences.</returns>
        public static abstract int SumSquaredDifferences(TSample source, TSample prediction, out int sum);

        /// <summary>
        /// Calculates eight squared differences and their signed sum; byte inputs occupy only the lower eight lanes.
        /// </summary>
        /// <param name="source">The eight source samples.</param>
        /// <param name="prediction">The eight prediction samples.</param>
        /// <param name="sum">The signed sum of source-minus-prediction differences.</param>
        /// <returns>The sum of squared differences.</returns>
        public static abstract int SumSquaredDifferences(Vector128<TSample> source, Vector128<TSample> prediction, out int sum);
    }

    /// <summary>
    /// Widens 8-bit samples before subtraction so every residual is represented without precision loss.
    /// </summary>
    internal readonly struct ByteOperator : IResidualOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> LoadDifference(ref byte source, ref byte prediction, int offset, Vector128<short> width)
        {
            // Exactly eight bytes are read from each row, so a row of eight samples is covered
            // without touching the row that follows it.
            Vector128<ushort> s = Vector128.WidenLower(
                Vector128.Create(Vector64.LoadUnsafe(ref source, (nuint)offset), Vector64<byte>.Zero));

            Vector128<ushort> p = Vector128.WidenLower(
                Vector128.Create(Vector64.LoadUnsafe(ref prediction, (nuint)offset), Vector64<byte>.Zero));

            // Both operands are below 256, so the wrapped unsigned subtraction reinterprets as the
            // signed difference the residual is defined to be.
            return (s - p).AsInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> LoadDifference(ref byte source, ref byte prediction, int offset, Vector256<short> width)
        {
            Vector256<ushort> s = Vector256.WidenLower(
                Vector256.Create(Vector128.LoadUnsafe(ref source, (nuint)offset), Vector128<byte>.Zero));

            Vector256<ushort> p = Vector256.WidenLower(
                Vector256.Create(Vector128.LoadUnsafe(ref prediction, (nuint)offset), Vector128<byte>.Zero));

            return (s - p).AsInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> LoadDifference(ref byte source, ref byte prediction, int offset, Vector512<short> width)
        {
            Vector512<ushort> s = Vector512.WidenLower(
                Vector512.Create(Vector256.LoadUnsafe(ref source, (nuint)offset), Vector256<byte>.Zero));

            Vector512<ushort> p = Vector512.WidenLower(
                Vector512.Create(Vector256.LoadUnsafe(ref prediction, (nuint)offset), Vector256<byte>.Zero));

            return (s - p).AsInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SumAbsoluteDifferences(byte source, byte prediction) => Math.Abs(Subtract(source, prediction));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SumAbsoluteDifferences(Vector128<byte> source, Vector128<byte> prediction)
        {
            // Widen byte samples before subtraction; twelve-bit word samples already fit signed-short lanes.
            // Eight absolute residuals sum to at most 32760, so the signed-short horizontal sum remains exact.
            Vector128<short> difference = Vector128.WidenLower(source).AsInt16() - Vector128.WidenLower(prediction).AsInt16();
            return Vector128.Sum(Vector128.Abs(difference));
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SumSquaredDifferences(Vector128<byte> source, Vector128<byte> prediction, out int sum)
        {
            Vector128<short> difference = Vector128.WidenLower(source).AsInt16() - Vector128.WidenLower(prediction).AsInt16();
            sum = Vector128.Sum(difference);

            // The shared square reduction widens to int before multiplying. All eight twelve-bit squares
            // fit in the returned int; no short multiplication or premature bit-depth rounding is permitted.
            return (int)SumSquares(difference);
        }

        /// <inheritdoc/>
        public static Vector128<short> Subtract(Vector128<byte> source, Vector128<byte> prediction, out Vector128<short> upper)
        {
            Vector128<short> lower = Vector128.WidenLower(source).AsInt16() - Vector128.WidenLower(prediction).AsInt16();
            upper = Vector128.WidenUpper(source).AsInt16() - Vector128.WidenUpper(prediction).AsInt16();
            return lower;
        }

        /// <inheritdoc/>
        public static Vector256<short> Subtract(Vector256<byte> source, Vector256<byte> prediction, out Vector256<short> upper)
        {
            Vector256<short> lower = Vector256.WidenLower(source).AsInt16() - Vector256.WidenLower(prediction).AsInt16();
            upper = Vector256.WidenUpper(source).AsInt16() - Vector256.WidenUpper(prediction).AsInt16();
            return lower;
        }

        /// <inheritdoc/>
        public static Vector512<short> Subtract(Vector512<byte> source, Vector512<byte> prediction, out Vector512<short> upper)
        {
            Vector512<short> lower = Vector512.WidenLower(source).AsInt16() - Vector512.WidenLower(prediction).AsInt16();
            upper = Vector512.WidenUpper(source).AsInt16() - Vector512.WidenUpper(prediction).AsInt16();
            return lower;
        }

        /// <inheritdoc/>
        public static short Subtract(byte source, byte prediction) => (short)(source - prediction);
    }

    /// <summary>
    /// Subtracts high-bit-depth samples directly because AV1's 10-bit and 12-bit ranges fit signed-short lanes.
    /// </summary>
    internal readonly struct UInt16Operator : IResidualOperator<ushort>
    {
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
        public static int SumAbsoluteDifferences(ushort source, ushort prediction) => Math.Abs(Subtract(source, prediction));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SumAbsoluteDifferences(Vector128<ushort> source, Vector128<ushort> prediction)
        {
            // Widen byte samples before subtraction; twelve-bit word samples already fit signed-short lanes.
            // Eight absolute residuals sum to at most 32760, so the signed-short horizontal sum remains exact.
            Vector128<short> difference = source.AsInt16() - prediction.AsInt16();
            return Vector128.Sum(Vector128.Abs(difference));
        }

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SumSquaredDifferences(Vector128<ushort> source, Vector128<ushort> prediction, out int sum)
        {
            Vector128<short> difference = source.AsInt16() - prediction.AsInt16();
            sum = Vector128.Sum(difference);

            // The shared square reduction widens to int before multiplying. All eight twelve-bit squares
            // fit in the returned int; no short multiplication or premature bit-depth rounding is permitted.
            return (int)SumSquares(difference);
        }

        /// <inheritdoc/>
        public static Vector128<short> Subtract(Vector128<ushort> source, Vector128<ushort> prediction, out Vector128<short> upper)
        {
            upper = default;
            return source.AsInt16() - prediction.AsInt16();
        }

        /// <inheritdoc/>
        public static Vector256<short> Subtract(Vector256<ushort> source, Vector256<ushort> prediction, out Vector256<short> upper)
        {
            upper = default;
            return source.AsInt16() - prediction.AsInt16();
        }

        /// <inheritdoc/>
        public static Vector512<short> Subtract(Vector512<ushort> source, Vector512<ushort> prediction, out Vector512<short> upper)
        {
            upper = default;
            return source.AsInt16() - prediction.AsInt16();
        }

        /// <inheritdoc/>
        public static short Subtract(ushort source, ushort prediction) => (short)(source - prediction);
    }
}

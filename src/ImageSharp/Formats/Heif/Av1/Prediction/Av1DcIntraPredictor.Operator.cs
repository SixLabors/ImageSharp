// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Defines reference reduction and rounded mean arithmetic for AV1 DC intra prediction.
/// </content>
internal static class Av1DcIntraPredictor
{
    /// <summary>
    /// Defines scalar and SIMD reference reduction for AV1 DC intra prediction.
    /// </summary>
    internal interface IDcPredictionOperator
    {
        /// <summary>
        /// Sums one 8-bit reference sample.
        /// </summary>
        /// <param name="sample">The reference sample.</param>
        /// <returns>The sample value.</returns>
        public static abstract int Sum(byte sample);

        /// <summary>
        /// Sums sixteen 8-bit reference samples.
        /// </summary>
        /// <param name="samples">The reference samples.</param>
        /// <returns>The exact sum.</returns>
        public static abstract int Sum(Vector128<byte> samples);

        /// <summary>
        /// Sums thirty-two 8-bit reference samples.
        /// </summary>
        /// <param name="samples">The reference samples.</param>
        /// <returns>The exact sum.</returns>
        public static abstract int Sum(Vector256<byte> samples);

        /// <summary>
        /// Sums sixty-four 8-bit reference samples.
        /// </summary>
        /// <param name="samples">The reference samples.</param>
        /// <returns>The exact sum.</returns>
        public static abstract int Sum(Vector512<byte> samples);

        /// <summary>
        /// Sums one high-bit-depth reference sample.
        /// </summary>
        /// <param name="sample">The reference sample.</param>
        /// <returns>The sample value.</returns>
        public static abstract int Sum(short sample);

        /// <summary>
        /// Sums eight high-bit-depth reference samples.
        /// </summary>
        /// <param name="samples">The reference samples.</param>
        /// <returns>The exact sum.</returns>
        public static abstract int Sum(Vector128<short> samples);

        /// <summary>
        /// Sums sixteen high-bit-depth reference samples.
        /// </summary>
        /// <param name="samples">The reference samples.</param>
        /// <returns>The exact sum.</returns>
        public static abstract int Sum(Vector256<short> samples);

        /// <summary>
        /// Sums thirty-two high-bit-depth reference samples.
        /// </summary>
        /// <param name="samples">The reference samples.</param>
        /// <returns>The exact sum.</returns>
        public static abstract int Sum(Vector512<short> samples);

        /// <summary>
        /// Calculates the 8-bit DC prediction.
        /// </summary>
        /// <param name="sum">The sum of available reference samples.</param>
        /// <param name="count">The number of available reference samples.</param>
        /// <returns>The rounded DC prediction.</returns>
        public static abstract byte Predict(int sum, int count);

        /// <summary>
        /// Calculates the high-bit-depth DC prediction.
        /// </summary>
        /// <param name="sum">The sum of available reference samples.</param>
        /// <param name="count">The number of available reference samples.</param>
        /// <param name="bitDepth">The reconstructed sample precision.</param>
        /// <returns>The rounded DC prediction.</returns>
        public static abstract short Predict(int sum, int count, int bitDepth);
    }

    /// <summary>
    /// Predicts an 8-bit DC block.
    /// </summary>
    public static void Predict(bool hasLeft, bool hasAbove, Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height)
        => Predictor<DcOperator>.Predict(hasLeft, hasAbove, destination, destinationStride, above, left, width, height);

    /// <summary>
    /// Predicts a high-bit-depth DC block.
    /// </summary>
    public static void Predict(bool hasLeft, bool hasAbove, Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, int bitDepth)
        => Predictor<DcOperator>.Predict(hasLeft, hasAbove, destination, destinationStride, above, left, width, height, bitDepth);

    /// <summary>
    /// Predicts an 8-bit DC block without hardware intrinsics.
    /// </summary>
    public static void PredictScalar(bool hasLeft, bool hasAbove, Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height)
        => Predictor<DcOperator>.PredictScalar(hasLeft, hasAbove, destination, destinationStride, above, left, width, height);

    /// <summary>
    /// Predicts a high-bit-depth DC block without hardware intrinsics.
    /// </summary>
    public static void PredictScalar(bool hasLeft, bool hasAbove, Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, int bitDepth)
        => Predictor<DcOperator>.PredictScalar(hasLeft, hasAbove, destination, destinationStride, above, left, width, height, bitDepth);

    /// <summary>
    /// Calculates the DC value from the available neighboring samples.
    /// </summary>
    internal readonly struct DcOperator : IDcPredictionOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(byte sample) => sample;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(Vector128<byte> samples)
        {
            (Vector128<ushort> lower, Vector128<ushort> upper) = Vector128.Widen(samples);

            return Vector128.Sum(lower) + Vector128.Sum(upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(Vector256<byte> samples)
        {
            (Vector256<ushort> lower, Vector256<ushort> upper) = Vector256.Widen(samples);

            return Vector256.Sum(lower) + Vector256.Sum(upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(Vector512<byte> samples)
        {
            (Vector512<ushort> lower, Vector512<ushort> upper) = Vector512.Widen(samples);

            return Vector512.Sum(lower) + Vector512.Sum(upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(short sample) => sample;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(Vector128<short> samples)
        {
            (Vector128<int> lower, Vector128<int> upper) = Vector128.Widen(samples);

            return Vector128.Sum(lower) + Vector128.Sum(upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(Vector256<short> samples)
        {
            (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(samples);

            return Vector256.Sum(lower) + Vector256.Sum(upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(Vector512<short> samples)
        {
            (Vector512<int> lower, Vector512<int> upper) = Vector512.Widen(samples);

            return Vector512.Sum(lower) + Vector512.Sum(upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Predict(int sum, int count) => count == 0 ? (byte)128 : (byte)((sum + (count >> 1)) / count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Predict(int sum, int count, int bitDepth)
            => count == 0 ? (short)(1 << (bitDepth - 1)) : (short)((sum + (count >> 1)) / count);
    }

    /// <summary>
    /// Reconstructs DC blocks through one closed reduction operator.
    /// </summary>
    /// <typeparam name="TOperator">The reference reduction and rounded mean arithmetic.</typeparam>
    private static class Predictor<TOperator>
        where TOperator : struct, IDcPredictionOperator
    {
        /// <summary>
        /// Predicts an 8-bit DC block.
        /// </summary>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="destination">The destination block.</param>
        /// <param name="destinationStride">The distance between destination rows.</param>
        /// <param name="above">The top reference.</param>
        /// <param name="left">The left reference.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        public static void Predict(bool hasLeft, bool hasAbove, Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height)
        {
            int count = (hasAbove ? width : 0) + (hasLeft ? height : 0);
            int sum = (hasAbove ? Sum(above[..width]) : 0) + (hasLeft ? Sum(left[..height]) : 0);
            byte prediction = TOperator.Predict(sum, count);

            for (int row = 0; row < height; row++)
            {
                destination.Slice(row * destinationStride, width).Fill(prediction);
            }
        }

        /// <summary>
        /// Predicts a high-bit-depth DC block.
        /// </summary>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="destination">The destination block.</param>
        /// <param name="destinationStride">The distance between destination rows.</param>
        /// <param name="above">The top reference.</param>
        /// <param name="left">The left reference.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="bitDepth">The reconstructed sample precision.</param>
        public static void Predict(bool hasLeft, bool hasAbove, Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, int bitDepth)
        {
            int count = (hasAbove ? width : 0) + (hasLeft ? height : 0);
            int sum = (hasAbove ? Sum(above[..width]) : 0) + (hasLeft ? Sum(left[..height]) : 0);
            short prediction = TOperator.Predict(sum, count, bitDepth);

            for (int row = 0; row < height; row++)
            {
                destination.Slice(row * destinationStride, width).Fill(prediction);
            }
        }

        /// <summary>
        /// Predicts an 8-bit DC block without hardware intrinsics.
        /// </summary>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="destination">The destination block.</param>
        /// <param name="destinationStride">The distance between destination rows.</param>
        /// <param name="above">The top reference.</param>
        /// <param name="left">The left reference.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        public static void PredictScalar(bool hasLeft, bool hasAbove, Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height)
        {
            int count = (hasAbove ? width : 0) + (hasLeft ? height : 0);
            int sum = (hasAbove ? SumScalar(above[..width]) : 0) + (hasLeft ? SumScalar(left[..height]) : 0);
            byte prediction = TOperator.Predict(sum, count);

            for (int row = 0; row < height; row++)
            {
                ref byte destinationRow = ref destination[row * destinationStride];
                for (int column = 0; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = prediction;
                }
            }
        }

        /// <summary>
        /// Predicts a high-bit-depth DC block without hardware intrinsics.
        /// </summary>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="destination">The destination block.</param>
        /// <param name="destinationStride">The distance between destination rows.</param>
        /// <param name="above">The top reference.</param>
        /// <param name="left">The left reference.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="bitDepth">The reconstructed sample precision.</param>
        public static void PredictScalar(bool hasLeft, bool hasAbove, Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, int bitDepth)
        {
            int count = (hasAbove ? width : 0) + (hasLeft ? height : 0);
            int sum = (hasAbove ? SumScalar(above[..width]) : 0) + (hasLeft ? SumScalar(left[..height]) : 0);
            short prediction = TOperator.Predict(sum, count, bitDepth);

            for (int row = 0; row < height; row++)
            {
                ref short destinationRow = ref destination[row * destinationStride];
                for (int column = 0; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = prediction;
                }
            }
        }

        /// <summary>
        /// Sums 8-bit references through the widest available SIMD widths.
        /// </summary>
        /// <param name="samples">The reference samples.</param>
        /// <returns>The exact sum.</returns>
        private static int Sum(ReadOnlySpan<byte> samples)
        {
            ref byte samplesBase = ref MemoryMarshal.GetReference(samples);
            int sum = 0;
            int index = 0;

            // The shared index deliberately continues through narrower widths. This handles every legal AV1 edge
            // length without a separate dispatch tree and leaves only an incomplete final vector to scalar code.
            if (Vector512.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = samples.Length - Vector512<byte>.Count;
                for (; index <= oneVectorFromEnd; index += Vector512<byte>.Count)
                {
                    sum += TOperator.Sum(Vector512.LoadUnsafe(ref samplesBase, (nuint)index));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = samples.Length - Vector256<byte>.Count;
                for (; index <= oneVectorFromEnd; index += Vector256<byte>.Count)
                {
                    sum += TOperator.Sum(Vector256.LoadUnsafe(ref samplesBase, (nuint)index));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = samples.Length - Vector128<byte>.Count;
                for (; index <= oneVectorFromEnd; index += Vector128<byte>.Count)
                {
                    sum += TOperator.Sum(Vector128.LoadUnsafe(ref samplesBase, (nuint)index));
                }
            }

            for (; index < samples.Length; index++)
            {
                sum += TOperator.Sum(Unsafe.Add(ref samplesBase, index));
            }

            return sum;
        }

        /// <summary>
        /// Sums high-bit-depth references through the widest available SIMD widths.
        /// </summary>
        /// <param name="samples">The reference samples.</param>
        /// <returns>The exact sum.</returns>
        private static int Sum(ReadOnlySpan<short> samples)
        {
            ref short samplesBase = ref MemoryMarshal.GetReference(samples);
            int sum = 0;
            int index = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = samples.Length - Vector512<short>.Count;
                for (; index <= oneVectorFromEnd; index += Vector512<short>.Count)
                {
                    sum += TOperator.Sum(Vector512.LoadUnsafe(ref samplesBase, (nuint)index));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = samples.Length - Vector256<short>.Count;
                for (; index <= oneVectorFromEnd; index += Vector256<short>.Count)
                {
                    sum += TOperator.Sum(Vector256.LoadUnsafe(ref samplesBase, (nuint)index));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = samples.Length - Vector128<short>.Count;
                for (; index <= oneVectorFromEnd; index += Vector128<short>.Count)
                {
                    sum += TOperator.Sum(Vector128.LoadUnsafe(ref samplesBase, (nuint)index));
                }
            }

            for (; index < samples.Length; index++)
            {
                sum += TOperator.Sum(Unsafe.Add(ref samplesBase, index));
            }

            return sum;
        }

        /// <summary>
        /// Sums 8-bit references without hardware intrinsics.
        /// </summary>
        /// <param name="samples">The reference samples.</param>
        /// <returns>The exact sum.</returns>
        private static int SumScalar(ReadOnlySpan<byte> samples)
        {
            ref byte samplesBase = ref MemoryMarshal.GetReference(samples);
            int sum = 0;

            for (int index = 0; index < samples.Length; index++)
            {
                sum += TOperator.Sum(Unsafe.Add(ref samplesBase, index));
            }

            return sum;
        }

        /// <summary>
        /// Sums high-bit-depth references without hardware intrinsics.
        /// </summary>
        /// <param name="samples">The reference samples.</param>
        /// <returns>The exact sum.</returns>
        private static int SumScalar(ReadOnlySpan<short> samples)
        {
            ref short samplesBase = ref MemoryMarshal.GetReference(samples);
            int sum = 0;

            for (int index = 0; index < samples.Length; index++)
            {
                sum += TOperator.Sum(Unsafe.Add(ref samplesBase, index));
            }

            return sum;
        }
    }
}

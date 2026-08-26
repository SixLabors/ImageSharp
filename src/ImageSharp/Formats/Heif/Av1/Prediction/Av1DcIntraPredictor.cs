// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Reconstructs AV1 DC intra-prediction blocks from the available neighboring samples.
/// </summary>
internal static class Av1DcIntraPredictor
{
    /// <summary>
    /// Predicts an 8-bit DC block.
    /// </summary>
    /// <param name="hasLeft">Whether the prepared left reference is available.</param>
    /// <param name="hasAbove">Whether the prepared top reference is available.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    public static void Predict(bool hasLeft, bool hasAbove, Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height)
    {
        int count = (hasAbove ? width : 0) + (hasLeft ? height : 0);
        int sum = (hasAbove ? Sum(above[..width]) : 0) + (hasLeft ? Sum(left[..height]) : 0);

        // Section 7.11.2.2 defines the unsigned midpoint when no reference is available. Otherwise adding half
        // the reference count implements the specified rounded mean before extending it across the whole block.
        byte prediction = count == 0 ? (byte)128 : (byte)((sum + (count >> 1)) / count);
        for (int row = 0; row < height; row++)
        {
            destination.Slice(row * destinationStride, width).Fill(prediction);
        }
    }

    /// <summary>
    /// Predicts a high-bit-depth DC block.
    /// </summary>
    /// <param name="hasLeft">Whether the prepared left reference is available.</param>
    /// <param name="hasAbove">Whether the prepared top reference is available.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="bitDepth">The reconstructed sample precision.</param>
    public static void Predict(bool hasLeft, bool hasAbove, Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, int bitDepth)
    {
        int count = (hasAbove ? width : 0) + (hasLeft ? height : 0);
        int sum = (hasAbove ? Sum(above[..width]) : 0) + (hasLeft ? Sum(left[..height]) : 0);
        short prediction = count == 0 ? (short)(1 << (bitDepth - 1)) : (short)((sum + (count >> 1)) / count);
        for (int row = 0; row < height; row++)
        {
            destination.Slice(row * destinationStride, width).Fill(prediction);
        }
    }

    /// <summary>
    /// Predicts an 8-bit DC block without hardware intrinsics.
    /// </summary>
    /// <param name="hasLeft">Whether the prepared left reference is available.</param>
    /// <param name="hasAbove">Whether the prepared top reference is available.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    public static void PredictScalar(bool hasLeft, bool hasAbove, Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height)
    {
        int count = (hasAbove ? width : 0) + (hasLeft ? height : 0);
        int sum = (hasAbove ? SumScalar(above[..width]) : 0) + (hasLeft ? SumScalar(left[..height]) : 0);
        byte prediction = count == 0 ? (byte)128 : (byte)((sum + (count >> 1)) / count);
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
    /// <param name="hasLeft">Whether the prepared left reference is available.</param>
    /// <param name="hasAbove">Whether the prepared top reference is available.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="bitDepth">The reconstructed sample precision.</param>
    public static void PredictScalar(bool hasLeft, bool hasAbove, Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, int bitDepth)
    {
        int count = (hasAbove ? width : 0) + (hasLeft ? height : 0);
        int sum = (hasAbove ? SumScalar(above[..width]) : 0) + (hasLeft ? SumScalar(left[..height]) : 0);
        short prediction = count == 0 ? (short)(1 << (bitDepth - 1)) : (short)((sum + (count >> 1)) / count);
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
    /// Sums 8-bit neighboring samples using the widest available SIMD width.
    /// </summary>
    /// <param name="samples">The samples to sum.</param>
    /// <returns>The exact sum.</returns>
    private static int Sum(ReadOnlySpan<byte> samples)
    {
        ref byte samplesBase = ref MemoryMarshal.GetReference(samples);
        int sum = 0;
        int index = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = samples.Length - Vector512<byte>.Count;
            for (; index <= oneVectorFromEnd; index += Vector512<byte>.Count)
            {
                (Vector512<ushort> low, Vector512<ushort> high) = Vector512.Widen(Vector512.LoadUnsafe(ref samplesBase, (nuint)index));
                sum += Vector512.Sum(low) + Vector512.Sum(high);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = samples.Length - Vector256<byte>.Count;
            for (; index <= oneVectorFromEnd; index += Vector256<byte>.Count)
            {
                (Vector256<ushort> low, Vector256<ushort> high) = Vector256.Widen(Vector256.LoadUnsafe(ref samplesBase, (nuint)index));
                sum += Vector256.Sum(low) + Vector256.Sum(high);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = samples.Length - Vector128<byte>.Count;
            for (; index <= oneVectorFromEnd; index += Vector128<byte>.Count)
            {
                (Vector128<ushort> low, Vector128<ushort> high) = Vector128.Widen(Vector128.LoadUnsafe(ref samplesBase, (nuint)index));
                sum += Vector128.Sum(low) + Vector128.Sum(high);
            }
        }

        for (; index < samples.Length; index++)
        {
            sum += Unsafe.Add(ref samplesBase, index);
        }

        return sum;
    }

    /// <summary>
    /// Sums high-bit-depth neighboring samples using the widest available SIMD width.
    /// </summary>
    /// <param name="samples">The samples to sum.</param>
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
                (Vector512<int> low, Vector512<int> high) = Vector512.Widen(Vector512.LoadUnsafe(ref samplesBase, (nuint)index));
                sum += Vector512.Sum(low) + Vector512.Sum(high);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = samples.Length - Vector256<short>.Count;
            for (; index <= oneVectorFromEnd; index += Vector256<short>.Count)
            {
                (Vector256<int> low, Vector256<int> high) = Vector256.Widen(Vector256.LoadUnsafe(ref samplesBase, (nuint)index));
                sum += Vector256.Sum(low) + Vector256.Sum(high);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = samples.Length - Vector128<short>.Count;
            for (; index <= oneVectorFromEnd; index += Vector128<short>.Count)
            {
                (Vector128<int> low, Vector128<int> high) = Vector128.Widen(Vector128.LoadUnsafe(ref samplesBase, (nuint)index));
                sum += Vector128.Sum(low) + Vector128.Sum(high);
            }
        }

        for (; index < samples.Length; index++)
        {
            sum += Unsafe.Add(ref samplesBase, index);
        }

        return sum;
    }

    /// <summary>
    /// Sums 8-bit neighboring samples without hardware intrinsics.
    /// </summary>
    /// <param name="samples">The samples to sum.</param>
    /// <returns>The exact sum.</returns>
    private static int SumScalar(ReadOnlySpan<byte> samples)
    {
        int sum = 0;
        foreach (byte sample in samples)
        {
            sum += sample;
        }

        return sum;
    }

    /// <summary>
    /// Sums high-bit-depth neighboring samples without hardware intrinsics.
    /// </summary>
    /// <param name="samples">The samples to sum.</param>
    /// <returns>The exact sum.</returns>
    private static int SumScalar(ReadOnlySpan<short> samples)
    {
        int sum = 0;
        foreach (short sample in samples)
        {
            sum += sample;
        }

        return sum;
    }
}

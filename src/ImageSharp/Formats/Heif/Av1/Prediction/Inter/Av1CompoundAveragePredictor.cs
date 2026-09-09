// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Combines two AV1 inter predictors with equal-weight rounded averaging.
/// </content>
internal static partial class Av1CompoundAveragePredictor
{
    /// <summary>
    /// Averages an 8-bit predictor into an existing prediction block.
    /// </summary>
    /// <param name="destination">The first predictor and combined output.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="second">The second predictor.</param>
    /// <param name="secondStride">The distance between second-predictor rows in samples.</param>
    /// <param name="width">The active block width.</param>
    /// <param name="height">The active block height.</param>
    public static void Average(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height)
        => Average<CompoundAverageOperator>(destination, destinationStride, second, secondStride, width, height);

    /// <summary>
    /// Averages an 8-bit predictor through one closed compound operator.
    /// </summary>
    /// <typeparam name="TOperator">The equal-weight averaging arithmetic.</typeparam>
    /// <param name="destination">The first predictor and combined output.</param>
    /// <param name="destinationStride">The distance between destination rows.</param>
    /// <param name="second">The second predictor.</param>
    /// <param name="secondStride">The distance between second-predictor rows.</param>
    /// <param name="width">The active block width.</param>
    /// <param name="height">The active block height.</param>
    private static void Average<TOperator>(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height)
        where TOperator : struct, IAv1CompoundAverageOperator
    {
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            ref byte destinationReference = ref MemoryMarshal.GetReference(destinationRow);
            ref byte secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector512Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector512<byte>.Count)
                {
                    Vector512<byte> firstVector = Vector512.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector512<byte> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector256Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<byte>.Count)
                {
                    Vector256<byte> firstVector = Vector256.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector256<byte> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector128Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<byte>.Count)
                {
                    Vector128<byte> firstVector = Vector128.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector128<byte> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                destinationRow[column] = TOperator.Blend(destinationRow[column], secondRow[column]);
            }
        }
    }

    /// <summary>
    /// Averages a high-bit-depth predictor into an existing prediction block.
    /// </summary>
    /// <param name="destination">The first predictor and combined output.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="second">The second predictor.</param>
    /// <param name="secondStride">The distance between second-predictor rows in samples.</param>
    /// <param name="width">The active block width.</param>
    /// <param name="height">The active block height.</param>
    public static void Average(
        Span<ushort> destination,
        int destinationStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height)
        => Average<CompoundAverageOperator>(destination, destinationStride, second, secondStride, width, height);

    /// <summary>
    /// Averages a high-bit-depth predictor through one closed compound operator.
    /// </summary>
    /// <typeparam name="TOperator">The equal-weight averaging arithmetic.</typeparam>
    /// <param name="destination">The first predictor and combined output.</param>
    /// <param name="destinationStride">The distance between destination rows.</param>
    /// <param name="second">The second predictor.</param>
    /// <param name="secondStride">The distance between second-predictor rows.</param>
    /// <param name="width">The active block width.</param>
    /// <param name="height">The active block height.</param>
    private static void Average<TOperator>(
        Span<ushort> destination,
        int destinationStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height)
        where TOperator : struct, IAv1CompoundAverageOperator
    {
        for (int row = 0; row < height; row++)
        {
            Span<ushort> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<ushort> secondRow = second.Slice(row * secondStride, width);
            ref ushort destinationReference = ref MemoryMarshal.GetReference(destinationRow);
            ref ushort secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector512Count<ushort>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector512<ushort>.Count)
                {
                    Vector512<ushort> firstVector = Vector512.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector512<ushort> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector256Count<ushort>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<ushort>.Count)
                {
                    Vector256<ushort> firstVector = Vector256.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector256<ushort> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector128Count<ushort>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<ushort>.Count)
                {
                    Vector128<ushort> firstVector = Vector128.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector128<ushort> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                destinationRow[column] = TOperator.Blend(destinationRow[column], secondRow[column]);
            }
        }
    }

    /// <summary>
    /// Averages an 8-bit predictor without explicit hardware intrinsics.
    /// </summary>
    /// <param name="destination">The first predictor and combined output.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="second">The second predictor.</param>
    /// <param name="secondStride">The distance between second-predictor rows in samples.</param>
    /// <param name="width">The active block width.</param>
    /// <param name="height">The active block height.</param>
    public static void AverageScalar(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height)
        => AverageScalar<CompoundAverageOperator>(destination, destinationStride, second, secondStride, width, height);

    /// <summary>
    /// Averages an 8-bit predictor through one closed scalar compound operator.
    /// </summary>
    /// <typeparam name="TOperator">The equal-weight averaging arithmetic.</typeparam>
    /// <param name="destination">The first predictor and combined output.</param>
    /// <param name="destinationStride">The distance between destination rows.</param>
    /// <param name="second">The second predictor.</param>
    /// <param name="secondStride">The distance between second-predictor rows.</param>
    /// <param name="width">The active block width.</param>
    /// <param name="height">The active block height.</param>
    private static void AverageScalar<TOperator>(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height)
        where TOperator : struct, IAv1CompoundAverageOperator
    {
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            for (int column = 0; column < width; column++)
            {
                destinationRow[column] = TOperator.Blend(destinationRow[column], secondRow[column]);
            }
        }
    }

    /// <summary>
    /// Averages a high-bit-depth predictor without explicit hardware intrinsics.
    /// </summary>
    /// <param name="destination">The first predictor and combined output.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="second">The second predictor.</param>
    /// <param name="secondStride">The distance between second-predictor rows in samples.</param>
    /// <param name="width">The active block width.</param>
    /// <param name="height">The active block height.</param>
    public static void AverageScalar(
        Span<ushort> destination,
        int destinationStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height)
        => AverageScalar<CompoundAverageOperator>(destination, destinationStride, second, secondStride, width, height);

    /// <summary>
    /// Averages a high-bit-depth predictor through one closed scalar compound operator.
    /// </summary>
    /// <typeparam name="TOperator">The equal-weight averaging arithmetic.</typeparam>
    /// <param name="destination">The first predictor and combined output.</param>
    /// <param name="destinationStride">The distance between destination rows.</param>
    /// <param name="second">The second predictor.</param>
    /// <param name="secondStride">The distance between second-predictor rows.</param>
    /// <param name="width">The active block width.</param>
    /// <param name="height">The active block height.</param>
    private static void AverageScalar<TOperator>(
        Span<ushort> destination,
        int destinationStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height)
        where TOperator : struct, IAv1CompoundAverageOperator
    {
        for (int row = 0; row < height; row++)
        {
            Span<ushort> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<ushort> secondRow = second.Slice(row * secondStride, width);
            for (int column = 0; column < width; column++)
            {
                destinationRow[column] = TOperator.Blend(destinationRow[column], secondRow[column]);
            }
        }
    }
}

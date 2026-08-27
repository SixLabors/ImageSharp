// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;

/// <content>
/// Provides the width-progressive SIMD traversal shared by the intra-block-copy interpolation operators.
/// </content>
internal static partial class Av1IntraBlockCopyPredictor
{
    /// <summary>
    /// Applies one closed interpolation operator to an 8-bit source block.
    /// </summary>
    /// <typeparam name="TOperator">The source-phase-specific interpolation arithmetic.</typeparam>
    private static void Predict<TOperator>(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height)
        where TOperator : struct, IOperator
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);

        if (Vector128.IsHardwareAccelerated && width is 4 or 8)
        {
            // AV1 permits 4- and 8-sample transform widths, both smaller than a byte Vector128. The frame allocation's
            // 72-sample prediction border makes each full source load readable; exact-width stores avoid touching
            // destination padding.
            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                Vector128<byte> topLeft = Vector128.LoadUnsafe(ref sourceRow);
                Vector128<byte> topRight = TOperator.UsesRight ? Vector128.LoadUnsafe(ref sourceRow, 1) : default;
                Vector128<byte> bottomLeft = TOperator.UsesBottom ? Vector128.LoadUnsafe(ref sourceRow, (nuint)sourceStride) : default;
                Vector128<byte> bottomRight = TOperator.UsesRight && TOperator.UsesBottom
                    ? Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + 1))
                    : default;

                Vector128<byte> prediction = TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight);
                if (width == 8)
                {
                    prediction.GetLower().StoreUnsafe(ref destinationRow);
                }
                else
                {
                    Unsafe.As<byte, uint>(ref destinationRow) = prediction.AsUInt32().GetElement(0);
                }
            }

            return;
        }

        int processedColumns = 0;

        // AV1 transform widths are powers of two. The widest supported tier normally consumes the complete row; the
        // cumulative narrower tiers preserve the same contract for future legal widths without over-reading a tail.
        if (Vector512.IsHardwareAccelerated)
        {
            int vectorizedColumns = width - (width % Vector512<byte>.Count);
            if (vectorizedColumns > 0)
            {
                for (int row = 0; row < height; row++)
                {
                    ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                    for (int column = 0; column < vectorizedColumns; column += Vector512<byte>.Count)
                    {
                        Vector512<byte> topLeft = Vector512.LoadUnsafe(ref sourceRow, (nuint)column);
                        Vector512<byte> topRight = TOperator.UsesRight ? Vector512.LoadUnsafe(ref sourceRow, (nuint)(column + 1)) : default;
                        Vector512<byte> bottomLeft = TOperator.UsesBottom
                            ? Vector512.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column))
                            : default;
                        Vector512<byte> bottomRight = TOperator.UsesRight && TOperator.UsesBottom
                            ? Vector512.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1))
                            : default;

                        TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                processedColumns = vectorizedColumns;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int remainingColumns = width - processedColumns;
            int vectorizedColumns = remainingColumns - (remainingColumns % Vector256<byte>.Count);
            int endColumn = processedColumns + vectorizedColumns;

            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = processedColumns; column < endColumn; column += Vector256<byte>.Count)
                {
                    Vector256<byte> topLeft = Vector256.LoadUnsafe(ref sourceRow, (nuint)column);
                    Vector256<byte> topRight = TOperator.UsesRight ? Vector256.LoadUnsafe(ref sourceRow, (nuint)(column + 1)) : default;
                    Vector256<byte> bottomLeft = TOperator.UsesBottom
                        ? Vector256.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column))
                        : default;
                    Vector256<byte> bottomRight = TOperator.UsesRight && TOperator.UsesBottom
                        ? Vector256.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1))
                        : default;

                    TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            processedColumns = endColumn;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int remainingColumns = width - processedColumns;
            int vectorizedColumns = remainingColumns - (remainingColumns % Vector128<byte>.Count);
            int endColumn = processedColumns + vectorizedColumns;

            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = processedColumns; column < endColumn; column += Vector128<byte>.Count)
                {
                    Vector128<byte> topLeft = Vector128.LoadUnsafe(ref sourceRow, (nuint)column);
                    Vector128<byte> topRight = TOperator.UsesRight ? Vector128.LoadUnsafe(ref sourceRow, (nuint)(column + 1)) : default;
                    Vector128<byte> bottomLeft = TOperator.UsesBottom
                        ? Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column))
                        : default;
                    Vector128<byte> bottomRight = TOperator.UsesRight && TOperator.UsesBottom
                        ? Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1))
                        : default;

                    TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            processedColumns = endColumn;
        }

        // FeatureTestRunner can disable every intrinsic tier. Keeping the scalar continuation in the same traversal
        // proves the fallback without changing source addressing or the normative rounding performed by the operator.
        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

            for (int column = processedColumns; column < width; column++)
            {
                byte topLeft = Unsafe.Add(ref sourceRow, column);
                byte topRight = TOperator.UsesRight ? Unsafe.Add(ref sourceRow, column + 1) : default;
                byte bottomLeft = TOperator.UsesBottom ? Unsafe.Add(ref sourceRow, sourceStride + column) : default;
                byte bottomRight = TOperator.UsesRight && TOperator.UsesBottom
                    ? Unsafe.Add(ref sourceRow, sourceStride + column + 1)
                    : default;

                Unsafe.Add(ref destinationRow, column) = TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight);
            }
        }
    }

    /// <summary>
    /// Applies one closed interpolation operator to a high-bit-depth source block.
    /// </summary>
    /// <typeparam name="TOperator">The source-phase-specific interpolation arithmetic.</typeparam>
    private static void Predict<TOperator>(
        ReadOnlySpan<short> source,
        int sourceStride,
        Span<short> destination,
        int destinationStride,
        int width,
        int height)
        where TOperator : struct, IOperator
    {
        ref short sourceBase = ref MemoryMarshal.GetReference(source);
        ref short destinationBase = ref MemoryMarshal.GetReference(destination);

        if (Vector128.IsHardwareAccelerated && width == 4)
        {
            // Four high-bit-depth samples occupy the lower half of a Vector128. The frame allocation's prediction
            // border makes the full source load readable; storing only the lower four lanes avoids relying on writable
            // samples beyond the transform boundary.
            for (int row = 0; row < height; row++)
            {
                ref short sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                Vector128<short> topLeft = Vector128.LoadUnsafe(ref sourceRow);
                Vector128<short> topRight = TOperator.UsesRight ? Vector128.LoadUnsafe(ref sourceRow, 1) : default;
                Vector128<short> bottomLeft = TOperator.UsesBottom ? Vector128.LoadUnsafe(ref sourceRow, (nuint)sourceStride) : default;
                Vector128<short> bottomRight = TOperator.UsesRight && TOperator.UsesBottom
                    ? Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + 1))
                    : default;

                TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).GetLower().StoreUnsafe(ref destinationRow);
            }

            return;
        }

        int processedColumns = 0;

        // High-bit-depth lanes hold half as many samples, but retain the same descending-width traversal and one scalar
        // continuation as the byte path.
        if (Vector512.IsHardwareAccelerated)
        {
            int vectorizedColumns = width - (width % Vector512<short>.Count);
            if (vectorizedColumns > 0)
            {
                for (int row = 0; row < height; row++)
                {
                    ref short sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                    ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                    for (int column = 0; column < vectorizedColumns; column += Vector512<short>.Count)
                    {
                        Vector512<short> topLeft = Vector512.LoadUnsafe(ref sourceRow, (nuint)column);
                        Vector512<short> topRight = TOperator.UsesRight ? Vector512.LoadUnsafe(ref sourceRow, (nuint)(column + 1)) : default;
                        Vector512<short> bottomLeft = TOperator.UsesBottom
                            ? Vector512.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column))
                            : default;
                        Vector512<short> bottomRight = TOperator.UsesRight && TOperator.UsesBottom
                            ? Vector512.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1))
                            : default;

                        TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                processedColumns = vectorizedColumns;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int remainingColumns = width - processedColumns;
            int vectorizedColumns = remainingColumns - (remainingColumns % Vector256<short>.Count);
            int endColumn = processedColumns + vectorizedColumns;

            for (int row = 0; row < height; row++)
            {
                ref short sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = processedColumns; column < endColumn; column += Vector256<short>.Count)
                {
                    Vector256<short> topLeft = Vector256.LoadUnsafe(ref sourceRow, (nuint)column);
                    Vector256<short> topRight = TOperator.UsesRight ? Vector256.LoadUnsafe(ref sourceRow, (nuint)(column + 1)) : default;
                    Vector256<short> bottomLeft = TOperator.UsesBottom
                        ? Vector256.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column))
                        : default;
                    Vector256<short> bottomRight = TOperator.UsesRight && TOperator.UsesBottom
                        ? Vector256.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1))
                        : default;

                    TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            processedColumns = endColumn;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int remainingColumns = width - processedColumns;
            int vectorizedColumns = remainingColumns - (remainingColumns % Vector128<short>.Count);
            int endColumn = processedColumns + vectorizedColumns;

            for (int row = 0; row < height; row++)
            {
                ref short sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = processedColumns; column < endColumn; column += Vector128<short>.Count)
                {
                    Vector128<short> topLeft = Vector128.LoadUnsafe(ref sourceRow, (nuint)column);
                    Vector128<short> topRight = TOperator.UsesRight ? Vector128.LoadUnsafe(ref sourceRow, (nuint)(column + 1)) : default;
                    Vector128<short> bottomLeft = TOperator.UsesBottom
                        ? Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column))
                        : default;
                    Vector128<short> bottomRight = TOperator.UsesRight && TOperator.UsesBottom
                        ? Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1))
                        : default;

                    TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            processedColumns = endColumn;
        }

        for (int row = 0; row < height; row++)
        {
            ref short sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
            ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

            for (int column = processedColumns; column < width; column++)
            {
                short topLeft = Unsafe.Add(ref sourceRow, column);
                short topRight = TOperator.UsesRight ? Unsafe.Add(ref sourceRow, column + 1) : default;
                short bottomLeft = TOperator.UsesBottom ? Unsafe.Add(ref sourceRow, sourceStride + column) : default;
                short bottomRight = TOperator.UsesRight && TOperator.UsesBottom
                    ? Unsafe.Add(ref sourceRow, sourceStride + column + 1)
                    : default;

                Unsafe.Add(ref destinationRow, column) = TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight);
            }
        }
    }

    /// <summary>
    /// Applies one closed interpolation operator to an 8-bit source block without explicit hardware intrinsics.
    /// </summary>
    /// <typeparam name="TOperator">The source-phase-specific interpolation arithmetic.</typeparam>
    private static void PredictScalar<TOperator>(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height)
        where TOperator : struct, IOperator
    {
        for (int row = 0; row < height; row++)
        {
            int sourceRow = row * sourceStride;
            int destinationRow = row * destinationStride;

            for (int column = 0; column < width; column++)
            {
                byte topLeft = source[sourceRow + column];
                byte topRight = TOperator.UsesRight ? source[sourceRow + column + 1] : default;
                byte bottomLeft = TOperator.UsesBottom ? source[sourceRow + sourceStride + column] : default;
                byte bottomRight = TOperator.UsesRight && TOperator.UsesBottom ? source[sourceRow + sourceStride + column + 1] : default;
                destination[destinationRow + column] = TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight);
            }
        }
    }

    /// <summary>
    /// Applies one closed interpolation operator to a high-bit-depth source block without explicit hardware intrinsics.
    /// </summary>
    /// <typeparam name="TOperator">The source-phase-specific interpolation arithmetic.</typeparam>
    private static void PredictScalar<TOperator>(
        ReadOnlySpan<short> source,
        int sourceStride,
        Span<short> destination,
        int destinationStride,
        int width,
        int height)
        where TOperator : struct, IOperator
    {
        for (int row = 0; row < height; row++)
        {
            int sourceRow = row * sourceStride;
            int destinationRow = row * destinationStride;

            for (int column = 0; column < width; column++)
            {
                short topLeft = source[sourceRow + column];
                short topRight = TOperator.UsesRight ? source[sourceRow + column + 1] : default;
                short bottomLeft = TOperator.UsesBottom ? source[sourceRow + sourceStride + column] : default;
                short bottomRight = TOperator.UsesRight && TOperator.UsesBottom ? source[sourceRow + sourceStride + column + 1] : default;
                destination[destinationRow + column] = TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight);
            }
        }
    }
}

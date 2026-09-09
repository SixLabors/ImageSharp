// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;

/// <content>
/// Provides the family-owned scalar and width-progressive SIMD traversal for bilinear intra-block-copy prediction.
/// </content>
internal static partial class Av1IntraBlockCopyBilinearPredictor
{
    /// <summary>
    /// Reconstructs an 8-bit filtered intra-block-copy prediction.
    /// </summary>
    public static void Predict(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height)
        => Predict<IntraBlockCopyBilinearOperator>(source, sourceStride, destination, destinationStride, width, height);

    /// <summary>
    /// Reconstructs a high-bit-depth filtered intra-block-copy prediction.
    /// </summary>
    public static void Predict(
        ReadOnlySpan<short> source,
        int sourceStride,
        Span<short> destination,
        int destinationStride,
        int width,
        int height)
        => Predict<IntraBlockCopyBilinearOperator>(source, sourceStride, destination, destinationStride, width, height);

    /// <summary>
    /// Reconstructs an 8-bit filtered intra-block-copy prediction without explicit hardware intrinsics.
    /// </summary>
    public static void PredictScalar(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height)
        => PredictScalar<IntraBlockCopyBilinearOperator>(source, sourceStride, destination, destinationStride, width, height);

    /// <summary>
    /// Reconstructs a high-bit-depth filtered intra-block-copy prediction without explicit hardware intrinsics.
    /// </summary>
    public static void PredictScalar(
        ReadOnlySpan<short> source,
        int sourceStride,
        Span<short> destination,
        int destinationStride,
        int width,
        int height)
        => PredictScalar<IntraBlockCopyBilinearOperator>(source, sourceStride, destination, destinationStride, width, height);

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
        where TOperator : struct, IAv1IntraBlockCopyBilinearOperator
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
                Vector128<byte> topRight = Vector128.LoadUnsafe(ref sourceRow, 1);
                Vector128<byte> bottomLeft = Vector128.LoadUnsafe(ref sourceRow, (nuint)sourceStride);
                Vector128<byte> bottomRight = Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + 1));

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
            int vectorizedColumns = (int)(Numerics.Vector512Count<byte>(width) * (nuint)Vector512<byte>.Count);
            if (vectorizedColumns > 0)
            {
                for (int row = 0; row < height; row++)
                {
                    ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                    for (int column = 0; column < vectorizedColumns; column += Vector512<byte>.Count)
                    {
                        Vector512<byte> topLeft = Vector512.LoadUnsafe(ref sourceRow, (nuint)column);
                        Vector512<byte> topRight = Vector512.LoadUnsafe(ref sourceRow, (nuint)(column + 1));
                        Vector512<byte> bottomLeft = Vector512.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column));
                        Vector512<byte> bottomRight = Vector512.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1));

                        TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                processedColumns = vectorizedColumns;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int remainingColumns = width - processedColumns;
            int vectorizedColumns = (int)(Numerics.Vector256Count<byte>(remainingColumns) * (nuint)Vector256<byte>.Count);
            int endColumn = processedColumns + vectorizedColumns;

            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = processedColumns; column < endColumn; column += Vector256<byte>.Count)
                {
                    Vector256<byte> topLeft = Vector256.LoadUnsafe(ref sourceRow, (nuint)column);
                    Vector256<byte> topRight = Vector256.LoadUnsafe(ref sourceRow, (nuint)(column + 1));
                    Vector256<byte> bottomLeft = Vector256.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column));
                    Vector256<byte> bottomRight = Vector256.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1));

                    TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            processedColumns = endColumn;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int remainingColumns = width - processedColumns;
            int vectorizedColumns = (int)(Numerics.Vector128Count<byte>(remainingColumns) * (nuint)Vector128<byte>.Count);
            int endColumn = processedColumns + vectorizedColumns;

            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = processedColumns; column < endColumn; column += Vector128<byte>.Count)
                {
                    Vector128<byte> topLeft = Vector128.LoadUnsafe(ref sourceRow, (nuint)column);
                    Vector128<byte> topRight = Vector128.LoadUnsafe(ref sourceRow, (nuint)(column + 1));
                    Vector128<byte> bottomLeft = Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column));
                    Vector128<byte> bottomRight = Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1));

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
                byte topRight = Unsafe.Add(ref sourceRow, column + 1);
                byte bottomLeft = Unsafe.Add(ref sourceRow, sourceStride + column);
                byte bottomRight = Unsafe.Add(ref sourceRow, sourceStride + column + 1);

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
        where TOperator : struct, IAv1IntraBlockCopyBilinearOperator
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
                Vector128<short> topRight = Vector128.LoadUnsafe(ref sourceRow, 1);
                Vector128<short> bottomLeft = Vector128.LoadUnsafe(ref sourceRow, (nuint)sourceStride);
                Vector128<short> bottomRight = Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + 1));

                TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).GetLower().StoreUnsafe(ref destinationRow);
            }

            return;
        }

        int processedColumns = 0;

        // High-bit-depth lanes hold half as many samples, but retain the same descending-width traversal and one scalar
        // continuation as the byte path.
        if (Vector512.IsHardwareAccelerated)
        {
            int vectorizedColumns = (int)(Numerics.Vector512Count<short>(width) * (nuint)Vector512<short>.Count);
            if (vectorizedColumns > 0)
            {
                for (int row = 0; row < height; row++)
                {
                    ref short sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                    ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                    for (int column = 0; column < vectorizedColumns; column += Vector512<short>.Count)
                    {
                        Vector512<short> topLeft = Vector512.LoadUnsafe(ref sourceRow, (nuint)column);
                        Vector512<short> topRight = Vector512.LoadUnsafe(ref sourceRow, (nuint)(column + 1));
                        Vector512<short> bottomLeft = Vector512.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column));
                        Vector512<short> bottomRight = Vector512.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1));

                        TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                processedColumns = vectorizedColumns;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int remainingColumns = width - processedColumns;
            int vectorizedColumns = (int)(Numerics.Vector256Count<short>(remainingColumns) * (nuint)Vector256<short>.Count);
            int endColumn = processedColumns + vectorizedColumns;

            for (int row = 0; row < height; row++)
            {
                ref short sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = processedColumns; column < endColumn; column += Vector256<short>.Count)
                {
                    Vector256<short> topLeft = Vector256.LoadUnsafe(ref sourceRow, (nuint)column);
                    Vector256<short> topRight = Vector256.LoadUnsafe(ref sourceRow, (nuint)(column + 1));
                    Vector256<short> bottomLeft = Vector256.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column));
                    Vector256<short> bottomRight = Vector256.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1));

                    TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            processedColumns = endColumn;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int remainingColumns = width - processedColumns;
            int vectorizedColumns = (int)(Numerics.Vector128Count<short>(remainingColumns) * (nuint)Vector128<short>.Count);
            int endColumn = processedColumns + vectorizedColumns;

            for (int row = 0; row < height; row++)
            {
                ref short sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = processedColumns; column < endColumn; column += Vector128<short>.Count)
                {
                    Vector128<short> topLeft = Vector128.LoadUnsafe(ref sourceRow, (nuint)column);
                    Vector128<short> topRight = Vector128.LoadUnsafe(ref sourceRow, (nuint)(column + 1));
                    Vector128<short> bottomLeft = Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column));
                    Vector128<short> bottomRight = Vector128.LoadUnsafe(ref sourceRow, (nuint)(sourceStride + column + 1));

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
                short topRight = Unsafe.Add(ref sourceRow, column + 1);
                short bottomLeft = Unsafe.Add(ref sourceRow, sourceStride + column);
                short bottomRight = Unsafe.Add(ref sourceRow, sourceStride + column + 1);

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
        where TOperator : struct, IAv1IntraBlockCopyBilinearOperator
    {
        for (int row = 0; row < height; row++)
        {
            int sourceRow = row * sourceStride;
            int destinationRow = row * destinationStride;

            for (int column = 0; column < width; column++)
            {
                byte topLeft = source[sourceRow + column];
                byte topRight = source[sourceRow + column + 1];
                byte bottomLeft = source[sourceRow + sourceStride + column];
                byte bottomRight = source[sourceRow + sourceStride + column + 1];
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
        where TOperator : struct, IAv1IntraBlockCopyBilinearOperator
    {
        for (int row = 0; row < height; row++)
        {
            int sourceRow = row * sourceStride;
            int destinationRow = row * destinationStride;

            for (int column = 0; column < width; column++)
            {
                short topLeft = source[sourceRow + column];
                short topRight = source[sourceRow + column + 1];
                short bottomLeft = source[sourceRow + sourceStride + column];
                short bottomRight = source[sourceRow + sourceStride + column + 1];
                destination[destinationRow + column] = TOperator.Filter(topLeft, topRight, bottomLeft, bottomRight);
            }
        }
    }
}

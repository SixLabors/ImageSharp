// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Measures the correlation of residual neighbors.
/// </content>
internal static partial class Av1ResidualBuilder
{
    /// <summary>
    /// Returns the correlation of each residual with its left neighbor and with its top neighbor. Reference:
    /// av1_get_horver_correlation_full().
    /// </summary>
    /// <param name="residual">The residual samples at the block origin.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="width">The block width, at least two.</param>
    /// <param name="height">The block height, at least two.</param>
    /// <param name="horizontal">Receives the correlation with the left neighbor.</param>
    /// <param name="vertical">Receives the correlation with the top neighbor.</param>
    public static void GetHorizontalVerticalCorrelation(
        ReadOnlySpan<short> residual,
        int stride,
        int width,
        int height,
        out float horizontal,
        out float vertical)
    {
        // x is a sample, y its left neighbor and z its top neighbor. The sums over the pairs follow from the
        // sums over the whole block minus its first or last row or column.
        long squareSum = SumAndSumSquares<ResidualSquaresOperator>(residual, stride, width, height, out long sum);
        long leftProducts = SumProducts<ResidualSquaresOperator>(residual[1..], residual, stride, width - 1, height);
        long topProducts = SumProducts<ResidualSquaresOperator>(residual[stride..], residual, stride, width, height - 1);
        long firstRowSquares = SumAndSumSquares<ResidualSquaresOperator>(residual, stride, width, 1, out long firstRow);
        long finalRowSquares = SumAndSumSquares<ResidualSquaresOperator>(residual[((height - 1) * stride)..], stride, width, 1, out long finalRow);
        long firstColumnSquares = SumAndSumSquares<ResidualSquaresOperator>(residual, stride, 1, height, out long firstColumn);
        long finalColumnSquares = SumAndSumSquares<ResidualSquaresOperator>(residual[(width - 1)..], stride, 1, height, out long finalColumn);

        long horizontalSum = sum - finalColumn;
        long verticalSum = sum - finalRow;
        long leftSum = sum - firstColumn;
        long topSum = sum - firstRow;
        long horizontalSquares = squareSum - finalColumnSquares;
        long verticalSquares = squareSum - finalRowSquares;
        long leftSquares = squareSum - firstColumnSquares;
        long topSquares = squareSum - firstRowSquares;

        float horizontalCount = height * (width - 1);
        float verticalCount = (height - 1) * width;
        float horizontalVariance = horizontalSquares - ((horizontalSum * horizontalSum) / horizontalCount);
        float verticalVariance = verticalSquares - ((verticalSum * verticalSum) / verticalCount);
        float leftVariance = leftSquares - ((leftSum * leftSum) / horizontalCount);
        float topVariance = topSquares - ((topSum * topSum) / verticalCount);
        float leftCovariance = leftProducts - ((horizontalSum * leftSum) / horizontalCount);
        float topCovariance = topProducts - ((verticalSum * topSum) / verticalCount);

        if (horizontalVariance > 0 && leftVariance > 0)
        {
            horizontal = leftCovariance / MathF.Sqrt(horizontalVariance * leftVariance);
            horizontal = horizontal < 0 ? 0 : horizontal;
        }
        else
        {
            horizontal = 1F;
        }

        if (verticalVariance > 0 && topVariance > 0)
        {
            vertical = topCovariance / MathF.Sqrt(verticalVariance * topVariance);
            vertical = vertical < 0 ? 0 : vertical;
        }
        else
        {
            vertical = 1F;
        }
    }

    /// <summary>
    /// Traverses the sum of the products of two equally strided residual rectangles at descending register widths.
    /// </summary>
    private static long SumProducts<TOperator>(ReadOnlySpan<short> first, ReadOnlySpan<short> second, int stride, int width, int height)
        where TOperator : struct, IResidualSquaresOperator
    {
        int extent = height == 0 ? 0 : ((height - 1) * stride) + width;
        ref short firstBase = ref MemoryMarshal.GetReference(first[..extent]);
        ref short secondBase = ref MemoryMarshal.GetReference(second[..extent]);
        Vector512<long> total512 = Vector512<long>.Zero;
        Vector256<long> total256 = Vector256<long>.Zero;
        Vector128<long> total128 = Vector128<long>.Zero;
        long total = 0;
        for (int y = 0; y < height; y++)
        {
            ref short firstRow = ref Unsafe.Add(ref firstBase, y * stride);
            ref short secondRow = ref Unsafe.Add(ref secondBase, y * stride);
            int x = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<short>.Count; x += Vector512<short>.Count)
                {
                    total512 = TOperator.AccumulateProducts(
                        Vector512.LoadUnsafe(ref firstRow, (nuint)x), Vector512.LoadUnsafe(ref secondRow, (nuint)x), total512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<short>.Count; x += Vector256<short>.Count)
                {
                    total256 = TOperator.AccumulateProducts(
                        Vector256.LoadUnsafe(ref firstRow, (nuint)x), Vector256.LoadUnsafe(ref secondRow, (nuint)x), total256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<short>.Count; x += Vector128<short>.Count)
                {
                    total128 = TOperator.AccumulateProducts(
                        Vector128.LoadUnsafe(ref firstRow, (nuint)x), Vector128.LoadUnsafe(ref secondRow, (nuint)x), total128);
                }
            }

            for (; x < width; x++)
            {
                total = TOperator.AccumulateProducts(Unsafe.Add(ref firstRow, x), Unsafe.Add(ref secondRow, x), total);
            }
        }

        total256 += total512.GetLower() + total512.GetUpper();
        total128 += total256.GetLower() + total256.GetUpper();
        return total + Vector128.Sum(total128);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Computes restoration-unit correlations without reducing the source sample precision.
/// </content>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// Computes centered source correlations and the symmetric neighborhood covariance matrix.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <param name="source">The exact original restoration unit.</param>
    /// <param name="reconstruction">The corresponding reconstructed unit with addressable filter borders.</param>
    /// <param name="window">The five- or seven-tap fitting window.</param>
    /// <param name="bitDepth">The component precision.</param>
    /// <param name="downsample">Whether to sample every fourth row for eight-bit statistics.</param>
    /// <param name="correlation">The window-squared source correlation output.</param>
    /// <param name="covariance">The square covariance matrix output.</param>
    private static void ComputeWienerStatistics<TSample>(
        Av1PlaneRegion<TSample> source,
        Av1PlaneRegion<TSample> reconstruction,
        int window,
        int bitDepth,
        bool downsample,
        Span<long> correlation,
        Span<long> covariance)
        where TSample : unmanaged
    {
        // The mean of the reconstructed unit centers every product. Reference: find_average().
        GetSampleMoments(reconstruction, out long sum, out _);
        int average = (int)(sum / (reconstruction.Width * reconstruction.Height));
        ReadOnlySpan<TSample> original = source.Samples;
        ReadOnlySpan<TSample> degraded = reconstruction.Samples;
        int sourceOrigin = (source.Bounds.Y * source.Stride) + source.Bounds.X;
        int degradedOrigin = (reconstruction.Bounds.Y * reconstruction.Stride) + reconstruction.Bounds.X;
        int count = window * window;
        int half = window >> 1;
        int rowStep = bitDepth == 8 && downsample ? 4 : 1;
        int divisor = 1 << (bitDepth - 8);
        for (int first = 0; first < count; first++)
        {
            // Neighborhood indices advance vertically first, then horizontally. Keeping this order
            // preserves the axis interpretation used by the constrained separable coefficient solve.
            int firstOffset = degradedOrigin + (((first % window) - half) * reconstruction.Stride) + (first / window) - half;
            correlation[first] = Correlate(
                degraded[firstOffset..],
                reconstruction.Stride,
                original[sourceOrigin..],
                source.Stride,
                source.Width,
                source.Height,
                average,
                rowStep) / divisor;

            for (int second = first; second < count; second++)
            {
                int secondOffset = degradedOrigin + (((second % window) - half) * reconstruction.Stride) + (second / window) - half;
                long value = Correlate(
                    degraded[firstOffset..],
                    reconstruction.Stride,
                    degraded[secondOffset..],
                    reconstruction.Stride,
                    source.Width,
                    source.Height,
                    average,
                    rowStep) / divisor;

                covariance[(first * count) + second] = value;
                covariance[(second * count) + first] = value;
            }
        }
    }

    /// <summary>
    /// Accumulates centered products over identically sized component regions.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <param name="first">The first region's first sample.</param>
    /// <param name="firstStride">The first region's row stride.</param>
    /// <param name="second">The second region's first sample.</param>
    /// <param name="secondStride">The second region's row stride.</param>
    /// <param name="width">The region width.</param>
    /// <param name="height">The region height.</param>
    /// <param name="average">The common reconstructed-sample average.</param>
    /// <param name="rowStep">The row sampling interval.</param>
    /// <returns>The weighted sum of centered products.</returns>
    private static long Correlate<TSample>(
        ReadOnlySpan<TSample> first,
        int firstStride,
        ReadOnlySpan<TSample> second,
        int secondStride,
        int width,
        int height,
        int average,
        int rowStep)
        where TSample : unmanaged
    {
        // Every sampled row stands for rowStep rows except a short final group, which stands for the rows left. The
        // full groups share one set of lane totals and the short group keeps its own, so each is weighted once.
        LaneTotals full = default;
        LaneTotals last = default;
        int lastRow = ((height - 1) / rowStep) * rowStep;
        int lastWeight = height - lastRow;
        for (int row = 0; row < height; row += rowStep)
        {
            ref LaneTotals totals = ref row == lastRow && lastWeight != rowStep ? ref last : ref full;
            AccumulateCorrelationRow<TSample, CorrelationOperator>(
                first.Slice(row * firstStride, width), second.Slice(row * secondStride, width), average, ref totals);
        }

        return (full.Sum() * rowStep) + (last.Sum() * lastWeight);
    }

    /// <summary>
    /// Adds the centered products of one row, walking the widest register first.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <typeparam name="TOperator">The correlation arithmetic.</typeparam>
    /// <param name="first">The first row.</param>
    /// <param name="second">The second row.</param>
    /// <param name="average">The common reconstructed-sample average.</param>
    /// <param name="totals">The lane totals of every register width.</param>
    private static void AccumulateCorrelationRow<TSample, TOperator>(
        ReadOnlySpan<TSample> first,
        ReadOnlySpan<TSample> second,
        int average,
        ref LaneTotals totals)
        where TSample : unmanaged
        where TOperator : struct, ICorrelationOperator
    {
        ref TSample firstBase = ref MemoryMarshal.GetReference(first);
        ref TSample secondBase = ref MemoryMarshal.GetReference(second);
        int width = first.Length;
        int column = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; column <= width - Vector512<short>.Count; column += Vector512<short>.Count)
            {
                totals.Lanes512 = TOperator.AccumulateProducts(
                    ref Unsafe.Add(ref firstBase, column), ref Unsafe.Add(ref secondBase, column), average, totals.Lanes512);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; column <= width - Vector256<short>.Count; column += Vector256<short>.Count)
            {
                totals.Lanes256 = TOperator.AccumulateProducts(
                    ref Unsafe.Add(ref firstBase, column), ref Unsafe.Add(ref secondBase, column), average, totals.Lanes256);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= width - Vector128<short>.Count; column += Vector128<short>.Count)
            {
                totals.Lanes128 = TOperator.AccumulateProducts(
                    ref Unsafe.Add(ref firstBase, column), ref Unsafe.Add(ref secondBase, column), average, totals.Lanes128);
            }
        }

        for (; column < width; column++)
        {
            totals.Scalar = TOperator.AccumulateProducts(
                ref Unsafe.Add(ref firstBase, column), ref Unsafe.Add(ref secondBase, column), average, totals.Scalar);
        }
    }

    /// <summary>
    /// Measures the fitted filter's quadratic score relative to the identity filter.
    /// </summary>
    /// <param name="window">The fitting window width.</param>
    /// <param name="correlation">The source correlations.</param>
    /// <param name="covariance">The neighborhood covariance matrix.</param>
    /// <param name="unit">The fitted transmitted Wiener taps.</param>
    /// <returns>A positive value when the fitted filter is worse than identity.</returns>
    private static long GetWienerScore(int window, ReadOnlySpan<long> correlation, ReadOnlySpan<long> covariance, Av1LoopRestorationUnit unit)
    {
        InlineArray8<int> verticalStorage = default;
        InlineArray8<int> horizontalStorage = default;
        InlineArray65<int> productStorage = default;
        Span<int> vertical = verticalStorage;
        Span<int> horizontal = horizontalStorage;
        Span<int> product = productStorage;
        vertical[3] = horizontal[3] = 128;
        for (int index = 0; index < 3; index++)
        {
            vertical[index] = vertical[6 - index] = unit.WienerVertical[index];
            horizontal[index] = horizontal[6 - index] = unit.WienerHorizontal[index];
            vertical[3] -= 2 * vertical[index];
            horizontal[3] -= 2 * horizontal[index];
        }

        int inset = (7 - window) >> 1;
        int count = window * window;
        for (int column = 0; column < window; column++)
        {
            for (int row = 0; row < window; row++)
            {
                product[(column * window) + row] = vertical[row + inset] * horizontal[column + inset];
            }
        }

        long linear = 0;
        long quadratic = 0;
        for (int first = 0; first < count; first++)
        {
            linear += product[first] * correlation[first] / 128 / 128;
            for (int second = 0; second < count; second++)
            {
                quadratic += product[first] * covariance[(first * count) + second] * product[second] / 128 / 128 / 128 / 128;
            }
        }

        int centerIndex = count >> 1;
        long identity = covariance[(centerIndex * count) + centerIndex] - (2 * correlation[centerIndex]);
        return quadratic - (2 * linear) - identity;
    }
}

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
        Buffer2DRegion<TSample> source,
        Buffer2DRegion<TSample> reconstruction,
        int window,
        int bitDepth,
        bool downsample,
        Span<long> correlation,
        Span<long> covariance)
        where TSample : unmanaged
    {
        long sum = 0;
        for (int row = 0; row < reconstruction.Height; row++)
        {
            ReadOnlySpan<TSample> samples = reconstruction.DangerousGetRowSpan(row);
            for (int column = 0; column < samples.Length; column++)
            {
                sum += Av1RestorationSampleOperations.Load(samples[column]);
            }
        }

        int average = (int)(sum / (reconstruction.Width * reconstruction.Height));
        ReadOnlySpan<TSample> original = source.Buffer.DangerousGetSingleSpan();
        ReadOnlySpan<TSample> degraded = reconstruction.Buffer.DangerousGetSingleSpan();
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
        ref TSample firstReference = ref MemoryMarshal.GetReference(first);
        ref TSample secondReference = ref MemoryMarshal.GetReference(second);
        long total = 0;
        for (int row = 0; row < height; row += rowStep)
        {
            int column = 0;
            long rowTotal = 0;
            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<long> lower = Vector256<long>.Zero;
                Vector256<long> upper = Vector256<long>.Zero;
                Vector256<int> center = Vector256.Create(average);
                for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
                {
                    Vector256<int> a = Av1RestorationSampleOperations.LoadToInt32(
                        ref Unsafe.Add(ref firstReference, (row * firstStride) + column), Vector256<int>.Zero) - center;

                    Vector256<int> b = Av1RestorationSampleOperations.LoadToInt32(
                        ref Unsafe.Add(ref secondReference, (row * secondStride) + column), Vector256<int>.Zero) - center;

                    // Each lane is one neighboring sample product. Twelve-bit centered products fit
                    // in Int32, but a full unit's sum does not; widen before accumulating across rows.
                    Vector256<int> product = a * b;
                    lower += Vector256.WidenLower(product);
                    upper += Vector256.WidenUpper(product);
                }

                rowTotal += Vector256.Sum(lower + upper);
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<long> lower = Vector128<long>.Zero;
                Vector128<long> upper = Vector128<long>.Zero;
                Vector128<int> center = Vector128.Create(average);
                for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    Vector128<int> a = Av1RestorationSampleOperations.LoadToInt32(
                        ref Unsafe.Add(ref firstReference, (row * firstStride) + column), Vector128<int>.Zero) - center;

                    Vector128<int> b = Av1RestorationSampleOperations.LoadToInt32(
                        ref Unsafe.Add(ref secondReference, (row * secondStride) + column), Vector128<int>.Zero) - center;

                    Vector128<int> product = a * b;
                    lower += Vector128.WidenLower(product);
                    upper += Vector128.WidenUpper(product);
                }

                rowTotal += Vector128.Sum(lower + upper);
            }

            // The scalar tail reads only visible samples. A short final sampling group receives its
            // actual remaining row count instead of the nominal factor of four.
            for (; column < width; column++)
            {
                int a = Av1RestorationSampleOperations.Load(Unsafe.Add(ref firstReference, (row * firstStride) + column)) - average;
                int b = Av1RestorationSampleOperations.Load(Unsafe.Add(ref secondReference, (row * secondStride) + column)) - average;
                rowTotal += (long)a * b;
            }

            total += rowTotal * Math.Min(rowStep, height - row);
        }

        return total;
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
        InlineArray8<int> vertical = default;
        InlineArray8<int> horizontal = default;
        InlineArray65<int> product = default;
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

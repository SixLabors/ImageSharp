// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <summary>
/// Selects frame restoration parameters from original and reconstructed component samples.
/// </summary>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// Finds the self-guided parameter set and refines its projection coefficients for one restoration unit.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <param name="source">The exact original unit region.</param>
    /// <param name="reconstruction">The corresponding reconstructed unit region.</param>
    /// <param name="bitDepth">The component precision.</param>
    /// <param name="processingWidth">The processing block width.</param>
    /// <param name="processingHeight">The processing block height.</param>
    /// <param name="pruning">The parameter-set pruning policy.</param>
    /// <param name="filtered0">The retained radius-two results.</param>
    /// <param name="filtered1">The retained radius-one results.</param>
    /// <param name="scratch">The shared processing-block workspace.</param>
    /// <returns>The selected self-guided parameters.</returns>
    private static Av1LoopRestorationUnit SearchSelfGuided<TSample>(
        Av1PlaneRegion<TSample> source,
        Av1PlaneRegion<TSample> reconstruction,
        int bitDepth,
        int processingWidth,
        int processingHeight,
        int pruning,
        Span<int> filtered0,
        Span<int> filtered1,
        Span<int> scratch)
        where TSample : unmanaged
    {
        Av1LoopRestorationUnit best = default;
        best.FilterType = Av1RestorationFilterType.SgrProjection;
        long bestError = long.MaxValue;
        ReadOnlySpan<int> seeds = [0, 3, 6, 9];
        ReadOnlySpan<int> radiusOneSets = [10, 10, 11, 11, 12, 12, 13, 13, 13, 13, -1, -1, -1, -1];
        ReadOnlySpan<int> radiusTwoSets = [14, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15, 15, 15, 15];
        int firstCount = pruning == 0 ? 16 : seeds.Length;
        for (int index = 0; index < firstCount; index++)
        {
            int parameterSet = pruning == 0 ? index : seeds[index];
            EvaluateParameterSet(
                source,
                reconstruction,
                bitDepth,
                processingWidth,
                processingHeight,
                parameterSet,
                filtered0,
                filtered1,
                scratch,
                ref bestError,
                ref best);
        }

        if (pruning == 1)
        {
            // Neighbors are chosen around the seed winner, while the single-radius groups use the
            // current winner. Preserve this order because a group winner changes the next lookup.
            int seedWinner = best.SgrParameterSet;
            for (int parameterSet = seedWinner - 1; parameterSet <= seedWinner + 1; parameterSet += 2)
            {
                if ((uint)parameterSet <= 9)
                {
                    EvaluateParameterSet(
                        source,
                        reconstruction,
                        bitDepth,
                        processingWidth,
                        processingHeight,
                        parameterSet,
                        filtered0,
                        filtered1,
                        scratch,
                        ref bestError,
                        ref best);
                }
            }

            EvaluateParameterSet(
                source,
                reconstruction,
                bitDepth,
                processingWidth,
                processingHeight,
                radiusOneSets[best.SgrParameterSet],
                filtered0,
                filtered1,
                scratch,
                ref bestError,
                ref best);

            EvaluateParameterSet(
                source,
                reconstruction,
                bitDepth,
                processingWidth,
                processingHeight,
                radiusTwoSets[best.SgrParameterSet],
                filtered0,
                filtered1,
                scratch,
                ref bestError,
                ref best);
        }

        return best;
    }

    /// <summary>
    /// Selects the projection traversal once for the parameter set's active radii.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    private static void EvaluateParameterSet<TSample>(
        Av1PlaneRegion<TSample> source,
        Av1PlaneRegion<TSample> reconstruction,
        int bitDepth,
        int processingWidth,
        int processingHeight,
        int parameterSet,
        Span<int> filtered0,
        Span<int> filtered1,
        Span<int> scratch,
        ref long bestError,
        ref Av1LoopRestorationUnit best)
        where TSample : unmanaged
    {
        // The first ten parameter sets use both radii, the next four use radius one,
        // and the final two use radius two. Closed generic traversal removes that
        // choice from the per-sample statistics and projection-error loops.
        if (parameterSet < 10)
        {
            EvaluateSelfGuided<TSample, DualRadiusProjection>(
                source,
                reconstruction,
                bitDepth,
                processingWidth,
                processingHeight,
                parameterSet,
                filtered0,
                filtered1,
                scratch,
                ref bestError,
                ref best);
        }
        else if (parameterSet < 14)
        {
            EvaluateSelfGuided<TSample, RadiusOneProjection>(
                source,
                reconstruction,
                bitDepth,
                processingWidth,
                processingHeight,
                parameterSet,
                filtered0,
                filtered1,
                scratch,
                ref bestError,
                ref best);
        }
        else
        {
            EvaluateSelfGuided<TSample, RadiusTwoProjection>(
                source,
                reconstruction,
                bitDepth,
                processingWidth,
                processingHeight,
                parameterSet,
                filtered0,
                filtered1,
                scratch,
                ref bestError,
                ref best);
        }
    }

    /// <summary>
    /// Generates one parameter set's fixed-point results, solves its projection, and refines the coded coefficients.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
    /// <param name="source">The original unit region.</param>
    /// <param name="reconstruction">The reconstructed unit region.</param>
    /// <param name="bitDepth">The component precision.</param>
    /// <param name="processingWidth">The processing block width.</param>
    /// <param name="processingHeight">The processing block height.</param>
    /// <param name="parameterSet">The parameter set to evaluate.</param>
    /// <param name="filtered0">The retained radius-two results.</param>
    /// <param name="filtered1">The retained radius-one results.</param>
    /// <param name="scratch">The shared processing-block workspace.</param>
    /// <param name="bestError">The lowest error found so far.</param>
    /// <param name="best">The parameters associated with that error.</param>
    private static void EvaluateSelfGuided<TSample, TProjection>(
        Av1PlaneRegion<TSample> source,
        Av1PlaneRegion<TSample> reconstruction,
        int bitDepth,
        int processingWidth,
        int processingHeight,
        int parameterSet,
        Span<int> filtered0,
        Span<int> filtered1,
        Span<int> scratch,
        ref long bestError,
        ref Av1LoopRestorationUnit best)
        where TSample : unmanaged
        where TProjection : struct, IProjectionOperator
    {
        ReadOnlySpan<int> radii = Av1SelfGuidedFilter.ParameterRadii.Slice(parameterSet * 2, 2);
        ReadOnlySpan<TSample> storage = reconstruction.Samples;
        ProjectionMoments moments = default;
        int offset = 0;
        for (int y = 0; y < source.Height; y += processingHeight)
        {
            int height = Math.Min(processingHeight, source.Height - y);
            for (int x = 0; x < source.Width; x += processingWidth)
            {
                int width = Math.Min(processingWidth, source.Width - x);
                int length = width * height;
                int origin = ((reconstruction.Bounds.Y + y - 3) * reconstruction.Stride) + reconstruction.Bounds.X + x - 3;
                Span<int> first = filtered0.Slice(offset, length);
                Span<int> second = filtered1.Slice(offset, length);
                Av1SelfGuidedFilter.GenerateFilters(
                    storage[origin..], reconstruction.Stride, width, height, bitDepth, parameterSet, first, second, scratch);

                // Retain packed processing blocks directly in the unit workspace. No repacking is needed:
                // coefficient refinement walks this same block order. All sums remain integer until the
                // complete unit has been accumulated, so block boundaries introduce no rounding.
                for (int row = 0; row < height; row++)
                {
                    AccumulateProjectionMoments<TSample, TProjection, ProjectionStatisticsOperator>(
                        source.GetRowSpan(y + row).Slice(x, width),
                        reconstruction.GetRowSpan(y + row).Slice(x, width),
                        first.Slice(row * width, width),
                        second.Slice(row * width, width),
                        ref moments);
                }

                offset += length;
            }
        }

        int area = source.Width * source.Height;
        long h00 = moments.Sum(0) / area;
        long h01 = moments.Sum(1) / area;
        long h11 = moments.Sum(2) / area;
        long c0 = moments.Sum(3) / area;
        long c1 = moments.Sum(4) / area;
        int projection0 = 0;
        int projection1 = 0;
        if (!TProjection.UsesRadiusTwo)
        {
            if (h11 != 0)
            {
                projection1 = (int)DivideRounded(c1 * 128, h11);
            }
        }
        else if (!TProjection.UsesRadiusOne)
        {
            if (h00 != 0)
            {
                projection0 = (int)DivideRounded(c0 * 128, h00);
            }
        }
        else
        {
            long determinant = (h00 * h11) - (h01 * h01);
            if (determinant != 0)
            {
                // Scale the divisor instead when scaling the numerator would overflow. Singular
                // systems retain zero projections; they occur naturally in flat reconstructed units.
                long numerator0 = (h11 * c0) - (h01 * c1);
                long numerator1 = (h00 * c1) - (h01 * c0);
                projection0 = (int)(numerator0 > long.MaxValue / 128 || numerator0 < long.MinValue / 128
                    ? DivideRounded(numerator0, determinant / 128)
                    : DivideRounded(numerator0 * 128, determinant));

                projection1 = (int)(numerator1 > long.MaxValue / 128 || numerator1 < long.MinValue / 128
                    ? DivideRounded(numerator1, determinant / 128)
                    : DivideRounded(numerator1 * 128, determinant));
            }
        }

        Av1LoopRestorationUnit candidate = default;
        candidate.FilterType = Av1RestorationFilterType.SgrProjection;
        candidate.SgrParameterSet = parameterSet;
        candidate.SgrProjectionCoefficients[0] = !TProjection.UsesRadiusTwo ? 0 : Math.Clamp(projection0, -96, 31);
        candidate.SgrProjectionCoefficients[1] = Math.Clamp(
            128 - candidate.SgrProjectionCoefficients[0] - (!TProjection.UsesRadiusOne ? 0 : projection1), -32, 95);

        long error = GetProjectionError<TSample, TProjection>(source, reconstruction, processingWidth, processingHeight, candidate, filtered0, filtered1);
        for (int step = 2; step >= 1; step >>= 1)
        {
            for (int coefficient = 0; coefficient < 2; coefficient++)
            {
                if (radii[coefficient] == 0)
                {
                    continue;
                }

                int minimum = coefficient == 0 ? -96 : -32;
                int maximum = coefficient == 0 ? 31 : 95;
                bool movedDown = false;
                while (candidate.SgrProjectionCoefficients[coefficient] - step >= minimum)
                {
                    candidate.SgrProjectionCoefficients[coefficient] -= step;
                    long nextError = GetProjectionError<TSample, TProjection>(
                        source, reconstruction, processingWidth, processingHeight, candidate, filtered0, filtered1);

                    if (nextError > error)
                    {
                        candidate.SgrProjectionCoefficients[coefficient] += step;
                        break;
                    }

                    error = nextError;
                    movedDown = true;
                    if (step != 2)
                    {
                        break;
                    }
                }

                if (movedDown)
                {
                    break;
                }

                while (candidate.SgrProjectionCoefficients[coefficient] + step <= maximum)
                {
                    candidate.SgrProjectionCoefficients[coefficient] += step;
                    long nextError = GetProjectionError<TSample, TProjection>(
                        source, reconstruction, processingWidth, processingHeight, candidate, filtered0, filtered1);

                    if (nextError > error)
                    {
                        candidate.SgrProjectionCoefficients[coefficient] -= step;
                        break;
                    }

                    error = nextError;
                    if (step != 2)
                    {
                        break;
                    }
                }
            }
        }

        if (error < bestError)
        {
            bestError = error;
            best = candidate;
        }
    }

    /// <summary>
    /// Measures projection error before sample clipping, using the retained processing-block layout.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
    /// <param name="source">The original unit region.</param>
    /// <param name="reconstruction">The reconstructed unit region.</param>
    /// <param name="processingWidth">The processing block width.</param>
    /// <param name="processingHeight">The processing block height.</param>
    /// <param name="candidate">The coded projection coefficients.</param>
    /// <param name="filtered0">The radius-two results.</param>
    /// <param name="filtered1">The radius-one results.</param>
    /// <returns>The sum of squared component errors.</returns>
    private static long GetProjectionError<TSample, TProjection>(
        Av1PlaneRegion<TSample> source,
        Av1PlaneRegion<TSample> reconstruction,
        int processingWidth,
        int processingHeight,
        Av1LoopRestorationUnit candidate,
        ReadOnlySpan<int> filtered0,
        ReadOnlySpan<int> filtered1)
        where TSample : unmanaged
        where TProjection : struct, IProjectionOperator
    {
        int firstWeight = !TProjection.UsesRadiusTwo ? 0 : candidate.SgrProjectionCoefficients[0];
        int secondWeight = !TProjection.UsesRadiusOne ? 0 : 128 - firstWeight - candidate.SgrProjectionCoefficients[1];
        ProjectionWeights weights = new(firstWeight, secondWeight);
        LaneTotals totals = default;
        int offset = 0;
        for (int y = 0; y < source.Height; y += processingHeight)
        {
            int height = Math.Min(processingHeight, source.Height - y);
            for (int x = 0; x < source.Width; x += processingWidth)
            {
                int width = Math.Min(processingWidth, source.Width - x);
                for (int row = 0; row < height; row++, offset += width)
                {
                    AccumulateProjectionError<TSample, TProjection, ProjectionStatisticsOperator>(
                        source.GetRowSpan(y + row).Slice(x, width),
                        reconstruction.GetRowSpan(y + row).Slice(x, width),
                        filtered0.Slice(offset, width),
                        filtered1.Slice(offset, width),
                        weights,
                        ref totals);
                }
            }
        }

        return totals.Sum();
    }

    /// <summary>
    /// Adds the projection moments of one row, walking the widest register first.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
    /// <typeparam name="TOperator">The statistics arithmetic.</typeparam>
    /// <param name="original">The original samples.</param>
    /// <param name="reconstructed">The reconstructed samples.</param>
    /// <param name="first">The radius-two filtered values.</param>
    /// <param name="second">The radius-one filtered values.</param>
    /// <param name="moments">The lane totals of every register width.</param>
    private static void AccumulateProjectionMoments<TSample, TProjection, TOperator>(
        ReadOnlySpan<TSample> original,
        ReadOnlySpan<TSample> reconstructed,
        ReadOnlySpan<int> first,
        ReadOnlySpan<int> second,
        ref ProjectionMoments moments)
        where TSample : unmanaged
        where TProjection : struct, IProjectionOperator
        where TOperator : struct, IProjectionStatisticsOperator
    {
        ref TSample originalBase = ref MemoryMarshal.GetReference(original);
        ref TSample reconstructedBase = ref MemoryMarshal.GetReference(reconstructed);
        ref int firstBase = ref MemoryMarshal.GetReference(first);
        ref int secondBase = ref MemoryMarshal.GetReference(second);
        int width = original.Length;
        int column = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
            {
                TOperator.AccumulateMoments<TSample, TProjection>(
                    ref Unsafe.Add(ref originalBase, column),
                    ref Unsafe.Add(ref reconstructedBase, column),
                    ref Unsafe.Add(ref firstBase, column),
                    ref Unsafe.Add(ref secondBase, column),
                    ref moments.Lanes512);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
            {
                TOperator.AccumulateMoments<TSample, TProjection>(
                    ref Unsafe.Add(ref originalBase, column),
                    ref Unsafe.Add(ref reconstructedBase, column),
                    ref Unsafe.Add(ref firstBase, column),
                    ref Unsafe.Add(ref secondBase, column),
                    ref moments.Lanes256);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
            {
                TOperator.AccumulateMoments<TSample, TProjection>(
                    ref Unsafe.Add(ref originalBase, column),
                    ref Unsafe.Add(ref reconstructedBase, column),
                    ref Unsafe.Add(ref firstBase, column),
                    ref Unsafe.Add(ref secondBase, column),
                    ref moments.Lanes128);
            }
        }

        for (; column < width; column++)
        {
            TOperator.AccumulateMoments<TSample, TProjection>(
                ref Unsafe.Add(ref originalBase, column),
                ref Unsafe.Add(ref reconstructedBase, column),
                ref Unsafe.Add(ref firstBase, column),
                ref Unsafe.Add(ref secondBase, column),
                ref moments.Scalar);
        }
    }

    /// <summary>
    /// Adds the squared projection errors of one row, walking the widest register first.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
    /// <typeparam name="TOperator">The statistics arithmetic.</typeparam>
    /// <param name="original">The original samples.</param>
    /// <param name="reconstructed">The reconstructed samples.</param>
    /// <param name="first">The radius-two filtered values.</param>
    /// <param name="second">The radius-one filtered values.</param>
    /// <param name="weights">The decoded projection weights.</param>
    /// <param name="totals">The lane totals of every register width.</param>
    private static void AccumulateProjectionError<TSample, TProjection, TOperator>(
        ReadOnlySpan<TSample> original,
        ReadOnlySpan<TSample> reconstructed,
        ReadOnlySpan<int> first,
        ReadOnlySpan<int> second,
        ProjectionWeights weights,
        ref LaneTotals totals)
        where TSample : unmanaged
        where TProjection : struct, IProjectionOperator
        where TOperator : struct, IProjectionStatisticsOperator
    {
        ref TSample originalBase = ref MemoryMarshal.GetReference(original);
        ref TSample reconstructedBase = ref MemoryMarshal.GetReference(reconstructed);
        ref int firstBase = ref MemoryMarshal.GetReference(first);
        ref int secondBase = ref MemoryMarshal.GetReference(second);
        int width = original.Length;
        int column = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
            {
                totals.Lanes512 = TOperator.AccumulateError<TSample, TProjection>(
                    ref Unsafe.Add(ref originalBase, column),
                    ref Unsafe.Add(ref reconstructedBase, column),
                    ref Unsafe.Add(ref firstBase, column),
                    ref Unsafe.Add(ref secondBase, column),
                    weights,
                    totals.Lanes512);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
            {
                totals.Lanes256 = TOperator.AccumulateError<TSample, TProjection>(
                    ref Unsafe.Add(ref originalBase, column),
                    ref Unsafe.Add(ref reconstructedBase, column),
                    ref Unsafe.Add(ref firstBase, column),
                    ref Unsafe.Add(ref secondBase, column),
                    weights,
                    totals.Lanes256);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
            {
                totals.Lanes128 = TOperator.AccumulateError<TSample, TProjection>(
                    ref Unsafe.Add(ref originalBase, column),
                    ref Unsafe.Add(ref reconstructedBase, column),
                    ref Unsafe.Add(ref firstBase, column),
                    ref Unsafe.Add(ref secondBase, column),
                    weights,
                    totals.Lanes128);
            }
        }

        for (; column < width; column++)
        {
            totals.Scalar = TOperator.AccumulateError<TSample, TProjection>(
                ref Unsafe.Add(ref originalBase, column),
                ref Unsafe.Add(ref reconstructedBase, column),
                ref Unsafe.Add(ref firstBase, column),
                ref Unsafe.Add(ref secondBase, column),
                weights,
                totals.Scalar);
        }
    }

    /// <summary>
    /// Divides signed fixed-point values with half-way cases rounded away from zero.
    /// </summary>
    /// <param name="numerator">The signed dividend.</param>
    /// <param name="denominator">The nonzero divisor.</param>
    /// <returns>The rounded quotient.</returns>
    private static long DivideRounded(long numerator, long denominator)
        => (numerator < 0 ? numerator - (denominator / 2) : numerator + (denominator / 2)) / denominator;
}

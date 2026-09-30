// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Selects unit filters while retaining independent fixed-mode and switchable coefficient histories.
/// </content>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// Searches the enabled filters for one unit and accumulates each frame mode's rate and error.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <param name="context">The active plane and reusable trial workspace.</param>
    /// <param name="bounds">The exact stripe-adjusted unit rectangle.</param>
    /// <param name="settings">The frame search policy.</param>
    /// <param name="writer">The shared coefficient syntax calculator.</param>
    /// <param name="costs">The retained frame restoration symbol costs.</param>
    /// <param name="rateMultiplier">The frame rate weight.</param>
    /// <param name="varianceThreshold">The minimum source variance for Wiener fitting.</param>
    /// <param name="allowSwitchable">Whether the plane can select switchable restoration.</param>
    /// <param name="correlation">The shared Wiener source-correlation workspace.</param>
    /// <param name="covariance">The shared Wiener covariance workspace.</param>
    /// <param name="filtered0">The retained radius-two projection results.</param>
    /// <param name="filtered1">The retained radius-one projection results.</param>
    /// <param name="fixedReference">The preceding coefficients for the two fixed frame modes.</param>
    /// <param name="switchableReference">The preceding coefficients for the switchable frame mode.</param>
    /// <param name="totalBits">The accumulated probability costs in none, Wiener, self-guided, switchable order.</param>
    /// <param name="totalErrors">The accumulated sample errors in the same mode order.</param>
    /// <returns>The candidate parameters and each frame mode's chosen unit filter.</returns>
    private static UnitSearchResult SearchUnit<TSample>(
        in UnitSearchContext<TSample> context,
        Rectangle bounds,
        SearchSettings settings,
        Av1SymbolEncoder writer,
        Av1ModeCosts costs,
        int rateMultiplier,
        long varianceThreshold,
        bool allowSwitchable,
        Span<long> correlation,
        Span<long> covariance,
        Span<int> filtered0,
        Span<int> filtered1,
        ref Av1LoopRestorationUnit fixedReference,
        ref Av1LoopRestorationUnit switchableReference,
        Span<long> totalBits,
        Span<long> totalErrors)
        where TSample : unmanaged
    {
        UnitSearchResult result = default;
        long unfilteredError = context.MeasureError(bounds, context.Reconstruction);
        long wienerError = long.MaxValue;
        long selfGuidedError = long.MaxValue;
        totalErrors[0] += unfilteredError;
        bool skipSelfGuided = false;
        Av1PlaneRegion<TSample> original = context.Source.GetSubRegion(bounds);
        Av1PlaneRegion<TSample> degraded = context.Reconstruction.GetSubRegion(bounds);
        bool chroma = context.Plane != 0;

        if (settings.EnableWiener)
        {
            long noneBits = costs.GetWienerRestoration(0);
            long selectedBits = noneBits;
            long selectedError = unfilteredError;
            bool pruneWiener = false;
            if (settings.WienerVariancePruning != 0)
            {
                GetSampleMoments(original, out long sum, out long squares);
                int area = original.Width * original.Height;

                // Compute population variance at native sample precision. The integer divisions
                // occur after the complete unit sum, so neither row nor block boundaries round it.
                long variance = (squares - ((sum * sum) / area)) / area;
                pruneWiener = variance < varianceThreshold || unfilteredError == 0;
            }

            int window = chroma ? 5 : settings.WienerWindow;
            if (!pruneWiener)
            {
                ComputeWienerStatistics(
                    original, degraded, window, context.BitDepth, settings.DownsampleWienerStatistics, correlation, covariance);

                result.Parameters = FitWiener(window, correlation, covariance);
                pruneWiener = GetWienerScore(window, correlation, covariance, result.Parameters) > 0;
            }

            if (pruneWiener)
            {
                skipSelfGuided = settings.SelfGuidedWienerPruning == 2;
            }
            else
            {
                wienerError = RefineWiener(in context, bounds, window, settings.RefineWiener, ref result.Parameters);
                long bits = costs.GetWienerRestoration(1) +
                    writer.GetRestorationCoefficientCost(result.Parameters, fixedReference, chroma);

                double noneCost = GetRestorationCost(rateMultiplier, noneBits, unfilteredError, context.BitDepth);
                double cost = GetRestorationCost(rateMultiplier, bits, wienerError, context.BitDepth);
                if (cost < noneCost)
                {
                    result.Choices[0] = Av1RestorationFilterType.Wiener;
                    selectedBits = bits;
                    selectedError = wienerError;
                    fixedReference.WienerVertical = result.Parameters.WienerVertical;
                    fixedReference.WienerHorizontal = result.Parameters.WienerHorizontal;
                }

                if (settings.SelfGuidedWienerPruning == 1)
                {
                    skipSelfGuided = cost > 1.01 * noneCost;
                }
                else if (settings.SelfGuidedWienerPruning == 2)
                {
                    skipSelfGuided = result.Choices[0] == Av1RestorationFilterType.None;
                }
            }

            totalBits[1] += selectedBits;
            totalErrors[1] += selectedError;
        }

        if (settings.EnableSelfGuided)
        {
            long noneBits = costs.GetSgrProjectionRestoration(0);
            long selectedBits = noneBits;
            long selectedError = unfilteredError;
            if (!skipSelfGuided)
            {
                Av1LoopRestorationUnit selfGuided = SearchSelfGuided(
                    original,
                    degraded,
                    context.BitDepth,
                    Av1LoopRestorationBoundary.ProcessingStripeSize >> context.SubsamplingX,
                    Av1LoopRestorationBoundary.ProcessingStripeSize >> context.SubsamplingY,
                    settings.SelfGuidedPruning,
                    filtered0,
                    filtered1,
                    context.SelfGuidedScratch);

                result.Parameters.SgrParameterSet = selfGuided.SgrParameterSet;
                result.Parameters.SgrProjectionCoefficients = selfGuided.SgrProjectionCoefficients;
                selfGuidedError = context.MeasureCandidate(bounds, selfGuided);
                long bits = costs.GetSgrProjectionRestoration(1) +
                    writer.GetRestorationCoefficientCost(selfGuided, fixedReference, chroma);

                double noneCost = GetRestorationCost(rateMultiplier, noneBits, unfilteredError, context.BitDepth);
                double cost = GetRestorationCost(rateMultiplier, bits, selfGuidedError, context.BitDepth);
                if (selfGuided.SgrParameterSet < 10)
                {
                    cost *= settings.DualSelfGuidedPenalty;
                }

                if (cost < noneCost)
                {
                    result.Choices[1] = Av1RestorationFilterType.SgrProjection;
                    selectedBits = bits;
                    selectedError = selfGuidedError;
                    fixedReference.SgrParameterSet = selfGuided.SgrParameterSet;
                    fixedReference.SgrProjectionCoefficients = selfGuided.SgrProjectionCoefficients;
                }
            }

            totalBits[2] += selectedBits;
            totalErrors[2] += selectedError;
        }

        if (allowSwitchable)
        {
            long selectedBits = costs.GetSwitchableRestoration(0);
            long selectedError = unfilteredError;
            double bestCost = GetRestorationCost(rateMultiplier, selectedBits, selectedError, context.BitDepth);
            for (int filter = 1; filter <= 2; filter++)
            {
                long error = filter == 1 ? wienerError : selfGuidedError;

                // A fixed frame mode can reject a useful filter because of its signaling cost.
                // Switchable mode has different costs and its own history, so retain candidates
                // with competitive distortion even when their fixed-mode decision was none.
                if (error > unfilteredError)
                {
                    continue;
                }

                Av1LoopRestorationUnit candidate = result.Parameters;
                candidate.FilterType = (Av1RestorationFilterType)filter;
                long bits = costs.GetSwitchableRestoration(filter) +
                    writer.GetRestorationCoefficientCost(candidate, switchableReference, chroma);

                double cost = GetRestorationCost(rateMultiplier, bits, error, context.BitDepth);
                if (filter == 2 && candidate.SgrParameterSet < 10)
                {
                    cost *= settings.DualSelfGuidedPenalty;
                }

                if (cost < bestCost)
                {
                    bestCost = cost;
                    selectedBits = bits;
                    selectedError = error;
                    result.Choices[2] = candidate.FilterType;
                }
            }

            if (result.Choices[2] == Av1RestorationFilterType.Wiener)
            {
                switchableReference.WienerVertical = result.Parameters.WienerVertical;
                switchableReference.WienerHorizontal = result.Parameters.WienerHorizontal;
            }
            else if (result.Choices[2] == Av1RestorationFilterType.SgrProjection)
            {
                switchableReference.SgrParameterSet = result.Parameters.SgrParameterSet;
                switchableReference.SgrProjectionCoefficients = result.Parameters.SgrProjectionCoefficients;
            }

            totalBits[3] += selectedBits;
            totalErrors[3] += selectedError;
        }

        return result;
    }

    /// <summary>
    /// Sums the samples of a unit and their squares. Reference: aom_var_2d_u8() and aom_var_2d_u16() as
    /// var_restoration_unit() calls them.
    /// </summary>
    /// <remarks>
    /// The residual moments against a row of zeros are the sample moments. A restoration unit spans at most 383x391
    /// samples, so the twelve-bit sample sum stays inside its 32-bit result.
    /// </remarks>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <param name="region">The unit samples.</param>
    /// <param name="sum">Receives the sample sum.</param>
    /// <param name="squares">Receives the sum of the squared samples.</param>
    private static void GetSampleMoments<TSample>(Av1PlaneRegion<TSample> region, out long sum, out long squares)
        where TSample : unmanaged
    {
        int offset = region.GetOffset(0, 0);
        int moment;
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            Span<byte> zeros = stackalloc byte[region.Width];
            zeros.Clear();
            Av1ResidualBuilder.GetMoments(
                MemoryMarshal.Cast<TSample, byte>(region.Samples)[offset..], region.Stride, zeros, 0, region.Width, region.Height, out moment, out squares);
        }
        else
        {
            Span<ushort> zeros = stackalloc ushort[region.Width];
            zeros.Clear();
            Av1ResidualBuilder.GetMoments(
                MemoryMarshal.Cast<TSample, ushort>(region.Samples)[offset..], region.Stride, zeros, 0, region.Width, region.Height, out moment, out squares);
        }

        sum = moment;
    }

    /// <summary>
    /// Combines restoration rates and native-depth sample distortion without rounding the rate term.
    /// </summary>
    /// <param name="rateMultiplier">The frame rate weight.</param>
    /// <param name="bits">The probability-cost rate before restoration's four-bit scale reduction.</param>
    /// <param name="error">The squared error at native precision.</param>
    /// <param name="bitDepth">The native sample precision.</param>
    /// <returns>The restoration rate-distortion cost.</returns>
    private static double GetRestorationCost(int rateMultiplier, long bits, long error, int bitDepth)
        => ((double)(bits >> 4) * rateMultiplier / (1 << Av1ProbabilityCost.CostShift)) +
            ((double)(error >> (2 * (bitDepth - 8))) * 128);

    /// <summary>
    /// Retains both fitted filters and the selected unit type for each enabled frame mode.
    /// </summary>
    private struct UnitSearchResult
    {
        /// <summary>
        /// The Wiener taps and self-guided projection parameters.
        /// </summary>
        public Av1LoopRestorationUnit Parameters;

        /// <summary>
        /// The chosen unit types for Wiener, self-guided, and switchable frame modes.
        /// </summary>
        public InlineArray3<Av1RestorationFilterType> Choices;
    }
}

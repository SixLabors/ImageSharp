// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// The flash, noise and correlation estimates of the look-ahead and its division into stable, varying, blending
/// and scene cut regions.
/// </content>
internal sealed partial class Av1SecondPass
{
    /// <summary>
    /// The length of the smoothing filter. Reference: SMOOTH_FILT_LEN.
    /// </summary>
    private const int SmoothFilterLength = 7;

    /// <summary>
    /// Reference: HALF_FILT_LEN.
    /// </summary>
    private const int HalfFilterLength = SmoothFilterLength / 2;

    /// <summary>
    /// The window of the region statistics. Reference: WINDOW_SIZE.
    /// </summary>
    private const int WindowSize = 7;

    /// <summary>
    /// Reference: HALF_WIN.
    /// </summary>
    private const int HalfWindow = WindowSize / 2;

    /// <summary>
    /// The capacity of the region lists, with room for the regions that insertions add.
    /// </summary>
    private const int RegionCapacity = (2 * MaximumAnalysisFrames) + 2;

    /// <summary>
    /// The regions of the latest analysis. Reference: regions of PRIMARY_RATE_CONTROL.
    /// </summary>
    private readonly Region[] regions = new Region[RegionCapacity];

    /// <summary>
    /// The regions of one scene of an analysis. Reference: temp_regions of identify_regions().
    /// </summary>
    private readonly Region[] temporaryRegions = new Region[RegionCapacity];

    /// <summary>
    /// Reference: filt_intra_err of identify_regions().
    /// </summary>
    private readonly double[] filteredIntraError = new double[MaximumAnalysisFrames];

    /// <summary>
    /// Reference: filt_coded_err of identify_regions().
    /// </summary>
    private readonly double[] filteredCodedError = new double[MaximumAnalysisFrames];

    /// <summary>
    /// Reference: grad_coded of identify_regions().
    /// </summary>
    private readonly double[] codedGradient = new double[MaximumAnalysisFrames];

    /// <summary>
    /// The number of regions. Reference: num_regions.
    /// </summary>
    private int regionCount;

    /// <summary>
    /// The frames the latest analysis covered. Reference: frames_till_regions_update.
    /// </summary>
    private int framesTillRegionsUpdate;

    /// <summary>
    /// The kinds of region. Reference: REGION_TYPES.
    /// </summary>
    private enum RegionType
    {
        /// <summary>Reference: STABLE_REGION.</summary>
        Stable = 0,

        /// <summary>Reference: HIGH_VAR_REGION.</summary>
        HighVariance = 1,

        /// <summary>Reference: SCENECUT_REGION.</summary>
        SceneCut = 2,

        /// <summary>Reference: BLENDING_REGION.</summary>
        Blending = 3
    }

    /// <summary>
    /// Gets the coefficients of the Gaussian smoothing filter. Reference: smooth_filt of smooth_filter_stats().
    /// </summary>
    private static ReadOnlySpan<double> SmoothFilter => [0.006, 0.061, 0.242, 0.383, 0.242, 0.061, 0.006];

    /// <summary>
    /// Marks each frame of the linear buffer whose next frame predicts better from the second reference, which
    /// indicates a flash. The last frame is never a flash. Reference: mark_flashes().
    /// </summary>
    private void MarkFlashes()
    {
        Span<Av1FirstPassStatistics> frames = this.statistics.AsSpan(0, this.statisticsCount);
        for (int i = 0; i < frames.Length - 1; i++)
        {
            ref readonly Av1FirstPassStatistics next = ref frames[i + 1];
            frames[i].IsFlash = next.PercentSecondReference > next.PercentInter && next.PercentSecondReference >= 0.5 ? 1 : 0;
        }

        if (frames.Length > 0)
        {
            frames[^1].IsFlash = 0;
        }
    }

    /// <summary>
    /// Estimates the noise variance of each frame of the linear buffer from the intra and inter errors of it and
    /// the two frames before it, fills frames without a trustworthy estimate from their neighbours, and smooths the
    /// result. Reference: estimate_noise() with smooth_filter_noise().
    /// </summary>
    private void EstimateNoise()
    {
        Span<Av1FirstPassStatistics> frames = this.statistics.AsSpan(0, this.statisticsCount);
        int last = frames.Length;
        for (int i = 2; i < last; i++)
        {
            frames[i].NoiseVariance = 0.0;

            // Flashes have highly correlated innovations, so they are skipped.
            if (IsNearFlash(frames, i))
            {
                continue;
            }

            double c1 = frames[i - 1].IntraError * (frames[i].IntraError - frames[i].CodedError);
            double c2 = frames[i - 2].IntraError * (frames[i - 1].IntraError - frames[i - 1].CodedError);
            double c3 = frames[i - 2].IntraError * (frames[i].IntraError - frames[i].SecondReferenceCodedError);
            if (c1 <= 0 || c2 <= 0 || c3 <= 0)
            {
                continue;
            }

            c1 = Math.Sqrt(c1);
            c2 = Math.Sqrt(c2);
            c3 = Math.Sqrt(c3);
            double noise = frames[i - 1].IntraError - (c1 * c2 / c3);
            frames[i].NoiseVariance = Math.Max(noise, 0.01);
        }

        // Copy the noise from a neighbour when the estimate is not trustworthy, the next frames first.
        for (int i = 2; i < last; i++)
        {
            if (IsNearFlash(frames, i) || frames[i].NoiseVariance >= 1.0)
            {
                continue;
            }

            bool found = false;
            for (int next = i + 1; next < last; next++)
            {
                if (IsNearFlash(frames, next) || frames[next].NoiseVariance < 1.0)
                {
                    continue;
                }

                found = true;
                frames[i].NoiseVariance = frames[next].NoiseVariance;
                break;
            }

            if (found)
            {
                continue;
            }

            for (int previous = i - 1; previous >= 2; previous--)
            {
                if (IsNearFlash(frames, previous) || frames[previous].NoiseVariance < 1.0)
                {
                    continue;
                }

                frames[i].NoiseVariance = frames[previous].NoiseVariance;
                break;
            }
        }

        // A flash takes the noise of the nearest frame that is not near a flash.
        for (int i = 2; i < last; i++)
        {
            if (!IsNearFlash(frames, i))
            {
                continue;
            }

            bool found = false;
            for (int next = i + 1; next < last; next++)
            {
                if (IsNearFlash(frames, next))
                {
                    continue;
                }

                found = true;
                frames[i].NoiseVariance = frames[next].NoiseVariance;
                break;
            }

            if (found)
            {
                continue;
            }

            for (int previous = i - 1; previous >= 2; previous--)
            {
                if (IsNearFlash(frames, previous))
                {
                    continue;
                }

                frames[i].NoiseVariance = frames[previous].NoiseVariance;
                break;
            }
        }

        // The first two frames take the noise of the third.
        for (int i = 0; i < 2 && last > 2; i++)
        {
            frames[i].NoiseVariance = frames[2].NoiseVariance;
        }

        // smooth_filter_noise(): a seven frame average that skips flashes.
        Span<double> smoothNoise = stackalloc double[frames.Length];
        for (int i = 0; i < last; i++)
        {
            double totalNoise = 0;
            double totalWeight = 0;
            for (int j = -HalfFilterLength; j <= HalfFilterLength; j++)
            {
                int index = Math.Clamp(i + j, 0, last - 1);
                if (frames[index].IsFlash != 0)
                {
                    continue;
                }

                totalNoise += frames[index].NoiseVariance;
                totalWeight += 1.0;
            }

            smoothNoise[i] = totalWeight > 0.01 ? totalNoise / totalWeight : frames[i].NoiseVariance;
        }

        for (int i = 0; i < last; i++)
        {
            frames[i].NoiseVariance = smoothNoise[i];
        }
    }

    /// <summary>
    /// Returns whether a frame or one of the two frames before it is a flash. Reference: the flash test of
    /// estimate_noise().
    /// </summary>
    /// <param name="frames">The linear buffer.</param>
    /// <param name="index">The frame, at least 2.</param>
    /// <returns>Whether the frame is near a flash.</returns>
    private static bool IsNearFlash(ReadOnlySpan<Av1FirstPassStatistics> frames, int index)
        => frames[index].IsFlash != 0 || frames[index - 1].IsFlash != 0 || frames[index - 2].IsFlash != 0;

    /// <summary>
    /// Estimates the correlation coefficient of each frame of the linear buffer with the frame before it, after
    /// removing the noise. The first frame has a coefficient of 1. Reference: estimate_coeff().
    /// </summary>
    private void EstimateCoefficients()
    {
        Span<Av1FirstPassStatistics> frames = this.statistics.AsSpan(0, this.statisticsCount);
        for (int i = 1; i < frames.Length; i++)
        {
            ref Av1FirstPassStatistics frame = ref frames[i];
            double previousIntraError = frames[i - 1].IntraError;
            double c = Math.Sqrt(Math.Max(previousIntraError * (frame.IntraError - frame.CodedError), 0.001));
            double coefficient = c / Math.Max(previousIntraError - frame.NoiseVariance, 0.001);
            frame.CorrelationCoefficient = coefficient *
                Math.Sqrt(Math.Max(previousIntraError - frame.NoiseVariance, 0.001) / Math.Max(frame.IntraError - frame.NoiseVariance, 0.001));

            frame.CorrelationCoefficient = Math.Clamp(frame.CorrelationCoefficient, 0.0, 1.0);
        }

        if (frames.Length > 0)
        {
            frames[0].CorrelationCoefficient = 1.0;
        }
    }

    /// <summary>
    /// Returns the region that holds a frame, or -1. Reference: find_regions_index().
    /// </summary>
    /// <param name="frameIndex">The frame index in region coordinates.</param>
    /// <returns>The region index.</returns>
    private int FindRegionsIndex(int frameIndex)
    {
        for (int k = 0; k < this.regionCount; k++)
        {
            if (this.regions[k].Start <= frameIndex && this.regions[k].Last >= frameIndex)
            {
                return k;
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns a region, or an empty stable region for an index outside the list. libaom reads the memory before
    /// the list for index -1 in comparisons whose result does not matter there.
    /// </summary>
    /// <param name="index">The region index.</param>
    /// <returns>The region.</returns>
    private ref readonly Region GetRegion(int index)
    {
        if ((uint)index < RegionCapacity)
        {
            return ref this.regions[index];
        }

        return ref Region.Empty;
    }

    /// <summary>
    /// Divides the look-ahead from a buffer position into regions: scene cuts first, then stable and highly varying
    /// regions from the smoothed errors, then blending regions such as fades. Region indices start at the buffer
    /// position. Reference: identify_regions() with an offset of 0.
    /// </summary>
    /// <param name="origin">The buffer position of region index 0.</param>
    /// <param name="totalFrames">The number of frames to analyse.</param>
    private void IdentifyRegions(int origin, int totalFrames)
    {
        // Fewer than two frames keep the previous regions.
        if (totalFrames <= 1)
        {
            return;
        }

        ReadOnlySpan<Av1FirstPassStatistics> frames = this.statistics.AsSpan(origin, totalFrames);
        Array.Clear(this.temporaryRegions);
        Array.Clear(this.filteredIntraError);
        Array.Clear(this.filteredCodedError);
        Array.Clear(this.codedGradient);

        int currentRegion = 0;
        int thisStart = 0;
        int nextSceneCut;
        do
        {
            // The obvious scene cuts first.
            nextSceneCut = FindNextSceneCut(frames, thisStart, totalFrames - 1);
            int thisLast = nextSceneCut >= 0 ? nextSceneCut - 1 : totalFrames - 1;

            // Low-pass filter the errors of the scene.
            this.SmoothFilterStatistics(frames, thisStart, thisLast);
            GetGradient(this.filteredCodedError, thisStart, thisLast, this.codedGradient);

            // Tentative stable and unstable regions.
            int count = FindStableRegions(frames, this.codedGradient, thisStart, thisLast, this.temporaryRegions);
            AdjustUnstableRegionBounds(frames, this.temporaryRegions, ref count);
            GetRegionStatistics(frames, this.temporaryRegions, count);

            // Blending regions among the unstable regions.
            FindBlendingRegions(frames, this.temporaryRegions, ref count);
            CleanupBlendings(this.temporaryRegions, ref count);

            // Every flash inside a stable region is a highly varying point.
            int k = 0;
            while (k < count)
            {
                if (this.temporaryRegions[k].Type != RegionType.Stable)
                {
                    k++;
                    continue;
                }

                int start = this.temporaryRegions[k].Start;
                int last = this.temporaryRegions[k].Last;
                for (int i = start; i <= last; i++)
                {
                    if (frames[i].IsFlash != 0)
                    {
                        InsertRegion(i, i, RegionType.HighVariance, this.temporaryRegions, ref count, ref k);
                    }
                }

                k++;
            }

            CleanupRegions(this.temporaryRegions, ref count);

            // Copy the regions of the scene.
            for (k = 0; k < count; k++)
            {
                if (this.temporaryRegions[k].Last < this.temporaryRegions[k].Start && k == count - 1)
                {
                    count--;
                    break;
                }

                this.regions[k + currentRegion] = this.temporaryRegions[k];
            }

            currentRegion += count;

            // Add the scene cut region and continue after it.
            if (nextSceneCut > -1)
            {
                this.regions[currentRegion].Type = RegionType.SceneCut;
                this.regions[currentRegion].Start = nextSceneCut;
                this.regions[currentRegion].Last = nextSceneCut;
                currentRegion++;
                thisStart = nextSceneCut + 1;
            }
        }
        while (nextSceneCut >= 0);

        int totalRegions = currentRegion;
        GetRegionStatistics(frames, this.regions, totalRegions);

        // A very minor scene cut is a highly varying region.
        for (int k = 0; k < totalRegions; k++)
        {
            ref Region region = ref this.regions[k];
            if (region.Type != RegionType.SceneCut ||
                region.AverageCorrelationCoefficient * (1 - (frames[region.Start].NoiseVariance / region.AverageIntraError)) < 0.8)
            {
                continue;
            }

            region.Type = RegionType.HighVariance;
        }

        CleanupRegions(this.regions, ref totalRegions);
        GetRegionStatistics(frames, this.regions, totalRegions);
        this.regionCount = totalRegions;
    }

    /// <summary>
    /// Smooths the intra and coded errors of a scene with a seven tap Gaussian that skips flashes; the coded error
    /// also skips the frame after a flash. Reference: smooth_filter_stats().
    /// </summary>
    /// <param name="frames">The statistics from region index 0.</param>
    /// <param name="start">The first frame.</param>
    /// <param name="last">The last frame.</param>
    private void SmoothFilterStatistics(ReadOnlySpan<Av1FirstPassStatistics> frames, int start, int last)
    {
        ReadOnlySpan<double> filter = SmoothFilter;
        for (int i = start; i <= last; i++)
        {
            double totalWeight = 0;
            for (int j = -HalfFilterLength; j <= HalfFilterLength; j++)
            {
                int index = Math.Clamp(i + j, start, last);
                if (frames[index].IsFlash != 0)
                {
                    continue;
                }

                this.filteredIntraError[i] += filter[j + HalfFilterLength] * frames[index].IntraError;
                totalWeight += filter[j + HalfFilterLength];
            }

            if (totalWeight > 0.01)
            {
                this.filteredIntraError[i] /= totalWeight;
            }
            else
            {
                this.filteredIntraError[i] = frames[i].IntraError;
            }
        }

        for (int i = start; i <= last; i++)
        {
            double totalWeight = 0;
            for (int j = -HalfFilterLength; j <= HalfFilterLength; j++)
            {
                int index = Math.Clamp(i + j, start, last);

                // The coded error involves the frame and the one before it.
                if (frames[index].IsFlash != 0 || (index > 0 && frames[index - 1].IsFlash != 0))
                {
                    continue;
                }

                this.filteredCodedError[i] += filter[j + HalfFilterLength] * frames[index].CodedError;
                totalWeight += filter[j + HalfFilterLength];
            }

            if (totalWeight > 0.01)
            {
                this.filteredCodedError[i] /= totalWeight;
            }
            else
            {
                this.filteredCodedError[i] = frames[i].CodedError;
            }
        }
    }

    /// <summary>
    /// Computes the central difference gradient of a range. Reference: get_gradient().
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="start">The first index.</param>
    /// <param name="last">The last index.</param>
    /// <param name="gradient">Receives the gradient.</param>
    private static void GetGradient(ReadOnlySpan<double> values, int start, int last, Span<double> gradient)
    {
        if (start == last)
        {
            gradient[start] = 0;
            return;
        }

        for (int i = start; i <= last; i++)
        {
            int previous = Math.Max(i - 1, start);
            int next = Math.Min(i + 1, last);
            gradient[i] = (values[next] - values[previous]) / (next - previous);
        }
    }

    /// <summary>
    /// Finds the next scene cut: a frame whose coded to intra error ratio and coded error both exceed twice those
    /// of its neighbourhood, and that the second reference does not predict well.
    /// Reference: find_next_scenecut().
    /// </summary>
    /// <param name="frames">The statistics from region index 0.</param>
    /// <param name="first">The first frame.</param>
    /// <param name="last">The last frame.</param>
    /// <returns>The scene cut frame, or -1.</returns>
    private static int FindNextSceneCut(ReadOnlySpan<Av1FirstPassStatistics> frames, int first, int last)
    {
        if (last - first == 0)
        {
            return -1;
        }

        for (int i = first; i <= last; i++)
        {
            if (frames[i].IsFlash != 0 || (i > 0 && frames[i - 1].IsFlash != 0))
            {
                continue;
            }

            double temporaryIntra = Math.Max(frames[i].IntraError, 0.01);
            double thisRatio = frames[i].CodedError / temporaryIntra;

            // The largest ratio and coded error in the preceding neighbourhood.
            double maximumPreviousRatio = 0;
            double maximumPreviousCoded = 0;
            for (int j = Math.Max(first, i - HalfWindow); j < i; j++)
            {
                if (frames[j].IsFlash != 0 || (j > 0 && frames[j - 1].IsFlash != 0))
                {
                    continue;
                }

                temporaryIntra = Math.Max(frames[j].IntraError, 0.01);
                double ratio = frames[j].CodedError / temporaryIntra;
                if (ratio > maximumPreviousRatio)
                {
                    maximumPreviousRatio = ratio;
                }

                if (frames[j].CodedError > maximumPreviousCoded)
                {
                    maximumPreviousCoded = frames[j].CodedError;
                }
            }

            // The largest ratio and coded error in the following neighbourhood. libaom tests the flashes of the
            // candidate here, not of the neighbour.
            double maximumNextRatio = 0;
            double maximumNextCoded = 0;
            for (int j = i + 1; j <= Math.Min(i + HalfWindow, last); j++)
            {
                if (frames[i].IsFlash != 0 || (i > 0 && frames[i - 1].IsFlash != 0))
                {
                    continue;
                }

                temporaryIntra = Math.Max(frames[j].IntraError, 0.01);
                double ratio = frames[j].CodedError / temporaryIntra;
                if (ratio > maximumNextRatio)
                {
                    maximumNextRatio = ratio;
                }

                if (frames[j].CodedError > maximumNextCoded)
                {
                    maximumNextCoded = frames[j].CodedError;
                }
            }

            if (maximumPreviousRatio < 0.001 && maximumNextRatio < 0.001)
            {
                // Very small ratios only check a small fixed threshold.
                if (thisRatio < 0.02)
                {
                    continue;
                }
            }
            else
            {
                // The frame must have a larger ratio than its neighbourhood.
                double maximumSecondReference = frames[i].SecondReferenceCodedError;
                if (i < last)
                {
                    maximumSecondReference = Math.Max(maximumSecondReference, frames[i + 1].SecondReferenceCodedError);
                }

                double secondReferenceRatio = maximumSecondReference / Math.Max(frames[i].CodedError, 0.01);
                if (secondReferenceRatio > 1.2)
                {
                    continue;
                }

                if (thisRatio < 2 * Math.Max(maximumPreviousRatio, maximumNextRatio) &&
                    frames[i].CodedError < 2 * Math.Max(maximumPreviousCoded, maximumNextCoded))
                {
                    continue;
                }
            }

            return i;
        }

        return -1;
    }

    /// <summary>
    /// Removes a region by merging it into the previous region, the next region, or both.
    /// Reference: remove_region().
    /// </summary>
    /// <param name="merge">0 to merge with the previous region, 1 with the next, 2 with both.</param>
    /// <param name="list">The regions.</param>
    /// <param name="count">The number of regions.</param>
    /// <param name="nextRegion">The region to remove; receives the index of the region after it.</param>
    private static void RemoveRegion(int merge, Span<Region> list, ref int count, ref int nextRegion)
    {
        int k = nextRegion;
        if (count == 1)
        {
            count = 0;
            return;
        }

        if (k == 0)
        {
            merge = 1;
        }
        else if (k == count - 1)
        {
            merge = 0;
        }

        int mergeCount = merge == 2 ? 2 : 1;
        switch (merge)
        {
            case 0:
                list[k - 1].Last = list[k].Last;
                nextRegion = k;
                break;
            case 1:
                list[k + 1].Start = list[k].Start;
                nextRegion = k + 1;
                break;
            default:
                list[k - 1].Last = list[k + 1].Last;
                nextRegion = k;
                break;
        }

        count -= mergeCount;
        for (k = nextRegion - (merge == 1 ? 1 : 0); k < count; k++)
        {
            list[k] = list[k + mergeCount];
        }
    }

    /// <summary>
    /// Inserts a region inside the current region, splitting it. Reference: insert_region().
    /// </summary>
    /// <param name="start">The first frame of the new region.</param>
    /// <param name="last">The last frame of the new region.</param>
    /// <param name="type">The type of the new region.</param>
    /// <param name="list">The regions.</param>
    /// <param name="count">The number of regions.</param>
    /// <param name="currentRegion">The region to split; receives the last region split from it.</param>
    private static void InsertRegion(int start, int last, RegionType type, Span<Region> list, ref int count, ref int currentRegion)
    {
        int k = currentRegion;
        RegionType thisRegionType = list[k].Type;
        int thisRegionLast = list[k].Last;
        int addCount = (start != list[k].Start ? 1 : 0) + (last != list[k].Last ? 1 : 0);

        // Move the following regions further back.
        for (int r = count - 1; r > k; r--)
        {
            list[r + addCount] = list[r];
        }

        count += addCount;
        if (start > list[k].Start)
        {
            list[k].Last = start - 1;
            k++;
            list[k].Start = start;
        }

        list[k].Type = type;
        if (last < thisRegionLast)
        {
            list[k].Last = last;
            k++;
            list[k].Start = last + 1;
            list[k].Last = thisRegionLast;
            list[k].Type = thisRegionType;
        }
        else
        {
            list[k].Last = thisRegionLast;
        }

        currentRegion = k;
    }

    /// <summary>
    /// Averages the statistics of a region: the correlation, the second reference to coded error ratio, and the
    /// intra and coded errors. Reference: analyze_region(), without the noise average that nothing reads.
    /// </summary>
    /// <param name="frames">The statistics from region index 0.</param>
    /// <param name="k">The region.</param>
    /// <param name="list">The regions.</param>
    private static void AnalyzeRegion(ReadOnlySpan<Av1FirstPassStatistics> frames, int k, Span<Region> list)
    {
        ref Region region = ref list[k];
        region.AverageCorrelationCoefficient = 0;
        region.AverageSecondReferenceRatio = 0;
        region.AverageIntraError = 0;
        region.AverageCodedError = 0;

        int checkFirstSecondReference = k != 0 ? 1 : 0;
        for (int i = region.Start; i <= region.Last; i++)
        {
            if (i > region.Start || checkFirstSecondReference != 0)
            {
                double frameCount = region.Last - region.Start + checkFirstSecondReference;
                double maximumCodedError = Math.Max(frames[i].CodedError, frames[i - 1].CodedError);
                double ratio = frames[i].SecondReferenceCodedError / Math.Max(maximumCodedError, 0.001);
                region.AverageSecondReferenceRatio += ratio / frameCount;
            }

            region.AverageIntraError += frames[i].IntraError / (double)(region.Last - region.Start + 1);
            region.AverageCodedError += frames[i].CodedError / (double)(region.Last - region.Start + 1);
            region.AverageCorrelationCoefficient += Math.Max(frames[i].CorrelationCoefficient, 0.001) / (double)(region.Last - region.Start + 1);
        }
    }

    /// <summary>
    /// Averages the statistics of every region. Reference: get_region_stats().
    /// </summary>
    /// <param name="frames">The statistics from region index 0.</param>
    /// <param name="list">The regions.</param>
    /// <param name="count">The number of regions.</param>
    private static void GetRegionStatistics(ReadOnlySpan<Av1FirstPassStatistics> frames, Span<Region> list, int count)
    {
        for (int k = 0; k < count; k++)
        {
            AnalyzeRegion(frames, k, list);
        }
    }

#pragma warning disable CA1517 // False positive: https://github.com/dotnet/sdk/issues/53388
    /// <summary>
    /// Classes each frame of a scene as stable or highly varying from the mean and variance of its errors in a
    /// window, and starts a region at each change. Reference: find_stable_regions().
    /// </summary>
    /// <param name="frames">The statistics from region index 0.</param>
    /// <param name="codedGradient">The gradient of the smoothed coded error.</param>
    /// <param name="thisStart">The first frame.</param>
    /// <param name="thisLast">The last frame.</param>
    /// <param name="list">Receives the regions.</param>
    /// <returns>The number of regions.</returns>
    private static int FindStableRegions(ReadOnlySpan<Av1FirstPassStatistics> frames, ReadOnlySpan<double> codedGradient, int thisStart, int thisLast, Span<Region> list)
    {
        int k = 0;
        list[k].Start = thisStart;
        for (int i = thisStart; i <= thisLast; i++)
        {
            // The mean and the variance of the errors in the window.
            double meanIntra = 0.001;
            double varianceIntra = 0.001;
            double meanCoded = 0.001;
            double varianceCoded = 0.001;
            int count = 0;
            for (int j = -HalfWindow; j <= HalfWindow; j++)
            {
                int index = Math.Clamp(i + j, thisStart, thisLast);
                if (frames[index].IsFlash != 0 || (index > 0 && frames[index - 1].IsFlash != 0))
                {
                    continue;
                }

                meanIntra += frames[index].IntraError;
                varianceIntra += frames[index].IntraError * frames[index].IntraError;
                meanCoded += frames[index].CodedError;
                varianceCoded += frames[index].CodedError * frames[index].CodedError;
                count++;
            }

            RegionType currentType;
            if (count > 0)
            {
                meanIntra /= count;
                varianceIntra /= count;
                meanCoded /= count;
                varianceCoded /= count;
                bool intraStable = varianceIntra / (meanIntra * meanIntra) < 1.03;
                bool codedStable = (varianceCoded / (meanCoded * meanCoded) < 1.04 && Math.Abs(codedGradient[i]) / meanCoded < 0.05) ||
                    meanCoded / meanIntra < 0.05;

                bool codedSmall = meanCoded < 0.5 * meanIntra;
                currentType = intraStable && codedStable && codedSmall ? RegionType.Stable : RegionType.HighVariance;
            }
            else
            {
                currentType = RegionType.HighVariance;
            }

            // A new region starts where the type changes.
            if (i == list[k].Start)
            {
                list[k].Type = currentType;
            }
            else if (currentType != list[k].Type)
            {
                list[k].Last = i - 1;
                list[k + 1].Start = i;
                list[k + 1].Type = currentType;
                k++;
            }
        }

        list[k].Last = thisLast;
        return k + 1;
    }
#pragma warning restore CA1517

    /// <summary>
    /// Merges consecutive regions of the same type, except scene cuts, and removes empty regions.
    /// Reference: cleanup_regions().
    /// </summary>
    /// <param name="list">The regions.</param>
    /// <param name="count">The number of regions.</param>
    private static void CleanupRegions(Span<Region> list, ref int count)
    {
        int k = 0;
        while (k < count)
        {
            if ((k > 0 && list[k - 1].Type == list[k].Type && list[k].Type != RegionType.SceneCut) ||
                list[k].Last < list[k].Start)
            {
                RemoveRegion(0, list, ref count, ref k);
            }
            else
            {
                k++;
            }
        }
    }

    /// <summary>
    /// Merges the regions of a type that are shorter than a length into their neighbours.
    /// Reference: remove_short_regions().
    /// </summary>
    /// <param name="list">The regions.</param>
    /// <param name="count">The number of regions.</param>
    /// <param name="type">The region type.</param>
    /// <param name="length">The shortest kept length.</param>
    private static void RemoveShortRegions(Span<Region> list, ref int count, RegionType type, int length)
    {
        int k = 0;
        while (k < count && count > 1)
        {
            if (list[k].Last - list[k].Start + 1 < length && list[k].Type == type)
            {
                RemoveRegion(2, list, ref count, ref k);
            }
            else
            {
                k++;
            }
        }

        CleanupRegions(list, ref count);
    }

    /// <summary>
    /// Removes very short regions, moves the bounds of the unstable regions onto frames that belong to the
    /// neighbouring stable regions, and merges short regions whose errors or correlation do not stand out.
    /// Reference: adjust_unstable_region_bounds().
    /// </summary>
    /// <param name="frames">The statistics from region index 0.</param>
    /// <param name="list">The regions.</param>
    /// <param name="count">The number of regions.</param>
    private static void AdjustUnstableRegionBounds(ReadOnlySpan<Av1FirstPassStatistics> frames, Span<Region> list, ref int count)
    {
        // Very short regions are likely noise.
        RemoveShortRegions(list, ref count, RegionType.Stable, HalfWindow);
        RemoveShortRegions(list, ref count, RegionType.HighVariance, HalfWindow);
        GetRegionStatistics(frames, list, count);

        // Adjust the region bounds. The thresholds are empirical.
        for (int k = 0; k < count; k++)
        {
            if (list[k].Type == RegionType.Stable)
            {
                continue;
            }

            if (k > 0)
            {
                // Adjust the previous bound from the average intra error of the previous neighbourhood.
                double averageIntraError = 0;
                int startIndex = Math.Max(list[k - 1].Last - WindowSize + 1, list[k - 1].Start + 1);
                int lastIndex = list[k - 1].Last;
                int countIntra = 0;
                for (int i = startIndex; i <= lastIndex; i++)
                {
                    averageIntraError += frames[i].IntraError;
                    countIntra++;
                }

                if (countIntra > 0)
                {
                    averageIntraError = Math.Max(averageIntraError / countIntra, 0.001);
                    int countCoded = 0;
                    const int countGradient = 0;
                    for (int j = lastIndex + 1; j <= list[k].Last; j++)
                    {
                        bool intraClose = Math.Abs(frames[j].IntraError - averageIntraError) / averageIntraError < 0.1;
                        bool codedSmall = frames[j].CodedError / averageIntraError < 0.1;
                        bool coefficientClose = frames[j].CorrelationCoefficient > 0.995;
                        if (!coefficientClose || !codedSmall)
                        {
                            countCoded--;
                        }

                        if (intraClose && countCoded >= 0 && countGradient >= 0)
                        {
                            // The frame probably belongs to the previous stable region.
                            list[k - 1].Last = j;
                            list[k].Start = j + 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            if (k < count - 1)
            {
                // Adjust the next bound from the average intra error of the next neighbourhood.
                double averageIntraError = 0;
                int startIndex = list[k + 1].Start;
                int lastIndex = Math.Min(list[k + 1].Last - 1, list[k + 1].Start + WindowSize - 1);
                int countIntra = 0;
                for (int i = startIndex; i <= lastIndex; i++)
                {
                    averageIntraError += frames[i].IntraError;
                    countIntra++;
                }

                if (countIntra > 0)
                {
                    averageIntraError = Math.Max(averageIntraError / countIntra, 0.001);

                    // At the bound the coded error is large, but the frame is still stable.
                    int countCoded = 1;
                    const int countGradient = 1;
                    for (int j = startIndex - 1; j >= list[k].Start; j--)
                    {
                        bool intraClose = Math.Abs(frames[j].IntraError - averageIntraError) / averageIntraError < 0.1;
                        bool codedSmall = frames[j + 1].CodedError / averageIntraError < 0.1;
                        bool coefficientClose = frames[j].CorrelationCoefficient > 0.995;
                        if (!coefficientClose || !codedSmall)
                        {
                            countCoded--;
                        }

                        if (intraClose && countCoded >= 0 && countGradient >= 0)
                        {
                            // The frame probably belongs to the next stable region.
                            list[k + 1].Start = j;
                            list[k].Last = j - 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        CleanupRegions(list, ref count);
        RemoveShortRegions(list, ref count, RegionType.HighVariance, HalfWindow);
        GetRegionStatistics(frames, list, count);

        // A short stable region with higher error or lower correlation than both neighbours merges into them, and
        // so does a short varying region with lower error or higher correlation than both.
        int m = 0;
        while (m < count && count > 1)
        {
            if (list[m].Type == RegionType.Stable &&
                list[m].Last - list[m].Start + 1 < 2 * WindowSize &&
                m > 0 &&
                (list[m].AverageCodedError > list[m - 1].AverageCodedError * 1.01 ||
                 list[m].AverageCorrelationCoefficient < list[m - 1].AverageCorrelationCoefficient * 0.999) &&
                m < count - 1 &&
                (list[m].AverageCodedError > list[m + 1].AverageCodedError * 1.01 ||
                 list[m].AverageCorrelationCoefficient < list[m + 1].AverageCorrelationCoefficient * 0.999))
            {
                RemoveRegion(2, list, ref count, ref m);
                AnalyzeRegion(frames, m - 1, list);
            }
            else if (list[m].Type == RegionType.HighVariance &&
                list[m].Last - list[m].Start + 1 < 2 * WindowSize &&
                m > 0 &&
                (list[m].AverageCodedError < list[m - 1].AverageCodedError * 0.99 ||
                 list[m].AverageCorrelationCoefficient > list[m - 1].AverageCorrelationCoefficient * 1.001) &&
                m < count - 1 &&
                (list[m].AverageCodedError < list[m + 1].AverageCodedError * 0.99 ||
                 list[m].AverageCorrelationCoefficient > list[m + 1].AverageCorrelationCoefficient * 1.001))
            {
                RemoveRegion(2, list, ref count, ref m);
                AnalyzeRegion(frames, m - 1, list);
            }
            else
            {
                m++;
            }
        }

        RemoveShortRegions(list, ref count, RegionType.Stable, WindowSize);
        RemoveShortRegions(list, ref count, RegionType.HighVariance, HalfWindow);
    }

    /// <summary>
    /// Finds blending regions, such as fades, as runs of consistent large change of the intra error inside the
    /// unstable regions, then merges or separates neighbouring blending regions.
    /// Reference: find_blending_regions().
    /// </summary>
    /// <param name="frames">The statistics from region index 0.</param>
    /// <param name="list">The regions.</param>
    /// <param name="count">The number of regions.</param>
    private static void FindBlendingRegions(ReadOnlySpan<Av1FirstPassStatistics> frames, Span<Region> list, ref int count)
    {
        int k = 0;
        int countStable = 0;
        while (k < count)
        {
            if (list[k].Type == RegionType.Stable)
            {
                k++;
                countStable++;
                continue;
            }

            int direction = 0;
            int start = 0;
            int last;
            for (int i = list[k].Start; i <= list[k].Last; i++)
            {
                // Mark the runs that have a consistent large change of intra error.
                if (k == 0 && i == list[k].Start)
                {
                    continue;
                }

                if (frames[i].IsFlash != 0 || (i > 0 && frames[i - 1].IsFlash != 0))
                {
                    continue;
                }

                double gradient = frames[i].IntraError - frames[i - 1].IntraError;
                bool largeChange = Math.Abs(gradient) / Math.Max(frames[i].IntraError, 0.01) > 0.05;
                int thisDirection = 0;
                if (largeChange)
                {
                    thisDirection = gradient > 0 ? 1 : -1;
                }

                // The current trend continues.
                if (direction == thisDirection)
                {
                    continue;
                }

                if (direction != 0)
                {
                    // End a run of large change and add it.
                    last = i - 1;
                    InsertRegion(start, last, RegionType.Blending, list, ref count, ref k);
                }

                direction = thisDirection;
                start = k == 0 && i == list[k].Start + 1 ? i - 1 : i;
            }

            if (direction != 0)
            {
                last = list[k].Last;
                InsertRegion(start, last, RegionType.Blending, list, ref count, ref k);
            }

            k++;
        }

        // A blending region with very low correlation cannot be used, so it is highly varying.
        GetRegionStatistics(frames, list, count);
        for (k = 0; k < count; k++)
        {
            if (list[k].Type != RegionType.Blending)
            {
                continue;
            }

            if (list[k].Last == list[k].Start || list[k].AverageCorrelationCoefficient < 0.6 || countStable == 0)
            {
                list[k].Type = RegionType.HighVariance;
            }
        }

        GetRegionStatistics(frames, list, count);

        // A blend can dip in intra error, first decreasing then increasing, which makes two blending regions.
        k = 1;
        while (k < count)
        {
            if (k < count - 1 && list[k].Type == RegionType.HighVariance)
            {
                // A short varying region between two blending regions may be the middle of one blend.
                if (list[k - 1].Type == RegionType.Blending &&
                    list[k + 1].Type == RegionType.Blending &&
                    list[k].Last - list[k].Start < 3)
                {
                    int previousDirection = frames[list[k - 1].Last].IntraError - frames[list[k - 1].Last - 1].IntraError > 0 ? 1 : -1;
                    int nextDirection = frames[list[k + 1].Last].IntraError - frames[list[k + 1].Last - 1].IntraError > 0 ? 1 : -1;
                    if (previousDirection < 0 && nextDirection > 0)
                    {
                        // Check the ratios of this possible middle of a blend.
                        double ratioThreshold = Math.Min(list[k - 1].AverageSecondReferenceRatio, list[k + 1].AverageSecondReferenceRatio) * 0.95;
                        if (list[k].AverageSecondReferenceRatio > ratioThreshold)
                        {
                            list[k].Type = RegionType.Blending;
                            RemoveRegion(2, list, ref count, ref k);
                            AnalyzeRegion(frames, k - 1, list);
                            continue;
                        }
                    }
                }
            }

            // A pair of consecutive blending regions.
            if (list[k - 1].Type == RegionType.Blending && list[k].Type == RegionType.Blending)
            {
                int previousDirection = frames[list[k - 1].Last].IntraError - frames[list[k - 1].Last - 1].IntraError > 0 ? 1 : -1;
                int nextDirection = frames[list[k].Last].IntraError - frames[list[k].Last - 1].IntraError > 0 ? 1 : -1;

                // Two very short regions need no check.
                int totalLength = list[k].Last - list[k - 1].Start + 1;
                if (totalLength < 4)
                {
                    list[k - 1].Type = RegionType.HighVariance;
                    k++;
                    continue;
                }

                bool toMerge = false;
                if (previousDirection < 0 && nextDirection > 0)
                {
                    // Check the last frame of the previous region.
                    double previousLength = list[k - 1].Last - list[k - 1].Start + 1;
                    double lastRatio;
                    double ratioThreshold;
                    double maximumCodedError = Math.Max(frames[list[k - 1].Last].CodedError, frames[list[k - 1].Last - 1].CodedError);
                    lastRatio = frames[list[k - 1].Last].SecondReferenceCodedError / Math.Max(maximumCodedError, 0.001);
                    if (previousLength < 2.01)
                    {
                        // The previous region is very short.
                        ratioThreshold = list[k].AverageSecondReferenceRatio * 0.95;
                    }
                    else
                    {
                        double previousRatio = ((list[k - 1].AverageSecondReferenceRatio * previousLength) - lastRatio) / (previousLength - 1.0);
                        ratioThreshold = Math.Min(previousRatio, list[k].AverageSecondReferenceRatio) * 0.95;
                    }

                    if (lastRatio > ratioThreshold)
                    {
                        toMerge = true;
                    }
                }

                if (toMerge)
                {
                    RemoveRegion(0, list, ref count, ref k);
                    AnalyzeRegion(frames, k - 1, list);
                    continue;
                }

                // Two separate blends: the bound frame is a highly varying region between them.
                int previousRegion = k - 1;
                InsertRegion(list[previousRegion].Last, list[previousRegion].Last, RegionType.HighVariance, list, ref count, ref previousRegion);
                AnalyzeRegion(frames, previousRegion, list);
                k = previousRegion + 1;
                AnalyzeRegion(frames, k, list);
            }

            k++;
        }

        CleanupRegions(list, ref count);
    }

    /// <summary>
    /// Removes blending regions shorter than five frames, and short varying regions between blending or stable
    /// regions, merging each into the neighbour with the closer correlation. Reference: cleanup_blendings().
    /// </summary>
    /// <param name="list">The regions.</param>
    /// <param name="count">The number of regions.</param>
    private static void CleanupBlendings(Span<Region> list, ref int count)
    {
        int k = 0;
        while (k < count && count > 1)
        {
            bool shortBlending = list[k].Type == RegionType.Blending && list[k].Last - list[k].Start + 1 < 5;
            bool shortHighVariance = list[k].Type == RegionType.HighVariance && list[k].Last - list[k].Start + 1 < 5;
            int stableNeighbor = (k > 0 && list[k - 1].Type == RegionType.Stable) || (k < count - 1 && list[k + 1].Type == RegionType.Stable) ? 1 : 0;
            int blendNeighbor = (k > 0 && list[k - 1].Type == RegionType.Blending) || (k < count - 1 && list[k + 1].Type == RegionType.Blending) ? 1 : 0;
            int totalNeighbors = (k > 0 ? 1 : 0) + (k < count - 1 ? 1 : 0);
            if (shortBlending || (shortHighVariance && stableNeighbor + blendNeighbor >= totalNeighbors))
            {
                // Merge with the neighbour whose correlation is closer.
                double previousDifference = k > 0 ? Math.Abs(list[k].AverageCorrelationCoefficient - list[k - 1].AverageCorrelationCoefficient) : 1;
                double nextDifference = k < count - 1 ? Math.Abs(list[k].AverageCorrelationCoefficient - list[k + 1].AverageCorrelationCoefficient) : 1;
                int merge = previousDifference > nextDifference ? 1 : 0;
                RemoveRegion(merge, list, ref count, ref k);
            }
            else
            {
                k++;
            }
        }

        CleanupRegions(list, ref count);
    }

    /// <summary>
    /// A run of frames of one kind with its averaged statistics. Reference: REGIONS.
    /// </summary>
    private struct Region
    {
        /// <summary>
        /// The empty region that stands in for an index outside the list.
        /// </summary>
        public static readonly Region Empty;

        /// <summary>Reference: start.</summary>
        public int Start;

        /// <summary>Reference: last.</summary>
        public int Last;

        /// <summary>Reference: avg_cor_coeff.</summary>
        public double AverageCorrelationCoefficient;

        /// <summary>Reference: avg_sr_fr_ratio.</summary>
        public double AverageSecondReferenceRatio;

        /// <summary>Reference: avg_intra_err.</summary>
        public double AverageIntraError;

        /// <summary>Reference: avg_coded_err.</summary>
        public double AverageCodedError;

        /// <summary>Reference: type.</summary>
        public RegionType Type;
    }
}

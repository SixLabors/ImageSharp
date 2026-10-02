// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// Golden frame group length, the alternate reference decisions and the boosts.
/// </content>
internal sealed partial class Av1SecondPass
{
    /// <summary>
    /// The smallest boost per frame of a golden frame group. Reference: GF_MIN_BOOST.
    /// </summary>
    private const int GoldenMinimumBoost = 50;

    /// <summary>
    /// The largest boost of one frame in the alternate reference boost. Reference: GF_MAX_BOOST.
    /// </summary>
    private const double GoldenMaximumFrameBoost = 90.0;

    /// <summary>
    /// The largest golden boost of a group without an alternate reference. Reference: MAX_GF_BOOST.
    /// </summary>
    private const int MaximumGoldenBoost = 5400;

    /// <summary>
    /// The base boost of the alternate reference boost. Reference: NORMAL_BOOST.
    /// </summary>
    private const int NormalBoost = 100;

    /// <summary>
    /// The smallest length of a shrunken golden interval. Reference: MIN_SHRINK_LEN.
    /// </summary>
    private const int MinimumShrinkLength = 6;

    /// <summary>
    /// The number of frames of the key frame group that the region analysis covers.
    /// Reference: MAX_FIRSTPASS_ANALYSIS_FRAMES.
    /// </summary>
    private const int MaximumAnalysisFrames = 150;

    /// <summary>
    /// Gets the error per macroblock that bounds the boost of frames with little intra error.
    /// Reference: baseline_err_per_mb().
    /// </summary>
    private double BaselineErrorPerMacroblock => (uint)this.height * (uint)this.width <= 640 * 360 ? 500.0 : 1000.0;

    /// <summary>
    /// Adds a small bias away from zero to a divisor. Reference: DOUBLE_DIVIDE_CHECK.
    /// </summary>
    /// <param name="x">The divisor.</param>
    /// <returns>The biased divisor.</returns>
    private static double DoubleDivideCheck(double x) => x < 0 ? x - 0.000001 : x + 0.000001;

    /// <summary>
    /// Returns how much the prediction from the second reference decays against the last frame, with a penalty for
    /// intra coding. Reference: get_sr_decay_rate().
    /// </summary>
    /// <param name="frame">The frame's statistics.</param>
    /// <returns>The decay rate, at least 0.75.</returns>
    private static double GetSecondReferenceDecayRate(Av1FirstPassStatistics frame)
    {
        double secondReferenceDifference = frame.SecondReferenceCodedError - frame.CodedError;
        double secondReferenceDecay = 1.0;
        double modifiedPercentInter = frame.PercentInter;

        // Reference: LOW_CODED_ERR_PER_MB and NCOUNT_FRAME_II_THRESH.
        if (frame.CodedError > 0.01 && frame.IntraError / DoubleDivideCheck(frame.CodedError) < 5.0)
        {
            modifiedPercentInter = frame.PercentInter - frame.PercentNeutral;
        }

        double modifiedPercentIntra = 100 * (1.0 - modifiedPercentInter);

        // Reference: LOW_SR_DIFF_TRHESH and INTRA_PART.
        if (secondReferenceDifference > 0.01)
        {
            double secondReferencePart = secondReferenceDifference * 0.25 / frame.IntraError;
            secondReferenceDecay = 1.0 - secondReferencePart - (0.005 * modifiedPercentIntra);
        }

        // Reference: DEFAULT_DECAY_LIMIT.
        return Math.Max(secondReferenceDecay, 0.75);
    }

    /// <summary>
    /// Returns the smaller of the second reference decay and the share of zero motion blocks.
    /// Reference: get_zero_motion_factor().
    /// </summary>
    /// <param name="frame">The frame's statistics.</param>
    /// <returns>The factor.</returns>
    private static double GetZeroMotionFactor(Av1FirstPassStatistics frame)
    {
        double zeroMotionPercent = frame.PercentInter - frame.PercentMotion;
        double secondReferenceDecay = GetSecondReferenceDecayRate(frame);
        return Math.Min(secondReferenceDecay, zeroMotionPercent);
    }

    /// <summary>
    /// Returns how badly the prediction quality decays from frame to frame. Reference: get_prediction_decay_rate().
    /// </summary>
    /// <param name="frame">The frame's statistics.</param>
    /// <returns>The decay rate.</returns>
    private static double GetPredictionDecayRate(Av1FirstPassStatistics frame)
    {
        double secondReferenceDecay = GetSecondReferenceDecayRate(frame);

        // Reference: DEFAULT_ZM_FACTOR.
        double zeroMotionFactor = 0.5 * (frame.PercentInter - frame.PercentMotion);
        if (zeroMotionFactor > 1.0)
        {
            zeroMotionFactor = 1.0;
        }
        else if (zeroMotionFactor < 0.0)
        {
            zeroMotionFactor = 0.0;
        }

        return Math.Max(zeroMotionFactor, secondReferenceDecay + ((1.0 - secondReferenceDecay) * zeroMotionFactor));
    }

    /// <summary>
    /// Updates the motion measures of a group from one frame: the balance of inward and outward motion and how
    /// uniform the motion field is. Reference: accumulate_frame_motion_stats().
    /// </summary>
    /// <param name="frame">The frame's statistics.</param>
    /// <param name="groupStatistics">The group measures.</param>
    /// <param name="frameWidth">The frame width.</param>
    /// <param name="frameHeight">The frame height.</param>
    private static void AccumulateFrameMotionStatistics(Av1FirstPassStatistics frame, ref GroupStatistics groupStatistics, double frameWidth, double frameHeight)
    {
        double percent = frame.PercentMotion;

        // Accumulate the motion into and out of the frame.
        groupStatistics.ThisFrameMotionInOut = frame.MotionVectorInOutCount * percent;
        groupStatistics.MotionInOutAccumulator += groupStatistics.ThisFrameMotionInOut;
        groupStatistics.AbsoluteMotionInOutAccumulator += Math.Abs(groupStatistics.ThisFrameMotionInOut);

        // Accumulate how uniform or how random the motion field is, as a ratio of the mean absolute to the mean.
        if (percent > 0.05)
        {
            double rowRatio = Math.Abs(frame.MotionVectorRowAbsolute) / DoubleDivideCheck(Math.Abs(frame.MotionVectorRow));
            double columnRatio = Math.Abs(frame.MotionVectorColumnAbsolute) / DoubleDivideCheck(Math.Abs(frame.MotionVectorColumn));
            groupStatistics.MotionRatioAccumulator +=
                percent * (rowRatio < frame.MotionVectorRowAbsolute * frameHeight ? rowRatio : frame.MotionVectorRowAbsolute * frameHeight);

            groupStatistics.MotionRatioAccumulator +=
                percent * (columnRatio < frame.MotionVectorColumnAbsolute * frameWidth ? columnRatio : frame.MotionVectorColumnAbsolute * frameWidth);
        }
    }

    /// <summary>
    /// Returns the share of the frame that is not blank border or very flat, between 0.5 and 1.
    /// Reference: calculate_active_area().
    /// </summary>
    /// <param name="frame">The frame's statistics.</param>
    /// <returns>The active area.</returns>
    private double CalculateActiveArea(Av1FirstPassStatistics frame)
    {
        double activePercent = 1.0 - ((frame.IntraSkipPercent / 2) + (frame.InactiveZoneRows * 2 / (double)this.macroblockRows));
        return Math.Clamp(activePercent, 0.5, 1.0);
    }

    /// <summary>
    /// Reads the statistics at the read position and advances it. Reference: input_stats().
    /// </summary>
    /// <param name="frame">Receives the statistics.</param>
    /// <returns>False at the end of the buffer.</returns>
    private bool InputStatistics(out Av1FirstPassStatistics frame)
    {
        if (this.statisticsPosition >= this.statisticsCount)
        {
            frame = default;
            return false;
        }

        frame = this.statistics[this.statisticsPosition++];
        return true;
    }

    /// <summary>
    /// Detects a flash from the frame after it: a high second reference share shows that the prediction from
    /// before the flash recovers. Reference: detect_flash() with read_frame_stats().
    /// </summary>
    /// <param name="basePosition">The buffer position the offset is relative to.</param>
    /// <param name="offset">The offset of the frame after the possible flash.</param>
    /// <returns>Whether a flash precedes the frame.</returns>
    private bool DetectFlash(int basePosition, int offset)
    {
        int position = basePosition + offset;
        if (position < 0 || position >= this.statisticsCount)
        {
            return false;
        }

        ref readonly Av1FirstPassStatistics next = ref this.statistics[position];
        return next.PercentSecondReference > next.PercentInter && next.PercentSecondReference >= 0.5;
    }

    /// <summary>
    /// Detects a complex transition followed by a static section, such as a fade between slides, from the ring
    /// of unmodified statistics. Reference: detect_transition_to_still().
    /// </summary>
    /// <param name="nextIndex">The offset of the first frame to test from the current frame.</param>
    /// <param name="frameInterval">The number of frames since the group start.</param>
    /// <param name="stillInterval">The number of frames that must be static.</param>
    /// <param name="loopDecayRate">The decay rate of the latest frame.</param>
    /// <param name="lastDecayRate">The decay rate of the frame before it.</param>
    /// <returns>Whether the frames become still.</returns>
    private bool DetectTransitionToStill(int nextIndex, int frameInterval, int stillInterval, double loopDecayRate, double lastDecayRate)
    {
        // Break clause to detect very still sections after motion, such as a static image after a fade.
        if (frameInterval > this.minimumGoldenInterval && loopDecayRate >= 0.999 && lastDecayRate < 0.9)
        {
            int statisticsLeft = this.statisticsInfo.GetFutureCount(nextIndex);
            if (statisticsLeft >= stillInterval)
            {
                // The static condition must persist for the whole interval.
                int j;
                for (j = 0; j < stillInterval; ++j)
                {
                    ref readonly Av1FirstPassStatistics frame = ref this.statisticsInfo.Peek(nextIndex + j);
                    if (frame.PercentInter - frame.PercentMotion < 0.999)
                    {
                        break;
                    }
                }

                return j == stillInterval;
            }
        }

        return false;
    }

    /// <summary>
    /// Updates the measures of a golden frame group from its next frame. A flash does not count toward the decay.
    /// Reference: accumulate_next_frame_stats(), without the averages that only the rate targets read.
    /// </summary>
    /// <param name="frame">The frame's statistics.</param>
    /// <param name="flashDetected">Whether the frame is a flash.</param>
    /// <param name="currentIndex">The frame's index in the group.</param>
    /// <param name="groupStatistics">The group measures.</param>
    private void AccumulateNextFrameStatistics(Av1FirstPassStatistics frame, bool flashDetected, int currentIndex, ref GroupStatistics groupStatistics)
    {
        AccumulateFrameMotionStatistics(frame, ref groupStatistics, this.width, this.height);
        groupStatistics.AverageSecondReferenceCodedError += frame.SecondReferenceCodedError;
        if (Math.Abs(frame.RawErrorStandardDeviation) > 0.000001)
        {
            groupStatistics.NonZeroDeviationCount++;
            groupStatistics.AverageRawErrorDeviation += frame.RawErrorStandardDeviation;
        }

        // Accumulate the effect of the prediction quality decay.
        if (!flashDetected)
        {
            groupStatistics.LastLoopDecayRate = groupStatistics.LoopDecayRate;
            groupStatistics.LoopDecayRate = GetPredictionDecayRate(frame);
            groupStatistics.DecayAccumulator *= groupStatistics.LoopDecayRate;

            // Monitor for static sections.
            if (this.framesSinceKey + currentIndex - 1 > 1)
            {
                groupStatistics.ZeroMotionAccumulator = Math.Min(groupStatistics.ZeroMotionAccumulator, GetZeroMotionFactor(frame));
            }
        }
    }

    /// <summary>
    /// Returns the boost of one frame from its intra to inter error ratio, scaled by the recent quantizer and by
    /// the motion into or out of the frame. Reference: calc_frame_boost().
    /// </summary>
    /// <param name="frame">The frame's statistics.</param>
    /// <param name="thisFrameMotionInOut">The frame's motion balance, from -1 to 1.</param>
    /// <param name="maximumBoost">The largest boost.</param>
    /// <param name="scaleMaximumBoost">Whether inward motion also raises the largest boost.</param>
    /// <returns>The boost.</returns>
    private double CalculateFrameBoost(Av1FirstPassStatistics frame, double thisFrameMotionInOut, double maximumBoost, bool scaleMaximumBoost)
    {
        double lastQ = Av1ConstantQuality.ConvertQIndexToQ(this.averageInterFrameQIndex, this.bitDepth);
        double boostQCorrection = Math.Min(0.5 + (lastQ * 0.015), 1.5);
        double activeArea = this.CalculateActiveArea(frame);

        // The underlying boost is the ratio of intra to inter error. Reference: BOOST_FACTOR.
        double frameBoost = Math.Max(this.BaselineErrorPerMacroblock * activeArea, frame.IntraError * activeArea) / DoubleDivideCheck(frame.CodedError);
        frameBoost = frameBoost * 12.5 * boostQCorrection;

        // New content coming into the frame raises the boost; a net motion out of the frame lowers it, at most
        // by half.
        if (thisFrameMotionInOut > 0.0)
        {
            frameBoost += frameBoost * (thisFrameMotionInOut * 2.0);
            if (scaleMaximumBoost)
            {
                maximumBoost += maximumBoost * (thisFrameMotionInOut * 2.0);
            }
        }
        else
        {
            frameBoost += frameBoost * (thisFrameMotionInOut / 2.0);
        }

        return Math.Min(frameBoost, maximumBoost * boostQCorrection);
    }

    /// <summary>
    /// Returns the boost of an alternate reference or golden frame from the decaying boosts of the frames after
    /// and before it, optionally projected to frames the look-ahead did not cover. Reference: av1_calc_arf_boost().
    /// </summary>
    /// <param name="basePosition">The buffer position the offset is relative to.</param>
    /// <param name="offset">The offset of the boosted frame.</param>
    /// <param name="forwardFrames">The number of frames after it.</param>
    /// <param name="backwardFrames">The number of frames before it.</param>
    /// <param name="trackStatistics">Whether to record the statistics used and required and project the boost.</param>
    /// <param name="scaleMaximumBoost">Whether inward motion also raises the largest frame boost.</param>
    /// <returns>The boost.</returns>
    private int CalculateArfBoost(int basePosition, int offset, int forwardFrames, int backwardFrames, bool trackStatistics, bool scaleMaximumBoost)
    {
        GroupStatistics groupStatistics = GroupStatistics.Create();
        double boostScore = NormalBoost;
        int statisticsUsed = 0;

        // Search forward from the boosted frame.
        for (int i = 0; i < forwardFrames; ++i)
        {
            int position = basePosition + i + offset;
            if (position < 0 || position >= this.statisticsCount)
            {
                break;
            }

            ref readonly Av1FirstPassStatistics frame = ref this.statistics[position];
            AccumulateFrameMotionStatistics(frame, ref groupStatistics, this.width, this.height);

            // A flash frame and the recovery frame after it both score poorly, so neither counts.
            bool flashDetected = this.DetectFlash(basePosition, i + offset) || this.DetectFlash(basePosition, i + offset + 1);

            // Accumulate the effect of the prediction quality decay. Reference: MIN_DECAY_FACTOR.
            if (!flashDetected)
            {
                groupStatistics.DecayAccumulator *= GetPredictionDecayRate(frame);
                groupStatistics.DecayAccumulator = groupStatistics.DecayAccumulator < 0.01 ? 0.01 : groupStatistics.DecayAccumulator;
            }

            boostScore += groupStatistics.DecayAccumulator *
                this.CalculateFrameBoost(frame, groupStatistics.ThisFrameMotionInOut, GoldenMaximumFrameBoost, scaleMaximumBoost);

            statisticsUsed++;
        }

        int arfBoost = (int)boostScore;

        // Search backward toward the previous golden frame.
        boostScore = 0.0;
        groupStatistics = GroupStatistics.Create();
        for (int i = -1; i >= -backwardFrames; --i)
        {
            int position = basePosition + i + offset;
            if (position < 0 || position >= this.statisticsCount)
            {
                break;
            }

            ref readonly Av1FirstPassStatistics frame = ref this.statistics[position];
            AccumulateFrameMotionStatistics(frame, ref groupStatistics, this.width, this.height);
            bool flashDetected = this.DetectFlash(basePosition, i + offset) || this.DetectFlash(basePosition, i + offset + 1);
            if (!flashDetected)
            {
                groupStatistics.DecayAccumulator *= GetPredictionDecayRate(frame);
                groupStatistics.DecayAccumulator = groupStatistics.DecayAccumulator < 0.01 ? 0.01 : groupStatistics.DecayAccumulator;
            }

            boostScore += groupStatistics.DecayAccumulator *
                this.CalculateFrameBoost(frame, groupStatistics.ThisFrameMotionInOut, GoldenMaximumFrameBoost, scaleMaximumBoost);

            statisticsUsed++;
        }

        arfBoost += (int)boostScore;
        if (trackStatistics)
        {
            this.statisticsUsedForGoldenBoost = statisticsUsed;
            this.statisticsRequiredForGoldenBoost = forwardFrames + backwardFrames;
            arfBoost = this.GetProjectedGoldenBoost(arfBoost, this.statisticsRequiredForGoldenBoost, statisticsUsed);
        }

        if (arfBoost < (backwardFrames + forwardFrames) * GoldenMinimumBoost)
        {
            arfBoost = (backwardFrames + forwardFrames) * GoldenMinimumBoost;
        }

        return arfBoost;
    }

    /// <summary>
    /// Scales a golden boost from the frames the look-ahead covered to the frames the boost was meant to cover.
    /// Reference: get_projected_gfu_boost().
    /// </summary>
    /// <param name="boost">The boost.</param>
    /// <param name="framesToProject">The frames the boost was meant to cover.</param>
    /// <param name="statisticsUsed">The frames the boost covered.</param>
    /// <returns>The projected boost.</returns>
    private int GetProjectedGoldenBoost(int boost, int framesToProject, int statisticsUsed)
    {
        if (statisticsUsed >= framesToProject)
        {
            return boost;
        }

        double minimumBoostFactor = Math.Sqrt(this.baselineGoldenInterval);
        double tplFactor = Av1ConstantQuality.GetGoldenBoostProjectionFactor(minimumBoostFactor, Av1ConstantQuality.MaximumBoostFactor, framesToProject);
        double tplFactorUsed = Av1ConstantQuality.GetGoldenBoostProjectionFactor(minimumBoostFactor, Av1ConstantQuality.MaximumBoostFactor, statisticsUsed);
        return (int)Math.Round(tplFactor * boost / tplFactorUsed, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Decides whether a golden interval ends at a frame: after a transition to still, after strong or zooming
    /// motion past the minimum interval, or past the maximum interval. Reference: detect_gf_cut() in good-quality
    /// mode.
    /// </summary>
    /// <param name="frameIndex">The frame's index from the group start.</param>
    /// <param name="currentStart">The index of the frame before the group.</param>
    /// <param name="flashDetected">Whether the frame is a flash.</param>
    /// <param name="activeMaximumInterval">The largest interval.</param>
    /// <param name="activeMinimumInterval">The smallest interval.</param>
    /// <param name="groupStatistics">The group measures.</param>
    /// <returns>Whether the interval ends before the frame.</returns>
    private bool DetectGoldenCut(
        int frameIndex,
        int currentStart,
        bool flashDetected,
        int activeMaximumInterval,
        int activeMinimumInterval,
        GroupStatistics groupStatistics)
    {
        // The motion breakout threshold depends on the image size.
        double motionRatioThreshold = (this.height + this.width) / 4.0;
        if (!flashDetected)
        {
            // A still section after motion, such as a static image after a fade.
            if (this.DetectTransitionToStill(
                this.statisticsPosition,
                frameIndex - currentStart,
                5,
                groupStatistics.LoopDecayRate,
                groupStatistics.LastLoopDecayRate))
            {
                return true;
            }
        }

        // Break out after the minimum interval on an odd length, away from the next key frame, on strong motion or
        // zoom. Reference: ARF_ABS_ZOOM_THRESH.
        if (frameIndex - currentStart >= activeMinimumInterval &&
            this.framesToKey - frameIndex >= this.minimumGoldenInterval &&
            ((frameIndex - currentStart) & 0x01) != 0 &&
            !flashDetected &&
            (groupStatistics.MotionRatioAccumulator > motionRatioThreshold || groupStatistics.AbsoluteMotionInOutAccumulator > 4.4))
        {
            return true;
        }

        return frameIndex - currentStart >= activeMaximumInterval + 1;
    }

    /// <summary>
    /// Decides the next golden interval from the look-ahead: it ends at a key frame, at the end of the statistics,
    /// or at a cut, and a cut may move back to a scene cut region or to the frame whose neighbourhood makes the best
    /// alternate reference. Reference: calculate_gf_length() in the one-pass look-ahead stage, which decides one
    /// interval.
    /// </summary>
    /// <param name="maximumGopLength">The largest interval.</param>
    private void CalculateGoldenLength(int maximumGopLength)
    {
        int startPosition = this.statisticsPosition;

        // The statistics are indexed from the key frame of a key frame group, which the read position has passed.
        int statisticsBase = startPosition - (this.framesSinceKey == 0 ? 1 : 0);
        int activeMinimumInterval = this.minimumGoldenInterval;
        int activeMaximumInterval = Math.Min(this.maximumGoldenInterval, maximumGopLength);
        int minimumShrinkInterval = Math.Max(MinimumShrinkLength, activeMinimumInterval);

        int i = this.framesSinceKey == 0 ? 1 : 0;
        const int maximumIntervals = 1;
        int countCuts = 1;

        // A group after a key frame or a golden frame starts one frame later than a group after an alternate
        // reference, whose overlay the previous group coded.
        int currentStart = -1 + (this.arfGoldenBoostLast ? 0 : 1);
        Span<int> cutPositions = stackalloc int[MaximumGoldenIntervalCount + 1];
        cutPositions.Clear();
        cutPositions[0] = -1;
        GroupStatistics groupStatistics = GroupStatistics.Create();
        while (countCuts < maximumIntervals + 1)
        {
            int cutHere;
            if (i >= this.framesToKey)
            {
                // The next key frame ends the interval.
                cutHere = 2;
            }
            else if (i - currentStart >= this.staticSceneMaximumGoldenInterval)
            {
                // The largest static interval.
                cutHere = 1;
            }
            else if (!this.InputStatistics(out Av1FirstPassStatistics next))
            {
                // The last frame of the look-ahead.
                cutHere = 2;
            }
            else
            {
                // A brief flash after which the prediction from an earlier frame recovers.
                bool flashDetected = this.DetectFlash(this.statisticsPosition, 0);
                this.AccumulateNextFrameStatistics(next, flashDetected, i, ref groupStatistics);
                cutHere = this.DetectGoldenCut(i, currentStart, flashDetected, activeMaximumInterval, activeMinimumInterval, groupStatistics) ? 1 : 0;
            }

            if (cutHere != 0)
            {
                int currentLast = i - 1;
                int originalLast = currentLast;

                // The regions are indexed from the frame of their analysis, not from the key frame.
                int offset = this.framesSinceKey;
                currentLast = this.ShrinkGoldenInterval(statisticsBase, offset, currentStart, currentLast, activeMaximumInterval, activeMinimumInterval, minimumShrinkInterval);

                cutPositions[countCuts] = currentLast;
                countCuts++;

                // Continue from the chosen cut.
                this.statisticsPosition = startPosition + currentLast;
                currentStart = currentLast;
                int regionIndex = this.FindRegionsIndex(currentStart + 1 + offset);
                if (regionIndex >= 0 && this.regions[regionIndex].Type == RegionType.SceneCut)
                {
                    currentStart++;
                }

                i = currentLast;
                if (cutHere > 1 && currentLast == originalLast)
                {
                    break;
                }

                groupStatistics = GroupStatistics.Create();
            }

            ++i;
        }

        for (int n = 1; n < countCuts; n++)
        {
            this.goldenIntervals[n - 1] = cutPositions[n] - cutPositions[n - 1];
        }

        this.currentGoldenIndex = 0;
        this.statisticsPosition = startPosition;
    }

    /// <summary>
    /// Moves the end of a golden interval back to a scene cut region inside it, or to the frame whose neighbourhood
    /// of correlated, low noise frames makes the best alternate reference.
    /// Reference: the shrinking part of calculate_gf_length().
    /// </summary>
    /// <param name="statisticsBase">The buffer position of index 0.</param>
    /// <param name="offset">The offset of the region indices from the interval indices.</param>
    /// <param name="currentStart">The index of the frame before the interval.</param>
    /// <param name="currentLast">The index of the last frame of the interval.</param>
    /// <param name="activeMaximumInterval">The largest interval.</param>
    /// <param name="activeMinimumInterval">The smallest interval.</param>
    /// <param name="minimumShrinkInterval">The smallest shrunken interval.</param>
    /// <returns>The index of the chosen last frame.</returns>
    private int ShrinkGoldenInterval(
        int statisticsBase,
        int offset,
        int currentStart,
        int currentLast,
        int activeMaximumInterval,
        int activeMinimumInterval,
        int minimumShrinkInterval)
    {
        // Only an interval no longer than the maximum is shrunk.
        if (currentLast - currentStart > activeMaximumInterval || currentLast <= currentStart)
        {
            return currentLast;
        }

        int count = this.regionCount;
        int startRegion = this.FindRegionsIndex(currentStart + offset);
        int lastRegion = this.FindRegionsIndex(currentLast + offset);
        if (currentStart + offset == 0)
        {
            startRegion = 0;
        }

        // A scene cut inside the interval, far enough from its start, ends it.
        int sceneCutIndex = -1;
        for (int r = startRegion + 1; r <= lastRegion; r++)
        {
            if (this.regions[r].Type == RegionType.SceneCut &&
                this.regions[r].Last - offset - currentStart > activeMinimumInterval)
            {
                sceneCutIndex = r;
                break;
            }
        }

        // A scene cut very close to the end of the analysed frames is ignored.
        if (this.GetRegion(count - 1).Last - this.GetRegion(sceneCutIndex).Last < 4)
        {
            sceneCutIndex = -1;
        }

        if (sceneCutIndex != -1)
        {
            // A minor scene cut is part of the interval; a major one starts the next.
            ref readonly Region sceneCut = ref this.regions[sceneCutIndex];
            ref readonly Av1FirstPassStatistics cutStatistics = ref this.statistics[statisticsBase + sceneCut.Start - offset];
            bool minorSceneCut = sceneCut.AverageCorrelationCoefficient * (1 - (cutStatistics.NoiseVariance / sceneCut.AverageIntraError)) > 0.6;
            return sceneCut.Last - offset - (minorSceneCut ? 0 : 1);
        }

        // Close to the end of the analysed frames a shrink could leave intervals that are too short.
        bool lastAnalysed = lastRegion == count - 1 && currentLast + offset == this.GetRegion(lastRegion).Last;
        bool notEnoughRegions = lastRegion - startRegion <= 1 + (this.GetRegion(startRegion).Type == RegionType.SceneCut ? 1 : 0);
        if (lastAnalysed && notEnoughRegions)
        {
            return currentLast;
        }

        const double arfLengthFactor = 0.1;
        double bestScore = 0;
        int bestIndex = -1;
        int firstFrame = this.GetRegion(0).Start - offset;
        int lastFrame = this.GetRegion(count - 1).Last - offset;

        // The score of how much an alternate reference helps the whole group.
        double baseScore = 0.0;
        int countBase = 0;
        bool staticFrames = false;
        for (int j = currentStart + 1; j < currentStart + minimumShrinkInterval; j++)
        {
            if (statisticsBase + j >= this.statisticsCount)
            {
                break;
            }

            baseScore = (baseScore + 1.0) * this.statistics[statisticsBase + j].CorrelationCoefficient;
            countBase++;
        }

        if (countBase != 0 && baseScore / countBase > 0.992)
        {
            staticFrames = true;
        }

        // A group includes at most one blending region.
        bool metBlending = false;
        bool lastBlending = false;
        for (int j = currentStart + minimumShrinkInterval; j <= currentLast; j++)
        {
            if (statisticsBase + j >= this.statisticsCount)
            {
                break;
            }

            baseScore = (baseScore + 1.0) * this.statistics[statisticsBase + j].CorrelationCoefficient;
            int thisRegion = this.FindRegionsIndex(j + offset);
            if (thisRegion < 0)
            {
                continue;
            }

            if (this.regions[thisRegion].Type == RegionType.Blending)
            {
                lastBlending = true;
                if (metBlending)
                {
                    break;
                }

                baseScore = 0;
                continue;
            }

            if (lastBlending)
            {
                metBlending = true;
            }

            lastBlending = false;

            // Add how well the neighbourhood of the candidate predicts: the correlation and the share of signal
            // in the next three frames and the frames before.
            double thisScore = arfLengthFactor * baseScore;
            double accumulatedCoefficient = 1.0;
            int countForward = 0;
            for (int n = j + 1; n <= j + 3 && n <= lastFrame; n++)
            {
                if (statisticsBase + n >= this.statisticsCount)
                {
                    break;
                }

                ref readonly Av1FirstPassStatistics frame = ref this.statistics[statisticsBase + n];
                accumulatedCoefficient *= frame.CorrelationCoefficient;
                thisScore += accumulatedCoefficient * Math.Sqrt(Math.Max(0.5, 1 - (frame.NoiseVariance / Math.Max(frame.IntraError, 0.001))));
                countForward++;
            }

            accumulatedCoefficient = 1.0;
            for (int n = j; n > j - (3 * 2) + countForward && n > firstFrame; n--)
            {
                if (statisticsBase + n < 0)
                {
                    break;
                }

                ref readonly Av1FirstPassStatistics frame = ref this.statistics[statisticsBase + n];
                accumulatedCoefficient *= frame.CorrelationCoefficient;
                thisScore += accumulatedCoefficient * Math.Sqrt(Math.Max(0.5, 1 - (frame.NoiseVariance / Math.Max(frame.IntraError, 0.001))));
            }

            // Slightly relax the condition for videos that start with frozen frames.
            if (thisScore + (staticFrames ? 0.5 : 0) > bestScore)
            {
                bestScore = thisScore;
                bestIndex = j;
            }
        }

        // In a blending area, move one more frame in case the first blending frame was missed.
        int bestRegion = this.FindRegionsIndex(bestIndex + offset);
        if (bestRegion < count - 1 && bestRegion > 0)
        {
            if (this.regions[bestRegion - 1].Type == RegionType.Blending &&
                this.regions[bestRegion + 1].Type == RegionType.Blending)
            {
                if (bestIndex + offset == this.regions[bestRegion].Start && bestIndex + offset < this.regions[bestRegion].Last)
                {
                    bestIndex += 1;
                }
                else if (bestIndex + offset == this.regions[bestRegion].Last && bestIndex + offset > this.regions[bestRegion].Start)
                {
                    bestIndex -= 1;
                }
            }
        }

        if (currentLast - bestIndex < 2)
        {
            bestIndex = currentLast;
        }

        // Without a good candidate the interval keeps its end.
        if (bestIndex > 0 && bestScore > 0.1)
        {
            currentLast = bestIndex;
        }

        return currentLast;
    }

    /// <summary>
    /// Measures the golden frame group of the current interval: its coded error, skip and inactive sums, its
    /// motion, its decay, its static share and its second reference error. Reference: accumulate_gop_stats(),
    /// without the modified error sum that a look-ahead replaces by the group length.
    /// </summary>
    /// <param name="intraOnly">Whether the group starts with a key frame.</param>
    /// <param name="startPosition">The buffer position of the group start.</param>
    /// <param name="groupStatistics">Receives the group measures.</param>
    /// <returns>The golden interval.</returns>
    private int AccumulateGopStatistics(bool intraOnly, int startPosition, out GroupStatistics groupStatistics)
    {
        groupStatistics = GroupStatistics.Create();
        this.statisticsPosition = startPosition;

        // The key frame, or the overlay of the previous alternate reference, is already accounted for.
        // accumulate_this_frame_stats(): the error sums of the frames of the group, which only a bit budget reads.
        int i = intraOnly ? 1 : 0;
        while (this.UsesBitBudget && i < this.goldenIntervals[this.currentGoldenIndex])
        {
            if (!this.InputStatistics(out Av1FirstPassStatistics frame))
            {
                break;
            }

            groupStatistics.RawError += frame.CodedError;
            groupStatistics.SkipPercent += frame.IntraSkipPercent;
            groupStatistics.InactiveZoneRows += frame.InactiveZoneRows;
            ++i;
        }

        this.statisticsPosition = startPosition;
        i = intraOnly ? 1 : 0;
        this.InputStatistics(out _);
        while (i < this.goldenIntervals[this.currentGoldenIndex])
        {
            if (!this.InputStatistics(out Av1FirstPassStatistics next))
            {
                break;
            }

            bool flashDetected = this.DetectFlash(this.statisticsPosition, 0);
            this.AccumulateNextFrameStatistics(next, flashDetected, i, ref groupStatistics);
            ++i;
        }

        i = this.goldenIntervals[this.currentGoldenIndex];

        // average_gf_stats().
        if (i != 0)
        {
            groupStatistics.AverageSecondReferenceCodedError /= i;
        }

        if (groupStatistics.NonZeroDeviationCount != 0)
        {
            groupStatistics.AverageRawErrorDeviation /= groupStatistics.NonZeroDeviationCount;
        }

        return i;
    }

    /// <summary>
    /// Defines the golden frame group of the current interval: whether it uses an alternate reference and internal
    /// alternate references, a one frame length reduction before a short last group, its structure and its boost.
    /// Reference: define_gf_group() in the one-pass look-ahead stage.
    /// </summary>
    /// <param name="finalPass">Whether this is the final definition of the group, not a trial.</param>
    private void DefineGoldenGroup(bool finalPass)
    {
        int startPosition = this.statisticsPosition;
        bool intraOnly = this.framesSinceKey == 0;
        this.internalAltrefAllowed = MaximumPyramidHeight > 1;

        // A key frame group has already reset the group.
        if (!intraOnly)
        {
            this.group.Clear();
            this.groupFrameIndex = 0;
        }

        this.CorrectFramesToKey();
        int i = this.AccumulateGopStatistics(intraOnly, startPosition, out GroupStatistics groupStatistics);

        // Still groups do not use internal alternate references. Reference: MIN_ZERO_MOTION, MAX_SR_CODED_ERROR
        // and MAX_RAW_ERR_VAR.
        if (MinimumPyramidHeight <= 1 &&
            groupStatistics.ZeroMotionAccumulator > 0.95 &&
            groupStatistics.AverageSecondReferenceCodedError < 40 &&
            groupStatistics.AverageRawErrorDeviation < 2000)
        {
            this.internalAltrefAllowed = false;
        }

        bool useAltRef = this.useArfInThisKeyFrameGroup && i < this.lagInFrames && i >= MinimumGoldenInterval;
        this.group.MaxLayerDepthAllowed = useAltRef ? MaximumPyramidHeight : 0;

        // The length reduction suits constant quality at low quantizers and groups without internal references.
        int altOffset = 0;
        bool allowLengthReduction = ((!this.UsesBitBudget && this.cqLevel <= 128) || !this.internalAltrefAllowed) && !this.lossless;
        if (allowLengthReduction && useAltRef)
        {
            // Shorten a long group when only one overlay would be left, or when the next group would be much
            // shorter. Reference: REDUCE_GF_LENGTH_THRESH, REDUCE_GF_LENGTH_TO_KEY_THRESH and REDUCE_GF_LENGTH_BY.
            int nextGoldenLength = this.framesToKey - i;
            bool singleOverlayLeft = nextGoldenLength == 0 && i > 4;
            bool unbalancedGroup = i > 9 && nextGoldenLength + 1 < 9 && nextGoldenLength + 1 >= this.minimumGoldenInterval;
            if (singleOverlayLeft || unbalancedGroup)
            {
                const int rollBack = 1;

                // Shorten only when the minimum interval is still respected.
                if (i - rollBack >= this.minimumGoldenInterval + 1)
                {
                    altOffset = -rollBack;
                    this.goldenIntervals[this.currentGoldenIndex] -= rollBack;
                    i = this.AccumulateGopStatistics(intraOnly, startPosition, out groupStatistics);
                }
            }
        }

        // update_gop_length().
        if (finalPass)
        {
            this.currentGoldenIndex++;
        }

        this.constrainedGoldenGroup = i >= this.framesToKey;
        this.baselineGoldenInterval = i;

        this.SetupGopStructure();
        this.SetGopBoost(i, intraOnly, finalPass, useAltRef, altOffset, startPosition);

        // The rest of set_gop_bits_boost(): the bits of the group.
        this.AllocateGoldenGroupBits(
            finalPass,
            useAltRef,
            groupStatistics.RawError,
            groupStatistics.SkipPercent,
            groupStatistics.InactiveZoneRows);
    }

    /// <summary>
    /// Sets the golden boost of the group, its average over the key frame group, and the reduced alternate
    /// reference boost of the last group of a key frame group.
    /// Reference: the boost part of set_gop_bits_boost() in good-quality mode.
    /// </summary>
    /// <param name="interval">The golden interval.</param>
    /// <param name="intraOnly">Whether the group starts with a key frame.</param>
    /// <param name="finalPass">Whether this is the final definition of the group.</param>
    /// <param name="useAltRef">Whether the group uses an alternate reference.</param>
    /// <param name="altOffset">The offset of the alternate reference boost.</param>
    /// <param name="startPosition">The buffer position of the group start.</param>
    private void SetGopBoost(int interval, bool intraOnly, bool finalPass, bool useAltRef, int altOffset, int startPosition)
    {
        // The average boost of the golden intervals of a new key frame group. Its frame boosts do not scale the
        // largest boost.
        int savedPosition = this.statisticsPosition;
        if (this.framesSinceKey == 0)
        {
            int boostSum = 0;
            int boostCount = 0;
            int accumulated = 0;
            for (int k = 0; k < MaximumGoldenIntervalCount; k++)
            {
                if (this.goldenIntervals[k] == 0)
                {
                    break;
                }

                int newInterval = this.goldenIntervals[k];
                int extendedLength = newInterval - (k == 0 && intraOnly ? 1 : 0);
                if (useAltRef)
                {
                    if (accumulated >= this.framesToKey)
                    {
                        break;
                    }

                    int forwardFrames = this.framesToKey - accumulated - newInterval >= extendedLength
                        ? extendedLength
                        : Math.Max(0, this.framesToKey - accumulated - newInterval);

                    if (k != 0)
                    {
                        this.statisticsPosition += newInterval;
                        if (this.statisticsPosition >= this.statisticsCount)
                        {
                            this.statisticsPosition = this.statisticsCount;
                        }
                    }

                    boostSum += this.CalculateArfBoost(this.statisticsPosition, altOffset, forwardFrames, extendedLength, true, false);
                }

                boostCount++;
                accumulated += newInterval;
            }

            this.averageGoldenBoost = boostSum / boostCount;
        }

        this.statisticsPosition = savedPosition;

        // The boost of the alternate reference, or of the golden frame of a group without one.
        int extendedInterval = interval - (intraOnly ? 1 : 0);
        if (useAltRef)
        {
            int forwardFrames = this.framesToKey - interval >= extendedInterval ? extendedInterval : Math.Max(0, this.framesToKey - interval);
            this.goldenBoost = this.CalculateArfBoost(this.statisticsPosition, altOffset, forwardFrames, extendedInterval, true, true);
        }
        else
        {
            this.statisticsPosition = startPosition;
            this.goldenBoost = Math.Min(MaximumGoldenBoost, this.CalculateArfBoost(this.statisticsPosition, altOffset, extendedInterval, 0, true, true));
        }

        // The alternate reference of the last group of a key frame group has a reduced boost.
        // Reference: LAST_ALR_BOOST_FACTOR, a float constant.
        this.arfBoostFactor = 1.0;
        if (useAltRef && !this.lossless)
        {
            if (this.framesToKey - extendedInterval == 1 || this.framesToKey - extendedInterval == 0)
            {
                this.arfBoostFactor = 0.2f;
            }
        }

        this.statisticsPosition = startPosition;
        if (finalPass)
        {
            this.arfGoldenBoostLast = useAltRef;
        }
    }

    /// <summary>
    /// Returns the lowest quantizer of an inter frame: for leaves and overlays the constant-quality level, or under
    /// a bit budget the inter floor of the highest quantizer; the golden floor for golden frames and alternate
    /// references; and for internal alternate references the alternate reference quantizer, or the golden floor in
    /// the variable-bitrate mode, moved halfway to the highest quantizer per layer. The constrained-quality mode keeps
    /// every floor at or above its level and lowers the golden floor slightly.
    /// Reference: get_active_best_quality().
    /// </summary>
    /// <param name="activeWorst">The highest quantizer.</param>
    /// <param name="groupIndex">The frame's index in the group.</param>
    /// <param name="constantQualityLevel">The active constant-quality level. Reference: get_active_cq_level().</param>
    /// <returns>The lowest quantizer.</returns>
    private int GetActiveBestQuality(int activeWorst, int groupIndex, int constantQualityLevel)
    {
        Av1FrameUpdateType updateType = this.group.UpdateTypes[groupIndex];
        bool intermediateArf = updateType == Av1FrameUpdateType.IntermediateAlternate;
        bool leafFrame = !(updateType == Av1FrameUpdateType.Alternate || updateType == Av1FrameUpdateType.Golden || intermediateArf);
        bool overlayFrame = updateType is Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay;
        bool constrainedQuality = this.mode == Av1RateControlMode.ConstrainedQuality;
        int shortSide = Math.Min(this.width, this.height);
        int resolutionIndex = (shortSide >= 480 ? 1 : 0) + (shortSide >= 608 ? 1 : 0);
        if (leafFrame || overlayFrame)
        {
            if (!this.UsesBitBudget)
            {
                return constantQualityLevel;
            }

            int interQuality = Av1ConstantQuality.GetInterActiveQuality(activeWorst, resolutionIndex > 1, this.bitDepth);
            return constrainedQuality ? Math.Max(interQuality, constantQualityLevel) : interQuality;
        }

        // The lower of the active worst quality and the recent average sets the golden floor, unless the last
        // frame was a key frame.
        int q = activeWorst;
        if (this.framesSinceKey > 1 && this.averageInterFrameQIndex < activeWorst)
        {
            q = this.averageInterFrameQIndex;
        }

        if (constrainedQuality && q < constantQualityLevel)
        {
            q = constantQualityLevel;
        }

        int activeBestQuality = Av1ConstantQuality.GetGoldenActiveQuality(q, this.goldenBoost, this.averageGoldenBoost, resolutionIndex, this.bitDepth);

        // The constrained-quality mode uses a slightly lower floor.
        if (constrainedQuality)
        {
            activeBestQuality = activeBestQuality * 15 / 16;
        }

        int minimumBoost = Av1ConstantQuality.GetGoldenHighMotionQuality(q, resolutionIndex > 1, this.bitDepth);
        int boost = minimumBoost - activeBestQuality;
        activeBestQuality = minimumBoost - (int)(boost * this.arfBoostFactor);
        if (!intermediateArf)
        {
            return activeBestQuality;
        }

        if (this.mode is Av1RateControlMode.Quality or Av1RateControlMode.ConstrainedQuality)
        {
            activeBestQuality = this.arfQ;
        }

        int thisHeight = this.group.LayerDepths[groupIndex];
        while (thisHeight > 1)
        {
            activeBestQuality = (activeBestQuality + activeWorst + 1) / 2;
            --thisHeight;
        }

        return activeBestQuality;
    }

    /// <summary>
    /// The measures of a golden frame group. Reference: GF_GROUP_STATS.
    /// </summary>
    private struct GroupStatistics
    {
        /// <summary>Reference: gf_group_raw_error.</summary>
        public double RawError;

        /// <summary>Reference: gf_group_skip_pct.</summary>
        public double SkipPercent;

        /// <summary>Reference: gf_group_inactive_zone_rows.</summary>
        public double InactiveZoneRows;

        /// <summary>Reference: mv_ratio_accumulator.</summary>
        public double MotionRatioAccumulator;

        /// <summary>Reference: decay_accumulator.</summary>
        public double DecayAccumulator;

        /// <summary>Reference: zero_motion_accumulator.</summary>
        public double ZeroMotionAccumulator;

        /// <summary>Reference: loop_decay_rate.</summary>
        public double LoopDecayRate;

        /// <summary>Reference: last_loop_decay_rate.</summary>
        public double LastLoopDecayRate;

        /// <summary>Reference: this_frame_mv_in_out.</summary>
        public double ThisFrameMotionInOut;

        /// <summary>Reference: mv_in_out_accumulator.</summary>
        public double MotionInOutAccumulator;

        /// <summary>Reference: abs_mv_in_out_accumulator.</summary>
        public double AbsoluteMotionInOutAccumulator;

        /// <summary>Reference: avg_sr_coded_error.</summary>
        public double AverageSecondReferenceCodedError;

        /// <summary>Reference: avg_raw_err_stdev.</summary>
        public double AverageRawErrorDeviation;

        /// <summary>Reference: non_zero_stdev_count.</summary>
        public int NonZeroDeviationCount;

        /// <summary>
        /// Returns the initial measures. Reference: init_gf_stats().
        /// </summary>
        /// <returns>The measures.</returns>
        public static GroupStatistics Create() => new()
        {
            DecayAccumulator = 1.0,
            ZeroMotionAccumulator = 1.0,
            LoopDecayRate = 1.0,
            LastLoopDecayRate = 1.0
        };
    }
}

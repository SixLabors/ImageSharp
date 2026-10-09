// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// Key frame placement and the key frame boost.
/// </content>
internal sealed partial class Av1SecondPass
{
    /// <summary>
    /// The number of recent frames whose prediction decay the key frame search multiplies.
    /// </summary>
    private const int FramesToCheckDecay = 8;

    /// <summary>
    /// The lower bound of the largest boost of one frame of a key frame group without a bit budget.
    /// </summary>
    private const double KeyFrameMinimumFrameBoost = 80.0;

    /// <summary>
    /// The largest boost of one frame of a key frame group.
    /// </summary>
    private const double KeyFrameMaximumFrameBoost = 128.0;

    /// <summary>
    /// The smallest boost of a key frame group that is not static.
    /// </summary>
    private const int MinimumKeyFrameBoost = 600;

    /// <summary>
    /// The smallest boost of a static key frame group.
    /// </summary>
    private const int MinimumStaticKeyFrameBoost = 5400;

    /// <summary>
    /// The zero motion share above which a key frame group is static.
    /// </summary>
    private const double StaticKeyFrameGroupThreshold = 0.99;

    /// <summary>
    /// Starts a key frame group at the current frame: finds the next key frame, the key frame boost and the bits of the group and of the
    /// key frame.
    /// </summary>
    /// <param name="thisFrame">The statistics of the key frame.</param>
    private void FindNextKeyFrame(Av1FirstPassStatistics thisFrame)
    {
        this.framesSinceKey = 0;
        this.useArfInThisKeyFrameGroup = this.lagInFrames >= AlternateReferenceMinimumLag;
        this.group.Clear();
        this.groupFrameIndex = 0;

        int startPosition = this.statisticsPosition;
        double zeroMotionAccumulator = 1.0;
        double secondReferenceAccumulator = 0.0;
        double keyFrameRawError = thisFrame.IntraError;

        this.thisKeyFrameForced = this.nextKeyFrameForced;

        // The current frame is a key frame, so the search for the next one starts at the following frame.
        int framesToKeyFrame = this.DefineKeyFrameInterval(this.keyFrameMaximumDistance, 1);
        this.framesToKey = framesToKeyFrame != -1 ? Math.Min(this.keyFrameMaximumDistance, framesToKeyFrame) : this.keyFrameMaximumDistance;
        this.CorrectFramesToKey();

        // An automatic interval between one and two maximum distances centres the extra key frame.
        if (this.framesToKey > this.keyFrameMaximumDistance)
        {
            this.framesToKey /= 2;
            this.nextKeyFrameForced = true;
        }
        else
        {
            this.nextKeyFrameForced = this.framesToKey >= this.keyFrameMaximumDistance;
        }

        // Only count the statistics of the key frame group. The error range of the look-ahead is empty, so every error term is zero and
        // later code reads only the count.
        for (int i = 0; i < this.framesToKey; ++i)
        {
            if (this.statisticsInfo.Contains(i))
            {
                ++this.statisticsUsedForKeyFrameBoost;
            }
        }

        this.statisticsPosition = startPosition;
        double boostScore = this.GetKeyFrameBoostScore(keyFrameRawError, ref zeroMotionAccumulator, ref secondReferenceAccumulator, false);
        this.statisticsPosition = startPosition;
        this.keyFrameZeroMotionPercent = (int)(zeroMotionAccumulator * 100.0);

        this.keyFrameBoost = (int)boostScore;
        if (this.UsesBitBudget)
        {
            // The frames of the group beyond the look-ahead add their boost at the average statistics.
            boostScore = this.GetKeyFrameBoostScore(keyFrameRawError, ref zeroMotionAccumulator, ref secondReferenceAccumulator, true);
            this.statisticsPosition = startPosition;
            this.keyFrameBoost += (int)boostScore;
        }
        else
        {
            this.keyFrameBoost = this.GetProjectedKeyFrameBoost();
        }

        // Static content keeps a large boost unless the group is very short.
        if (zeroMotionAccumulator > StaticKeyFrameGroupThreshold && this.framesToKey > 8)
        {
            this.keyFrameBoost = Math.Max(this.keyFrameBoost, MinimumStaticKeyFrameBoost);
        }
        else
        {
            this.keyFrameBoost = Math.Max(this.keyFrameBoost, this.framesToKey * 3);
            this.keyFrameBoost = Math.Max(this.keyFrameBoost, MinimumKeyFrameBoost);
        }

        this.AllocateKeyFrameGroupBits();
        this.group.UpdateTypes[0] = Av1FrameUpdateType.Key;
    }

    /// <summary>
    /// Returns the number of frames to the next key frame from the scene cuts and the still transitions in the look-ahead, or -1 when the
    /// look-ahead finds neither.
    /// </summary>
    /// <param name="framesToDetect">The number of frames to search.</param>
    /// <param name="searchStart">The first frame to test, 1 when the current frame is a key frame.</param>
    /// <returns>The frames to the next key frame, or -1.</returns>
    private int DefineKeyFrameInterval(int framesToDetect, int searchStart)
    {
        Span<double> recentLoopDecay = stackalloc double[FramesToCheckDecay];
        int framesToKeyFrame = searchStart;
        int framesSinceKeyFrame = this.framesSinceKey + 1;
        bool sceneCutDetected = false;

        if (framesToDetect == 0)
        {
            return this.framesToKey;
        }

        recentLoopDecay.Fill(1.0);
        int i = 0;
        int futureCount = this.statisticsInfo.GetFutureCount(0);
        while (framesToKeyFrame < futureCount && framesToKeyFrame < framesToDetect)
        {
            // Provided that the look-ahead has the next frame.
            if (this.sceneCutDetection > 0 && framesToKeyFrame + 1 < futureCount)
            {
                // Check for a scene cut.
                if (framesSinceKeyFrame >= KeyFrameMinimumDistance)
                {
                    sceneCutDetected = this.TestCandidateKeyFrame(framesToKeyFrame, framesSinceKeyFrame);
                    if (sceneCutDetected)
                    {
                        // A cut is kept only when a frame of the next 32 predicts much better from the long term reference than from the
                        // previous frame.
                        bool testNextGop = false;
                        for (int j = 0; j < 32; ++j)
                        {
                            if (!this.statisticsInfo.Contains(framesToKeyFrame + j))
                            {
                                continue;
                            }

                            ref readonly Av1FirstPassStatistics next = ref this.statisticsInfo.Peek(framesToKeyFrame + j);
                            if (this.frameNumber + framesToKeyFrame + j > 2 && next.LongTermCodedError * 2.5 < next.CodedError)
                            {
                                testNextGop = true;
                            }
                        }

                        if (!testNextGop)
                        {
                            break;
                        }
                    }
                }

                // Multiply the prediction decay rates of the recent frames.
                double loopDecayRate = GetPredictionDecayRate(this.statisticsInfo.Peek(framesToKeyFrame + 1));
                recentLoopDecay[i % FramesToCheckDecay] = loopDecayRate;
                double decayAccumulator = 1.0;
                for (int j = 0; j < FramesToCheckDecay; ++j)
                {
                    decayAccumulator *= recentLoopDecay[j];
                }

                // A transition or high motion followed by a static scene makes the key frame a good predictor for the frames after it, so the
                // group does not use an alternate reference.
                if (framesSinceKeyFrame >= KeyFrameMinimumDistance)
                {
                    sceneCutDetected = this.DetectTransitionToStill(
                        framesToKeyFrame + 1,
                        i,
                        this.keyFrameMaximumDistance - i,
                        loopDecayRate,
                        decayAccumulator);

                    if (sceneCutDetected)
                    {
                        this.useArfInThisKeyFrameGroup = false;
                        break;
                    }
                }

                ++framesToKeyFrame;
                ++framesSinceKeyFrame;

                // Without a real key frame within two maximum distances the search ends.
                if (framesToKeyFrame >= 2 * this.keyFrameMaximumDistance)
                {
                    break;
                }
            }
            else
            {
                ++framesToKeyFrame;
                ++framesSinceKeyFrame;
            }

            ++i;
        }

        // A look-ahead that finds no cut leaves the interval to the forced key frames, of which there are none.
        if (!sceneCutDetected)
        {
            framesToKeyFrame = -1;
        }

        return framesToKeyFrame;
    }

    /// <summary>
    /// Tests whether a frame is a scene cut: it must look like an intra frame, not like a flash, and predict the frames after it well.
    /// </summary>
    /// <param name="index">The candidate's offset from the current frame.</param>
    /// <param name="frameCountSoFar">The number of frames since the last key frame.</param>
    /// <returns>Whether the candidate is a viable key frame.</returns>
    private bool TestCandidateKeyFrame(int index, int frameCountSoFar)
    {
        if (!this.statisticsInfo.Contains(index - 1) || !this.statisticsInfo.Contains(index) || !this.statisticsInfo.Contains(index + 1))
        {
            return false;
        }

        ref readonly Av1FirstPassStatistics last = ref this.statisticsInfo.Peek(index - 1);
        ref readonly Av1FirstPassStatistics candidate = ref this.statisticsInfo.Peek(index);
        ref readonly Av1FirstPassStatistics next = ref this.statisticsInfo.Peek(index + 1);

        double percentIntra = 1.0 - candidate.PercentInter;
        double modifiedPercentInter = candidate.PercentInter - candidate.PercentNeutral;
        double secondReferenceUsageThreshold = GetSecondReferenceUsageThreshold(frameCountSoFar);
        int framesToTest = SceneCutKeyTestInterval;
        int countForTolerablePrediction = 3;

        // The candidate itself is not counted.
        int statisticsAfter = this.statisticsInfo.GetFutureCount(index) - 1;
        if (this.sceneCutDetection == 1)
        {
            if (statisticsAfter < 3)
            {
                return false;
            }

            framesToTest = 3;
            countForTolerablePrediction = 1;
        }

        framesToTest = Math.Min(framesToTest, statisticsAfter);

        // The candidate must satisfy the primary criteria of a key frame. Constant quality needs three frames since the last key frame.
        // The candidate and the next frame must use little second reference. The candidate must also have almost no inter blocks, be a
        // slide transition, or have a high intra share with a low intra to inter ratio. That last case also needs a large change in error
        // or a high intra to inter ratio in the next frame.
        if (frameCountSoFar >= 3 &&
            candidate.PercentSecondReference < secondReferenceUsageThreshold &&
            next.PercentSecondReference < secondReferenceUsageThreshold &&
            (candidate.PercentInter < 0.05 ||
             IsSlideTransition(candidate, last, next) ||
             (percentIntra > 0.25 &&
              percentIntra > 2.0 * modifiedPercentInter &&
              candidate.IntraError / DoubleDivideCheck(candidate.CodedError) < 1.9 &&
              (Math.Abs(last.CodedError - candidate.CodedError) / DoubleDivideCheck(candidate.CodedError) > 0.4 ||
               Math.Abs(last.IntraError - candidate.IntraError) / DoubleDivideCheck(candidate.IntraError) > 0.4 ||
               next.IntraError / DoubleDivideCheck(next.CodedError) > 3.5))))
        {
            double boostScore = 0.0;
            double oldBoostScore = 0.0;
            double decayAccumulator = 1.0;

            // Examine how well the key frame predicts the frames after it.
            int i;
            for (i = 1; i <= framesToTest; ++i)
            {
                ref readonly Av1FirstPassStatistics local = ref this.statisticsInfo.Peek(index + i);
                if ((local.IntraError - candidate.IntraError) / DoubleDivideCheck(candidate.IntraError) > 0.1 &&
                    candidate.CodedError > local.CodedError * 6)
                {
                    break;
                }

                // The intra to inter error ratio, scaled by 12.5 and limited to 128.
                double nextIntraInterRatio = 12.5 * local.IntraError / DoubleDivideCheck(local.CodedError);
                if (nextIntraInterRatio > 128.0)
                {
                    nextIntraInterRatio = 128.0;
                }

                // The cumulative effect of the decay in prediction quality.
                if (local.PercentInter > 0.85)
                {
                    decayAccumulator *= local.PercentInter;
                }
                else
                {
                    decayAccumulator *= (0.85 + local.PercentInter) / 2.0;
                }

                boostScore += decayAccumulator * nextIntraInterRatio;

                // Test the breakout clauses.
                if (local.PercentInter < 0.05 ||
                    nextIntraInterRatio < 1.5 ||
                    (local.PercentInter - local.PercentNeutral < 0.20 && nextIntraInterRatio < 3.0) ||
                    boostScore - oldBoostScore < 3.0 ||
                    local.IntraError < 200.0 / this.macroblockCount)
                {
                    break;
                }

                oldBoostScore = boostScore;
            }

            // Tolerable prediction for at least the next three frames, or the next frame when `sceneCutDetection` is 1, makes the candidate viable.
            return boostScore > 30.0 && i > countForTolerablePrediction;
        }

        return false;
    }

    /// <summary>
    /// Returns the second reference usage above which a candidate looks like a flash or an occlusion. The threshold rises over the first 32
    /// frames of a key frame group.
    /// </summary>
    /// <param name="frameCountSoFar">The number of frames since the last key frame.</param>
    /// <returns>The threshold.</returns>
    private static double GetSecondReferenceUsageThreshold(int frameCountSoFar)
    {
        const int adaptUpTo = 32;
        const double minimumThreshold = 0.085;
        const double maximumDelta = 0.035;
        if (frameCountSoFar >= adaptUpTo)
        {
            return minimumThreshold + maximumDelta;
        }

        return minimumThreshold + ((double)frameCountSoFar / (adaptUpTo - 1) * maximumDelta);
    }

    /// <summary>
    /// Tests for a slide show transition: a single frame whose error spikes against low error either side, with similar intra and inter error.
    /// </summary>
    /// <param name="candidate">The candidate frame.</param>
    /// <param name="last">The frame before it.</param>
    /// <param name="next">The frame after it.</param>
    /// <returns>Whether the candidate is a slide transition.</returns>
    private static bool IsSlideTransition(Av1FirstPassStatistics candidate, Av1FirstPassStatistics last, Av1FirstPassStatistics next)
        => candidate.IntraError < candidate.CodedError * 1.5 &&
           candidate.CodedError > last.CodedError * 5.0 &&
           candidate.CodedError > next.CodedError * 5.0;

    /// <summary>
    /// Returns the key frame boost of the frames after the key frame, weighted by their share of static blocks, and the zero motion share of
    /// the group. With averaged statistics, only the frames of the group beyond the look-ahead count, each with the average statistics of the
    /// frames the look-ahead holds.
    /// </summary>
    /// <param name="keyFrameRawError">The key frame's intra error.</param>
    /// <param name="zeroMotionAccumulator">The smallest zero motion share so far.</param>
    /// <param name="secondReferenceAccumulator">The accumulated growth of the second reference error.</param>
    /// <param name="useAverage">Whether to score the frames beyond the look-ahead with the average statistics.</param>
    /// <returns>The boost score.</returns>
    private double GetKeyFrameBoostScore(double keyFrameRawError, ref double zeroMotionAccumulator, ref double secondReferenceAccumulator, bool useAverage)
    {
        double boostScore = 0.0;

        // A bit budget lets every frame boost the key frame up to the largest frame boost.
        double keyFrameMaximumBoost = this.UsesBitBudget
            ? KeyFrameMaximumFrameBoost
            : Math.Clamp(this.framesToKey * 2.0, KeyFrameMinimumFrameBoost, KeyFrameMaximumFrameBoost);

        Av1FirstPassStatistics frame = default;
        int firstFrame = useAverage ? this.GetAverageStatistics(ref frame) : 0;
        for (int i = firstFrame; i < this.framesToKey - 1; ++i)
        {
            if (!useAverage && !this.InputStatistics(out frame))
            {
                break;
            }

            // Monitor for static sections. The second reference of the first frame of the group is invalid.
            if (i > 0)
            {
                zeroMotionAccumulator = Math.Min(zeroMotionAccumulator, GetZeroMotionFactor(frame));
            }
            else
            {
                zeroMotionAccumulator = frame.PercentInter - frame.PercentMotion;
            }

            // A frame counts toward the boost while the second reference error growth stays below 1.5 times the key frame error, up to twice
            // the largest golden interval.
            if (secondReferenceAccumulator < keyFrameRawError * 1.50 && i <= this.maximumGoldenInterval * 2)
            {
                // A factor of 0.75 to 1.25 from the static share of the frame.
                double zeroMotionFactor = 0.75 + (zeroMotionAccumulator / 2.0);
                if (i < 2)
                {
                    secondReferenceAccumulator = 0.0;
                }

                double frameBoost = this.CalculateKeyFrameFrameBoost(frame, ref secondReferenceAccumulator, keyFrameMaximumBoost);
                boostScore += frameBoost * zeroMotionFactor;
            }
        }

        return boostScore;
    }

    /// <summary>
    /// Reads the statistics of the frames of the key frame group after the key frame that the look-ahead holds and averages them when there
    /// are at least two.
    /// </summary>
    /// <param name="average">Receives the sum of the statistics, averaged when at least two frames are read.</param>
    /// <returns>The number of frames read.</returns>
    private int GetAverageStatistics(ref Av1FirstPassStatistics average)
    {
        int frameCount;
        for (frameCount = 0; frameCount < this.framesToKey - 1; ++frameCount)
        {
            if (!this.InputStatistics(out Av1FirstPassStatistics frame))
            {
                break;
            }

            Av1FirstPassStatisticsAccumulator.Accumulate(ref average, frame);
        }

        if (frameCount < 2)
        {
            return frameCount;
        }

        // Every field the boost reads is averaged, as are the other summed fields.
        average.Weight /= frameCount;
        average.IntraError /= frameCount;
        average.FrameAverageWaveletEnergy /= frameCount;
        average.CodedError /= frameCount;
        average.SecondReferenceCodedError /= frameCount;
        average.PercentInter /= frameCount;
        average.PercentMotion /= frameCount;
        average.PercentSecondReference /= frameCount;
        average.PercentNeutral /= frameCount;
        average.IntraSkipPercent /= frameCount;
        average.InactiveZoneRows /= frameCount;
        average.InactiveZoneColumns /= frameCount;
        average.MotionVectorRow /= frameCount;
        average.MotionVectorRowAbsolute /= frameCount;
        average.MotionVectorColumn /= frameCount;
        average.MotionVectorColumnAbsolute /= frameCount;
        average.MotionVectorRowVariance /= frameCount;
        average.MotionVectorColumnVariance /= frameCount;
        average.MotionVectorInOutCount /= frameCount;
        average.NewMotionVectorCount /= frameCount;
        average.Count /= frameCount;
        average.Duration /= frameCount;
        return frameCount;
    }

    /// <summary>
    /// Returns the key frame boost of one frame from its intra to inter error ratio, with the accumulated growth of the second reference error
    /// added to its inter error.
    /// </summary>
    /// <param name="frame">The frame's statistics.</param>
    /// <param name="secondReferenceAccumulator">The accumulated growth of the second reference error.</param>
    /// <param name="maximumBoost">The largest boost of one frame.</param>
    /// <returns>The boost.</returns>
    private double CalculateKeyFrameFrameBoost(Av1FirstPassStatistics frame, ref double secondReferenceAccumulator, double maximumBoost)
    {
        double lastQ = Av1ConstantQuality.ConvertQIndexToQ(this.averageInterFrameQIndex, this.bitDepth);
        double boostQCorrection = Math.Min(0.50 + (lastQ * 0.015), 2.00);
        double activeArea = this.CalculateActiveArea(frame);

        // The underlying boost is the ratio of intra to inter error.
        double frameBoost = Math.Max(this.BaselineErrorPerMacroblock * activeArea, frame.IntraError * activeArea) /
            DoubleDivideCheck((frame.CodedError + secondReferenceAccumulator) * activeArea);

        // The accumulator tracks how much the coded error grows over time.
        secondReferenceAccumulator += frame.SecondReferenceCodedError - frame.CodedError;
        secondReferenceAccumulator = Math.Max(0.0, secondReferenceAccumulator);

        // 40 is the experimentally derived minimum, in line with the minimum per frame alternate reference boost.
        frameBoost = (frameBoost + 40.0) * boostQCorrection;
        return Math.Min(frameBoost, maximumBoost * boostQCorrection);
    }

    /// <summary>
    /// Scales the key frame boost from the frames the look-ahead covered to the whole key frame group.
    /// </summary>
    /// <returns>The projected boost.</returns>
    private int GetProjectedKeyFrameBoost()
    {
        if (this.statisticsUsedForKeyFrameBoost >= this.framesToKey)
        {
            return this.keyFrameBoost;
        }

        double tplFactor = Av1ConstantQuality.GetKeyFrameBoostProjectionFactor(this.framesToKey);
        double tplFactorUsed = Av1ConstantQuality.GetKeyFrameBoostProjectionFactor(this.statisticsUsedForKeyFrameBoost);
        return (int)Math.Round(tplFactor * this.keyFrameBoost / tplFactorUsed, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Sets the quantizer bounds of an intra frame. Without a bit budget, the only frame of its key frame group codes at the constant-quality level.
    /// </summary>
    /// <param name="activeBest">Receives the lowest quantizer.</param>
    /// <param name="activeWorst">The highest quantizer. Receives the constant-quality level for the only frame of a key frame group.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    private void GetIntraQAndBounds(ref int activeBest, ref int activeWorst, bool screenContent)
    {
        int activeBestQuality;
        int activeWorstQuality = activeWorst;
        bool largeResolution = Math.Min(this.width, this.height) >= 608;
        if (this.framesToKey <= 1 && !this.UsesBitBudget)
        {
            // The only frame of its key frame group codes at the constant-quality level.
            activeBestQuality = this.cqLevel;
            activeWorstQuality = this.cqLevel;
        }
        else if (this.thisKeyFrameForced)
        {
            // A key frame forced by the interval stays near the last boosted quantizer to limit popping.
            int qIndex = this.lastBoostedQIndex;
            double lastBoostedQ = Av1ConstantQuality.ConvertQIndexToQ(qIndex, this.bitDepth);
            int deltaQIndex = Av1ConstantQuality.ComputeQDelta(lastBoostedQ, lastBoostedQ * 0.50, this.bitDepth, this.bestQuality, this.worstQuality);
            activeBestQuality = Math.Max(qIndex + deltaQIndex, this.bestQuality);
        }
        else
        {
            // The floor of the active worst quality at the key frame boost.
            double qAdjustmentFactor = 1.0;
            activeBestQuality = Av1ConstantQuality.GetKeyFrameActiveQuality(activeWorstQuality, this.keyFrameBoost, largeResolution, this.bitDepth);
            if (screenContent)
            {
                activeBestQuality /= 2;
            }

            // Small formats allow a somewhat lower key frame quantizer.
            if (this.width * this.height <= 352 * 288)
            {
                qAdjustmentFactor -= 0.25;
            }

            double qValue = Av1ConstantQuality.ConvertQIndexToQ(activeBestQuality, this.bitDepth);
            activeBestQuality += Av1ConstantQuality.ComputeQDelta(qValue, qValue * qAdjustmentFactor, this.bitDepth, this.bestQuality, this.worstQuality);
        }

        activeBest = activeBestQuality;
        activeWorst = activeWorstQuality;
    }
}

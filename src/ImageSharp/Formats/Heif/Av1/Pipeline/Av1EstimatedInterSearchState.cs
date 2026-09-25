// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Retains prediction estimates and motion choices for one real-time inter block search.
/// </summary>
internal struct Av1EstimatedInterSearchState
{
    /// <summary>
    /// Per-mode motion vectors indexed first by prediction mode and then by reference slot.
    /// </summary>
    public InlineArray25<InlineArray8<Av1MotionVector>> MotionVectors;

    /// <summary>
    /// Vectors retained when a candidate wins, before later compound candidates reuse its mode entries.
    /// </summary>
    public InlineArray25<InlineArray8<Av1MotionVector>> WinningMotionVectors;

    /// <summary>
    /// Evaluated-mode flags indexed by prediction mode and reference slot.
    /// </summary>
    public InlineArray25<InlineArray8<byte>> EvaluatedModes;

    /// <summary>
    /// Normalized luma variances for nearest, near, global, and new motion in each reference slot.
    /// </summary>
    public InlineArray4<InlineArray8<uint>> Variances;

    /// <summary>
    /// Modeled chroma distortion for each single-reference mode and slot.
    /// </summary>
    public InlineArray4<InlineArray8<long>> ChromaDistortions;

    /// <summary>
    /// Single-reference mode syntax costs in 1/512-bit units.
    /// </summary>
    public InlineArray4<InlineArray8<int>> ModeCosts;

    /// <summary>
    /// Best spatial-predictor SAD for each reference.
    /// </summary>
    public InlineArray8<int> PredictorSad;

    /// <summary>
    /// Nearest spatial-predictor SAD for each reference.
    /// </summary>
    public InlineArray8<int> NearestSad;

    /// <summary>
    /// Near spatial-predictor SAD for each reference.
    /// </summary>
    public InlineArray8<int> NearSad;

    /// <summary>
    /// Reference-selection syntax costs, including the intra entry.
    /// </summary>
    public InlineArray8<int> ReferenceCosts;

    /// <summary>
    /// Reference slots admitted by the current frame and block policy.
    /// </summary>
    public InlineArray8<bool> UseReference;

    /// <summary>
    /// Gets or sets the retained candidate's combined rate and distortion.
    /// </summary>
    public Av1RateDistortionStatistics BestStatistics { get; set; }

    /// <summary>
    /// Gets or sets the retained luma prediction error before transform scaling.
    /// </summary>
    public long BestSquaredError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the winner skipped without replacing a coded residual estimate.
    /// </summary>
    public bool BestInitialSkip { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the winner ended residual estimation before transform evaluation.
    /// </summary>
    public bool BestEarlyTermination { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a completed candidate evaluated the retained superblock displacement.
    /// </summary>
    public bool SuperblockMotionTested { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the inter search has met its early termination condition.
    /// </summary>
    public bool EndSearch { get; set; }

    /// <summary>
    /// Gets or sets the smallest modeled chroma distortion among completed inter candidates.
    /// </summary>
    public long MinimumChromaDistortion { get; set; }

    /// <summary>
    /// Initializes validity and comparison state for the next block.
    /// </summary>
    /// <param name="writer">The current tile symbol costs.</param>
    /// <param name="intraInterContext">The neighboring intra/inter context.</param>
    /// <param name="compoundTypeContext">The neighboring compound-reference type context.</param>
    /// <param name="selectReferenceMode">Whether this block can select single or compound prediction.</param>
    public void Reset(Av1SymbolEncoder writer, int intraInterContext, int compoundTypeContext, bool selectReferenceMode)
    {
        // Motion and syntax entries are filled only for admitted references. Clear their validity,
        // not their unused payloads; a rejected or unavailable reference must never consume an entry.
        this.EvaluatedModes[..].Clear();
        this.UseReference[..].Clear();
        this.PredictorSad[..].Fill(int.MaxValue);
        this.NearestSad[..].Fill(int.MaxValue);
        this.NearSad[..].Fill(int.MaxValue);
        for (int mode = 0; mode < 4; mode++)
        {
            this.Variances[mode][..].Fill(uint.MaxValue);
            this.ChromaDistortions[mode][..].Fill(long.MaxValue);
        }

        this.BestStatistics = Av1RateDistortionStatistics.Invalid;
        this.BestSquaredError = long.MaxValue;
        this.BestInitialSkip = false;
        this.BestEarlyTermination = false;
        this.SuperblockMotionTested = false;
        this.EndSearch = false;
        this.MinimumChromaDistortion = long.MaxValue;
        Av1ModeCosts costs = writer.ModeCosts;
        this.ReferenceCosts[(int)Av1ReferenceFrameType.Intra] = writer.GetIsInterCost(false, intraInterContext);
        int baseCost = writer.GetIsInterCost(true, intraInterContext);
        if (selectReferenceMode)
        {
            baseCost += costs.GetCompoundReferenceType(compoundTypeContext, 1);
        }

        // Estimated reference costs share three branches. The mode search deliberately retains
        // these estimates until publication rather than mixing them with the complete syntax tree.
        int lastCost = baseCost + costs.GetSingleReference(0, 0, 0);
        int goldenCost = baseCost + costs.GetSingleReference(0, 0, 1) + costs.GetSingleReference(0, 1, 0);
        int alternateCost = baseCost + costs.GetSingleReference(0, 0, 1) + costs.GetSingleReference(0, 2, 0);
        this.ReferenceCosts[(int)Av1ReferenceFrameType.Last] = lastCost;
        this.ReferenceCosts[(int)Av1ReferenceFrameType.Last2] = lastCost;
        this.ReferenceCosts[(int)Av1ReferenceFrameType.Last3] = lastCost;
        this.ReferenceCosts[(int)Av1ReferenceFrameType.Golden] = goldenCost;
        this.ReferenceCosts[(int)Av1ReferenceFrameType.Backward] = goldenCost;
        this.ReferenceCosts[(int)Av1ReferenceFrameType.Alternate2] = alternateCost;
        this.ReferenceCosts[(int)Av1ReferenceFrameType.Alternate] = alternateCost;
    }

    /// <summary>
    /// Rejects references or near motion using their spatial prediction errors.
    /// </summary>
    /// <param name="mode">The single-reference mode.</param>
    /// <param name="reference">The reference slot.</param>
    /// <param name="referencePruning">The reference pruning level.</param>
    /// <returns>Whether the candidate can be omitted.</returns>
    public readonly bool SkipByPredictorSad(Av1PredictionMode mode, Av1ReferenceFrameType reference, int referencePruning)
    {
        int slot = (int)reference;
        if (referencePruning > 0 && reference != Av1ReferenceFrameType.Last && this.PredictorSad[slot] != int.MaxValue)
        {
            int lastSad = this.PredictorSad[(int)Av1ReferenceFrameType.Last];
            long threshold = lastSad == int.MaxValue ? long.MaxValue
                : ((long)lastSad * 2) + (referencePruning == 1 ? lastSad >> 2 : 0);

            if (this.PredictorSad[slot] > threshold)
            {
                return true;
            }
        }

        return mode == Av1PredictionMode.NearMotionVector
            && this.NearSad[slot] != int.MaxValue
            && this.NearSad[slot] > unchecked(this.NearestSad[slot] << 1);
    }

    /// <summary>
    /// Adds single-reference syntax costs and records a completed candidate.
    /// </summary>
    /// <param name="writer">The current symbol-cost state.</param>
    /// <param name="referenceVectors">The candidate reference-vector context.</param>
    /// <param name="rateMultiplier">The block rate-distortion multiplier.</param>
    /// <param name="mode">The searched mode, replaced when equivalent global syntax costs less.</param>
    /// <param name="reference">The reference slot.</param>
    /// <param name="motionRate">The searched vector's coding rate.</param>
    /// <param name="variance">The normalized luma variance.</param>
    /// <param name="squaredError">The normalized luma squared error.</param>
    /// <param name="chromaDistortion">The modeled chroma distortion, or its unavailable sentinel.</param>
    /// <param name="statistics">The candidate's residual estimate, updated with syntax costs.</param>
    /// <param name="checkGlobalMotion">Whether subsequent candidates still need an explicit global-motion trial.</param>
    /// <returns>Whether the candidate replaced the current winner.</returns>
    public bool CompleteSingleCandidate(
        Av1SymbolEncoder writer,
        in Av1ReferenceMotionVectors referenceVectors,
        int rateMultiplier,
        ref Av1PredictionMode mode,
        Av1ReferenceFrameType reference,
        int motionRate,
        uint variance,
        long squaredError,
        long chromaDistortion,
        ref Av1RateDistortionStatistics statistics,
        ref bool checkGlobalMotion)
    {
        int slot = (int)reference;
        Av1PredictionMode searchedMode = mode;
        Av1MotionVector vector = this.MotionVectors[(int)mode][slot];
        int modeIndex = (int)mode - (int)Av1PredictionMode.SingleInterModeStart;
        int globalIndex = (int)Av1PredictionMode.GlobalMotionVector - (int)Av1PredictionMode.SingleInterModeStart;
        this.Variances[modeIndex][slot] = variance;
        this.ChromaDistortions[modeIndex][slot] = chromaDistortion;
        if (vector.IsZero)
        {
            this.Variances[globalIndex][slot] = variance;
        }

        if (mode != Av1PredictionMode.GlobalMotionVector
            && vector == this.MotionVectors[(int)Av1PredictionMode.GlobalMotionVector][slot])
        {
            int alternativeRate = motionRate + this.ModeCosts[modeIndex][slot];
            if (mode is Av1PredictionMode.NewMotionVector or Av1PredictionMode.NearMotionVector && referenceVectors.Count > 1)
            {
                int context = Av1SymbolContextHelper.GetDrlContext(referenceVectors.Weights, 0);
                alternativeRate += writer.GetDynamicReferenceListCost(false, context);
            }

            if (alternativeRate > this.ModeCosts[globalIndex][slot])
            {
                mode = Av1PredictionMode.GlobalMotionVector;
                modeIndex = globalIndex;
            }
        }

        // The motion estimate remains part of the candidate rate even if equivalent global syntax
        // wins. This decision changes the selected syntax, not the search result that produced it.
        statistics.Add(
            rateMultiplier,
            new Av1RateDistortionStatistics(rateMultiplier, motionRate + this.ModeCosts[modeIndex][slot] + this.ReferenceCosts[slot], 0));

        this.EvaluatedModes[(int)searchedMode][slot] = 1;
        this.EvaluatedModes[(int)mode][slot] = 1;
        if (checkGlobalMotion && Math.Abs(vector.Row) + Math.Abs(vector.Column) < 2)
        {
            checkGlobalMotion = false;
        }

        if (statistics.Cost >= this.BestStatistics.Cost)
        {
            return false;
        }

        this.BestStatistics = statistics;
        this.BestSquaredError = squaredError;
        this.WinningMotionVectors[(int)mode][slot] = this.MotionVectors[(int)mode][slot];
        return true;
    }

    /// <summary>
    /// Determines whether a candidate's prediction error warrants residual evaluation.
    /// </summary>
    /// <param name="speed">The configured encoding speed.</param>
    /// <param name="blockSize">The prediction block geometry.</param>
    /// <param name="squaredError">The candidate's normalized squared error.</param>
    /// <returns>Whether to reject the candidate before transform estimation.</returns>
    public readonly bool SkipByPredictionError(HeifEncodingSpeed speed, Av1BlockSize blockSize, long squaredError)
    {
        ReadOnlySpan<byte> sizeGroups = [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 0, 0, 1, 1, 2, 2];
        ReadOnlySpan<double> thresholds =
        [
            0.65, 0.65, 0.65, 0.7,
            0.6, 0.65, 0.85, 0.9,
            0.5, 0.5, 0.55, 0.6
        ];

        int index = (((int)speed - (int)HeifEncodingSpeed.Level7) * 4) + sizeGroups[(int)blockSize];
        return thresholds[index] * squaredError > this.BestSquaredError;
    }

    /// <summary>
    /// Rejects motion modes using block geometry and temporal activity.
    /// </summary>
    /// <param name="mode">The single-reference mode.</param>
    /// <param name="reference">The reference slot.</param>
    /// <param name="blockSize">The current block geometry.</param>
    /// <param name="referencePruning">The frame's reference pruning level.</param>
    /// <param name="zeroMotionError">The normalized zero-motion squared error.</param>
    /// <param name="aggressive">Whether stronger block-size pruning is enabled.</param>
    /// <param name="forceLowTemporalSkip">Whether temporal variance permits early rejection.</param>
    /// <param name="sourceSad">The superblock's source-change classification.</param>
    /// <returns>Whether the candidate can be omitted.</returns>
    public readonly bool SkipSingleByActivity(
        Av1PredictionMode mode,
        Av1ReferenceFrameType reference,
        Av1BlockSize blockSize,
        int referencePruning,
        uint zeroMotionError,
        bool aggressive,
        bool forceLowTemporalSkip,
        Av1SourceSadLevel sourceSad)
    {
        bool secondaryReference = reference != Av1ReferenceFrameType.Last;
        if (mode == Av1PredictionMode.NewMotionVector
            && ((secondaryReference && zeroMotionError < 500) || blockSize == Av1BlockSize.Block128x128))
        {
            return true;
        }

        if (referencePruning != 0)
        {
            if (referencePruning > 1 && secondaryReference && blockSize > Av1BlockSize.Block16x16
                && mode == Av1PredictionMode.NewMotionVector)
            {
                return true;
            }

            if (mode == Av1PredictionMode.NearMotionVector
                && (secondaryReference || (aggressive && blockSize >= Av1BlockSize.Block32x32)))
            {
                return true;
            }
        }

        return forceLowTemporalSkip
            && ((secondaryReference && !this.MotionVectors[(int)mode][(int)reference].IsZero)
                || (sourceSad != Av1SourceSadLevel.High && blockSize >= Av1BlockSize.Block64x64
                    && mode == Av1PredictionMode.NewMotionVector));
    }

    /// <summary>
    /// Gets the luma variance above which a compound candidate is dropped: the smaller variance of its two
    /// single-reference modes. Reference: var_threshold in handle_inter_mode_nonrd() under
    /// prune_compoundmode_with_singlecompound_var.
    /// </summary>
    /// <param name="mode">The compound mode.</param>
    /// <param name="first">The first reference.</param>
    /// <param name="second">The second reference.</param>
    /// <returns>The variance threshold, or <see cref="uint.MaxValue"/> when neither mode was measured.</returns>
    public readonly uint GetCompoundVarianceThreshold(
        Av1PredictionMode mode,
        Av1ReferenceFrameType first,
        Av1ReferenceFrameType second)
        => Math.Min(
            this.Variances[(int)mode.GetFirstReferenceMode() - (int)Av1PredictionMode.SingleInterModeStart][(int)first],
            this.Variances[(int)mode.GetSecondReferenceMode() - (int)Av1PredictionMode.SingleInterModeStart][(int)second]);

    /// <summary>
    /// Determines whether the single-reference results already make compound search unnecessary.
    /// </summary>
    /// <param name="blockSize">The current coding-block geometry.</param>
    /// <returns>Whether compound candidates can be omitted.</returns>
    public readonly bool SkipCompoundByVariance(Av1BlockSize blockSize)
    {
        uint bestVariance = uint.MaxValue;
        for (int mode = 0; mode < 4; mode++)
        {
            for (int reference = 0; reference < 8; reference++)
            {
                bestVariance = Math.Min(bestVariance, this.Variances[mode][reference]);
            }
        }

        // The square-block thresholds scale their trained 32- and 64-sample decisions to the
        // adjacent square sizes. Rectangles have no threshold and retain compound evaluation.
        const uint threshold64 = (uint)(0.57356805F * 8659);
        const uint threshold32 = (uint)(0.23964763F * 4281);
        return blockSize switch
        {
            Av1BlockSize.Block128x128 => bestVariance < 4 * threshold64,
            Av1BlockSize.Block64x64 => bestVariance < threshold64,
            Av1BlockSize.Block32x32 => bestVariance < threshold32,
            Av1BlockSize.Block16x16 => bestVariance < threshold32 / 4,
            _ => false
        };
    }

    /// <summary>
    /// Rejects a compound candidate when its matching single-reference predictors performed poorly.
    /// </summary>
    /// <param name="mode">The compound mode.</param>
    /// <param name="firstReference">The first reference slot.</param>
    /// <param name="secondReference">The second reference slot.</param>
    /// <returns>Whether the available matching single-reference evidence rejects the candidate.</returns>
    public readonly bool SkipCompoundBySingleResults(
        Av1PredictionMode mode,
        Av1ReferenceFrameType firstReference,
        Av1ReferenceFrameType secondReference)
    {
        Av1PredictionMode singleMode = mode == Av1PredictionMode.GlobalGlobalMotionVector
            ? Av1PredictionMode.GlobalMotionVector
            : Av1PredictionMode.NearestMotionVector;

        bool firstValid = false;
        bool secondValid = false;
        bool firstBad = false;
        bool secondBad = false;
        for (int component = 0; component < 2; component++)
        {
            int reference = (int)(component == 0 ? firstReference : secondReference);
            int singleIndex = (int)singleMode - (int)Av1PredictionMode.SingleInterModeStart;
            if (this.EvaluatedModes[(int)singleMode][reference] == 0 ||
                this.MotionVectors[(int)singleMode][reference] != this.MotionVectors[(int)mode][reference] ||
                this.Variances[singleIndex][reference] == uint.MaxValue)
            {
                continue;
            }

            uint bestVariance = uint.MaxValue;
            long bestChromaDistortion = long.MaxValue;
            for (int index = 0; index < 4; index++)
            {
                bestVariance = Math.Min(bestVariance, this.Variances[index][reference]);
                bestChromaDistortion = Math.Min(bestChromaDistortion, this.ChromaDistortions[index][reference]);
            }

            // A one-eighth margin rejects consistently weak predictors. If chroma distinguishes
            // this predictor, both luma and chroma must be worse before its evidence can reject it.
            bool bad = 1.125F * bestVariance < this.Variances[singleIndex][reference];
            long chromaDistortion = this.ChromaDistortions[singleIndex][reference];
            if (chromaDistortion < long.MaxValue && bestChromaDistortion != chromaDistortion)
            {
                bad &= 1.125F * bestChromaDistortion < chromaDistortion;
            }

            if (component == 0)
            {
                firstValid = true;
                firstBad = bad;
            }
            else
            {
                secondValid = true;
                secondBad = bad;
            }
        }

        return firstValid && secondValid ? firstBad && secondBad : firstBad || secondBad;
    }
}

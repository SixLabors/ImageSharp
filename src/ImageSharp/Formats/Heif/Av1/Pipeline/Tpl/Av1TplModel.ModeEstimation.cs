// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchBase;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchSettings;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <content>
/// Measures one frame: for each 16x16 block, the best intra mode, the best single and compound inter predictions,
/// and the rate and distortion of coding the winner against source and against reconstructed references.
/// </content>
internal sealed partial class Av1TplModel<TSample, TSearchOperator, TSampleOperator>
{
    /// <summary>
    /// Gets the reference pairs of the compound search, as reference indices. Reference: comp_ref_frames.
    /// </summary>
    private static ReadOnlySpan<byte> CompoundPairs => [0, 4, 0, 6, 3, 6];

    /// <summary>
    /// Sets up the references, the rate multiplier and the quantizer for one frame. Reference: init_mc_flow_dispenser().
    /// </summary>
    /// <param name="input">The encoder state.</param>
    /// <param name="frameIndex">The group index of the frame.</param>
    /// <param name="modelQIndex">The leaf quantizer of the model. Reference: pframe_qindex.</param>
    private void InitializeFlowDispenser(Av1TplSetupInput<TSample> input, int frameIndex, int modelQIndex)
    {
        Av1TplGroup group = input.Group;
        Av1TplSpeedFeatures speedFeatures = input.SpeedFeatures;
        Av1TplFrameStatistics frame = this.GetFrame(frameIndex);
        this.frameIndex = frameIndex;
        int boostIndex = Av1TplRateDistortion.GetBoostIndex(input.GoldenBoost);
        int layerDepth = Math.Min(group.LayerDepth[0], 6);

        Span<int> displayIndices = stackalloc int[Av1TplModelConstants.InterReferenceCount];
        for (int reference = 0; reference < Av1TplModelConstants.InterReferenceCount; reference++)
        {
            int entry = GetEntry(frame.ReferenceMapIndex[reference]);
            this.referenceEntries[reference] = entry;
            this.hasReference[reference] = this.reconstructionIds[entry] != NoPicture;
            this.hasSourceReference[reference] = this.sourceIds[entry] != NoPicture;
            displayIndices[reference] = this.frames[entry].DisplayIndex;
        }

        // A reference whose reconstruction repeats one earlier in priority order is removed. Absent references compare
        // equal, as their pointers do. Reference: get_ref_frame_flags().
        int referenceFlags = 0x7F;
        ReadOnlySpan<byte> order = ReferencePriorityOrder;
        for (int i = 1; i < Av1TplModelConstants.InterReferenceCount; i++)
        {
            int current = this.reconstructionIds[this.referenceEntries[order[i] - 1]];
            for (int j = 0; j < i; j++)
            {
                if (current == this.reconstructionIds[this.referenceEntries[order[j] - 1]] &&
                    (referenceFlags & (1 << (order[j] - 1))) != 0)
                {
                    referenceFlags &= ~(1 << (order[i] - 1));
                    break;
                }
            }
        }

        referenceFlags = EnforceMaximumReferenceFrames(referenceFlags, speedFeatures.SelectiveReferenceFrame, displayIndices, frame.DisplayIndex);
        for (int reference = 0; reference < Av1TplModelConstants.InterReferenceCount; reference++)
        {
            if ((referenceFlags & (1 << reference)) == 0)
            {
                this.hasReference[reference] = false;
            }
        }

        // The selective reference pruning of the coding search also skips references here, except for frames past the
        // group, whose references differ from those of the coding search. Reference: is_frame_eligible_for_ref_pruning().
        bool pruningEnabled = speedFeatures.SelectiveReferenceFrame > 0 &&
            speedFeatures.PruneReferenceFrames &&
            !group.IsTplEligible(frameIndex);

        if (pruningEnabled && frameIndex < GetGopLength(group))
        {
            for (int reference = 0; reference < Av1TplModelConstants.InterReferenceCount; reference++)
            {
                if (PruneBySelectiveReferenceFrame(speedFeatures.SelectiveReferenceFrame, reference + 1, displayIndices))
                {
                    this.hasReference[reference] = false;
                }
            }
        }

        this.rateMultiplier = Math.Max(
            1,
            Av1TplRateDistortion.GetRateMultiplier(
                modelQIndex,
                this.bitDepth,
                group.UpdateType[0],
                layerDepth,
                boostIndex,
                this.lastEntryIsKeyFrame,
                input.UseFixedQpOffsets,
                input.IsStatConsumptionStage,
                input.Tuning));

        this.sadPerBit = Av1RateDistortion.GetMotionSearchSadPerBit(modelQIndex, this.bitDepth);
        frame.IsValid = true;
        this.qIndex = modelQIndex;
        frame.BaseRateMultiplier = Av1RateDistortion.GetRateMultiplier(modelQIndex, this.bitDepth, group.UpdateType[0], input.Tuning, false) / 6;

        // The model runs before the frame level speed features, so the key frame exception is applied here.
        int layerDepthThreshold = speedFeatures.UseSadForModeDecision == 1 ? 5 : 0;
        frame.UsePredictionSad = speedFeatures.UseSadForModeDecision != 0 &&
            group.UpdateType[0] != Av1FrameUpdateType.Key &&
            group.LayerDepth[frameIndex] >= layerDepthThreshold;
    }

    /// <summary>
    /// Disables references past the limit that the speed features allow, in a fixed order. The fourth disabled
    /// reference, BWDREF, clears the GOLDEN flag, as the reference encoder does. Reference: enforce_max_ref_frames(),
    /// with get_max_allowed_ref_frames() and get_num_refs_to_disable() of one-pass encoding.
    /// </summary>
    private static int EnforceMaximumReferenceFrames(int referenceFlags, int selectiveReferenceFrame, ReadOnlySpan<int> displayIndices, int currentDisplayIndex)
    {
        int totalValid = 0;
        for (int reference = 0; reference < Av1TplModelConstants.InterReferenceCount; reference++)
        {
            if ((referenceFlags & (1 << reference)) != 0)
            {
                totalValid++;
            }
        }

        int disableCount = 0;
        if (selectiveReferenceFrame >= 3)
        {
            disableCount++;
            if (selectiveReferenceFrame >= 6)
            {
                // LAST2 and ALTREF2 go as well.
                disableCount += 2;
            }
            else if (selectiveReferenceFrame == 5 && (referenceFlags & (1 << ((int)Av1ReferenceFrameType.Last2 - 1))) != 0)
            {
                // A temporally distant LAST2 goes. The low coded-error test needs two-pass statistics.
                int distance = displayIndices[(int)Av1ReferenceFrameType.Last2 - 1] - currentDisplayIndex;
                if (Math.Abs(distance) > 2)
                {
                    disableCount++;
                }
            }
        }

        // The configured maximum is seven references.
        int maximumAllowed = Math.Min(Av1TplModelConstants.InterReferenceCount - disableCount, Av1TplModelConstants.InterReferenceCount);
        ReadOnlySpan<byte> disableOrder = DisableOrder;
        for (int i = 0; i < 4 && totalValid > maximumAllowed; i++)
        {
            int reference = disableOrder[i];
            if ((referenceFlags & (1 << (reference - 1))) == 0)
            {
                continue;
            }

            int cleared = reference == (int)Av1ReferenceFrameType.Backward ? (int)Av1ReferenceFrameType.Golden : reference;
            referenceFlags &= ~(1 << (cleared - 1));
            totalValid--;
        }

        return referenceFlags;
    }

    /// <summary>
    /// Returns whether the selective reference search drops a single reference: from level two LAST2 and LAST3 that
    /// precede GOLDEN, and from level three ALTREF2 and BWDREF that precede LAST. The model has no block statistics that
    /// keep a reference. Reference: prune_ref_by_selective_ref_frame() with a NULL block and prune_ref().
    /// </summary>
    private static bool PruneBySelectiveReferenceFrame(int level, int reference, ReadOnlySpan<int> displayIndices)
    {
        if (level == 0)
        {
            return false;
        }

        if (level >= 2)
        {
            int anchor = displayIndices[(int)Av1ReferenceFrameType.Golden - 1];
            if ((reference == (int)Av1ReferenceFrameType.Last3 || reference == (int)Av1ReferenceFrameType.Last2) &&
                displayIndices[reference - 1] - anchor < 0)
            {
                return true;
            }
        }

        if (level >= 3)
        {
            int anchor = displayIndices[(int)Av1ReferenceFrameType.Last - 1];
            if ((reference == (int)Av1ReferenceFrameType.Alternate2 || reference == (int)Av1ReferenceFrameType.Backward) &&
                displayIndices[reference - 1] - anchor < 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Measures every block of the current frame in raster order and stores its statistics. Reference:
    /// mc_flow_dispenser() and av1_mc_flow_dispenser_row() in a single thread.
    /// </summary>
    private void DispenseFlow(Av1TplSetupInput<TSample> input)
    {
        Av1TplFrameStatistics frame = this.GetFrame(this.frameIndex);
        const int Step = 1 << Av1TplModelConstants.BlockModeInfoLog2;
        for (int modeInfoRow = 0; modeInfoRow < this.ModeInfoRows; modeInfoRow += Step)
        {
            for (int modeInfoColumn = 0; modeInfoColumn < this.ModeInfoColumns; modeInfoColumn += Step)
            {
                Av1TplBlockStatistics statistics = default;
                this.EstimateMode(input, modeInfoRow, modeInfoColumn, ref statistics);

                // Every stored cost and distortion is at least one. Reference: tpl_model_store().
                ref Av1TplBlockStatistics stored = ref frame.GetBlock(modeInfoRow, modeInfoColumn);
                stored = statistics;
                stored.IntraCost = Math.Max(1, stored.IntraCost);
                stored.InterCost = Math.Max(1, stored.InterCost);
                stored.SourceReferenceDistortion = Math.Max(1, stored.SourceReferenceDistortion);
                stored.SourceReferenceSse = Math.Max(1, stored.SourceReferenceSse);
                stored.ReconstructedReferenceDistortion = Math.Max(1, stored.ReconstructedReferenceDistortion);
                stored.SourceReferenceRate = Math.Max(1, stored.SourceReferenceRate);
                stored.ReconstructedReferenceRate = Math.Max(1, stored.ReconstructedReferenceRate);
                stored.CompoundReconstructedDistortion[0] = Math.Max(1, stored.CompoundReconstructedDistortion[0]);
                stored.CompoundReconstructedDistortion[1] = Math.Max(1, stored.CompoundReconstructedDistortion[1]);
                stored.CompoundReconstructedRate[0] = Math.Max(1, stored.CompoundReconstructedRate[0]);
                stored.CompoundReconstructedRate[1] = Math.Max(1, stored.CompoundReconstructedRate[1]);
            }
        }
    }

    /// <summary>
    /// Measures one 16x16 block: the intra search, the single reference motion search with neighbor starting vectors,
    /// the compound search, then the rates and distortions of the winner. Reference: mode_estimation().
    /// </summary>
    private void EstimateMode(Av1TplSetupInput<TSample> input, int modeInfoRow, int modeInfoColumn, ref Av1TplBlockStatistics statistics)
    {
        const int Size = Av1TplModelConstants.BlockSize;
        const int ModeInfoSize = Size >> 2;
        Av1TplSpeedFeatures speedFeatures = input.SpeedFeatures;
        Av1TplFrameStatistics frame = this.GetFrame(this.frameIndex);
        int entry = GetEntry(this.frameIndex);
        PlaneAccess source = GetPlane(this.sourcePictures[entry], 0);
        PlaneAccess reconstruction = GetPlane(this.reconstructionPictures[entry], 0);
        int x = modeInfoColumn * 4;
        int y = modeInfoRow * 4;
        int sourceIndex = source.IndexOf(x, y);
        int reconstructionIndex = reconstruction.IndexOf(x, y);
        Span<TSample> predictorSamples = this.predictor;
        int gridColumn = modeInfoColumn >> Av1TplModelConstants.BlockModeInfoLog2;
        int gridRow = modeInfoRow >> Av1TplModelConstants.BlockModeInfoLog2;
        long reconstructionError = 1;
        long predictionError = 1;

        statistics.ReferenceFrameIndex[0] = -1;
        statistics.ReferenceFrameIndex[1] = -1;

        // set_mi_row_col() in the single tile the model uses.
        this.upAvailable = modeInfoRow > 0;
        this.leftAvailable = modeInfoColumn > 0;

        // The intra search starts from an intra reference field.
        this.modeInfo.SetInter(gridColumn, gridRow, false);

        // The bottom-left neighbors belong to the next block row, which the model has not reconstructed, while the
        // availability rules of a superblock may declare them present. Repeat the last left sample below the block.
        if (this.leftAvailable && modeInfoRow + ModeInfoSize < this.ModeInfoRows)
        {
            Span<TSample> samples = reconstruction.Samples;
            int stride = reconstruction.Stride;
            TSample value = samples[reconstructionIndex + ((Size - 1) * stride) - 1];
            for (int i = 0; i < Size; i++)
            {
                samples[reconstructionIndex + ((Size + i) * stride) - 1] = value;
            }
        }

        // Pruned intra search tests DC_PRED, V_PRED and H_PRED only.
        Av1PredictionMode lastIntraMode = speedFeatures.PruneIntraModes ? Av1PredictionMode.Directional45Degrees : Av1PredictionMode.IntraModeEnd;
        int bestIntraCost = int.MaxValue;
        Av1PredictionMode bestMode = Av1PredictionMode.DC;
        for (Av1PredictionMode mode = Av1PredictionMode.DC; mode < lastIntraMode; mode++)
        {
            this.PredictIntra(input, 0, mode, reconstruction, x, y, predictorSamples, Size, Av1TransformSize.Size16x16, modeInfoRow, modeInfoColumn);
            int intraCost = frame.UsePredictionSad
                ? this.GetSad(source.Samples[sourceIndex..], source.Stride, predictorSamples, Size)
                : this.GetSatdCost(source.Samples[sourceIndex..], source.Stride, predictorSamples, Size);

            if (intraCost < bestIntraCost)
            {
                bestIntraCost = intraCost;
                bestMode = mode;
            }
        }

        // Mode decision by absolute differences still reports the transform cost of the winner, which the intra
        // pruning of the coding search reads.
        if (frame.UsePredictionSad)
        {
            this.PredictIntra(input, 0, bestMode, reconstruction, x, y, predictorSamples, Size, Av1TransformSize.Size16x16, modeInfoRow, modeInfoColumn);
            bestIntraCost = this.GetSatdCost(source.Samples[sourceIndex..], source.Stride, predictorSamples, Size);
        }

        // Motion compensated prediction.
        int bestReference = -1;
        Span<Av1MotionVector> bestVectors = stackalloc Av1MotionVector[2];
        Span<Av1MotionVector> singleVectors = stackalloc Av1MotionVector[Av1TplModelConstants.InterReferenceCount];
        bestVectors[0] = Av1TplBlockStatistics.InvalidMotionVector;
        bestVectors[1] = Av1TplBlockStatistics.InvalidMotionVector;
        int bestInterCost = int.MaxValue;
        Rectangle frameBounds = Av1MotionVector.GetFrameSearchBounds(
            new Rectangle(x, y, Size, Size),
            new Size(this.ModeInfoColumns * 4, this.ModeInfoRows * 4),
            Av1TplModelConstants.Border);

        bool keyFrameUpdate = input.Group.UpdateType[0] == Av1FrameUpdateType.Key;
        Span<Av1MotionVector> centers = stackalloc Av1MotionVector[4];
        Span<int> centerSads = stackalloc int[4];
        for (int reference = 0; reference < Av1TplModelConstants.InterReferenceCount; reference++)
        {
            singleVectors[reference] = Av1TplBlockStatistics.InvalidMotionVector;
            if (!this.hasReference[reference] || !this.hasSourceReference[reference])
            {
                statistics.MotionVectors[reference] = Av1TplBlockStatistics.InvalidMotionVector;
                continue;
            }

            PlaneAccess referencePlane = GetPlane(this.sourcePictures[this.referenceEntries[reference]], 0);
            int referenceIndex = referencePlane.IndexOf(x, y);

            // The zero vector and the vectors of the above, left and above-right neighbors start the search, unless
            // alike to an earlier start.
            centers.Clear();
            centerSads.Fill(int.MaxValue);
            int startCount = 1;
            if (this.upAvailable)
            {
                Av1MotionVector vector = frame.GetBlock(modeInfoRow - ModeInfoSize, modeInfoColumn).MotionVectors[reference];
                if (!IsAlike(vector, centers[..startCount], speedFeatures.SkipAlikeStartingMotionVector))
                {
                    centers[startCount++] = vector;
                }
            }

            if (this.leftAvailable)
            {
                Av1MotionVector vector = frame.GetBlock(modeInfoRow, modeInfoColumn - ModeInfoSize).MotionVectors[reference];
                if (!IsAlike(vector, centers[..startCount], speedFeatures.SkipAlikeStartingMotionVector))
                {
                    centers[startCount++] = vector;
                }
            }

            if (this.upAvailable && modeInfoColumn + ModeInfoSize < this.ModeInfoColumns)
            {
                Av1MotionVector vector = frame.GetBlock(modeInfoRow - ModeInfoSize, modeInfoColumn + ModeInfoSize).MotionVectors[reference];
                if (!IsAlike(vector, centers[..startCount], speedFeatures.SkipAlikeStartingMotionVector))
                {
                    centers[startCount++] = vector;
                }
            }

            if (speedFeatures.PruneStartingMotionVector != 0 && startCount > 1)
            {
                // Rank the starts by the absolute difference at their clamped full-pixel positions.
                for (int index = 0; index < startCount; index++)
                {
                    Point full = ToFullPixel(centers[index]);
                    full = new Point(
                        Math.Clamp(full.X, frameBounds.Left, frameBounds.Right - 1),
                        Math.Clamp(full.Y, frameBounds.Top, frameBounds.Bottom - 1));

                    centerSads[index] = this.GetSad(
                        source.Samples[sourceIndex..],
                        source.Stride,
                        referencePlane.Samples[(referenceIndex + (full.Y * referencePlane.Stride) + full.X)..],
                        referencePlane.Stride);
                }

                SortBySad(centers[..startCount], centerSads[..startCount]);
                startCount = Math.Min(4 - speedFeatures.PruneStartingMotionVector, startCount);

                // Drop the last start when its difference is much larger than the one before it.
                if (startCount > 1)
                {
                    int lastSad = centerSads[startCount - 1];
                    int secondToLastSad = centerSads[startCount - 2];
                    if ((lastSad - secondToLastSad) * 5 > secondToLastSad)
                    {
                        startCount--;
                    }
                }
            }

            Av1MotionVector bestReferenceVector = default;
            uint bestSme = uint.MaxValue;
            for (int index = 0; index < startCount; index++)
            {
                uint sme = this.EstimateMotion(
                    input,
                    source.Samples[sourceIndex..],
                    source.Stride,
                    referencePlane,
                    referenceIndex,
                    frameBounds,
                    centers[index],
                    keyFrameUpdate,
                    out Av1MotionVector vector);

                if (sme < bestSme)
                {
                    bestSme = sme;
                    bestReferenceVector = vector;
                }
            }

            statistics.MotionVectors[reference] = bestReferenceVector;
            singleVectors[reference] = bestReferenceVector;
            int interCost = this.GetInterCost(input, source.Samples[sourceIndex..], source.Stride, reference, x, y, bestReferenceVector, frame.UsePredictionSad);

            // The inter cost of each reference lets the coding search prune inter modes.
            statistics.PredictionError[reference] = Math.Max(1, interCost);

            // A saved alternate reference of the previous group may only win for the frames of that group.
            if (interCost < bestInterCost &&
                (this.PreviousArfDisplayOrder < 0 || this.SourceDiffersFromReconstruction(reference)))
            {
                bestReference = reference;
                bestInterCost = interCost;
                bestVectors[0] = bestReferenceVector;
            }
        }

        // Mode decision by absolute differences still reports the transform cost of the inter winner.
        if (bestInterCost < int.MaxValue && frame.UsePredictionSad)
        {
            bestInterCost = this.GetInterCost(input, source.Samples[sourceIndex..], source.Stride, bestReference, x, y, bestVectors[0], false);
        }

        if (bestReference != -1 && bestInterCost < bestIntraCost)
        {
            bestMode = Av1PredictionMode.NewMotionVector;
            this.modeInfo.SetInter(gridColumn, gridRow, true);
        }

        // The compound search tries three reference pairs with a joint motion search from the single vectors.
        int bestCompound = -1;
        int compoundCount = speedFeatures.AllowCompoundPrediction ? 3 : 0;
        Span<Av1MotionVector> trialVectors = stackalloc Av1MotionVector[2];
        for (int compound = 0; compound < compoundCount; compound++)
        {
            int first = CompoundPairs[2 * compound];
            int second = CompoundPairs[(2 * compound) + 1];
            if (!this.hasReference[first] || !this.hasSourceReference[first] ||
                !this.hasReference[second] || !this.hasSourceReference[second])
            {
                continue;
            }

            // The trial writes the pair and NEW_NEWMV into the block's mode-information fields.
            this.modeInfo.SetInter(gridColumn, gridRow, true);
            this.modeInfo.SetMode(gridColumn, gridRow, Av1PredictionMode.NewNewMotionVector);
            trialVectors[0] = singleVectors[first];
            trialVectors[1] = singleVectors[second];
            this.SearchJointMotion(input, source.Samples[sourceIndex..], source.Stride, first, second, x, y, frameBounds, trialVectors);

            this.PredictCompound(
                this.sourcePictures[this.referenceEntries[first]],
                this.sourcePictures[this.referenceEntries[second]],
                trialVectors,
                0,
                x,
                y,
                predictorSamples,
                Size);

            int interCost = this.GetSatdCost(source.Samples[sourceIndex..], source.Stride, predictorSamples, Size);
            if (interCost < bestInterCost &&
                (this.PreviousArfDisplayOrder < 0 ||
                (this.SourceDiffersFromReconstruction(first) && this.SourceDiffersFromReconstruction(second))))
            {
                bestCompound = compound;
                bestInterCost = interCost;
                bestVectors[0] = trialVectors[0];
                bestVectors[1] = trialVectors[1];
            }
        }

        if (bestCompound != -1 && bestInterCost < bestIntraCost)
        {
            bestMode = Av1PredictionMode.NewNewMotionVector;
        }

        bool isInter = bestMode >= Av1PredictionMode.NearestMotionVector;
        int bestFirst = bestCompound >= 0 ? CompoundPairs[2 * bestCompound] : bestReference;
        int bestSecond = bestCompound >= 0 ? CompoundPairs[(2 * bestCompound) + 1] : -1;
        if (bestInterCost < int.MaxValue && isInter)
        {
            // Code the winner against source references.
            this.GetRateDistortion(
                input,
                bestMode,
                this.GetPicture(bestFirst, source: true),
                bestSecond >= 0 ? this.GetPicture(bestSecond, source: true) : default,
                bestVectors,
                modeInfoRow,
                modeInfoColumn,
                false,
                out int sourceRate,
                out reconstructionError,
                out predictionError);

            statistics.SourceReferenceRate = sourceRate;
        }

        bestIntraCost = Math.Max(bestIntraCost, 1);
        bestInterCost = Math.Min(bestIntraCost, bestInterCost);
        statistics.InterCost = bestInterCost;
        statistics.IntraCost = bestIntraCost;
        statistics.SourceReferenceDistortion = reconstructionError << Av1TplModelConstants.DependencyCostScaleLog2;
        statistics.SourceReferenceSse = predictionError << Av1TplModelConstants.DependencyCostScaleLog2;

        if (bestMode == Av1PredictionMode.NewNewMotionVector)
        {
            // Each compound reference reconstructed while the other stays a source.
            this.GetRateDistortion(
                input,
                bestMode,
                this.GetPicture(bestFirst, source: false),
                this.GetPicture(bestSecond, source: true),
                bestVectors,
                modeInfoRow,
                modeInfoColumn,
                false,
                out int firstRate,
                out reconstructionError,
                out predictionError);

            statistics.CompoundReconstructedDistortion[0] = reconstructionError << Av1TplModelConstants.DependencyCostScaleLog2;
            statistics.CompoundReconstructedRate[0] = firstRate;

            this.GetRateDistortion(
                input,
                bestMode,
                this.GetPicture(bestFirst, source: true),
                this.GetPicture(bestSecond, source: false),
                bestVectors,
                modeInfoRow,
                modeInfoColumn,
                false,
                out int secondRate,
                out reconstructionError,
                out predictionError);

            statistics.CompoundReconstructedDistortion[1] = reconstructionError << Av1TplModelConstants.DependencyCostScaleLog2;
            statistics.CompoundReconstructedRate[1] = secondRate;
        }

        // Final encode. D203_PRED reads the chroma below the left neighbor, which the next block row has not
        // reconstructed yet; repeat the last left chroma sample below the block.
        int planes = speedFeatures.LumaOnlyRateDistortion ? 1 : this.planeCount;
        if (bestMode == Av1PredictionMode.Directional203Degrees && this.leftAvailable && modeInfoRow + ModeInfoSize < this.ModeInfoRows)
        {
            for (int plane = 1; plane < planes; plane++)
            {
                PlaneAccess chroma = GetPlane(this.reconstructionPictures[entry], plane);
                int chromaHeight = Size >> this.subsamplingY;
                int chromaIndex = chroma.IndexOf(x >> this.subsamplingX, y >> this.subsamplingY);
                Span<TSample> samples = chroma.Samples;
                TSample value = samples[chromaIndex + ((chromaHeight - 1) * chroma.Stride) - 1];
                for (int i = 0; i < chromaHeight; i++)
                {
                    samples[chromaIndex + ((chromaHeight + i) * chroma.Stride) - 1] = value;
                }
            }
        }

        this.GetRateDistortion(
            input,
            bestMode,
            bestMode == Av1PredictionMode.NewNewMotionVector || bestReference >= 0 ? this.GetPicture(bestFirst, source: false) : default,
            bestMode == Av1PredictionMode.NewNewMotionVector ? this.GetPicture(bestSecond, source: false) : default,
            bestVectors,
            modeInfoRow,
            modeInfoColumn,
            true,
            out int finalRate,
            out reconstructionError,
            out predictionError);

        statistics.ReconstructedReferenceDistortion = reconstructionError << Av1TplModelConstants.DependencyCostScaleLog2;
        statistics.ReconstructedReferenceSse = predictionError << Av1TplModelConstants.DependencyCostScaleLog2;
        statistics.ReconstructedReferenceRate = finalRate;

        if (!isInter)
        {
            statistics.SourceReferenceDistortion = reconstructionError << Av1TplModelConstants.DependencyCostScaleLog2;
            statistics.SourceReferenceRate = finalRate;
            statistics.SourceReferenceSse = predictionError << Av1TplModelConstants.DependencyCostScaleLog2;
        }

        statistics.ReconstructedReferenceDistortion = Math.Max(statistics.SourceReferenceDistortion, statistics.ReconstructedReferenceDistortion);
        statistics.ReconstructedReferenceRate = Math.Max(statistics.SourceReferenceRate, statistics.ReconstructedReferenceRate);

        if (bestMode == Av1PredictionMode.NewMotionVector)
        {
            statistics.MotionVectors[bestReference] = bestVectors[0];
            statistics.ReferenceFrameIndex[0] = (sbyte)bestReference;
            statistics.ReferenceFrameIndex[1] = -1;
        }
        else if (bestMode == Av1PredictionMode.NewNewMotionVector)
        {
            // The compound rates and distortions lie between the source and the reconstructed reference results.
            for (int reference = 0; reference < 2; reference++)
            {
                long distortion = Math.Max(statistics.SourceReferenceDistortion, statistics.CompoundReconstructedDistortion[reference]);
                int rate = Math.Max(statistics.SourceReferenceRate, statistics.CompoundReconstructedRate[reference]);
                statistics.CompoundReconstructedDistortion[reference] = Math.Min(statistics.ReconstructedReferenceDistortion, distortion);
                statistics.CompoundReconstructedRate[reference] = Math.Min(statistics.ReconstructedReferenceRate, rate);
            }

            statistics.ReferenceFrameIndex[0] = (sbyte)bestFirst;
            statistics.ReferenceFrameIndex[1] = (sbyte)bestSecond;
            statistics.MotionVectors[bestFirst] = bestVectors[0];
            statistics.MotionVectors[bestSecond] = bestVectors[1];
        }
    }

    /// <summary>
    /// Returns whether a reference's source and reconstruction are different buffers, which holds for the frames of
    /// the group and for the saved alternate reference, but not for the reference slots.
    /// </summary>
    private bool SourceDiffersFromReconstruction(int reference)
    {
        int entry = this.referenceEntries[reference];
        return this.sourceIds[entry] != this.reconstructionIds[entry];
    }

    /// <summary>
    /// Returns the source or the reconstruction of a reference.
    /// </summary>
    private ReferencePicture GetPicture(int reference, bool source)
    {
        int entry = this.referenceEntries[reference];
        return source
            ? new ReferencePicture(this.sourcePictures[entry], this.sourceIds[entry] != NoPicture)
            : new ReferencePicture(this.reconstructionPictures[entry], this.reconstructionIds[entry] != NoPicture);
    }

    /// <summary>
    /// Returns whether a starting vector is close to an earlier one: within one eighth sample, or eight or sixteen
    /// samples, per the speed level. Reference: is_alike_mv().
    /// </summary>
    private static bool IsAlike(Av1MotionVector candidate, ReadOnlySpan<Av1MotionVector> centers, int level)
    {
        ReadOnlySpan<int> thresholds = [1, 8 << 3, 16 << 3];
        int threshold = thresholds[level];
        for (int i = 0; i < centers.Length; i++)
        {
            if (Math.Abs(centers[i].Column - candidate.Column) < threshold && Math.Abs(centers[i].Row - candidate.Row) < threshold)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sorts the starting vectors by increasing absolute difference. The reference encoder sorts them with the C
    /// library qsort, and the x64 reference build links the Microsoft C runtime, whose qsort sorts lists of up to eight
    /// entries by moving the first largest entry to the end. Reference: the qsort() of mode_estimation() with
    /// compare_sad().
    /// </summary>
    private static void SortBySad(Span<Av1MotionVector> vectors, Span<int> sads)
    {
        for (int high = vectors.Length - 1; high > 0; high--)
        {
            int largest = 0;
            for (int index = 1; index <= high; index++)
            {
                if (sads[index] > sads[largest])
                {
                    largest = index;
                }
            }

            (vectors[largest], vectors[high]) = (vectors[high], vectors[largest]);
            (sads[largest], sads[high]) = (sads[high], sads[largest]);
        }
    }

    /// <summary>
    /// Searches one starting vector: the full-pixel search with the model method, then a fractional refinement with
    /// bilinear interpolation and no vector cost. Reference: motion_estimation().
    /// </summary>
    /// <returns>The error of the selected vector.</returns>
    private uint EstimateMotion(
        Av1TplSetupInput<TSample> input,
        ReadOnlySpan<TSample> source,
        int sourceStride,
        PlaneAccess reference,
        int referenceIndex,
        Rectangle frameBounds,
        Av1MotionVector center,
        bool keyFrameUpdate,
        out Av1MotionVector best)
    {
        const int Size = Av1TplModelConstants.BlockSize;
        Av1TplSpeedFeatures speedFeatures = input.SpeedFeatures;
        Av1MotionSearchSettings settings = input.MotionSettings;
        Point start = ToFullPixel(center);
        int stepParameter = Math.Min(speedFeatures.ReduceFirstStepSize, 11 - 2);
        Av1MotionSearchSites sites = this.GetSearchSites(speedFeatures.SearchMethod, reference.Stride);
        Av1MotionVectorCosts costs = new(this.motionVectorCostStorage.Memory.Span, this.costPrecision);
        FullPixelSearch<TSample, TSearchOperator> fullSearch = new(
            source,
            sourceStride,
            reference.Samples,
            reference.Stride,
            referenceIndex,
            new Size(Size, Size),
            center.GetFullPixelSearchBounds(frameBounds),
            center,
            costs,
            this.bitDepth,
            this.sadPerBit,
            this.rateMultiplier,
            [],
            []);

        // The integer neighborhood is published only for the pruned fractional trees with use_fullpel_costlist, which
        // only real-time usage enables; good quality searches without it. Reference: cond_cost_list().
        FullPixelResult integer = fullSearch.Search(
            start,
            stepParameter,
            speedFeatures.SearchMethod,
            sites,
            settings,
            keyFrameUpdate,
            false,
            false,
            Span<int>.Empty,
            out _);

        best = new Av1MotionVector(integer.Vector.Y * 8, integer.Vector.X * 8);
        if (speedFeatures.SubpelForceStop == SearchPrecision.Integer)
        {
            return (uint)integer.Cost;
        }

        // The fractional search starts from the integer variance with the vector cost removed.
        Av1MotionVectorCosts noCosts = new(this.zeroCostStorage.Memory.Span, this.costPrecision);
        FractionalSearch<TSample, TSearchOperator> fractionalSearch = new(
            source,
            sourceStride,
            reference.Samples,
            reference.Stride,
            referenceIndex,
            this.searchPrediction,
            new Size(Size, Size),
            center.GetSubpixelSearchBounds(frameBounds),
            center,
            noCosts,
            this.bitDepth,
            this.rateMultiplier,
            [],
            []);

        int cost = fractionalSearch.Search(
            best,
            new FullPixelResult(integer.Vector, integer.Variance, integer.SquaredError, 0),
            settings.FractionalMethod,
            speedFeatures.SubpelForceStop,
            input.AllowHighPrecisionMotionVector,
            settings.FractionalIterationsPerStep,
            2,
            ReadOnlySpan<int>.Empty,
            [],
            out FractionalResult result);

        best = result.Vector;
        return (uint)cost;
    }

    /// <summary>
    /// Refines a compound pair: each reference in turn is searched with the other's prediction fixed, twice each, until
    /// neither improves. Reference: av1_joint_motion_search() with NUM_JOINT_ME_REFINE_ITER, no mask, the vector cost
    /// removed from the fractional search, and the eight-point refining search that disable_extensive_joint_motion_search
    /// selects in good quality.
    /// </summary>
    private void SearchJointMotion(
        Av1TplSetupInput<TSample> input,
        ReadOnlySpan<TSample> source,
        int sourceStride,
        int first,
        int second,
        int x,
        int y,
        Rectangle frameBounds,
        Span<Av1MotionVector> vectors)
    {
        const int Size = Av1TplModelConstants.BlockSize;
        const int RefineIterations = 2;
        Av1MotionSearchSettings settings = input.MotionSettings;
        Span<Av1MotionVector> initial = stackalloc Av1MotionVector[2];
        vectors.CopyTo(initial);

        // The differential references are the single vectors the pair started from.
        Span<Av1MotionVector> referenceVectors = stackalloc Av1MotionVector[2];
        vectors.CopyTo(referenceVectors);
        Span<int> lastBestError = stackalloc int[2];
        lastBestError.Fill(int.MaxValue);
        Span<int> references = stackalloc int[2];
        references[0] = first;
        references[1] = second;
        Av1MotionVectorCosts costs = new(this.motionVectorCostStorage.Memory.Span, this.costPrecision);
        Av1MotionVectorCosts noCosts = new(this.zeroCostStorage.Memory.Span, this.costPrecision);
        Span<TSample> secondPredictionSamples = this.secondPrediction;

        for (int iteration = 0; iteration < 2 * RefineIterations; iteration++)
        {
            int id = iteration & 1;
            int other = 1 - id;
            if (iteration >= 2 && vectors[other] == initial[other])
            {
                if (vectors[id] == initial[id])
                {
                    break;
                }

                if ((vectors[id].Column >> 3) == (initial[id].Column >> 3) && (vectors[id].Row >> 3) == (initial[id].Row >> 3))
                {
                    break;
                }
            }

            // The prediction of the other reference, rounded to samples.
            Av1EncoderFrame<TSample> otherFrame = this.sourcePictures[this.referenceEntries[references[other]]];
            this.PredictSingle(otherFrame, 0, vectors[other], x, y, secondPredictionSamples, Size, Size, Size, visibleSize: true);

            PlaneAccess moving = GetPlane(this.sourcePictures[this.referenceEntries[references[id]]], 0);
            int movingIndex = moving.IndexOf(x, y);
            Av1MotionVector referenceVector = referenceVectors[id];
            FullPixelSearch<TSample, TSearchOperator> fullSearch = new(
                source,
                sourceStride,
                moving.Samples,
                moving.Stride,
                movingIndex,
                new Size(Size, Size),
                referenceVector.GetFullPixelSearchBounds(frameBounds),
                referenceVector,
                costs,
                this.bitDepth,
                this.sadPerBit,
                this.rateMultiplier,
                secondPredictionSamples[..(Size * Size)],
                []);

            Point selected = fullSearch.RefineCompound(ToFullPixel(vectors[id]), out int bestError);
            Av1MotionVector bestVector = new(selected.Y * 8, selected.X * 8);
            if (!input.ForceIntegerMotionVector && bestError < int.MaxValue)
            {
                FractionalSearch<TSample, TSearchOperator> fractionalSearch = new(
                    source,
                    sourceStride,
                    moving.Samples,
                    moving.Stride,
                    movingIndex,
                    this.searchPrediction,
                    new Size(Size, Size),
                    referenceVector.GetSubpixelSearchBounds(frameBounds),
                    referenceVector,
                    noCosts,
                    this.bitDepth,
                    this.rateMultiplier,
                    secondPredictionSamples[..(Size * Size)],
                    []);

                bestError = fractionalSearch.Search(
                    bestVector,
                    null,
                    settings.FractionalMethod,
                    SearchPrecision.EighthSample,
                    input.AllowHighPrecisionMotionVector,
                    settings.FractionalIterationsPerStep,
                    settings.FractionalInterpolationTaps,
                    [],
                    [],
                    out FractionalResult result);

                bestVector = result.Vector;
            }

            if (bestError < lastBestError[id])
            {
                vectors[id] = bestVector;
                lastBestError[id] = bestError;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Returns the cost of a single reference prediction: the fractional prediction from the source reference, or the
    /// source reference itself at an integer vector, measured by absolute differences or by the transform cost.
    /// Reference: get_inter_cost().
    /// </summary>
    private int GetInterCost(
        Av1TplSetupInput<TSample> input,
        ReadOnlySpan<TSample> source,
        int sourceStride,
        int reference,
        int x,
        int y,
        Av1MotionVector vector,
        bool usePredictionSad)
    {
        const int Size = Av1TplModelConstants.BlockSize;
        Av1EncoderFrame<TSample> referenceFrame = this.sourcePictures[this.referenceEntries[reference]];
        if (input.SpeedFeatures.SubpelForceStop != SearchPrecision.Integer)
        {
            Span<TSample> prediction = this.predictor;
            this.PredictSingle(referenceFrame, 0, vector, x, y, prediction, Size, Size, Size);
            return usePredictionSad
                ? this.GetSad(source, sourceStride, prediction, Size)
                : this.GetSatdCost(source, sourceStride, prediction, Size);
        }

        // Without a fractional search the prediction is the reference block at the whole-sample vector.
        PlaneAccess plane = GetPlane(referenceFrame, 0);
        Point full = ToFullPixel(vector);
        ReadOnlySpan<TSample> block = plane.Samples[plane.IndexOf(x + full.X, y + full.Y)..];
        return usePredictionSad
            ? this.GetSad(source, sourceStride, block, plane.Stride)
            : this.GetSatdCost(source, sourceStride, block, plane.Stride);
    }

    /// <summary>
    /// Returns the absolute difference of a 16x16 block in the 8-bit error domain. Reference: the fn_ptr sdf of
    /// BLOCK_16X16, whose high-bit-depth variants shift by the extra precision.
    /// </summary>
    private int GetSad(ReadOnlySpan<TSample> source, int sourceStride, ReadOnlySpan<TSample> prediction, int predictionStride)
    {
        const int Size = Av1TplModelConstants.BlockSize;
        int sad = TSearchOperator.SumAbsoluteDifferences(source, sourceStride, prediction, predictionStride, Size, Size, 1);
        return sad >> (this.bitDepth.GetBitCount() - 8);
    }

    /// <summary>
    /// Returns the sum of the magnitudes of the DCT coefficients of a 16x16 residual. Reference: tpl_get_satd_cost().
    /// </summary>
    private int GetSatdCost(ReadOnlySpan<TSample> source, int sourceStride, ReadOnlySpan<TSample> prediction, int predictionStride)
    {
        const int Size = Av1TplModelConstants.BlockSize;
        TSampleOperator.Subtract(source, sourceStride, prediction, predictionStride, this.residual, Size, Size);
        Av1ForwardTransformer.Transform2d(
            this.residual,
            this.coefficients,
            Size,
            Av1TransformType.DctDct,
            Av1TransformSize.Size16x16,
            this.bitDepth.GetBitCount(),
            this.transformWorkspace);

        return (int)Av1CoefficientMeasures.SumAbsolute(this.coefficients.AsSpan(0, Size * Size));
    }

    /// <summary>
    /// A reference picture for prediction, or an absent one.
    /// </summary>
    private readonly struct ReferencePicture
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReferencePicture"/> struct.
        /// </summary>
        /// <param name="frame">The picture.</param>
        /// <param name="isPresent">Whether the picture exists.</param>
        public ReferencePicture(Av1EncoderFrame<TSample> frame, bool isPresent)
        {
            this.Frame = frame;
            this.IsPresent = isPresent;
        }

        /// <summary>
        /// Gets the picture.
        /// </summary>
        public Av1EncoderFrame<TSample> Frame { get; }

        /// <summary>
        /// Gets a value indicating whether the picture exists.
        /// </summary>
        public bool IsPresent { get; }
    }
}

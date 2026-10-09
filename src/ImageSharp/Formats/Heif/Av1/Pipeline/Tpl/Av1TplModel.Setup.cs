// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <content>
/// Runs the model over a golden group: assigns sources and references, measures every frame, and propagates the
/// dependencies backwards.
/// </content>
internal sealed partial class Av1TplModel<TSample, TSearchOperator, TSampleOperator>
{
    /// <summary>
    /// Measures and propagates the statistics of the golden group that starts at the current frame.
    /// An approximate evaluation measures only the lower alternate layers, and decides if the group stays long.
    /// </summary>
    /// <param name="input">The encoder state that the run reads.</param>
    /// <param name="approximateEvaluation">False for the coding run. True for the length evaluation of the golden group.</param>
    /// <returns>
    /// For an evaluation, one keeps the longer group and zero shortens it. A coding run gives zero, and a group with no alternate layers gives one.
    /// </returns>
    public int SetupStatistics(Av1TplSetupInput<TSample> input, bool approximateEvaluation)
    {
        Av1TplGroup group = input.Group;
        int gopLengthDecisionMethod = input.SpeedFeatures.GopLengthDecisionMethod;

        // The group setup visits every entry and leaves the frame type of the last entry as the current frame type. The
        // rate multiplier of the model reads that frame type.
        this.lastEntryIsKeyFrame = group.Size > 0 ? group.IsKeyFrame[group.Size - 1] : input.IsKeyFrame;

        // Only the external rate control reads the number of look-ahead frames past the group.
        _ = this.InitializeGroupFrames(input, out int groupFrames, out int modelQIndex);
        this.BaseLayerQIndex = modelQIndex;
        this.MeasuredFrame = false;
        this.InitializeStatistics();

        // A key frame restores the default vector distributions before the costs are captured.
        this.FillMotionVectorCosts(input);

        // Every block of the model takes its bounds from the first tile.
        this.tileModeInfoRowEnd = input.TileModeInfoRowEnd;
        this.tileModeInfoColumnEnd = input.TileModeInfoColumnEnd;

        // The model runs before the frame sets its own speed features. Thus the leaf frame reduction is off here for the
        // first group of a key frame interval, and for groups with a layer depth of two or less.
        bool reduceNumberOfFrames = input.SpeedFeatures.ReduceNumberOfFrames &&
            group.UpdateType[0] != Av1FrameUpdateType.Key &&
            group.MaximumLayerDepth > 2;

        // A skip of the leaf frames changes the importance measure. A factor of 1.6 compensates, because the model then
        // measures about sixty percent of a group.
        this.R0AdjustFactor = reduceNumberOfFrames ? 1.6 : 1.0;

        for (int frameIndex = 0; frameIndex < groupFrames; frameIndex++)
        {
            if (SkipFrame(group, frameIndex, gopLengthDecisionMethod, approximateEvaluation, reduceNumberOfFrames))
            {
                continue;
            }

            this.InitializeFlowDispenser(input, frameIndex, modelQIndex);
            this.MeasuredFrame = true;
            this.DispenseFlow(input);

            // Luma only models leave the chroma of their reconstructions untouched.
            this.GetReconstruction(frameIndex).ExtendBorders();
        }

        // Backward propagation from the last frame to the second. The first frame references nothing in the group.
        for (int frameIndex = groupFrames - 1; frameIndex >= 0; frameIndex--)
        {
            if (SkipFrame(group, frameIndex, gopLengthDecisionMethod, approximateEvaluation, reduceNumberOfFrames))
            {
                continue;
            }

            this.SynthesizeFlow(frameIndex);
        }

        if (!approximateEvaluation)
        {
            this.Ready = true;
        }

        if (group.MaximumLayerDepthAllowed == 0)
        {
            return 1;
        }

        if (!approximateEvaluation)
        {
            return 0;
        }

        Span<double> beta = stackalloc double[2];
        int firstIndex = group.ArfIndex;
        int secondIndex = Math.Min(groupFrames - 1, group.ArfIndex + 1);
        beta[0] = Av1TplDecisions.GetFrameImportance(this.GetFrame(firstIndex));
        beta[1] = Av1TplDecisions.GetFrameImportance(this.GetFrame(secondIndex));
        return EvaluateGopLength(beta, gopLengthDecisionMethod);
    }

    /// <summary>
    /// Decides the golden group length from the importance of the base alternate reference and of the next layer.
    /// </summary>
    /// <param name="beta">The importance of the base alternate reference and of the next layer.</param>
    /// <param name="gopLengthDecisionMethod">The decision method of the speed, zero or one.</param>
    /// <returns>One to keep the longer group, zero to shorten it.</returns>
    private static int EvaluateGopLength(ReadOnlySpan<double> beta, int gopLengthDecisionMethod)
    {
        switch (gopLengthDecisionMethod)
        {
            case 0:
                // Keep the long group only if the base layer importance is more than 1.4 and at least 0.1 above the next layer.
                return (beta[0] < beta[1] + 0.1) || beta[0] <= 1.4 ? 0 : 1;
            case 1:
                return beta[0] > 1.1 ? 1 : 0;
            default:
                throw new InvalidOperationException("The golden group length decision method is disabled.");
        }
    }

    /// <summary>
    /// Returns whether a group entry is not measured. Overlays are never measured. An approximate evaluation skips higher
    /// layers and extension frames. A reduced measurement skips the leaf frames of the group.
    /// </summary>
    /// <param name="group">The golden group.</param>
    /// <param name="frameIndex">The group entry.</param>
    /// <param name="gopLengthDecisionMethod">The decision method of the speed.</param>
    /// <param name="approximateEvaluation">True when the run is an approximate length evaluation.</param>
    /// <param name="reduceNumberOfFrames">True when the leaf frames get no measure.</param>
    /// <returns>True when the entry is skipped.</returns>
    private static bool SkipFrame(Av1TplGroup group, int frameIndex, int gopLengthDecisionMethod, bool approximateEvaluation, bool reduceNumberOfFrames)
    {
        // Method zero measures the base layer and two more alternate layers. Method one measures only one more layer.
        int alternateLayers = gopLengthDecisionMethod == 0 ? 3 : 2;
        int gopLength = GetGopLength(group);
        if (group.UpdateType[frameIndex] is Av1FrameUpdateType.IntermediateOverlay or Av1FrameUpdateType.Overlay)
        {
            return true;
        }

        if (approximateEvaluation && (group.LayerDepth[frameIndex] > alternateLayers || frameIndex >= gopLength))
        {
            return true;
        }

        return reduceNumberOfFrames && group.UpdateType[frameIndex] == Av1FrameUpdateType.Last && frameIndex < gopLength;
    }

    /// <summary>
    /// Returns the number of group entries that the model can hold.
    /// </summary>
    /// <param name="group">The golden group.</param>
    /// <returns>The group size, at most one less than <see cref="Av1TplModelConstants.MaximumFrameIndex"/>.</returns>
    private static int GetGopLength(Av1TplGroup group) => Math.Min(group.Size, Av1TplModelConstants.MaximumFrameIndex - 1);

    /// <summary>
    /// Returns the pyramid level that ranks a frame for reference mapping. The frame at display order zero gets the lowest
    /// level. Layer depth 6 maps to the maximum layer depth of the group, and layer depth 7 maps to the lowest level.
    /// </summary>
    /// <param name="frameLevel">The layer depth of the frame.</param>
    /// <param name="frameOrder">The display order of the frame.</param>
    /// <param name="maximumLayerDepth">The largest layer depth of the group.</param>
    /// <returns>The pyramid level, at least one.</returns>
    private static int GetTruePyramidLevel(int frameLevel, int frameOrder, int maximumLayerDepth)
    {
        const int MaximumArfLayers = 6;
        const int MinimumPyramidLevel = 1;
        if (frameOrder == 0)
        {
            return MinimumPyramidLevel;
        }

        if (frameLevel == MaximumArfLayers)
        {
            return maximumLayerDepth;
        }

        return frameLevel == MaximumArfLayers + 1 ? MinimumPyramidLevel : Math.Max(MinimumPyramidLevel, frameLevel);
    }

    /// <summary>
    /// Returns the first slot of a refresh mask, or -1.
    /// </summary>
    /// <param name="refreshMask">The refresh mask, one bit per slot.</param>
    /// <returns>The lowest set bit position, or -1 for an empty mask.</returns>
    private static int GetRefreshSlot(int refreshMask)
    {
        if (refreshMask == 0)
        {
            return -1;
        }

        int slot = 0;
        while (((refreshMask >> slot) & 1) == 0)
        {
            slot++;
        }

        return slot;
    }

    /// <summary>
    /// Assigns the source, the reconstruction, the statistics storage and the reference mapping of every frame of the
    /// group and of the look-ahead frames past it. It simulates the reference slot updates.
    /// </summary>
    /// <param name="input">The encoder state.</param>
    /// <param name="groupFrames">Receives the number of model frames.</param>
    /// <param name="modelQIndex">Receives the leaf quantizer of the model.</param>
    /// <returns>The number of look-ahead frames past the group.</returns>
    private int InitializeGroupFrames(Av1TplSetupInput<TSample> input, out int groupFrames, out int modelQIndex)
    {
        Av1TplGroup group = input.Group;
        IAv1TplReferenceMapper mapper = input.ReferenceMapper!;
        modelQIndex = 0;
        int leafQIndex = modelQIndex;
        const int SlotCount = Av1TplModelConstants.ReferenceFrameSlotCount;
        Span<int> pairOrders = stackalloc int[SlotCount];
        Span<int> pairLevels = stackalloc int[SlotCount];
        input.PairDisplayOrders.CopyTo(pairOrders);
        input.PairPyramidLevels.CopyTo(pairLevels);
        Span<int> remapped = stackalloc int[SlotCount];
        Span<int> pictureMap = stackalloc int[SlotCount];
        bool hasPreviousArf = false;

        // The reference slots are frames -1 to -8. A key frame starts without references. Otherwise each slot lends its
        // reconstruction as both source and reconstruction, except the saved alternate reference of the previous group,
        // whose source and model reconstruction were kept apart.
        for (int slot = 0; slot < SlotCount; slot++)
        {
            int entry = GetEntry(-slot - 1);
            if (input.IsKeyFrame)
            {
                this.sourceIds[entry] = NoPicture;
                this.reconstructionIds[entry] = NoPicture;
                this.frames[entry].DisplayIndex = 0;
            }
            else
            {
                if (input.SlotDisplayOrderHints[slot] == this.PreviousArfDisplayOrder)
                {
                    this.sourcePictures[entry] = this.previousArfSource.Frame;
                    this.sourceIds[entry] = PreviousArfSourceId;
                    this.reconstructionPictures[entry] = this.previousArfReconstruction.Frame;
                    this.reconstructionIds[entry] = PreviousArfReconstructionId;
                    hasPreviousArf = true;
                }
                else
                {
                    this.sourcePictures[entry] = input.SlotFrames[slot];
                    this.sourceIds[entry] = SlotIdBase + input.SlotBufferIds[slot];
                    this.reconstructionPictures[entry] = input.SlotFrames[slot];
                    this.reconstructionIds[entry] = SlotIdBase + input.SlotBufferIds[slot];
                }

                this.frames[entry].DisplayIndex = input.SlotDisplayOrderHints[slot];
            }

            pictureMap[slot] = -slot - 1;
        }

        if (!input.IsKeyFrame && !hasPreviousArf)
        {
            this.PreviousArfDisplayOrder = -1;
        }

        groupFrames = 0;
        int processFrameCount = 0;
        int gopLength = GetGopLength(group);
        int groupIndex;
        for (groupIndex = 0; groupIndex < gopLength; groupIndex++)
        {
            int entry = GetEntry(groupIndex);
            Av1TplFrameStatistics frame = this.frames[entry];
            Av1FrameUpdateType updateType = group.UpdateType[groupIndex];
            int lookaheadIndex = group.CurrentFrameIndex[groupIndex] + group.ArfSourceOffset[groupIndex];
            bool showExistingFrame = updateType is Av1FrameUpdateType.IntermediateOverlay or Av1FrameUpdateType.Overlay;

            if (updateType == Av1FrameUpdateType.Last)
            {
                modelQIndex = group.QIndex[groupIndex];
                leafQIndex = modelQIndex;
                if (input.AdjustLeafQuantizer)
                {
                    // The model codes leaf frames at a lower quantizer. The step shrinks by up to one half as the
                    // quantizer grows, and the clamp keeps the result in the working range of the model.
                    double q = Av1TplRateDistortion.ConvertQIndexToQ(modelQIndex, this.bitDepth);
                    double qIndexRatio = (double)modelQIndex / Av1Constants.MaxQ;
                    double qStepRatio = 1.0 - (qIndexRatio * qIndexRatio * 0.5);
                    int deltaQIndex = Av1TplRateDistortion.ComputeQDelta(q, q * qStepRatio, this.bitDepth, input.BestQuality, input.WorstQuality);
                    modelQIndex = Math.Clamp(modelQIndex + deltaQIndex, MinimumQIndex, MaximumQIndex);
                }
            }

            if (lookaheadIndex < 0 || lookaheadIndex >= input.LookaheadCount)
            {
                break;
            }

            this.sourcePictures[entry] = input.Lookahead[lookaheadIndex];
            this.sourceIds[entry] = LookaheadIdBase + lookaheadIndex;

            // The filtered source makes the statistics more precise.
            if (input.HasFilteredFrame[groupIndex])
            {
                this.sourcePictures[entry] = input.FilteredFrames[groupIndex];
                this.sourceIds[entry] = FilteredIdBase + groupIndex;
            }

            frame.DisplayIndex = lookaheadIndex + input.FrameNumber;
            if (!showExistingFrame)
            {
                this.AttachPool(entry, processFrameCount);
                processFrameCount++;
            }

            int trueDisplay = frame.DisplayIndex;
            mapper.GetReferenceFrames(pairOrders, pairLevels, trueDisplay, groupIndex, remapped);
            int refreshMask = mapper.GetRefreshFrameFlags(groupIndex, updateType, showExistingFrame, trueDisplay, pairOrders, pairLevels);

            // Frames that no other frame references refresh nothing.
            if (group.IsNonReference[groupIndex])
            {
                refreshMask = 0;
            }

            int refreshSlot = GetRefreshSlot(refreshMask);
            if (refreshSlot >= 0)
            {
                pairOrders[refreshSlot] = Math.Max(0, trueDisplay);
                pairLevels[refreshSlot] = GetTruePyramidLevel(group.LayerDepth[groupIndex], trueDisplay, group.MaximumLayerDepth);
            }

            for (int reference = 0; reference < Av1TplModelConstants.InterReferenceCount; reference++)
            {
                frame.ReferenceMapIndex[reference] = pictureMap[remapped[reference]];
            }

            if (refreshMask != 0)
            {
                pictureMap[refreshSlot] = groupIndex;
            }

            groupFrames++;
        }

        // Past the group, the model measures more look-ahead frames as leaf frames that reference only the nearer frames.
        // Their number is at most the look-ahead depth minus the longest golden interval, and they stop before the next key frame.
        int extension = input.LagInFrames - Av1TplModelConstants.MaximumGoldenInterval;
        int extendFrameCount = 0;
        int extendFrameLength = Math.Min(extension, input.FramesToKey - input.BaselineGoldenInterval);
        int frameDisplayIndex = group.CurrentFrameIndex[gopLength - 1] + group.ArfSourceOffset[gopLength - 1] + 1;
        for (; groupIndex < Av1TplModelConstants.MaximumFrameIndex && extendFrameCount < extendFrameLength; groupIndex++)
        {
            int entry = GetEntry(groupIndex);
            Av1TplFrameStatistics frame = this.frames[entry];
            int lookaheadIndex = frameDisplayIndex;
            if (lookaheadIndex < 0 || lookaheadIndex >= input.LookaheadCount)
            {
                break;
            }

            this.sourcePictures[entry] = input.Lookahead[lookaheadIndex];
            this.sourceIds[entry] = LookaheadIdBase + lookaheadIndex;
            this.AttachPool(entry, processFrameCount);
            frame.DisplayIndex = frameDisplayIndex + input.FrameNumber;
            processFrameCount++;

            group.UpdateType[groupIndex] = Av1FrameUpdateType.Last;

            // The extension entries record the unadjusted leaf quantizer, not the lower quantizer of the model.
            group.QIndex[groupIndex] = leafQIndex;
            int trueDisplay = frame.DisplayIndex;
            mapper.GetReferenceFrames(pairOrders, pairLevels, trueDisplay, groupIndex, remapped);
            int refreshMask = mapper.GetRefreshFrameFlags(groupIndex, Av1FrameUpdateType.Last, false, trueDisplay, pairOrders, pairLevels);
            int refreshSlot = GetRefreshSlot(refreshMask);
            if (refreshSlot >= 0)
            {
                pairOrders[refreshSlot] = Math.Max(0, trueDisplay);
                pairLevels[refreshSlot] = GetTruePyramidLevel(group.LayerDepth[groupIndex], trueDisplay, group.MaximumLayerDepth);
            }

            for (int reference = 0; reference < Av1TplModelConstants.InterReferenceCount; reference++)
            {
                frame.ReferenceMapIndex[reference] = pictureMap[remapped[reference]];
            }

            // Extension frames reference only LAST, LAST2 and GOLDEN.
            frame.ReferenceMapIndex[(int)Av1ReferenceFrameType.Alternate - 1] = -1;
            frame.ReferenceMapIndex[(int)Av1ReferenceFrameType.Last3 - 1] = -1;
            frame.ReferenceMapIndex[(int)Av1ReferenceFrameType.Backward - 1] = -1;
            frame.ReferenceMapIndex[(int)Av1ReferenceFrameType.Alternate2 - 1] = -1;

            if (refreshMask != 0)
            {
                pictureMap[refreshSlot] = groupIndex;
            }

            groupFrames++;
            extendFrameCount++;
            frameDisplayIndex++;
        }

        return extendFrameCount;
    }

    /// <summary>
    /// Lends the statistics storage and the reconstruction of a pool entry to a frame entry.
    /// </summary>
    /// <param name="entry">The frame entry that receives the storage.</param>
    /// <param name="poolIndex">The pool entry that lends the storage.</param>
    private void AttachPool(int entry, int poolIndex)
    {
        // Each pool entry is one equal slice of the single statistics allocation.
        int length = this.statisticsEntryLength;
        this.frames[entry].AttachStatistics(this.statisticsPool.Memory.Slice(poolIndex * length, length));
        this.reconstructionPictures[entry] = this.reconstructionPool[poolIndex].Frame;
        this.reconstructionIds[entry] = poolIndex;
    }

    /// <summary>
    /// Returns the storage entry of a model frame index. The reserved entry and the reference slots come first.
    /// </summary>
    /// <param name="frameIndex">The model frame index, -8 for the last reference slot.</param>
    /// <returns>The storage entry.</returns>
    private static int GetEntry(int frameIndex) => frameIndex + Av1TplModelConstants.ReferenceFrameSlotCount + 1;

    /// <summary>
    /// Propagates the dependencies of every block of a frame to the frames that it references. The first frame of the
    /// group propagates nothing.
    /// </summary>
    /// <param name="frameIndex">The group index of the frame.</param>
    private void SynthesizeFlow(int frameIndex)
    {
        if (frameIndex == 0)
        {
            return;
        }

        int step = 1 << Av1TplModelConstants.BlockModeInfoLog2;
        Av1TplFrameStatistics frame = this.GetFrame(frameIndex);
        Span<Av1TplBlockStatistics> blocks = frame.Statistics;
        for (int row = 0; row < this.ModeInfoRows; row += step)
        {
            for (int column = 0; column < this.ModeInfoColumns; column += step)
            {
                // Each block propagates through its first and its second prediction.
                ref Av1TplBlockStatistics block = ref blocks[GetBlockPosition(frame, row, column)];
                this.UpdateBlock(frame, ref block, row, column, 0);
                this.UpdateBlock(frame, ref block, row, column, 1);
            }
        }
    }

    /// <summary>
    /// Returns the index of a block in the statistics of a frame.
    /// </summary>
    /// <param name="frame">The frame statistics.</param>
    /// <param name="modeInfoRow">The mode-information row of the block.</param>
    /// <param name="modeInfoColumn">The mode-information column of the block.</param>
    /// <returns>The index in <see cref="Av1TplFrameStatistics.Statistics"/>.</returns>
    private static int GetBlockPosition(Av1TplFrameStatistics frame, int modeInfoRow, int modeInfoColumn)
        => Av1TplFrameStatistics.GetPosition(modeInfoRow, modeInfoColumn, frame.Stride, Av1TplModelConstants.BlockModeInfoLog2);

    /// <summary>
    /// Spreads the dependency of one prediction of a block over the up to four blocks of the referenced frame that its
    /// motion-compensated footprint overlaps. The overlap area weights each share.
    /// </summary>
    /// <param name="frame">The statistics of the frame of the block.</param>
    /// <param name="block">The statistics of the block.</param>
    /// <param name="modeInfoRow">The mode-information row of the block.</param>
    /// <param name="modeInfoColumn">The mode-information column of the block.</param>
    /// <param name="reference">The prediction of the block, zero or one.</param>
    private void UpdateBlock(Av1TplFrameStatistics frame, ref Av1TplBlockStatistics block, int modeInfoRow, int modeInfoColumn, int reference)
    {
        bool compound = block.ReferenceFrameIndex[1] >= 0;
        if (block.ReferenceFrameIndex[reference] < 0)
        {
            return;
        }

        int referenceFrameIndex = block.ReferenceFrameIndex[reference];
        int referenceMapIndex = frame.ReferenceMapIndex[referenceFrameIndex];
        if (referenceMapIndex < 0)
        {
            return;
        }

        Av1TplFrameStatistics referenceFrame = this.GetFrame(referenceMapIndex);
        Span<Av1TplBlockStatistics> referenceStatistics = referenceFrame.Statistics;
        Point fullVector = ToFullPixel(block.MotionVectors[referenceFrameIndex]);
        int referenceRow = (modeInfoRow * 4) + fullVector.Y;
        int referenceColumn = (modeInfoColumn * 4) + fullVector.X;
        const int Size = Av1TplModelConstants.BlockSize;
        const int ModeInfoSize = Size >> 2;
        const int PixelCount = Size * Size;

        // The top-left grid-aligned block of the footprint.
        int gridRowBase = RoundFloor(referenceRow, Size) * Size;
        int gridColumnBase = RoundFloor(referenceColumn, Size) * Size;

        // A compound block propagates through each reference the distortion measured with the other reconstructed.
        long sourceDistortion = compound ? block.CompoundReconstructedDistortion[reference == 0 ? 1 : 0] : block.SourceReferenceDistortion;
        long sourceRate = compound
            ? (long)block.CompoundReconstructedRate[reference == 0 ? 1 : 0] << Av1TplModelConstants.DependencyCostScaleLog2
            : (long)block.SourceReferenceRate << Av1TplModelConstants.DependencyCostScaleLog2;

        long currentDependencyDistortion = block.ReconstructedReferenceDistortion - sourceDistortion;
        long dependencyDistortion = (long)(block.DependencyDistortion *
            ((double)(block.ReconstructedReferenceDistortion - sourceDistortion) / block.ReconstructedReferenceDistortion));

        long deltaRate = ((long)block.ReconstructedReferenceRate << Av1TplModelConstants.DependencyCostScaleLog2) - sourceRate;
        long dependencyRate = GetDeltaRateCost(block.DependencyRate, block.ReconstructedReferenceDistortion, sourceDistortion, PixelCount);

        for (int corner = 0; corner < 4; corner++)
        {
            int gridRow = gridRowBase + (Size * (corner >> 1));
            int gridColumn = gridColumnBase + (Size * (corner & 1));
            if (gridRow >= 0 && gridRow < referenceFrame.ModeInfoRows * 4 &&
                gridColumn >= 0 && gridColumn < referenceFrame.ModeInfoColumns * 4)
            {
                int overlapArea = GetOverlapArea(gridRow, gridColumn, referenceRow, referenceColumn, Size, Size);
                int referenceModeInfoRow = RoundFloor(gridRow, Size) * ModeInfoSize;
                int referenceModeInfoColumn = RoundFloor(gridColumn, Size) * ModeInfoSize;
                ref Av1TplBlockStatistics destination = ref referenceStatistics[Av1TplFrameStatistics.GetPosition(
                    referenceModeInfoRow,
                    referenceModeInfoColumn,
                    referenceFrame.Stride,
                    Av1TplModelConstants.BlockModeInfoLog2)];

                destination.DependencyDistortion += (currentDependencyDistortion + dependencyDistortion) * overlapArea / PixelCount;
                destination.DependencyRate += (deltaRate + dependencyRate) * overlapArea / PixelCount;
            }
        }
    }

    /// <summary>
    /// Returns the area that two blocks of the same size share, or zero.
    /// </summary>
    /// <param name="rowA">The top row of the first block.</param>
    /// <param name="columnA">The left column of the first block.</param>
    /// <param name="rowB">The top row of the second block.</param>
    /// <param name="columnB">The left column of the second block.</param>
    /// <param name="width">The width of both blocks.</param>
    /// <param name="height">The height of both blocks.</param>
    /// <returns>The overlap area in samples.</returns>
    private static int GetOverlapArea(int rowA, int columnA, int rowB, int columnB, int width, int height)
    {
        int minimumRow = Math.Max(rowA, rowB);
        int maximumRow = Math.Min(rowA + height, rowB + height);
        int minimumColumn = Math.Max(columnA, columnB);
        int maximumColumn = Math.Min(columnA + width, columnB + width);
        if (minimumRow < maximumRow && minimumColumn < maximumColumn)
        {
            return (maximumRow - minimumRow) * (maximumColumn - minimumColumn);
        }

        return 0;
    }

    /// <summary>
    /// Returns the floor of a position divided by a block size. Integer division truncates toward zero, so negative
    /// positions need their own branch.
    /// </summary>
    /// <param name="position">The position in samples. It can be negative.</param>
    /// <param name="blockSize">The block size in samples.</param>
    /// <returns>The block index.</returns>
    private static int RoundFloor(int position, int blockSize)
        => position < 0 ? -(1 + ((-position - 1) / blockSize)) : position / blockSize;

    /// <summary>
    /// Returns the rate that the dependency of a referencing block costs. The model is exponential in the rate saved per
    /// sample and uses the ratio beta of the source-reference distortion to the reconstructed-reference distortion. The
    /// cost saturates when beta times two to the power of twice the rate per sample is more than ten. Blocks with a
    /// source distortion of 128 or less keep the accumulated rate.
    /// </summary>
    /// <param name="deltaRate">The accumulated dependency rate.</param>
    /// <param name="reconstructedDistortion">The reconstructed-reference distortion.</param>
    /// <param name="sourceDistortion">The source-reference distortion.</param>
    /// <param name="pixelCount">The number of samples of the block.</param>
    /// <returns>The scaled rate cost.</returns>
    public static long GetDeltaRateCost(long deltaRate, long reconstructedDistortion, long sourceDistortion, int pixelCount)
    {
        double beta = (double)sourceDistortion / reconstructedDistortion;
        long rateCost = deltaRate;
        if (sourceDistortion <= 128)
        {
            return rateCost;
        }

        const int Shift = Av1TplModelConstants.DependencyCostScaleLog2 + Av1TplModelConstants.ProbabilityCostShift;
        double deltaRatePerSample = (double)(deltaRate >> Shift) / pixelCount;
        double logDenominator = (Math.Log(beta) / Math.Log(2.0)) + (2.0 * deltaRatePerSample);
        if (logDenominator > Math.Log(10.0) / Math.Log(2.0))
        {
            rateCost = (long)(Math.Log(1.0 / beta) * pixelCount / Math.Log(2.0) / 2.0);
            rateCost <<= Shift;
            return rateCost;
        }

        double numerator = Math.Pow(2.0, logDenominator);
        double denominator = (numerator * beta) + ((1 - beta) * beta);
        rateCost = (long)(pixelCount * Math.Log(numerator / denominator) / Math.Log(2.0) / 2.0);
        rateCost <<= Shift;
        return rateCost;
    }

    /// <summary>
    /// Converts an eighth-sample vector to whole samples, with halves rounded away from zero.
    /// </summary>
    /// <param name="vector">The vector in eighth samples.</param>
    /// <returns>The vector in whole samples, column first.</returns>
    private static Point ToFullPixel(Av1MotionVector vector)
        => new(RawPixel(vector.Column), RawPixel(vector.Row));

    /// <summary>
    /// Rounds an eighth-sample component to whole samples, with halves rounded away from zero. A positive value adds 4
    /// before the arithmetic shift. A negative value adds 3, because the shift rounds toward negative infinity.
    /// </summary>
    /// <param name="value">The component in eighth samples.</param>
    /// <returns>The component in whole samples.</returns>
    private static int RawPixel(int value) => (value + 3 + (value >= 0 ? 1 : 0)) >> 3;
}

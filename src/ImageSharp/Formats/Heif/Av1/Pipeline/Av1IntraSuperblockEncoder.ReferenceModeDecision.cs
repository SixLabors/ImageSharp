// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Provides reference-frame and intra-block-copy mode decisions.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Gets the transform that predicts a skipped block, per block size. Reference: max_predict_sf_tx_size.
        /// </summary>
        private static ReadOnlySpan<Av1TransformSize> PredictSkipTransformSizes =>
        [
            Av1TransformSize.Size4x4, Av1TransformSize.Size4x8, Av1TransformSize.Size8x4, Av1TransformSize.Size8x8,
            Av1TransformSize.Size8x16, Av1TransformSize.Size16x8, Av1TransformSize.Size16x16, Av1TransformSize.Size16x16,
            Av1TransformSize.Size16x16, Av1TransformSize.Size16x16, Av1TransformSize.Size16x16, Av1TransformSize.Size16x16,
            Av1TransformSize.Size16x16, Av1TransformSize.Size16x16, Av1TransformSize.Size16x16, Av1TransformSize.Size16x16,
            Av1TransformSize.Size4x16, Av1TransformSize.Size16x4, Av1TransformSize.Size8x8, Av1TransformSize.Size8x8,
            Av1TransformSize.Size16x16, Av1TransformSize.Size16x16
        ];

        /// <summary>
        /// Gets the coefficient thresholds of a predicted skip, per bit depth and block size.
        /// Reference: skip_pred_threshold.
        /// </summary>
        private static uint[][] SkipPredictionThresholds { get; } =
        [
            [64, 64, 64, 70, 60, 60, 68, 68, 68, 68, 68, 68, 68, 68, 68, 68, 64, 64, 70, 70, 68, 68],
            [88, 88, 88, 86, 87, 87, 68, 68, 68, 68, 68, 68, 68, 68, 68, 68, 88, 88, 86, 86, 68, 68],
            [90, 93, 93, 90, 93, 93, 74, 74, 74, 74, 74, 74, 74, 74, 74, 74, 90, 90, 90, 90, 74, 74]
        ];

        /// <summary>
        /// Compares legal same-frame displacements with the retained intra winner.
        /// </summary>
        private Av1RateDistortionStatistics SelectIntraBlockCopy(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1RateDistortionStatistics regularStatistics,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            InlineArray8<Av1MotionVector> referenceCandidates = default;
            InlineArray8<int> referenceWeights = default;
            Av1MotionVector reference = Av1IntraBlockCopy.FindReference(
                this.picture,
                macroBlock,
                modeInfoPosition,
                blockSize,
                modeInfo.Block.PartitionType,
                referenceCandidates,
                referenceWeights);

            InlineArray2<Av1MotionVector> candidates = default;
            Av1MotionSearchSettings settings = this.picture.Parent.MotionSearchSettings;
            Av1MotionVectorCosts costs = this.blockWorkspace.GetDisplacementVectorCosts();

            // The displacement search reads the source frame, and only the rate-distortion trial below
            // predicts from the reconstruction. Reference: the xd->cur_buf, which is cpi->source, that
            // rd_pick_intrabc_mode_sb() passes to av1_setup_pred_block().
            int candidateCount = this.picture.IntraBlockCopySearch.FindCandidates<TSample, TOperator>(
                this.source.GetPlane(Av1Plane.Y),
                this.source.GetPlane(Av1Plane.Y),
                blockOrigin,
                blockSize,
                macroBlock.Tile,
                this.picture.Sequence.SequenceHeader,
                costs,
                reference,
                this.quantization.QIndex[0],
                this.rateMultiplier,
                this.picture.Parent.MotionSearchStepParameter,
                settings,
                this.blockWorkspace.GetMotionSearchSites(settings.GetFullPixelMethod(blockSize), this.reconstruction.GetPlane(Av1Plane.Y).Stride),
                candidates);

            if (Entropy.Av1SymbolWriter.DiagnosticSymbolTrace is not null)
            {
                System.Text.StringBuilder ibc = new($"IBC {blockOrigin.X},{blockOrigin.Y} {blockSize} ref {reference.Row},{reference.Column} n {candidateCount}");
                for (int i = 0; i < candidateCount; i++)
                {
                    ibc.Append(System.Globalization.CultureInfo.InvariantCulture, $" dv {candidates[i].Row},{candidates[i].Column}");
                }

                Entropy.Av1SymbolWriter.DiagnosticSymbolTrace.Add(ibc.ToString());
            }

            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Av1RateDistortionStatistics selectedStatistics = regularStatistics;
            Av1MotionVector selectedVector = default;
            InlineArray128<Av1EncoderTransformBlockState> selectedStates = default;
            InlineArray16<Av1TransformSize> selectedSizes = default;
            bool selected = false;
            bool selectedSkip = false;
            this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
            ref Av1ReferenceMotionVectors referenceMotionVectors = ref this.blockWorkspace.ReferenceMotionVectors;
            for (int index = 0; index < candidateCount; index++)
            {
                Av1MotionVector vector = candidates[index];
                int predictionRate = writer.GetUseIntraBlockCopyCost(true) + costs.GetDisplacementVectorCost(vector, reference);
                Av1RateDistortionStatistics statistics = this.EvaluateInterCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    long.MaxValue,
                    block.HasChroma,
                    predictionRate,
                    Av1ReferenceFrameType.Intra,
                    vector,
                    default,
                    Av1PredictionMode.DC,
                    Av1ReferenceFrameType.None,
                    usePreparedPrediction: false,
                    Av1CompoundType.Average,
                    0,
                    false,
                    Av1DifferenceWeightedMaskType.Type38,
                    0,
                    Av1InterpolationFilter.Bilinear,
                    Av1InterpolationFilter.Bilinear,
                    0,
                    false,
                    default,
                    false,
                    0,
                    in referenceMotionVectors,
                    workspace.LumaCandidateReconstruction,
                    workspace.LumaCandidateCoefficients,
                    workspace.BlueCandidateReconstruction,
                    workspace.BlueCandidateCoefficients,
                    workspace.RedCandidateReconstruction,
                    workspace.RedCandidateCoefficients,
                    out bool skip,
                    out InlineArray64<Av1EncoderTransformBlockState> lumaStates,
                    out InlineArray16<Av1TransformSize> sizes,
                    out InlineArray16<Av1EncoderTransformBlockState> blueStates,
                    out InlineArray16<Av1EncoderTransformBlockState> redStates);

                Entropy.Av1SymbolWriter.DiagnosticSymbolTrace?.Add(
                    $"IBCRD {blockOrigin.X},{blockOrigin.Y} {blockSize} dv {vector.Row},{vector.Column} mvrate {predictionRate} rate {statistics.Rate} dist {statistics.Distortion} rd {statistics.Cost} best {Math.Min(this.blockCostLimit, selectedStatistics.Cost)}");

                // Earlier intra modes and displacement candidates retain equal-cost ties.
                if (statistics.Cost < Math.Min(this.blockCostLimit, selectedStatistics.Cost))
                {
                    selected = true;
                    selectedStatistics = statistics;
                    selectedVector = vector;
                    selectedSkip = skip;
                    selectedSizes = sizes;
                    lumaStates[..].CopyTo(selectedStates);
                    blueStates[..].CopyTo(selectedStates[64..80]);
                    redStates[..].CopyTo(selectedStates[80..96]);
                }
            }

            if (selected)
            {
                // Publish syntax and reconstruction together after every displacement has been compared.
                // Rejected copies never alter pixels used as predictors by later coding blocks.
                modeInfo.Block.Mode = Av1PredictionMode.DC;
                modeInfo.Block.UvMode = Av1ChromaPredictionMode.DC;
                modeInfo.Block.ReferenceFrame = Av1ReferenceFrameType.Intra;
                modeInfo.Block.SecondaryReferenceFrame = Av1ReferenceFrameType.None;
                modeInfo.Block.TransformSize = this.picture.Parent.FrameHeader.CodedLossless
                    ? Av1TransformSize.Size4x4
                    : blockSize.GetMaximumTransformSize();
                selectedSizes[..].CopyTo(modeInfo.Block.InterTransformSizes);
                modeInfo.Block.Skip = selectedSkip;
                modeInfo.Block.UseIntraBlockCopy = true;
                modeInfo.Block.HorizontalInterpolationFilter = Av1InterpolationFilter.Bilinear;
                modeInfo.Block.VerticalInterpolationFilter = Av1InterpolationFilter.Bilinear;
                block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
                block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 0;
                block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 0;
                block.PredictionUnit.ChromaFromLumaIndex = 0;
                block.PredictionUnit.ChromaFromLumaSigns = 0;
                paletteInfo = default;
                this.encodedWithoutCoefficients = !this.ReconstructSelectedInterBlock(
                    writer, macroBlock, tileIndex, blockOrigin, modeInfo, block, selectedVector, default, selectedStates);
                this.picture.SetDisplacementVector(modeInfoPosition, selectedVector);
            }

            return selectedStatistics;
        }

        /// <summary>
        /// Evaluates reference-frame modes and retains the winning syntax and transform choices.
        /// </summary>
        private Av1RateDistortionStatistics SelectInterBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            out Av1MotionVector selectedVector,
            out Av1MotionVector selectedSecondaryVector,
            out InlineArray128<Av1EncoderTransformBlockState> selectedStates)
        {
            this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Candidate;
            int estimation = this.picture.Parent.SpeedSettings.InterModeEstimation;
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            this.estimateInterCandidates =
                (estimation == 1 && this.blockWorkspace.InterModeModels[(int)blockSize].IsReady) ||
                (estimation == 2 && blockSize.GetWidth() * blockSize.GetHeight() > 256);
            this.interCandidateCount = 0;
            this.compoundSearchRecordCount = 0;
            this.interpolationSearchRecordCount = 0;
            this.blockWorkspace.SingleReferenceFilterCosts.Fill(long.MaxValue);
            this.blockWorkspace.SingleReferenceSimpleCosts.Fill(long.MaxValue);
            this.bestInterEstimate = long.MaxValue;
            this.bestInterPredictionCost = long.MaxValue;
            this.bestInterLumaPredictionCost = long.MaxValue;
            this.interSourceVarianceCost = (long)this.interSourceVariance * blockSize.GetWidth() * blockSize.GetHeight() * 128;
            Span<long> topAverageCosts = stackalloc long[5];
            topAverageCosts.Fill(long.MaxValue);
            Span<int> compoundMaskHistory = stackalloc int[64];
            compoundMaskHistory.Fill(-1);
            Av1MacroBlockModeInfo initialModeInfo = modeInfo;
            Av1EncoderBlockStruct initialBlock = block;
            InlineArray8<Av1ReferenceMotionVectors> singleReferenceVectors = default;
            InlineArray8<int> interIntraModes = default;
            InlineArray8<long> bestSingleCosts = default;
            InlineArray8<Av1PredictionMode> bestSingleModes = default;
            bestSingleCosts[..].Fill(long.MaxValue);
            bestSingleModes[..].Fill(Av1PredictionMode.PredictionModeCount);
            interIntraModes[..].Fill(-1);
            InlineArray8<InlineArray3<Av1MotionVector>> newMotionVectors = default;
            InlineArray8<byte> newMotionVectorMasks = default;
            uint searchedSingleModes = 0;
            selectedVector = default;
            selectedSecondaryVector = default;
            selectedStates = default;
            Av1RateDistortionStatistics selectedStatistics = Av1RateDistortionStatistics.Invalid;
            ReadOnlySpan<Av1ReferenceFrameType> referenceOrder =
            [
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Last3,
                Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Alternate2, Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Golden
            ];

            byte availableReferences = this.picture.Parent.AvailableReferenceMask;
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            foreach (Av1ReferenceFrameType reference in referenceOrder)
            {
                if ((availableReferences & (1 << (int)reference)) == 0)
                {
                    continue;
                }

                singleReferenceVectors[(int)reference].Build(
                    this.picture,
                    macroBlock,
                    modeInfoPosition,
                    blockSize,
                    initialModeInfo.Block.PartitionType,
                    this.picture.Sequence.SequenceHeader,
                    this.picture.Parent.FrameHeader,
                    reference,
                    Av1ReferenceFrameType.None);
            }

            // Search the same syntax mode across the available references before advancing to the
            // next mode. NEWMV's complete DRL search is retained for the later compound candidates.
            ReadOnlySpan<Av1PredictionMode> modeOrder =
            [
                Av1PredictionMode.NearestMotionVector, Av1PredictionMode.NewMotionVector,
                Av1PredictionMode.NearMotionVector, Av1PredictionMode.GlobalMotionVector
            ];

            foreach (Av1PredictionMode mode in modeOrder)
            {
                foreach (Av1ReferenceFrameType reference in referenceOrder)
                {
                    int index = (int)reference;
                    if ((availableReferences & (1 << index)) == 0)
                    {
                        continue;
                    }

                    if (Av1ModeThresholds.ShouldSkip(
                        this.blockWorkspace.ModeThresholdFactors,
                        this.blockWorkspace.ModeThresholdQuantizerFactor,
                        this.blockWorkspace.ModeThresholdSkipMultiplier,
                        blockSize,
                        mode,
                        reference,
                        Av1ReferenceFrameType.None,
                        Math.Min(this.blockCostLimit, selectedStatistics.Cost),
                        modeInfo.Block.Skip))
                    {
                        continue;
                    }

                    int vectorCount = singleReferenceVectors[index].Count;
                    Av1GlobalMotionType globalType = this.picture.Parent.FrameHeader
                        .GetGlobalMotionParameters()[(int)reference - (int)Av1ReferenceFrameType.Last].Type;

                    Av1PredictionMode compareMode = Av1PredictionMode.PredictionModeCount;
                    if (mode == Av1PredictionMode.NearMotionVector)
                    {
                        if (vectorCount == 0)
                        {
                            compareMode = Av1PredictionMode.NearestMotionVector;
                        }
                        else if (vectorCount == 1 && globalType <= Av1GlobalMotionType.Translation)
                        {
                            compareMode = Av1PredictionMode.GlobalMotionVector;
                        }
                    }
                    else if (mode == Av1PredictionMode.GlobalMotionVector)
                    {
                        if (vectorCount == 0 && globalType <= Av1GlobalMotionType.Translation)
                        {
                            compareMode = Av1PredictionMode.NearestMotionVector;
                        }
                        else if (vectorCount == 1)
                        {
                            compareMode = Av1PredictionMode.NearMotionVector;
                        }
                    }

                    if (compareMode != Av1PredictionMode.PredictionModeCount)
                    {
                        int previousIndex = (((int)compareMode - (int)Av1PredictionMode.InterModeStart) * 3 *
                            Av1Constants.ReferenceFrameCount) + (int)reference;
                        long previousCost = this.blockWorkspace.SingleReferenceFilterCosts[previousIndex];
                        int context = singleReferenceVectors[index].ModeContext;
                        if (previousCost != long.MaxValue &&
                            writer.GetInterModeCost(mode, context) > writer.GetInterModeCost(compareMode, context))
                        {
                            // Identical vectors have identical prediction error. Retain the modeled cost
                            // for compound comparisons, but do not search the more expensive single syntax.
                            int currentIndex = (((int)mode - (int)Av1PredictionMode.InterModeStart) * 3 *
                                Av1Constants.ReferenceFrameCount) + (int)reference;

                            this.blockWorkspace.SingleReferenceFilterCosts[currentIndex] = previousCost;
                            continue;
                        }
                    }

                    if (mode == Av1PredictionMode.NearMotionVector &&
                        this.ShouldPruneNearMode(macroBlock, reference, Av1ReferenceFrameType.None, Math.Min(this.blockCostLimit, selectedStatistics.Cost)))
                    {
                        continue;
                    }

                    if (this.picture.Parent.SpeedSettings.PruneSpatialMotionByWeight &&
                        !this.picture.Parent.FrameHeader.AllowScreenContentTools &&
                        mode is Av1PredictionMode.NearestMotionVector or Av1PredictionMode.NearMotionVector &&
                        selectedStatistics.Cost != long.MaxValue && macroBlock.IsLeftAvailable && macroBlock.IsUpAvailable)
                    {
                        int count = Math.Min(3, vectorCount);
                        ReadOnlySpan<ushort> weights = singleReferenceVectors[index].Weights;
                        if (count != 0 &&
                            !(mode == Av1PredictionMode.NearestMotionVector && weights[0] >= Av1ReferenceMotionVectors.NearestCandidateWeight))
                        {
                            int nearestCount = 0;
                            for (int candidate = 0; candidate < count; candidate++)
                            {
                                nearestCount += weights[candidate] >= Av1ReferenceMotionVectors.NearestCandidateWeight ? 1 : 0;
                            }

                            if (nearestCount < (count >= 2 ? 2 : 1))
                            {
                                continue;
                            }
                        }
                    }

                    Av1MacroBlockModeInfo candidateModeInfo = initialModeInfo;
                    Av1EncoderBlockStruct candidateBlock = initialBlock;
                    ref InlineArray3<Av1MotionVector> newVectors = ref newMotionVectors[index];
                    ref byte newVectorMask = ref newMotionVectorMasks[index];
                    Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
                    if (!settings.SkipSingleInterpolationSearch &&
                        !(settings.UseWinnerInterpolation && this.picture.Parent.FrameHeader.ReferenceMode == ObuReferenceMode.SingleReference))
                    {
                        searchedSingleModes |= 1U << ((((int)mode - (int)Av1PredictionMode.InterModeStart) *
                            Av1Constants.ReferenceFrameCount) + (int)reference);
                    }

                    Av1RateDistortionStatistics candidateStatistics = this.SelectSingleReferenceMode(
                        writer,
                        macroBlock,
                        blockOrigin,
                        tileIndex,
                        Math.Min(this.blockCostLimit, selectedStatistics.Cost),
                        reference,
                        mode,
                        ref singleReferenceVectors[index],
                        ref interIntraModes[index],
                        ref candidateModeInfo,
                        ref candidateBlock,
                        out Av1MotionVector candidateVector,
                        out InlineArray128<Av1EncoderTransformBlockState> candidateStates,
                        ref newVectors,
                        ref newVectorMask);

                    if (candidateStatistics.Cost < bestSingleCosts[(int)reference])
                    {
                        bestSingleCosts[(int)reference] = candidateStatistics.Cost;
                        bestSingleModes[(int)reference] = mode;
                    }

                    if (candidateStatistics.Cost < Math.Min(this.blockCostLimit, selectedStatistics.Cost))
                    {
                        modeInfo = candidateModeInfo;
                        block = candidateBlock;
                        selectedVector = candidateVector;
                        selectedStates = candidateStates;
                        selectedStatistics = candidateStatistics;
                    }
                }
            }

            ReadOnlySpan<Av1ReferenceFrameType> compoundReferences =
            [
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Last3, Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Golden, Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Backward,
                Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Backward,
                Av1ReferenceFrameType.Last3, Av1ReferenceFrameType.Backward,
                Av1ReferenceFrameType.Golden, Av1ReferenceFrameType.Backward,
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Alternate2,
                Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Alternate2,
                Av1ReferenceFrameType.Last3, Av1ReferenceFrameType.Alternate2,
                Av1ReferenceFrameType.Golden, Av1ReferenceFrameType.Alternate2,
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last2,
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last3,
                Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Golden,
                Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Alternate
            ];

            ReadOnlySpan<byte> compoundSearchOrder = [4, 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

            // Nearest pairs establish a bound across all admitted references first. The remaining
            // motion families then complete each pair in order, retaining that pair's mask history.
            for (int phase = 0; phase < 2; phase++)
            {
                for (int index = 0; index < compoundSearchOrder.Length; index++)
                {
                    int pairIndex = phase == 0 ? index : compoundSearchOrder[index];
                    this.SelectCompoundBlock(
                        writer,
                        macroBlock,
                        blockOrigin,
                        tileIndex,
                        compoundReferences[pairIndex * 2],
                        compoundReferences[(pairIndex * 2) + 1],
                        phase == 0,
                        topAverageCosts,
                        compoundMaskHistory.Slice(pairIndex * 4, 4),
                        bestSingleModes,
                        searchedSingleModes,
                        singleReferenceVectors,
                        newMotionVectors,
                        newMotionVectorMasks,
                        ref modeInfo,
                        ref block,
                        ref selectedStatistics,
                        ref selectedVector,
                        ref selectedSecondaryVector,
                        ref selectedStates);
                }
            }

            if (this.estimateInterCandidates)
            {
                selectedStatistics = this.SearchRetainedInterCandidates(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    ref modeInfo,
                    ref block,
                    out selectedVector,
                    out selectedSecondaryVector,
                    out selectedStates);
            }

            return selectedStatistics;
        }

        /// <summary>
        /// Runs complete transform search on ranked predictions before publishing the inter winner.
        /// </summary>
        private Av1RateDistortionStatistics SearchRetainedInterCandidates(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            out Av1MotionVector selectedVector,
            out Av1MotionVector selectedSecondaryVector,
            out InlineArray128<Av1EncoderTransformBlockState> selectedStates)
        {
            this.estimateInterCandidates = false;
            Span<Av1InterModeCandidate> candidates = this.blockWorkspace.InterModeCandidates[..this.interCandidateCount];
            candidates.Sort();
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            int candidateCount = Math.Min(candidates.Length, settings.MaximumInterTransformCandidates);
            long firstEstimate = candidateCount == 0 ? long.MaxValue : candidates[0].EstimatedCost;
            Av1RateDistortionStatistics selected = Av1RateDistortionStatistics.Invalid;
            selectedVector = default;
            selectedSecondaryVector = default;
            selectedStates = default;
            InlineArray36<int> searchedModes = default;
            int searchCount = 0;
            bool searchedNewMotion = false;
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            for (int index = 0; index < candidateCount; index++)
            {
                Av1InterModeCandidate candidate = candidates[index];
                Av1EncoderBlockModeInfo predictionModeInfo = candidate.ModeInfo;
                Av1PredictionMode mode = predictionModeInfo.Mode;
                if (candidate.EstimatedCost * 0.80 > firstEstimate)
                {
                    break;
                }

                int modeIndex = (int)mode - (int)Av1PredictionMode.NearestMotionVector;
                int maximumRepeatedModes = mode == Av1PredictionMode.NearestMotionVector ? 2 : 1;
                if (searchCount > settings.InterModeRepeatThreshold && searchedModes[modeIndex] >= maximumRepeatedModes)
                {
                    continue;
                }

                if (!this.ShouldSearchInterTransforms(candidate))
                {
                    continue;
                }

                // Recreate only the predictions that reach transform search. Inter-intra owns its complete
                // three-plane blend; other modes use the same plane builder as the initial estimation pass.
                if (predictionModeInfo.SecondaryReferenceFrame == Av1ReferenceFrameType.Intra)
                {
                    this.PrepareInterIntraPrediction(
                        macroBlock,
                        blockOrigin,
                        predictionModeInfo.BlockSize,
                        block.HasChroma,
                        predictionModeInfo.ReferenceFrame,
                        candidate.Vector,
                        predictionModeInfo.HorizontalInterpolationFilter,
                        predictionModeInfo.VerticalInterpolationFilter,
                        predictionModeInfo.InterIntraMode,
                        predictionModeInfo.UseInterIntraWedge,
                        predictionModeInfo.InterIntraWedgeIndex);
                }
                else
                {
                    Av1EncoderFrame<TSample>.PlanarView primaryReference = this.references.Span[(int)predictionModeInfo.ReferenceFrame].CodedView;
                    int planeCount = block.HasChroma ? 3 : 1;
                    for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                    {
                        Av1Plane plane = (Av1Plane)planeIndex;
                        int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                        int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                        Av1BlockSize planeSize = predictionModeInfo.BlockSize.GetSubsampled(subX != 0, subY != 0);
                        Span<TSample> prediction = planeIndex == 0 ? workspace.LumaPrediction :
                            planeIndex == 1 ? workspace.BluePrediction : workspace.RedPrediction;

                        Buffer2DRegion<TSample> secondaryReference = predictionModeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                            ? this.references.Span[(int)predictionModeInfo.SecondaryReferenceFrame].CodedView.GetPlane(plane)
                            : default;

                        this.PrepareInterPlanePrediction(
                            candidate.Vector,
                            candidate.SecondaryVector,
                            plane,
                            mode,
                            predictionModeInfo.ReferenceFrame,
                            predictionModeInfo.SecondaryReferenceFrame,
                            false,
                            predictionModeInfo.CompoundType,
                            predictionModeInfo.CompoundWedgeIndex,
                            predictionModeInfo.CompoundWedgeSign,
                            predictionModeInfo.DifferenceWeightedMaskType,
                            predictionModeInfo.HorizontalInterpolationFilter,
                            predictionModeInfo.VerticalInterpolationFilter,
                            primaryReference.GetPlane(plane),
                            secondaryReference,
                            blockOrigin,
                            subX,
                            subY,
                            predictionModeInfo.BlockSize,
                            prediction,
                            workspace.Residual);
                    }
                }

                searchCount++;
                searchedModes[modeIndex]++;
                searchedNewMotion |= mode is Av1PredictionMode.NewMotionVector or Av1PredictionMode.NewNewMotionVector or
                    Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector or
                    Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NewNearestMotionVector;

                Av1RateDistortionStatistics statistics = this.EvaluatePreparedInterCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    Math.Min(this.blockCostLimit, selected.Cost),
                    block.HasChroma,
                    candidate,
                    workspace.LumaCandidateReconstruction,
                    workspace.LumaCandidateCoefficients,
                    workspace.BlueCandidateReconstruction,
                    workspace.BlueCandidateCoefficients,
                    workspace.RedCandidateReconstruction,
                    workspace.RedCandidateCoefficients,
                    out bool skip,
                    out InlineArray64<Av1EncoderTransformBlockState> lumaStates,
                    out InlineArray16<Av1TransformSize> sizes,
                    out InlineArray16<Av1EncoderTransformBlockState> blueStates,
                    out InlineArray16<Av1EncoderTransformBlockState> redStates);

                if (statistics.Cost == long.MaxValue)
                {
                    continue;
                }

                if (statistics.Cost < Math.Min(this.blockCostLimit, selected.Cost))
                {
                    selected = statistics;
                    selectedVector = candidate.Vector;
                    selectedSecondaryVector = candidate.SecondaryVector;
                    predictionModeInfo.PartitionType = modeInfo.Block.PartitionType;
                    predictionModeInfo.Skip = skip;
                    predictionModeInfo.TransformSize = this.picture.Parent.FrameHeader.CodedLossless
                        ? Av1TransformSize.Size4x4 : predictionModeInfo.BlockSize.GetMaximumTransformSize();
                    sizes[..].CopyTo(predictionModeInfo.InterTransformSizes);
                    modeInfo.Block = predictionModeInfo;
                    block.ReferenceMotionVectorIndex = candidate.ReferenceIndex;
                    lumaStates[..].CopyTo(selectedStates);
                    blueStates[..].CopyTo(selectedStates[64..80]);
                    redStates[..].CopyTo(selectedStates[80..96]);

                    if (index == 0 && settings.InterModeTransformBreakout != 0)
                    {
                        // The first full result limits later trials according to its actual residual
                        // decision, rather than treating a cheap estimate as a transform-skip decision.
                        if (skip)
                        {
                            ReadOnlySpan<int> limits = [2, 3, 5, 7, 9];
                            candidateCount = Math.Min(candidateCount, limits[(5 * this.quantization.QIndex[0]) >> 8]);
                        }
                        else if (predictionModeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                        {
                            ReadOnlySpan<int> limits = settings.InterModeTransformBreakout == 1 ? [10, 7, 5, 4] : [10, 7, 5, 3];
                            candidateCount = Math.Min(candidateCount, limits[(4 * this.quantization.QIndex[0]) >> 8]);
                        }
                    }
                }

                if (searchCount > settings.InterModeCandidateLimit && searchedNewMotion)
                {
                    break;
                }
            }

            return selected;
        }

        /// <summary>
        /// Re-evaluates a selected prediction's residual while retaining its prediction syntax rate.
        /// </summary>
        private void RefineInterTransformSize(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            ref Av1RateDistortionStatistics selectedStatistics,
            ref InlineArray128<Av1EncoderTransformBlockState> selectedStates)
        {
            Av1TransformSize maximumTransformSize = modeInfo.Block.BlockSize.GetMaximumTransformSize();
            if (this.picture.Parent.FrameHeader.CodedLossless)
            {
                return;
            }

            Av1MacroBlockModeInfo candidateModeInfo = modeInfo;
            int predictionRate = selectedStatistics.Rate - selectedStatistics.ResidualRate;
            bool preparedPrediction = false;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            if (this.picture.Parent.SpeedSettings.UseWinnerInterpolation &&
                frameHeader.ReferenceMode == ObuReferenceMode.SingleReference &&
                modeInfo.Block.Mode < Av1PredictionMode.CompoundInterModeStart &&
                Av1TileWriter.UsesSwitchableInterpolation(frameHeader, modeInfo.Block))
            {
                int originalFilterRate = writer.GetSwitchableInterpolationFilterCost(
                    modeInfo.Block.VerticalInterpolationFilter,
                    Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo.Block, macroBlock, 0));

                if (this.picture.Sequence.SequenceHeader.EnableDualFilter)
                {
                    originalFilterRate += writer.GetSwitchableInterpolationFilterCost(
                        modeInfo.Block.HorizontalInterpolationFilter,
                        Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo.Block, macroBlock, 1));
                }

                // This search ranks luma only. Chroma is prepared below after the selected luma tree
                // supplies its transform types, so no temporary chroma prediction needs to survive here.
                preparedPrediction = this.SelectInterFilters(
                    writer,
                    macroBlock,
                    blockOrigin,
                    modeInfo.Block.BlockSize,
                    false,
                    vector,
                    secondaryVector,
                    ref candidateModeInfo.Block,
                    long.MaxValue,
                    long.MaxValue,
                    out int filterRate,
                    out _);

                predictionRate += filterRate - originalFilterRate;
            }

            if (!preparedPrediction)
            {
                this.PrepareSelectedInterLumaPrediction(macroBlock, blockOrigin, candidateModeInfo, block, vector, secondaryVector);
            }

            InlineArray64<Av1EncoderTransformBlockState> candidateStates = default;
            candidateModeInfo.Block.TransformSize = maximumTransformSize;
            candidateModeInfo.Block.Skip = false;
            Av1RateDistortionStatistics candidateStatistics = this.EvaluateInterLumaTree(
                writer, macroBlock, blockOrigin, tileIndex, ref candidateModeInfo.Block, candidateStates, long.MaxValue, out int stateCount);

            if (candidateStatistics.Cost == long.MaxValue)
            {
                return;
            }

            Av1RateDistortionStatistics lumaStatistics = candidateStatistics;

            // Chroma must use the type at its own luma origin after the tree changes.
            Av1RateDistortionStatistics chromaStatistics = this.EvaluateRefinedInterChroma(
                writer,
                macroBlock,
                tileIndex,
                blockOrigin,
                candidateModeInfo,
                block,
                vector,
                secondaryVector,
                candidateStates[..stateCount],
                out InlineArray16<Av1EncoderTransformBlockState> blueStates,
                out InlineArray16<Av1EncoderTransformBlockState> redStates);

            candidateStatistics.Add(this.rateMultiplier, in chromaStatistics);
            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            int noSkipRate = writer.GetSkipCost(false, skipContext);
            int skipRate = writer.GetSkipCost(true, skipContext);
            bool allEmpty = !candidateStatistics.HasCoefficients;

            // Skipping removes the entire transform tree, including every partition and coefficient
            // symbol. Compare that complete syntax once both luma and chroma have been evaluated.
            bool skip = allEmpty ||
                Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, candidateStatistics.PredictionDistortion) <=
                Av1RateDistortion.GetCost(this.rateMultiplier, noSkipRate + candidateStatistics.Rate, candidateStatistics.Distortion);

            if (skip)
            {
                long predictionDistortion = candidateStatistics.PredictionDistortion;
                candidateStatistics = new(this.rateMultiplier, skipRate, predictionDistortion)
                {
                    PredictionDistortion = predictionDistortion
                };

                // A skipped block codes no transform tree, so it carries no split decisions and no
                // per-transform state. Returning the sizes to the largest the block permits, and
                // clearing the states, leaves the block in the shape the bitstream describes.
                candidateModeInfo.Block.Skip = true;
                candidateModeInfo.Block.TransformSize = maximumTransformSize;
                candidateModeInfo.Block.InterTransformSizes.Fill(maximumTransformSize);
                candidateStates[..].Clear();
                blueStates[..].Clear();
                redStates[..].Clear();
                stateCount = 1;
            }

            // A skipped block pays its skip flag through the statistics above, so only a coded
            // block adds the flag that says it is not skipped. The luma cost is kept apart from the
            // total because later stages compare luma alone when they decide whether to search
            // chroma at all.
            int residualRate = candidateStatistics.Rate + (skip ? 0 : noSkipRate);
            Av1RateDistortionStatistics refinedStatistics = new(
                this.rateMultiplier,
                predictionRate + residualRate,
                candidateStatistics.Distortion)
            {
                LumaCost = Av1RateDistortion.GetCost(
                    this.rateMultiplier,
                    predictionRate + (skip ? skipRate : noSkipRate + lumaStatistics.Rate),
                    skip ? lumaStatistics.PredictionDistortion : lumaStatistics.Distortion),
                ResidualRate = residualRate,
                PredictionDistortion = candidateStatistics.PredictionDistortion,
                HasCoefficients = candidateStatistics.HasCoefficients,
                AllTransformsEmpty = allEmpty
            };

            if (refinedStatistics.Cost >= selectedStatistics.Cost)
            {
                return;
            }

            selectedStatistics = refinedStatistics;

            // One array holds all three planes so that a winning candidate is retained by a single
            // copy. Luma owns the first 64 entries, which is the most transforms a superblock
            // partition can hold, and each chroma plane owns 16 after it.
            modeInfo = candidateModeInfo;
            candidateStates[..stateCount].CopyTo(selectedStates);
            blueStates[..].CopyTo(selectedStates[64..80]);
            redStates[..].CopyTo(selectedStates[80..96]);
        }

        /// <summary>
        /// Evaluates both chroma planes using the transform types at their corresponding luma positions.
        /// </summary>
        /// <param name="writer">The current entropy cost model.</param>
        /// <param name="macroBlock">The coded block and its frame-edge geometry.</param>
        /// <param name="tileIndex">The tile owning the coefficient contexts.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The selected prediction mode.</param>
        /// <param name="block">The selected block state.</param>
        /// <param name="vector">The primary motion vector.</param>
        /// <param name="secondaryVector">The secondary motion vector.</param>
        /// <param name="lumaStates">The coded luma transform states in tree order.</param>
        /// <param name="blueState">The U transform states in coding order.</param>
        /// <param name="redState">The V transform states in coding order.</param>
        /// <returns>The combined chroma rate and distortion.</returns>
        private Av1RateDistortionStatistics EvaluateRefinedInterChroma(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            ushort tileIndex,
            Point blockOrigin,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            ReadOnlySpan<Av1EncoderTransformBlockState> lumaStates,
            out InlineArray16<Av1EncoderTransformBlockState> blueState,
            out InlineArray16<Av1EncoderTransformBlockState> redState)
        {
            blueState = default;
            redState = default;
            Av1RateDistortionStatistics statistics = new(this.rateMultiplier, 0, 0);
            if (!block.HasChroma)
            {
                return statistics;
            }

            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            for (int planeIndex = 1; planeIndex < 3; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                this.EvaluateInterChromaPlane(
                    writer,
                    macroBlock,
                    tileIndex,
                    long.MaxValue,
                    blockOrigin,
                    modeInfo.Block.BlockSize,
                    plane,
                    modeInfo.Block.ReferenceFrame,
                    modeInfo.Block.SecondaryReferenceFrame,
                    vector,
                    secondaryVector,
                    modeInfo.Block.Mode,
                    modeInfo.Block.SecondaryReferenceFrame == Av1ReferenceFrameType.Intra,
                    modeInfo.Block.CompoundType,
                    modeInfo.Block.CompoundWedgeIndex,
                    modeInfo.Block.CompoundWedgeSign,
                    modeInfo.Block.DifferenceWeightedMaskType,
                    modeInfo.Block.HorizontalInterpolationFilter,
                    modeInfo.Block.VerticalInterpolationFilter,
                    modeInfo.Block.InterTransformSizes,
                    lumaStates,
                    plane == Av1Plane.U ? workspace.BlueCandidateReconstruction : workspace.RedCandidateReconstruction,
                    plane == Av1Plane.U ? workspace.BlueCandidateCoefficients : workspace.RedCandidateCoefficients,
                    out InlineArray16<Av1EncoderTransformBlockState> states,
                    out int rate,
                    out long distortion,
                    out long predictionDistortion,
                    out bool hasCoefficients);

                Av1RateDistortionStatistics planeStatistics = new(this.rateMultiplier, rate, distortion)
                {
                    PredictionDistortion = predictionDistortion,
                    HasCoefficients = hasCoefficients
                };

                statistics.Add(this.rateMultiplier, in planeStatistics);
                if (plane == Av1Plane.U)
                {
                    blueState = states;
                }
                else
                {
                    redState = states;
                }
            }

            return statistics;
        }

        private void PrepareSelectedInterLumaPrediction(
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector)
        {
            if (modeInfo.Block.SecondaryReferenceFrame == Av1ReferenceFrameType.Intra)
            {
                this.PrepareInterIntraPrediction(
                    macroBlock,
                    blockOrigin,
                    modeInfo.Block.BlockSize,
                    block.HasChroma,
                    modeInfo.Block.ReferenceFrame,
                    vector,
                    modeInfo.Block.HorizontalInterpolationFilter,
                    modeInfo.Block.VerticalInterpolationFilter,
                    modeInfo.Block.InterIntraMode,
                    modeInfo.Block.UseInterIntraWedge,
                    modeInfo.Block.InterIntraWedgeIndex);
                return;
            }

            Av1BlockSize predictionSize = modeInfo.Block.BlockSize;
            int sampleCount = predictionSize.GetWidth() * predictionSize.GetHeight();
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Span<TSample> prediction = workspace.LumaPrediction[..sampleCount];
            Span<short> residual = workspace.Residual[..sampleCount];
            if (modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
            {
                int firstWeight = 8;
                int secondWeight = 8;
                if (modeInfo.Block.CompoundType == Av1CompoundType.DistanceWeighted)
                {
                    Av1CompoundDistanceWeights.Derive(
                        this.picture.Sequence.SequenceHeader.OrderHintInfo,
                        this.picture.Parent.FrameHeader,
                        modeInfo.Block.ReferenceFrame,
                        modeInfo.Block.SecondaryReferenceFrame,
                        out firstWeight,
                        out secondWeight);
                }

                this.blockWorkspace.GetCompoundPredictionIntermediates(out Span<ushort> first, out Span<ushort> second);
                int primaryColumnQ4 = (blockOrigin.X << 4) + (vector.Column << 1);
                int primaryRowQ4 = (blockOrigin.Y << 4) + (vector.Row << 1);
                int secondaryColumnQ4 = (blockOrigin.X << 4) + (secondaryVector.Column << 1);
                int secondaryRowQ4 = (blockOrigin.Y << 4) + (secondaryVector.Row << 1);
                TOperator.PrepareCompoundInterPrediction(
                    this.source.GetPlane(Av1Plane.Y),
                    blockOrigin,
                    this.references.Span[(int)modeInfo.Block.ReferenceFrame].CodedView.GetPlane(Av1Plane.Y),
                    new Point(primaryColumnQ4 >> 4, primaryRowQ4 >> 4),
                    primaryColumnQ4 & 15,
                    primaryRowQ4 & 15,
                    this.references.Span[(int)modeInfo.Block.SecondaryReferenceFrame].CodedView.GetPlane(Av1Plane.Y),
                    new Point(secondaryColumnQ4 >> 4, secondaryRowQ4 >> 4),
                    secondaryColumnQ4 & 15,
                    secondaryRowQ4 & 15,
                    modeInfo.Block.HorizontalInterpolationFilter,
                    modeInfo.Block.VerticalInterpolationFilter,
                    prediction,
                    residual,
                    first,
                    second,
                    this.blockWorkspace.GetCompoundPredictionMask(),
                    workspace.PredictionScratch,
                    predictionSize,
                    this.bitDepth,
                    Av1Plane.Y,
                    modeInfo.Block.BlockSize,
                    modeInfo.Block.CompoundType,
                    firstWeight,
                    secondWeight,
                    0,
                    0,
                    modeInfo.Block.CompoundWedgeIndex,
                    modeInfo.Block.CompoundWedgeSign,
                    modeInfo.Block.DifferenceWeightedMaskType);
                return;
            }

            Av1EncoderFrame<TSample>.PlanarView reference = this.references.Span[(int)modeInfo.Block.ReferenceFrame].CodedView;
            int columnQ4 = (blockOrigin.X << 4) + (vector.Column << 1);
            int rowQ4 = (blockOrigin.Y << 4) + (vector.Row << 1);
            TOperator.PrepareTranslationalInterPrediction(
                this.source.GetPlane(Av1Plane.Y),
                blockOrigin,
                reference.GetPlane(Av1Plane.Y),
                new Point(columnQ4 >> 4, rowQ4 >> 4),
                modeInfo.Block.HorizontalInterpolationFilter,
                modeInfo.Block.VerticalInterpolationFilter,
                columnQ4 & 15,
                rowQ4 & 15,
                prediction,
                residual,
                workspace.PredictionScratch,
                predictionSize,
                this.bitDepth);
        }

        /// <summary>
        /// Evaluates a prepared luma prediction with local coefficient and transform edge contexts.
        /// </summary>
        private Av1RateDistortionStatistics EvaluateInterLumaTree(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1EncoderBlockModeInfo modeInfo,
            Span<Av1EncoderTransformBlockState> states,
            long costLimit,
            out int stateCount)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int width4 = modeInfo.BlockSize.Get4x4WideCount();
            int height4 = modeInfo.BlockSize.Get4x4HighCount();
            Span<byte> coefficientAbove = workspace.TransformContexts[..width4];
            Span<byte> coefficientLeft = workspace.TransformContexts.Slice(width4, height4);
            Span<byte> transformAbove = workspace.TransformContexts.Slice(width4 + height4, width4);
            Span<byte> transformLeft = workspace.TransformContexts.Slice((2 * width4) + height4, height4);
            Av1NeighborArrayUnit<byte> coefficientNeighbors = this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex];
            Av1NeighborArrayUnit<byte> transformNeighbors = this.picture.TransformFunctionContexts[tileIndex];
            coefficientNeighbors.Top.Slice(coefficientNeighbors.GetTopIndex(blockOrigin), width4).CopyTo(coefficientAbove);
            coefficientNeighbors.Left.Slice(coefficientNeighbors.GetLeftIndex(blockOrigin), height4).CopyTo(coefficientLeft);
            transformNeighbors.Top.Slice(transformNeighbors.GetTopIndex(blockOrigin), width4).CopyTo(transformAbove);
            transformNeighbors.Left.Slice(transformNeighbors.GetLeftIndex(blockOrigin), height4).CopyTo(transformLeft);

            stateCount = 0;

            // A residual predicted to quantize to nothing takes the largest transforms with every coefficient
            // zero, and the transform search is not run. Reference: the predict_skip_txfm() and set_skip_txfm()
            // step that opens av1_pick_recursive_tx_size_type_yrd() and av1_pick_uniform_tx_size_type_yrd().
            int skipPredictionLevel = this.GetSkipPredictionLevel();
            if (skipPredictionLevel != 0 && !this.picture.Parent.FrameHeader.CodedLossless &&
                this.PredictSkipTransform(macroBlock, blockOrigin, modeInfo.BlockSize, skipPredictionLevel, out long predictedDistortion))
            {
                return this.SetSkipTransform(
                    writer,
                    macroBlock,
                    blockOrigin,
                    ref modeInfo,
                    states,
                    coefficientAbove,
                    coefficientLeft,
                    transformAbove,
                    transformLeft,
                    predictedDistortion,
                    out stateCount);
            }

            int initialDepth = this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select &&
                !modeInfo.Skip &&
                (!this.picture.Parent.SpeedSettings.DeferTransformSizeSearch ||
                    this.blockWorkspace.EvaluationStage != Av1EncoderEvaluationStage.Candidate)
                ? this.picture.Parent.SpeedSettings.InterTransformSearchInitialDepth
                : Av1Constants.MaxVarTransform;

            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            int skipRate = writer.GetSkipCost(true, skipContext);
            int codedRate = writer.GetSkipCost(false, skipContext);
            long skipCost = Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, 0);
            long codedCost = Av1RateDistortion.GetCost(this.rateMultiplier, codedRate, 0);
            Av1TransformSize rootSize = this.picture.Parent.FrameHeader.CodedLossless
                ? Av1TransformSize.Size4x4
                : modeInfo.BlockSize.GetMaximumTransformSize();
            int rootWidth4 = rootSize.Get4x4WideCount();
            int rootHeight4 = rootSize.Get4x4HighCount();
            int visibleWidth4 = width4 + (Math.Min(0, macroBlock.ToRightEdge) >> 5);
            int visibleHeight4 = height4 + (Math.Min(0, macroBlock.ToBottomEdge) >> 5);
            Av1RateDistortionStatistics statistics = default;
            int rootIndex = 0;

            // A coding block can contain several maximum-size transforms. Each root consumes the
            // contexts left by its predecessor, while the bound retains both coded and skipped costs.
            int rootCount = width4 * height4 / (rootWidth4 * rootHeight4);
            for (int root = 0; root < rootCount; root++)
            {
                Point offset = rootSize.GetBlockPartitionOrigin(modeInfo.BlockSize, rootSize, root, 0, 0);
                int row = offset.Y >> Av1Constants.ModeInfoSizeLog2;
                int column = offset.X >> Av1Constants.ModeInfoSizeLog2;
                if (row < visibleHeight4 && column < visibleWidth4)
                {
                    long remainingCost = costLimit == long.MaxValue ? long.MaxValue : costLimit - Math.Min(skipCost, codedCost);
                    Av1RateDistortionStatistics rootStatistics = this.SelectInterTransformNode(
                        writer,
                        macroBlock,
                        blockOrigin,
                        ref modeInfo,
                        rootSize,
                        row,
                        column,
                        initialDepth,
                        coefficientAbove,
                        coefficientLeft,
                        transformAbove,
                        transformLeft,
                        states,
                        ref stateCount,
                        long.MaxValue,
                        remainingCost,
                        rootIndex++);

                    if (rootStatistics.Rate == int.MaxValue)
                    {
                        return Av1RateDistortionStatistics.Invalid;
                    }

                    statistics.Add(this.rateMultiplier, rootStatistics);
                    skipCost = Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, statistics.PredictionDistortion);
                    codedCost = Av1RateDistortion.GetCost(this.rateMultiplier, codedRate + statistics.Rate, statistics.Distortion);
                }
            }

            return statistics;
        }

        /// <summary>
        /// Gets the predicted-skip level of the current evaluation stage.
        /// Reference: predict_skip_levels, indexed by use_skip_flag_prediction and the mode evaluation type.
        /// </summary>
        private int GetSkipPredictionLevel()
            => this.blockWorkspace.EvaluationStage == Av1EncoderEvaluationStage.Candidate
                ? this.picture.Parent.SpeedSettings.SkipFlagPredictionLevel
                : 1;

        /// <summary>
        /// Predicts whether the whole luma residual of an inter or copied block quantizes to nothing.
        /// Reference: predict_skip_txfm().
        /// </summary>
        private bool PredictSkipTransform(
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int level,
            out long distortion)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            Span<short> residual = workspace.Residual[..(width * height)];
            TOperator.SubtractPrediction(
                this.source.GetPlane(Av1Plane.Y), blockOrigin, workspace.LumaPrediction, residual, width, height);

            // The distortion covers the visible samples only. Reference: av1_pixel_diff_dist().
            int visibleWidth = width + (Math.Min(0, macroBlock.ToRightEdge) >> 3);
            int visibleHeight = height + (Math.Min(0, macroBlock.ToBottomEdge) >> 3);
            distortion = 0;
            for (int y = 0; y < visibleHeight; y++)
            {
                ReadOnlySpan<short> row = residual.Slice(y * width, visibleWidth);
                for (int x = 0; x < row.Length; x++)
                {
                    distortion += row[x] * row[x];
                }
            }

            int qIndex = this.quantization.QIndex[0];
            int dcQuantizer = Av1QuantizationLookup.GetDcQuant(qIndex, 0, this.bitDepth);
            long meanSquaredError = distortion / width / height;
            long normalizedDcQuantizer = dcQuantizer >> 3;
            long meanSquaredErrorThreshold = normalizedDcQuantizer * normalizedDcQuantizer / 8;
            long predictionError = level >= 2 ? distortion : meanSquaredError;
            if (predictionError > meanSquaredErrorThreshold)
            {
                return false;
            }

            if (level >= 2)
            {
                return true;
            }

            Av1TransformSize transformSize = PredictSkipTransformSizes[(int)blockSize];
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int acQuantizer = Av1QuantizationLookup.GetAcQuant(qIndex, 0, this.bitDepth);
            int bitDepthIndex = this.bitDepth == Av1BitDepth.EightBit ? 0 : this.bitDepth == Av1BitDepth.TenBit ? 1 : 2;
            uint coefficientThreshold = SkipPredictionThresholds[bitDepthIndex][(int)blockSize];
            uint dcThreshold = coefficientThreshold * (uint)dcQuantizer;
            uint acThreshold = coefficientThreshold * (uint)acQuantizer;
            Span<int> coefficients = this.blockWorkspace.TransformCoefficients;
            int coefficientCount = transformWidth * transformHeight;
            for (int row = 0; row < height; row += transformHeight)
            {
                for (int column = 0; column < width; column += transformWidth)
                {
                    Av1ForwardTransformer.Transform2d(
                        residual[((row * width) + column)..],
                        coefficients,
                        (uint)width,
                        Av1TransformType.DctDct,
                        transformSize,
                        this.bitDepth.GetBitCount(),
                        this.blockWorkspace.TransformWorkspace);

                    if (((uint)Math.Abs(coefficients[0]) << 7) >= dcThreshold)
                    {
                        return false;
                    }

                    for (int i = 1; i < coefficientCount; i++)
                    {
                        if (((uint)Math.Abs(coefficients[i]) << 7) >= acThreshold)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Records a predicted-skip luma result: the largest transforms, each coded as all zero.
        /// Reference: set_skip_txfm().
        /// </summary>
        private Av1RateDistortionStatistics SetSkipTransform(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            Span<Av1EncoderTransformBlockState> states,
            Span<byte> coefficientAbove,
            Span<byte> coefficientLeft,
            Span<byte> transformAbove,
            Span<byte> transformLeft,
            long distortion,
            out int stateCount)
        {
            Av1BlockSize blockSize = modeInfo.BlockSize;
            Av1TransformSize transformSize = blockSize.GetMaximumTransformSize();
            modeInfo.TransformSize = transformSize;
            int width4 = transformSize.Get4x4WideCount();
            int height4 = transformSize.Get4x4HighCount();
            int blockWidth4 = blockSize.Get4x4WideCount();
            int blockHeight4 = blockSize.Get4x4HighCount();
            int visibleWidth4 = blockWidth4 + (Math.Min(0, macroBlock.ToRightEdge) >> 5);
            int visibleHeight4 = blockHeight4 + (Math.Min(0, macroBlock.ToBottomEdge) >> 5);

            // Every transform is costed with the all-zero rate of the first, whether or not it is visible.
            Av1TransformBlockContext firstContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Luminance, coefficientAbove[..width4], coefficientLeft[..height4], blockSize, transformSize);
            int zeroRate = writer.GetTransformBlockSkipCost(
                true, Av1SymbolContextHelper.GetTransformSizeContext(transformSize), firstContext.SkipContext);
            int transformCount = (blockWidth4 / width4) * (blockHeight4 / height4);

            // Should the block stay non-skip after chroma, its luma transforms code as empty DCT blocks.
            stateCount = 0;
            Size frameContextSize = new(this.picture.Parent.FrameHeader.ModeInfoColumnCount, this.picture.Parent.FrameHeader.ModeInfoRowCount);
            for (int row = 0; row < blockHeight4; row += height4)
            {
                for (int column = 0; column < blockWidth4; column += width4)
                {
                    for (int y = 0; y < height4; y++)
                    {
                        for (int x = 0; x < width4; x++)
                        {
                            modeInfo.InterTransformSizes[modeInfo.GetInterTransformSizeIndex(row + y, column + x)] = transformSize;
                        }
                    }

                    if (row >= visibleHeight4 || column >= visibleWidth4)
                    {
                        continue;
                    }

                    Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
                        Av1ComponentType.Luminance,
                        coefficientAbove.Slice(column, width4),
                        coefficientLeft.Slice(row, height4),
                        blockSize,
                        transformSize);
                    Av1EncoderTransformBlockState state = default;
                    state.TransformType = Av1TransformType.DctDct;
                    state.EntropyContext = (byte)(context.SkipContext | (context.DcSignContext << 4));
                    states[stateCount++] = state;
                    Av1TileWriter.UpdateCoefficientContexts(
                        coefficientAbove.Slice(column, width4),
                        coefficientLeft.Slice(row, height4),
                        0,
                        blockOrigin + new Size(column << 2, row << 2),
                        frameContextSize);
                    transformAbove.Slice(column, Math.Min(width4, visibleWidth4 - column)).Fill((byte)transformSize.GetWidth());
                    transformLeft.Slice(row, Math.Min(height4, visibleHeight4 - row)).Fill((byte)transformSize.GetHeight());
                }
            }

            int shift = (this.bitDepth.GetBitCount() - 8) * 2;
            long scaled = (shift == 0 ? distortion : (distortion + (1L << (shift - 1))) >> shift) << 4;
            return new Av1RateDistortionStatistics(this.rateMultiplier, zeroRate * transformCount, scaled)
            {
                PredictionDistortion = scaled,
                HasCoefficients = false,
                AllTransformsEmpty = true,
                SkipPredicted = true
            };
        }

        /// <summary>
        /// Selects a transform node and its children while keeping trial entropy contexts local.
        /// </summary>
        private Av1RateDistortionStatistics SelectInterTransformNode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            Av1TransformSize transformSize,
            int row,
            int column,
            int depth,
            Span<byte> coefficientAbove,
            Span<byte> coefficientLeft,
            Span<byte> transformAbove,
            Span<byte> transformLeft,
            Span<Av1EncoderTransformBlockState> states,
            ref int stateCount,
            long parentCost,
            long costLimit,
            int rootIndex)
        {
            if (costLimit < 0)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            Av1BlockSize blockSize = modeInfo.BlockSize;
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int width4 = transformSize.Get4x4WideCount();
            int height4 = transformSize.Get4x4HighCount();
            int visibleWidth4 = blockSize.Get4x4WideCount() + (Math.Min(0, macroBlock.ToRightEdge) >> 5);
            int visibleHeight4 = blockSize.Get4x4HighCount() + (Math.Min(0, macroBlock.ToBottomEdge) >> 5);
            int savedStateCount = stateCount;
            InlineArray32<byte> savedCoefficientAbove = default;
            InlineArray32<byte> savedCoefficientLeft = default;
            InlineArray32<byte> savedTransformAbove = default;
            InlineArray32<byte> savedTransformLeft = default;
            coefficientAbove.CopyTo(savedCoefficientAbove);
            coefficientLeft.CopyTo(savedCoefficientLeft);
            transformAbove.CopyTo(savedTransformAbove);
            transformLeft.CopyTo(savedTransformLeft);

            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            this.blockWorkspace.GetInterIntraStorage<TSample>(out Span<TSample> prediction, out _, out _);
            int sourceStride = blockSize.GetWidth();
            int sourceOffset = ((row << 2) * sourceStride) + (column << 2);
            for (int y = 0; y < height; y++)
            {
                workspace.LumaPrediction.Slice(sourceOffset + (y * sourceStride), width).CopyTo(prediction.Slice(y * width, width));
            }

            Point origin = blockOrigin + new Size(column << 2, row << 2);
            TOperator.SubtractPrediction(
                this.source.GetPlane(Av1Plane.Y), origin, prediction, workspace.Residual, transformSize.GetWidth(), transformSize.GetHeight());
            Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Luminance,
                coefficientAbove.Slice(column, width4),
                coefficientLeft.Slice(row, height4),
                blockSize,
                transformSize);

            bool canSplit = transformSize > Av1TransformSize.Size4x4 && depth < Av1Constants.MaxVarTransform;
            bool tryNoSplit = true;
            int pruningLevel = this.picture.Parent.SpeedSettings.InterTransformSizePruningLevel;
            if (canSplit && pruningLevel > 0 && this.bitDepth.GetBitCount() > 8)
            {
                // Compare the residual's mean and variance across the parent and its subdivisions.
                // Uniform residuals keep the larger transform; sharply different regions bypass it.
                int subWidth = width >= height ? width >> 1 : width;
                int subHeight = height >= width ? height >> 1 : height;
                int subArea = subWidth * subHeight;
                int sum = 0;
                long squaredSum = 0;
                float meanSum = 0;
                float varianceSum = 0;
                double squaredMeanSum = 0;
                double squaredVarianceSum = 0;
                int subCount = 0;
                for (int y = 0; y < height; y += subHeight)
                {
                    for (int x = 0; x < width; x += subWidth)
                    {
                        int subSum = 0;
                        long subSquaredSum = 0;
                        for (int sampleY = 0; sampleY < subHeight; sampleY++)
                        {
                            ReadOnlySpan<short> samples = workspace.Residual.Slice(((y + sampleY) * width) + x, subWidth);
                            for (int sampleX = 0; sampleX < samples.Length; sampleX++)
                            {
                                int sample = samples[sampleX];
                                subSum += sample;
                                subSquaredSum += sample * sample;
                            }
                        }

                        sum += subSum;
                        squaredSum += subSquaredSum;
                        float mean = (float)subSum / subArea;
                        float variance = (float)((double)subSquaredSum / subArea) - (mean * mean);
                        meanSum += mean;
                        varianceSum += variance;
                        squaredMeanSum += mean * mean;
                        squaredVarianceSum += variance * variance;
                        subCount++;
                    }
                }

                int area = width * height;
                float parentMean = (float)sum / area;
                float parentVariance = (float)((double)squaredSum / area) - (parentMean * parentMean);
                meanSum += parentMean;
                varianceSum += parentVariance;
                squaredMeanSum += parentMean * parentMean;
                squaredVarianceSum += parentVariance * parentVariance;

                // The mean normalization remains five for rectangular nodes as well as square nodes.
                // The second moments include the actual number of subdivisions plus the parent.
                float averageMean = meanSum / 5;
                float meanVariance = (float)(squaredMeanSum / (subCount + 1)) - (averageMean * averageMean);
                float meanDeviation = meanVariance > 0 ? MathF.Sqrt(meanVariance) : 0;
                float averageVariance = varianceSum / (subCount + 1);
                float varianceOfVariances = (float)(squaredVarianceSum / (subCount + 1)) - (averageVariance * averageVariance);
                int dcQuantizer = Av1QuantizationLookup.GetDcQuant(
                    this.quantization.QIndex[0], this.quantization.DeltaQDc[0], this.bitDepth) >> 3;
                int acQuantizer = Av1QuantizationLookup.GetAcQuant(
                    this.quantization.QIndex[0], this.quantization.DeltaQAc[0], this.bitDepth) >> 3;

                int noSplitScale = pruningLevel == 1 ? 24 : 8;
                int splitScale = pruningLevel == 1 ? 24 : pruningLevel == 2 ? 10 : 8;
                canSplit = meanDeviation > dcQuantizer || splitScale * varianceOfVariances > acQuantizer * acQuantizer;
                tryNoSplit = meanDeviation <= noSplitScale * dcQuantizer ||
                    varianceOfVariances <= noSplitScale * acQuantizer * acQuantizer;
            }

            Av1EncoderTransformBlockState noSplitState = default;
            byte coefficientContext = 0;
            int partitionContext = 0;
            if (transformSize > Av1TransformSize.Size4x4 && depth < Av1Constants.MaxVarTransform)
            {
                Av1TransformSize maximumSquare = blockSize.GetMaximumTransformSize().GetSquareUpSize();
                int category = ((transformSize.GetSquareUpSize() != maximumSquare && maximumSquare > Av1TransformSize.Size8x8) ? 1 : 0) +
                    ((((int)Av1TransformSize.SquareSizes - 1) - (int)maximumSquare) * 2);
                partitionContext = (category * 3) + (transformAbove[column] < width ? 1 : 0) + (transformLeft[row] < height ? 1 : 0);
            }

            Av1RateDistortionStatistics noSplit = Av1RateDistortionStatistics.Invalid;
            if (tryNoSplit)
            {
                this.EvaluateInterTransform(
                    writer,
                    Av1Plane.Y,
                    Av1ComponentType.Luminance,
                    modeInfo.Mode,
                    origin,
                    transformSize,
                    Av1TransformType.AllTransformTypes,
                    context,
                    prediction,
                    workspace.Residual,
                    width,
                    costLimit,
                    workspace.TransformReconstruction,
                    workspace.TransformCoefficients,
                    workspace.LumaCandidateReconstruction,
                    workspace.LumaCandidateCoefficients,
                    out noSplitState,
                    out int rate,
                    out long distortion,
                    out long predictionDistortion);

                int zeroRate = writer.GetCoefficientCost(
                    transformSize,
                    Av1TransformType.DctDct,
                    modeInfo.Mode,
                    workspace.LumaCandidateCoefficients,
                    Av1ComponentType.Luminance,
                    context,
                    0,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet,
                    Av1FilterIntraMode.AllFilterIntraModes,
                    usesInterTransformSet: true);

                // An empty residual can win even when quantization retained coefficients. Its distortion
                // is the prediction error, and its syntax consists only of the transform-skip symbol.
                if (noSplitState.EndOfBlock == 0 ||
                    (!this.picture.Parent.FrameHeader.CodedLossless &&
                    Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion) >=
                    Av1RateDistortion.GetCost(this.rateMultiplier, zeroRate, predictionDistortion)))
                {
                    noSplitState.EndOfBlock = 0;
                    noSplitState.TransformType = Av1TransformType.DctDct;
                    rate = zeroRate;
                    distortion = predictionDistortion;
                }

                coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                    workspace.LumaCandidateCoefficients, transformSize, noSplitState.TransformType, noSplitState.EndOfBlock);

                noSplitState.EntropyContext = (byte)(context.SkipContext | (context.DcSignContext << 4));
                if (transformSize > Av1TransformSize.Size4x4 && depth < Av1Constants.MaxVarTransform)
                {
                    rate += writer.GetTransformPartitionCost(false, partitionContext);
                }

                noSplit = new(this.rateMultiplier, rate, distortion)
                {
                    PredictionDistortion = predictionDistortion,
                    HasCoefficients = noSplitState.EndOfBlock != 0
                };

                int retainedCount = this.picture.Parent.SpeedSettings.InterTransformNoSplitCandidateCount;
                if (rootIndex >= 0 && retainedCount != 0 && !this.picture.Parent.FrameHeader.CodedLossless && !modeInfo.UseIntraBlockCopy)
                {
                    // Compare the same root across prediction modes. Child transforms do not enter
                    // this history: their areas and context-dependent costs are not interchangeable.
                    Span<long> previousCosts = this.interTransformNoSplitCosts[(rootIndex * 4)..((rootIndex * 4) + retainedCount)];
                    for (int index = 0; index < retainedCount; index++)
                    {
                        if (noSplit.Cost < previousCosts[index])
                        {
                            for (int previous = retainedCount - 1; previous > index; previous--)
                            {
                                previousCosts[previous] = previousCosts[previous - 1];
                            }

                            previousCosts[index] = noSplit.Cost;
                            break;
                        }
                    }

                    canSplit &= noSplit.Cost <= previousCosts[^1];
                }

                int searchLevel = this.picture.Parent.SpeedSettings.InterAdaptiveTransformSearchLevel;
                if (searchLevel != 0)
                {
                    if (noSplit.Cost - (noSplit.Cost >> (1 + searchLevel)) > costLimit)
                    {
                        return Av1RateDistortionStatistics.Invalid;
                    }

                    if (noSplit.Cost - (noSplit.Cost >> (2 + searchLevel)) > parentCost)
                    {
                        canSplit = false;
                    }
                }

                canSplit &= noSplitState.EndOfBlock != 0;
            }

            if (canSplit && this.bitDepth.GetBitCount() == 8 &&
                (costLimit != long.MaxValue || noSplit.Cost != long.MaxValue))
            {
                int splitScore = PredictTransformSplit(workspace.Residual, transformSize);
                canSplit = splitScore >= -this.picture.Parent.SpeedSettings.InterTransformSplitThreshold;
            }

            Av1RateDistortionStatistics split = Av1RateDistortionStatistics.Invalid;
            if (canSplit)
            {
                split = new(this.rateMultiplier, writer.GetTransformPartitionCost(true, partitionContext), 0);
                Av1TransformSize childSize = transformSize.GetSubSize();
                int childWidth4 = childSize.Get4x4WideCount();
                int childHeight4 = childSize.Get4x4HighCount();
                int childCount = (width4 / childWidth4) * (height4 / childHeight4);
                long splitLimit = Math.Min(noSplit.Cost, costLimit);
                for (int y = 0; y < height4 && row + y < visibleHeight4; y += childHeight4)
                {
                    for (int x = 0; x < width4 && column + x < visibleWidth4; x += childWidth4)
                    {
                        Av1RateDistortionStatistics child = this.SelectInterTransformNode(
                            writer,
                            macroBlock,
                            blockOrigin,
                            ref modeInfo,
                            childSize,
                            row + y,
                            column + x,
                            depth + 1,
                            coefficientAbove,
                            coefficientLeft,
                            transformAbove,
                            transformLeft,
                            states,
                            ref stateCount,
                            noSplit.Cost / childCount,
                            splitLimit - split.Cost,
                            -1);

                        if (child.Cost == long.MaxValue)
                        {
                            split = Av1RateDistortionStatistics.Invalid;
                            break;
                        }

                        split.Add(this.rateMultiplier, in child);
                        if (split.Cost > splitLimit)
                        {
                            split = Av1RateDistortionStatistics.Invalid;
                            break;
                        }
                    }

                    if (split.Cost == long.MaxValue)
                    {
                        break;
                    }
                }
            }

            // Equal costs retain the subdivision. A larger transform restores the incoming edge
            // contexts before publishing its own edges, discarding every child's trial state.
            if (split.Cost <= noSplit.Cost)
            {
                return split;
            }

            savedCoefficientAbove[..coefficientAbove.Length].CopyTo(coefficientAbove);
            savedCoefficientLeft[..coefficientLeft.Length].CopyTo(coefficientLeft);
            savedTransformAbove[..transformAbove.Length].CopyTo(transformAbove);
            savedTransformLeft[..transformLeft.Length].CopyTo(transformLeft);
            stateCount = savedStateCount;
            if (!this.picture.Parent.FrameHeader.CodedLossless)
            {
                states[stateCount++] = noSplitState;
            }

            Size frameContextSize = new(this.picture.Parent.FrameHeader.ModeInfoColumnCount, this.picture.Parent.FrameHeader.ModeInfoRowCount);
            Av1TileWriter.UpdateCoefficientContexts(
                coefficientAbove.Slice(column, width4),
                coefficientLeft.Slice(row, height4),
                coefficientContext,
                origin,
                frameContextSize);

            transformAbove.Slice(column, Math.Min(width4, visibleWidth4 - column)).Fill((byte)width);
            transformLeft.Slice(row, Math.Min(height4, visibleHeight4 - row)).Fill((byte)height);
            for (int y = 0; y < height4; y++)
            {
                for (int x = 0; x < width4; x++)
                {
                    modeInfo.InterTransformSizes[modeInfo.GetInterTransformSizeIndex(row + y, column + x)] = transformSize;
                }
            }

            return noSplit;
        }

        private void SelectSkipModeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            int skipModeContext,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1RateDistortionStatistics selectedStatistics,
            ref Av1MotionVector selectedVector,
            ref Av1MotionVector selectedSecondaryVector,
            ref InlineArray128<Av1EncoderTransformBlockState> selectedStates)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Av1ReferenceFrameType primaryReference = frameHeader.SkipModeParameters.FirstReferenceFrame;
            Av1ReferenceFrameType secondaryReference = frameHeader.SkipModeParameters.SecondReferenceFrame;
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            ref Av1ReferenceMotionVectors referenceMotionVectors = ref this.blockWorkspace.ReferenceMotionVectors;
            referenceMotionVectors.Build(
                this.picture,
                macroBlock,
                modeInfoPosition,
                blockSize,
                modeInfo.Block.PartitionType,
                this.picture.Sequence.SequenceHeader,
                frameHeader,
                primaryReference,
                secondaryReference);

            Av1MotionVector primaryVector = referenceMotionVectors.GetCompoundNearestReference(0);
            Av1MotionVector secondaryVector = referenceMotionVectors.GetCompoundNearestReference(1);
            Av1InterpolationFilter filter = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable
                ? Av1InterpolationFilter.Regular
                : frameHeader.InterpolationFilter;
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int rate = writer.GetSkipModeCost(true, skipModeContext);
            int normalizationShift = (this.bitDepth.GetBitCount() - 8) * 2;
            long distortion = 0;
            bool exactPrediction = true;
            int planeCount = block.HasChroma ? 3 : 1;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Point origin = planeIndex == 0 ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
                Av1BlockSize planeSize = blockSize.GetSubsampled(subX != 0, subY != 0);
                Span<TSample> prediction = plane switch
                {
                    Av1Plane.Y => workspace.LumaPrediction,
                    Av1Plane.U => workspace.BluePrediction,
                    _ => workspace.RedPrediction
                };

                this.PrepareInterPlanePrediction(
                    primaryVector,
                    secondaryVector,
                    plane,
                    Av1PredictionMode.NearestNearestMotionVector,
                    primaryReference,
                    secondaryReference,
                    false,
                    Av1CompoundType.Average,
                    0,
                    false,
                    Av1DifferenceWeightedMaskType.Type38,
                    filter,
                    filter,
                    this.references.Span[(int)primaryReference].CodedView.GetPlane(plane),
                    this.references.Span[(int)secondaryReference].CodedView.GetPlane(plane),
                    new Point(origin.X << subX, origin.Y << subY),
                    subX,
                    subY,
                    blockSize,
                    prediction,
                    workspace.Residual);

                // Skip mode carries no coefficient or transform-choice syntax. Accumulate visible
                // prediction error directly and abandon later planes once the candidate cannot win.
                Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                int width = Math.Min(planeSize.GetWidth(), sourcePlane.Width - origin.X);
                int height = Math.Min(planeSize.GetHeight(), sourcePlane.Height - origin.Y);
                long squaredError = 0;
                for (int row = 0; row < height; row++)
                {
                    squaredError += Av1ResidualBuilder.SumSquares(workspace.Residual.Slice(row * planeSize.GetWidth(), width));
                }

                exactPrediction &= squaredError == 0;
                distortion += normalizationShift == 0
                    ? squaredError << 4
                    : ((squaredError + (1L << (normalizationShift - 1))) >> normalizationShift) << 4;
                if (Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion) > selectedStatistics.Cost)
                {
                    return;
                }
            }

            // Normalization can round a small high-bit-depth error to zero. A reversible frame
            // still requires the original samples, so preserve the unrounded equality decision.
            if (frameHeader.CodedLossless && !exactPrediction)
            {
                return;
            }

            selectedStatistics = new(this.rateMultiplier, rate, distortion)
            {
                PredictionDistortion = distortion
            };
            selectedVector = primaryVector;
            selectedSecondaryVector = secondaryVector;
            selectedStates = default;
            modeInfo.Block.TransformSize = frameHeader.CodedLossless ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize();
            modeInfo.Block.InterTransformSizes.Fill(modeInfo.Block.TransformSize);
            modeInfo.Block.ReferenceFrame = primaryReference;
            modeInfo.Block.SecondaryReferenceFrame = secondaryReference;
            modeInfo.Block.Mode = Av1PredictionMode.NearestNearestMotionVector;
            modeInfo.Block.UvMode = Av1ChromaPredictionMode.DC;
            modeInfo.Block.UseIntraBlockCopy = false;
            modeInfo.Block.Skip = true;
            modeInfo.Block.SkipMode = true;
            modeInfo.Block.CompoundGroupIndex = false;
            modeInfo.Block.CompoundIndex = true;
            modeInfo.Block.CompoundType = Av1CompoundType.Average;
            modeInfo.Block.HorizontalInterpolationFilter = filter;
            modeInfo.Block.VerticalInterpolationFilter = filter;
            block.ReferenceMotionVectorIndex = 0;
            block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 0;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 0;
            block.PredictionUnit.ChromaFromLumaIndex = 0;
            block.PredictionUnit.ChromaFromLumaSigns = 0;
        }

        /// <summary>
        /// Searches one single-reference syntax mode and retains its dynamic-reference-list winner.
        /// </summary>
        private Av1RateDistortionStatistics SelectSingleReferenceMode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            long bestCost,
            Av1ReferenceFrameType referenceFrame,
            Av1PredictionMode requestedMode,
            ref Av1ReferenceMotionVectors referenceMotionVectors,
            ref int cachedInterIntraMode,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            out Av1MotionVector selectedVector,
            out InlineArray128<Av1EncoderTransformBlockState> selectedStates,
            ref InlineArray3<Av1MotionVector> searchedNewVectors,
            ref byte searchedNewVectorMask)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            Av1TransformSize lumaTransformSize = this.picture.Parent.FrameHeader.CodedLossless
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaximumTransformSize();

            // A single-reference candidate carries none of the compound syntax, and the block is
            // reused across candidates, so every compound field is returned to its default here
            // rather than where a compound candidate happens to have set it. The same applies to
            // the intra fields: an inter block signals no angle delta, no filter-intra mode and no
            // chroma-from-luma parameters.
            modeInfo.Block.ReferenceFrame = referenceFrame;
            modeInfo.Block.SecondaryReferenceFrame = Av1ReferenceFrameType.None;
            modeInfo.Block.CompoundGroupIndex = false;
            modeInfo.Block.CompoundIndex = true;
            modeInfo.Block.CompoundType = Av1CompoundType.Average;
            modeInfo.Block.CompoundWedgeIndex = 0;
            modeInfo.Block.CompoundWedgeSign = false;
            modeInfo.Block.DifferenceWeightedMaskType = Av1DifferenceWeightedMaskType.Type38;
            modeInfo.Block.UvMode = Av1ChromaPredictionMode.DC;
            modeInfo.Block.TransformSize = lumaTransformSize;
            modeInfo.Block.UseIntraBlockCopy = false;
            block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 0;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 0;
            block.PredictionUnit.ChromaFromLumaIndex = 0;
            block.PredictionUnit.ChromaFromLumaSigns = 0;

            Av1EncoderInterPredictionWorkspace<TSample> workspace =
                this.blockWorkspace.GetInterPredictionWorkspace<TSample>();

            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

            // The global motion of a reference is a warp model, so the vector it contributes
            // depends on where the block sits. Reference: gm_get_motion_vector().
            Av1MotionVector globalMotion = frameHeader
                .GetGlobalMotionParameters()[(int)referenceFrame - (int)Av1ReferenceFrameType.Last]
                .GetMotionVector(
                    frameHeader.AllowHighPrecisionMotionVector,
                    blockSize,
                    modeInfoPosition,
                    frameHeader.ForceIntegerMotionVector);

            // A mode that codes a dynamic reference list index searches more than one entry of that
            // list, and the reference caps the search at three. The near mode starts at the second
            // entry, because the first is what the nearest mode already covers, so it has one fewer
            // candidate. Every other mode has a single vector and no list index to code.
            // Reference: the ref_mv_idx loop of handle_inter_mode(), bounded by MAX_REF_MV_SEARCH.
            InlineArray3<Av1MotionVector> candidateVectors = default;
            InlineArray3<byte> candidateReferenceIndices = default;
            int candidateCount = requestedMode switch
            {
                Av1PredictionMode.NewMotionVector => Math.Min(3, Math.Max(1, referenceMotionVectors.Count)),
                Av1PredictionMode.NearMotionVector => Math.Min(3, Math.Max(1, referenceMotionVectors.Count - 1)),
                _ => 1
            };
            for (int referenceIndex = 0; referenceIndex < candidateCount; referenceIndex++)
            {
                candidateReferenceIndices[referenceIndex] = (byte)referenceIndex;
                candidateVectors[referenceIndex] = requestedMode switch
                {
                    Av1PredictionMode.NearestMotionVector => referenceMotionVectors.Nearest,
                    Av1PredictionMode.NewMotionVector => referenceMotionVectors.GetNewReference(referenceIndex),
                    Av1PredictionMode.NearMotionVector => referenceMotionVectors.GetNearReference(referenceIndex),
                    _ => globalMotion
                };
            }

            // Each plane keeps two buffers: one holding the best candidate so far and one the
            // candidate under test. Naming both here lets the winner be taken by exchanging spans
            // rather than by copying a block of samples for every improvement.
            Span<TSample> selectedLumaReconstruction = workspace.SelectedLumaReconstruction;
            Span<TSample> candidateLumaReconstruction = workspace.LumaCandidateReconstruction;
            Span<TSample> selectedBlueReconstruction = workspace.SelectedBlueReconstruction;
            Span<TSample> candidateBlueReconstruction = workspace.BlueCandidateReconstruction;
            Span<TSample> selectedRedReconstruction = workspace.SelectedRedReconstruction;
            Span<TSample> candidateRedReconstruction = workspace.RedCandidateReconstruction;
            Span<int> selectedLumaCoefficients = workspace.SelectedLumaCoefficients;
            Span<int> candidateLumaCoefficients = workspace.LumaCandidateCoefficients;
            Span<int> selectedBlueCoefficients = workspace.SelectedBlueCoefficients;
            Span<int> candidateBlueCoefficients = workspace.BlueCandidateCoefficients;
            Span<int> selectedRedCoefficients = workspace.SelectedRedCoefficients;
            Span<int> candidateRedCoefficients = workspace.RedCandidateCoefficients;
            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            Span<byte> referenceCounts = stackalloc byte[Av1Constants.ReferenceFrameCount];
            Av1TileWriter.CollectNeighborReferenceCounts(macroBlock, referenceCounts);

            // Every candidate of this reference pays the same syntax: the flag that says the block
            // is inter, and the reference selection itself. Pricing it once keeps it out of the
            // candidate loop.
            int commonPredictionRate = writer.GetIsInterCost(
                isInter: true,
                Av1TileWriter.GetIntraInterContext(macroBlock)) +
                writer.GetSingleReferenceCost(referenceFrame, referenceCounts);

            // A frame that lets each block choose its reference mode codes one flag saying whether
            // the block is compound. A block with a side below eight samples cannot be compound, so
            // it codes no flag and pays nothing.
            if (frameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect &&
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8)
            {
                commonPredictionRate += writer.ModeCosts.GetCompInter(Av1SymbolContextHelper.GetReferenceModeContext(macroBlock), 0);
            }

            // A frame that selects its transform size codes, for each block, whether the largest
            // transform is split. This search never splits, so the cost of the unsplit flag is
            // common to every candidate. The smallest transform cannot split and codes nothing.
            int transformPartitionRate = 0;
            if (frameHeader.TransformMode == Av1TransformMode.Select && lumaTransformSize != Av1TransformSize.Size4x4)
            {
                Av1NeighborArrayUnit<byte> transformContexts = this.picture.TransformFunctionContexts[tileIndex];
                int topIndex = transformContexts.GetTopIndex(blockOrigin);
                int leftIndex = transformContexts.GetLeftIndex(blockOrigin);
                int transformPartitionContext = Av1SymbolContextHelper.GetTransformPartitionContext(
                    transformContexts.Top[topIndex],
                    transformContexts.Left[leftIndex],
                    blockSize,
                    lumaTransformSize);

                transformPartitionRate = writer.GetTransformPartitionCost(false, transformPartitionContext);
            }

            Av1RateDistortionStatistics selectedStatistics = Av1RateDistortionStatistics.Invalid;
            selectedVector = default;
            selectedStates = default;
            Av1PredictionMode selectedMode = default;
            int selectedReferenceIndex = 0;
            bool selectedSkip = false;
            bool selectedInterIntra = false;
            Av1InterIntraMode selectedInterIntraMode = default;
            bool selectedInterIntraWedge = false;
            int selectedInterIntraWedgeIndex = 0;
            InlineArray64<Av1EncoderTransformBlockState> selectedLumaStates = default;
            InlineArray16<Av1TransformSize> selectedLumaSizes = default;
            InlineArray16<Av1EncoderTransformBlockState> selectedBlueState = default;
            InlineArray16<Av1EncoderTransformBlockState> selectedRedState = default;

            ObuSequenceHeader sequenceHeader = this.picture.Sequence.SequenceHeader;
            bool interIntraEligible = sequenceHeader.EnableInterIntraCompound &&
                blockSize is >= Av1BlockSize.Block8x8 and <= Av1BlockSize.Block32x32;
            bool isSwitchable = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable;
            Av1InterpolationFilter defaultFilter = isSwitchable ? Av1InterpolationFilter.Regular : frameHeader.InterpolationFilter;
            Av1InterpolationFilter selectedVerticalFilter = defaultFilter;
            Av1InterpolationFilter selectedHorizontalFilter = defaultFilter;
            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<TSample> referencePlane = this.references.Span[(int)referenceFrame].CodedView.GetPlane(Av1Plane.Y);
            int sourceOrigin = ((sourcePlane.Bounds.Y + blockOrigin.Y) * sourcePlane.Stride) + sourcePlane.Bounds.X + blockOrigin.X;
            int referenceOrigin = ((referencePlane.Bounds.Y + blockOrigin.Y) * referencePlane.Stride) + referencePlane.Bounds.X + blockOrigin.X;
            Size frameSize = new(
                this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

            Rectangle frameBounds = Av1MotionVector.GetFrameSearchBounds(
                new Rectangle(blockOrigin, new Size(blockSize.GetWidth(), blockSize.GetHeight())),
                frameSize,
                Math.Min(referencePlane.Bounds.X, referencePlane.Bounds.Y));

            Av1NeighborArrayUnit<byte> coefficientContexts = this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex];
            ReadOnlySpan<byte> aboveContexts = coefficientContexts.Top.Slice(
                coefficientContexts.GetTopIndex(blockOrigin),
                blockSize.Get4x4WideCount());

            ReadOnlySpan<byte> leftContexts = coefficientContexts.Left.Slice(
                coefficientContexts.GetLeftIndex(blockOrigin),
                blockSize.Get4x4HighCount());

            // Frame owners provide contiguous padded planes. Borrow those spans without copying source blocks
            // or reconstructing border samples, and keep the search scratch disjoint from retained inter winners.
            Av1MotionSearchBase.SingleReferenceSearch<TSample, TOperator> motionSearch = new(
                sourcePlane.Buffer.DangerousGetSingleSpan()[sourceOrigin..],
                sourcePlane.Stride,
                referencePlane.Buffer.DangerousGetSingleSpan(),
                referencePlane.Stride,
                referenceOrigin,
                blockSize,
                frameBounds,
                this.blockWorkspace,
                this.blockWorkspace.GetMotionSearchPrediction<TSample>(),
                this.blockWorkspace.Residual,
                workspace.PredictionScratch,
                workspace.TransformCoefficients,
                writer,
                aboveContexts,
                leftContexts,
                this.bitDepth,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[0],
                0,
                frameHeader.CodedLossless,
                this.rateMultiplier,
                transformPartitionRate,
                writer.GetSkipCost(false, skipContext),
                writer.GetSkipCost(true, skipContext),
                defaultFilter,
                defaultFilter,
                this.blockWorkspace.GetMotionVectorCosts(frameHeader.MotionVectorPrecision));

            // The first two reference predictors set the block's spatial range. Clamping at the last
            // potentially visible interpolation tap bounds padded reads without changing their prediction.
            int spatialMagnitude = 0;
            for (int index = 0; index < 2; index++)
            {
                Av1MotionVector spatial = referenceMotionVectors.GetNewReference(index);
                int column = Math.Clamp(spatial.Column, -(blockOrigin.X + blockSize.GetWidth() + 4) * 8, (frameSize.Width - blockOrigin.X + 4) * 8);
                int row = Math.Clamp(spatial.Row, -(blockOrigin.Y + blockSize.GetHeight() + 4) * 8, (frameSize.Height - blockOrigin.Y + 4) * 8);
                spatialMagnitude = Math.Max(spatialMagnitude, Math.Max(Math.Abs(row), Math.Abs(column)) >> 3);
            }

            Av1MotionSearchSettings motionSettings = this.picture.Parent.MotionSearchSettings;
            Av1MotionSearchBase.SingleReferenceState motionState = default;
            Span<Av1MotionSearchBase.StartingCandidate> motionStarts = stackalloc Av1MotionSearchBase.StartingCandidate[1];

            int candidateMask = (1 << candidateCount) - 1;
            int reductionLevel = this.picture.Parent.SpeedSettings.ReduceInterReferenceIndices;
            if (reductionLevel != 0 && candidateCount > 1)
            {
                bool secondaryPastReference = referenceFrame is Av1ReferenceFrameType.Last2 or Av1ReferenceFrameType.Last3;
                bool distantNewReference = reductionLevel >= 2 && requestedMode == Av1PredictionMode.NewMotionVector &&
                    referenceFrame != this.picture.Parent.NearestPastReference && referenceFrame != this.picture.Parent.NearestFutureReference;

                for (int index = 1; index < candidateCount; index++)
                {
                    int weightIndex = candidateReferenceIndices[index] + (requestedMode == Av1PredictionMode.NearMotionVector ? 1 : 0);
                    bool pruneByQuantizer = reductionLevel >= 3 ||
                        candidateReferenceIndices[index] >= (this.quantization.QIndex[0] * 3 / 256) + 1;

                    if ((secondaryPastReference || (distantNewReference && pruneByQuantizer)) &&
                        referenceMotionVectors.Weights[weightIndex] < Av1ReferenceMotionVectors.NearestCandidateWeight)
                    {
                        candidateMask &= ~(1 << index);
                    }
                }
            }

            if (requestedMode == Av1PredictionMode.NearMotionVector && candidateCount > 1)
            {
                InlineArray3<long> translationCosts = default;
                translationCosts[..].Fill(long.MaxValue);
                InlineArray4<Av1RateDistortionStatistics> planeStatistics = default;
                bool modelTranslation = this.picture.Parent.SpeedSettings.PruneNearMotionByTranslation &&
                    blockSize.GetWidth() * blockSize.GetHeight() > 64;

                modeInfo.Block.Mode = requestedMode;
                long bestTranslation = long.MaxValue;
                for (int index = 0; index < candidateCount; index++)
                {
                    if ((candidateMask & (1 << index)) == 0)
                    {
                        continue;
                    }

                    int modeRate = this.GetInterModeRate(
                        writer, requestedMode, candidateVectors[index], candidateReferenceIndices[index], in referenceMotionVectors);

                    // Every candidate of this mode pays the mode itself, so removing it leaves the
                    // part that separates one list entry from another. A candidate whose syntax
                    // alone already costs more than the block winner cannot win at any distortion,
                    // so it is dropped before any prediction is formed.
                    int drlRate = modeRate - writer.GetInterModeCost(requestedMode, referenceMotionVectors.ModeContext);

                    if (Av1RateDistortion.GetCost(this.rateMultiplier, commonPredictionRate + drlRate, 0) > bestCost)
                    {
                        candidateMask &= ~(1 << index);
                        continue;
                    }

                    // A surviving candidate is priced with a model of the prediction error rather
                    // than a transform search. The estimate is only used to rank the list entries
                    // against each other, so its absolute value does not have to be exact.
                    if (modelTranslation && Av1RateDistortion.GetCost(this.rateMultiplier, commonPredictionRate + modeRate, 0) <= bestCost)
                    {
                        Av1RateDistortionStatistics estimate = this.GetInterFilterModelCost(
                            candidateVectors[index],
                            default,
                            modeInfo.Block,
                            blockOrigin,
                            blockSize,
                            false,
                            defaultFilter,
                            defaultFilter,
                            commonPredictionRate + modeRate,
                            long.MaxValue,
                            100,
                            0,
                            workspace.LumaPrediction,
                            workspace.BluePrediction,
                            workspace.RedPrediction,
                            planeStatistics);

                        translationCosts[index] = estimate.Cost;
                        bestTranslation = Math.Min(bestTranslation, estimate.Cost);
                    }
                }

                if (modelTranslation)
                {
                    for (int index = 0; index < candidateCount; index++)
                    {
                        // The two relative bounds keep the best DRL estimate and reject entries that
                        // cannot approach either it or the current block winner.
                        if (!((double)translationCosts[index] / bestTranslation < 1.001 &&
                            (double)translationCosts[index] / bestCost < 5))
                        {
                            candidateMask &= ~(1 << index);
                        }
                    }
                }
            }

            // Rank interpolation families with prediction-error modeling before running a full transform search.
            // The selected inter reconstruction remains untouched while two existing prediction views alternate.
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                if ((candidateMask & (1 << candidateIndex)) == 0)
                {
                    continue;
                }

                if (requestedMode == Av1PredictionMode.NewMotionVector)
                {
                    int referenceIndex = candidateReferenceIndices[candidateIndex];
                    Av1MotionVector referenceVector = candidateVectors[candidateIndex];

                    // The list index is coded as a run of flags, each saying whether to move past
                    // the current entry. Reaching entry n therefore costs n flags that advance and
                    // one that stops, and the run ends at the first flag that does not advance.
                    int drlRate = 0;
                    for (int index = 0; index < 2 && referenceMotionVectors.Count > index + 1; index++)
                    {
                        bool advance = referenceIndex > index;
                        int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                        drlRate += writer.GetDynamicReferenceListCost(advance, context);
                        if (!advance)
                        {
                            break;
                        }
                    }

                    if (candidateCount > 1 &&
                        Av1RateDistortion.GetCost(this.rateMultiplier, commonPredictionRate + drlRate, 0) > bestCost)
                    {
                        continue;
                    }

                    // A later list entry usually starts near an entry already searched. Finding the
                    // nearest one that was searched lets this search cover only the ground between
                    // the two starting points plus the distance that search actually travelled,
                    // instead of the full range again.
                    int searchRange = int.MaxValue;
                    if (motionSettings.ReduceSearchRange && referenceIndex > 0)
                    {
                        int minimumDifference = int.MaxValue;
                        int bestMatch = 0;
                        for (int index = 0; index < referenceIndex; index++)
                        {
                            Av1MotionVector previousReference = motionState.References[index].ReferenceVector;
                            int difference = Math.Max(
                                Math.Abs(referenceVector.Row - previousReference.Row),
                                Math.Abs(referenceVector.Column - previousReference.Column));

                            if (difference < minimumDifference)
                            {
                                minimumDifference = difference;
                                bestMatch = index;
                            }
                        }

                        // The bound only holds when the two starting points are close. Sixteen
                        // samples, at the eighth-sample precision the vectors carry, is the
                        // distance beyond which the earlier search says nothing useful.
                        ref Av1MotionSearchBase.ReferenceSearchResult previous = ref motionState.References[bestMatch];
                        if (minimumDifference < 16 * 8 && previous.IsValid)
                        {
                            int displacement = Math.Max(
                                Math.Abs(previous.Vector.Row - previous.ReferenceVector.Row),
                                Math.Abs(previous.Vector.Column - previous.ReferenceVector.Column));

                            searchRange = (minimumDifference + displacement + 4) >> 3;
                        }
                    }

                    Point startVector = new(
                        (referenceVector.Column + 3 + (referenceVector.Column >= 0 ? 1 : 0)) >> 3,
                        (referenceVector.Row + 3 + (referenceVector.Row >= 0 ? 1 : 0)) >> 3);

                    motionStarts[0] = new Av1MotionSearchBase.StartingCandidate(startVector, 0);
                    if (!motionSearch.Search(
                        motionSettings,
                        this.picture.Parent.MotionSearchStepParameter,
                        spatialMagnitude,
                        frameHeader.ShowFrame,
                        searchRange,
                        frameHeader.ForceIntegerMotionVector,
                        frameHeader.AllowHighPrecisionMotionVector,
                        fineMeshInterval: false,
                        referenceIndex,
                        referenceVector,
                        drlRate,
                        motionStarts,
                        totalWeight: 0,
                        ref motionState,
                        out Av1MotionSearchBase.FractionalResult searchResult) ||
                        motionState.References[referenceIndex].Skip)
                    {
                        continue;
                    }

                    candidateVectors[candidateIndex] = searchResult.Vector;
                    searchedNewVectors[referenceIndex] = searchResult.Vector;
                    searchedNewVectorMask |= (byte)(1 << referenceIndex);
                }

                modeInfo.Block.Mode = requestedMode;
                long candidateLimit = Math.Min(bestCost, selectedStatistics.Cost);
                modeInfo.Block.HorizontalInterpolationFilter = defaultFilter;
                modeInfo.Block.VerticalInterpolationFilter = defaultFilter;
                int filterCostIndex = (((((int)requestedMode - (int)Av1PredictionMode.InterModeStart) * 3) +
                    candidateReferenceIndices[candidateIndex]) * Av1Constants.ReferenceFrameCount) + (int)referenceFrame;

                Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
                bool skipInterpolation = settings.SkipSingleInterpolationSearch ||
                    (settings.UseWinnerInterpolation && frameHeader.ReferenceMode == ObuReferenceMode.SingleReference);
                bool preparedPrediction = false;
                int filterRate = 0;
                if (!skipInterpolation)
                {
                    preparedPrediction = this.SelectInterFilters(
                        writer,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        block.HasChroma,
                        candidateVectors[candidateIndex],
                        default,
                        ref modeInfo.Block,
                        candidateLimit,
                        long.MaxValue,
                        out filterRate,
                        out long filterModelCost);

                    this.blockWorkspace.SingleReferenceFilterCosts[filterCostIndex] = filterModelCost;
                    if (settings.ModelBasedInterpolationBreakout &&
                        candidateLimit != long.MaxValue && (filterModelCost >> 3) * 3 > candidateLimit)
                    {
                        continue;
                    }
                }
                else if (Av1TileWriter.UsesSwitchableInterpolation(frameHeader, modeInfo.Block))
                {
                    filterRate = writer.GetSwitchableInterpolationFilterCost(
                        defaultFilter, Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo.Block, macroBlock, 0));

                    if (sequenceHeader.EnableDualFilter)
                    {
                        filterRate += writer.GetSwitchableInterpolationFilterCost(
                            defaultFilter, Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo.Block, macroBlock, 1));
                    }
                }

                Av1InterpolationFilter horizontalFilter = modeInfo.Block.HorizontalInterpolationFilter;
                Av1InterpolationFilter verticalFilter = modeInfo.Block.VerticalInterpolationFilter;
                Av1RateDistortionStatistics candidateStatistics = this.EvaluateInterCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    Math.Min(bestCost, selectedStatistics.Cost),
                    block.HasChroma,
                    commonPredictionRate + filterRate + (interIntraEligible ? writer.GetInterIntraCost(blockSize, false, default, false, 0) : 0),
                    referenceFrame,
                    candidateVectors[candidateIndex],
                    default,
                    requestedMode,
                    Av1ReferenceFrameType.None,
                    usePreparedPrediction: preparedPrediction,
                    Av1CompoundType.Average,
                    0,
                    false,
                    Av1DifferenceWeightedMaskType.Type38,
                    0,
                    horizontalFilter,
                    verticalFilter,
                    candidateReferenceIndices[candidateIndex],
                    false,
                    default,
                    false,
                    0,
                    in referenceMotionVectors,
                    candidateLumaReconstruction,
                    candidateLumaCoefficients,
                    candidateBlueReconstruction,
                    candidateBlueCoefficients,
                    candidateRedReconstruction,
                    candidateRedCoefficients,
                    out bool candidateSkip,
                    out InlineArray64<Av1EncoderTransformBlockState> candidateLumaStates,
                    out InlineArray16<Av1TransformSize> candidateLumaSizes,
                    out InlineArray16<Av1EncoderTransformBlockState> candidateBlueState,
                    out InlineArray16<Av1EncoderTransformBlockState> candidateRedState);

                this.blockWorkspace.SingleReferenceSimpleCosts[filterCostIndex] = candidateStatistics.Cost;

                // Strict replacement preserves nearest, new, near, then global mode order on equal RD cost.
                if (candidateStatistics.Cost < selectedStatistics.Cost)
                {
                    // The winner is taken by exchanging the two buffers of each plane rather than
                    // by copying samples. The loser becomes the scratch that the next candidate
                    // writes into, so no block of reconstruction or coefficients is ever moved.
                    Span<TSample> displacedLumaReconstruction = selectedLumaReconstruction;
                    selectedLumaReconstruction = candidateLumaReconstruction;
                    candidateLumaReconstruction = displacedLumaReconstruction;

                    Span<TSample> displacedBlueReconstruction = selectedBlueReconstruction;
                    selectedBlueReconstruction = candidateBlueReconstruction;
                    candidateBlueReconstruction = displacedBlueReconstruction;

                    Span<TSample> displacedRedReconstruction = selectedRedReconstruction;
                    selectedRedReconstruction = candidateRedReconstruction;
                    candidateRedReconstruction = displacedRedReconstruction;

                    Span<int> displacedLumaCoefficients = selectedLumaCoefficients;
                    selectedLumaCoefficients = candidateLumaCoefficients;
                    candidateLumaCoefficients = displacedLumaCoefficients;

                    Span<int> displacedBlueCoefficients = selectedBlueCoefficients;
                    selectedBlueCoefficients = candidateBlueCoefficients;
                    candidateBlueCoefficients = displacedBlueCoefficients;

                    Span<int> displacedRedCoefficients = selectedRedCoefficients;
                    selectedRedCoefficients = candidateRedCoefficients;
                    candidateRedCoefficients = displacedRedCoefficients;

                    selectedStatistics = candidateStatistics;
                    selectedVector = candidateVectors[candidateIndex];
                    selectedMode = requestedMode;
                    selectedHorizontalFilter = horizontalFilter;
                    selectedVerticalFilter = verticalFilter;
                    selectedReferenceIndex = candidateReferenceIndices[candidateIndex];
                    selectedSkip = candidateSkip;
                    selectedInterIntra = false;
                    selectedLumaStates = candidateLumaStates;
                    selectedLumaSizes = candidateLumaSizes;
                    selectedBlueState = candidateBlueState;
                    selectedRedState = candidateRedState;
                }

                if (!interIntraEligible)
                {
                    continue;
                }

                int interIntraMotionRate = 0;
                if (requestedMode == Av1PredictionMode.NewMotionVector)
                {
                    Av1MotionVector referenceVector = referenceMotionVectors.GetNewReference(candidateReferenceIndices[candidateIndex]);
                    Av1MotionVectorCosts costs = this.blockWorkspace.GetMotionVectorCosts(frameHeader.MotionVectorPrecision);
                    interIntraMotionRate = ((costs.GetCost(candidateVectors[candidateIndex], referenceVector) * 108) + 64) >> 7;
                }

                Av1MotionVector interIntraVector = candidateVectors[candidateIndex];
                if (!this.SelectInterIntraBlend(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    referenceFrame,
                    ref interIntraVector,
                    referenceMotionVectors.GetNewReference(candidateReferenceIndices[candidateIndex]),
                    requestedMode == Av1PredictionMode.NewMotionVector,
                    horizontalFilter,
                    verticalFilter,
                    interIntraMotionRate,
                    Math.Min(bestCost, selectedStatistics.Cost),
                    ref cachedInterIntraMode,
                    out Av1InterIntraMode interIntraMode,
                    out bool useWedge,
                    out int wedgeIndex))
                {
                    continue;
                }

                this.PrepareInterIntraPrediction(
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    block.HasChroma,
                    referenceFrame,
                    interIntraVector,
                    horizontalFilter,
                    verticalFilter,
                    interIntraMode,
                    useWedge,
                    wedgeIndex);

                int interIntraRate = writer.GetInterIntraCost(
                    blockSize,
                    true,
                    interIntraMode,
                    useWedge,
                    wedgeIndex);

                Av1RateDistortionStatistics interIntraStatistics = this.EvaluateInterCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    Math.Min(bestCost, selectedStatistics.Cost),
                    block.HasChroma,
                    commonPredictionRate + filterRate + interIntraRate,
                    referenceFrame,
                    interIntraVector,
                    default,
                    requestedMode,
                    Av1ReferenceFrameType.None,
                    usePreparedPrediction: true,
                    Av1CompoundType.Average,
                    0,
                    false,
                    Av1DifferenceWeightedMaskType.Type38,
                    0,
                    horizontalFilter,
                    verticalFilter,
                    candidateReferenceIndices[candidateIndex],
                    true,
                    interIntraMode,
                    useWedge,
                    wedgeIndex,
                    in referenceMotionVectors,
                    candidateLumaReconstruction,
                    candidateLumaCoefficients,
                    candidateBlueReconstruction,
                    candidateBlueCoefficients,
                    candidateRedReconstruction,
                    candidateRedCoefficients,
                    out bool interIntraSkip,
                    out InlineArray64<Av1EncoderTransformBlockState> interIntraLumaStates,
                    out InlineArray16<Av1TransformSize> interIntraLumaSizes,
                    out InlineArray16<Av1EncoderTransformBlockState> interIntraBlueState,
                    out InlineArray16<Av1EncoderTransformBlockState> interIntraRedState);

                if (interIntraStatistics.Cost >= selectedStatistics.Cost)
                {
                    continue;
                }

                Span<TSample> previousLumaReconstruction = selectedLumaReconstruction;
                selectedLumaReconstruction = candidateLumaReconstruction;
                candidateLumaReconstruction = previousLumaReconstruction;
                Span<TSample> previousBlueReconstruction = selectedBlueReconstruction;
                selectedBlueReconstruction = candidateBlueReconstruction;
                candidateBlueReconstruction = previousBlueReconstruction;
                Span<TSample> previousRedReconstruction = selectedRedReconstruction;
                selectedRedReconstruction = candidateRedReconstruction;
                candidateRedReconstruction = previousRedReconstruction;
                Span<int> previousLumaCoefficients = selectedLumaCoefficients;
                selectedLumaCoefficients = candidateLumaCoefficients;
                candidateLumaCoefficients = previousLumaCoefficients;
                Span<int> previousBlueCoefficients = selectedBlueCoefficients;
                selectedBlueCoefficients = candidateBlueCoefficients;
                candidateBlueCoefficients = previousBlueCoefficients;
                Span<int> previousRedCoefficients = selectedRedCoefficients;
                selectedRedCoefficients = candidateRedCoefficients;
                candidateRedCoefficients = previousRedCoefficients;
                selectedStatistics = interIntraStatistics;
                selectedVector = interIntraVector;
                selectedMode = requestedMode;
                selectedHorizontalFilter = horizontalFilter;
                selectedVerticalFilter = verticalFilter;
                selectedReferenceIndex = candidateReferenceIndices[candidateIndex];
                selectedSkip = interIntraSkip;
                selectedInterIntra = true;
                selectedInterIntraMode = interIntraMode;
                selectedInterIntraWedge = useWedge;
                selectedInterIntraWedgeIndex = wedgeIndex;
                selectedLumaStates = interIntraLumaStates;
                selectedLumaSizes = interIntraLumaSizes;
                selectedBlueState = interIntraBlueState;
                selectedRedState = interIntraRedState;
            }

            // Candidate pixels and coefficients remain scratch. Preserve the transform decisions so final
            // reconstruction can regenerate only the winner after other mode families reuse this storage.
            selectedLumaStates[..].CopyTo(selectedStates);
            selectedLumaSizes[..].CopyTo(modeInfo.Block.InterTransformSizes);
            selectedBlueState[..].CopyTo(selectedStates[64..80]);
            selectedRedState[..].CopyTo(selectedStates[80..96]);

            modeInfo.Block.Mode = selectedMode;
            modeInfo.Block.Skip = selectedSkip;
            if (selectedInterIntra)
            {
                modeInfo.Block.SecondaryReferenceFrame = Av1ReferenceFrameType.Intra;
                modeInfo.Block.InterIntraMode = selectedInterIntraMode;
                modeInfo.Block.UseInterIntraWedge = selectedInterIntraWedge;
                modeInfo.Block.InterIntraWedgeIndex = (byte)selectedInterIntraWedgeIndex;
            }

            modeInfo.Block.VerticalInterpolationFilter = selectedVerticalFilter;
            modeInfo.Block.HorizontalInterpolationFilter = selectedHorizontalFilter;
            block.ReferenceMotionVectorIndex = selectedReferenceIndex;
            return selectedStatistics;
        }

        /// <summary>
        /// Selects the intra mode and blend mask before the complete inter residual search.
        /// </summary>
        private bool SelectInterIntraBlend(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Av1ReferenceFrameType referenceFrame,
            ref Av1MotionVector vector,
            Av1MotionVector referenceVector,
            bool searchMotion,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int motionRate,
            long bestCost,
            ref int cachedMode,
            out Av1InterIntraMode selectedMode,
            out bool selectedWedge,
            out int selectedWedgeIndex)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            Av1ModeCosts modeCosts = writer.ModeCosts;
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int sampleCount = width * height;
            Span<TSample> interPrediction = workspace.TransformReconstruction[..sampleCount];
            Span<TSample> blendedPrediction = workspace.LumaPrediction[..sampleCount];
            Span<short> intraResidual = workspace.Residual[..sampleCount];
            Span<short> interResidual = workspace.Residual.Slice(sampleCount, sampleCount);
            Span<byte> mask = this.blockWorkspace.GetCompoundPredictionMask()[..sampleCount];
            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<TSample> referencePlane = this.references.Span[(int)referenceFrame].CodedView.GetPlane(Av1Plane.Y);
            int columnQ4 = (blockOrigin.X << 4) + (vector.Column << 1);
            int rowQ4 = (blockOrigin.Y << 4) + (vector.Row << 1);
            TOperator.PrepareTranslationalInterPrediction(
                sourcePlane,
                blockOrigin,
                referencePlane,
                new Point(columnQ4 >> 4, rowQ4 >> 4),
                horizontalFilter,
                verticalFilter,
                columnQ4 & 15,
                rowQ4 & 15,
                interPrediction,
                interResidual,
                workspace.PredictionScratch,
                blockSize,
                this.bitDepth);

            int shift = (this.bitDepth.GetBitCount() - 8) * 2;
            int acQuantizer = Av1QuantizationLookup.GetAcQuant(this.quantization.QIndex[0], this.quantization.DeltaQAc[0], this.bitDepth);
            selectedMode = Av1InterIntraMode.DC;
            selectedWedge = false;
            selectedWedgeIndex = 0;
            long bestModeCost = long.MaxValue;
            int firstMode = settings.ReuseInterIntraMode && cachedMode >= 0 ? cachedMode : 0;
            int lastMode = settings.ReuseInterIntraMode && cachedMode >= 0 ? cachedMode : (int)Av1InterIntraMode.Smooth;
            Size visible = this.GetPredictionModelSize(blockOrigin, blockSize, 0, 0);
            for (int modeIndex = firstMode; modeIndex <= lastMode; modeIndex++)
            {
                Av1InterIntraMode mode = (Av1InterIntraMode)modeIndex;
                if (mode == Av1InterIntraMode.Smooth && settings.DisableSmoothIntra)
                {
                    continue;
                }

                Span<TSample> intraPrediction = this.PrepareInterIntraPlane(macroBlock, blockOrigin, blockSize, Av1Plane.Y, mode);
                interPrediction.CopyTo(blendedPrediction);
                Av1InterIntraMaskBuilder.FillInterIntraMask(mask, width, width, height, mode, invert: true);
                TOperator.BlendInterIntraPrediction(blendedPrediction, intraPrediction, mask, width, height);
                TOperator.GetMoments(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin),
                    sourcePlane.Stride,
                    blendedPrediction,
                    width,
                    visible.Width,
                    visible.Height,
                    out _,
                    out long error);

                if (shift != 0)
                {
                    error = (error + (1L << (shift - 1))) >> shift;
                }

                Av1RateDistortion.ModelPredictionError(
                    blockSize,
                    error,
                    visible.Width * visible.Height,
                    acQuantizer,
                    this.bitDepth,
                    this.rateMultiplier,
                    out int rate,
                    out long distortion);

                long cost = Av1RateDistortion.GetCost(this.rateMultiplier, rate + modeCosts.GetInterIntraMode(blockSize, mode), distortion);
                if (cost < bestModeCost)
                {
                    bestModeCost = cost;
                    selectedMode = mode;
                }
            }

            cachedMode = (int)selectedMode;
            Span<TSample> selectedIntra = this.PrepareInterIntraPlane(macroBlock, blockOrigin, blockSize, Av1Plane.Y, selectedMode);
            interPrediction.CopyTo(blendedPrediction);
            Av1InterIntraMaskBuilder.FillInterIntraMask(mask, width, width, height, selectedMode, invert: true);
            TOperator.BlendInterIntraPrediction(blendedPrediction, selectedIntra, mask, width, height);
            int smoothRate = motionRate + modeCosts.GetInterIntraMode(blockSize, selectedMode) + modeCosts.GetWedgeInterIntra(blockSize, 0);
            long smoothBound = bestCost < 9 * (long.MaxValue / 16) ? (bestCost / 9) * 16 : long.MaxValue;
            smoothBound -= Av1RateDistortion.GetCost(this.rateMultiplier, smoothRate, 0);
            long smoothCost = this.EstimateInterPredictionResidual(
                writer, macroBlock, blockOrigin, blockSize, tileIndex, smoothRate, smoothBound, out _);

            if (smoothCost == long.MaxValue || (bestCost != long.MaxValue && (smoothCost >> 4) * 9 > bestCost))
            {
                return false;
            }

            if (this.interSourceVariance <= settings.InterIntraWedgeVarianceThreshold)
            {
                return true;
            }

            Av1InterIntraMode wedgeMode = selectedMode;
            int wedgeIndex = 0;
            long bestWedgeCost = long.MaxValue;
            long bestWedgeResidualCost = long.MaxValue;
            firstMode = settings.FastInterIntraWedgeSearch ? (int)selectedMode : 0;
            lastMode = settings.FastInterIntraWedgeSearch ? (int)selectedMode : (int)Av1InterIntraMode.Smooth;
            for (int modeIndex = firstMode; modeIndex <= lastMode; modeIndex++)
            {
                Av1InterIntraMode mode = (Av1InterIntraMode)modeIndex;
                _ = this.PrepareInterIntraPlane(macroBlock, blockOrigin, blockSize, Av1Plane.Y, mode);
                long bestMaskCost = long.MaxValue;
                int bestMaskIndex = 0;
                for (int index = 0; index < 16; index++)
                {
                    Av1WedgeMask.Fill(mask, width, blockSize, index, wedgeSign: false, 0, 0, invert: false);
                    long squaredError = 0;
                    for (int sample = 0; sample < sampleCount; sample++)
                    {
                        // Keep the blend in six-bit weight precision until the block sum is formed.
                        // Saturation makes the scalar error arithmetic agree with packed signed lanes.
                        int error = (interResidual[sample] * 64) + (mask[sample] * (intraResidual[sample] - interResidual[sample]));
                        error = Math.Clamp(error, short.MinValue, short.MaxValue);
                        squaredError += (long)error * error;
                    }

                    squaredError = (squaredError + 2048) >> 12;
                    if (shift != 0)
                    {
                        squaredError = (squaredError + (1L << (shift - 1))) >> shift;
                    }

                    Av1RateDistortion.ModelPredictionError(
                        blockSize,
                        squaredError,
                        sampleCount,
                        acQuantizer,
                        this.bitDepth,
                        this.rateMultiplier,
                        out int rate,
                        out long distortion);

                    int wedgeRate = modeCosts.GetWedgeIndex(blockSize, index);
                    long cost = Av1RateDistortion.GetCost(this.rateMultiplier, rate + wedgeRate, distortion);
                    if (cost < bestMaskCost)
                    {
                        bestMaskCost = cost;
                        bestMaskIndex = index;
                    }
                }

                int bestMaskRate = modeCosts.GetWedgeIndex(blockSize, bestMaskIndex);
                long modeCost = bestMaskCost - Av1RateDistortion.GetCost(this.rateMultiplier, bestMaskRate, 0);
                modeCost += Av1RateDistortion.GetCost(
                    this.rateMultiplier, modeCosts.GetInterIntraMode(blockSize, mode) + bestMaskRate, 0);

                if (modeCost < bestWedgeCost)
                {
                    bestWedgeCost = modeCost;
                    bestWedgeResidualCost = bestMaskCost - Av1RateDistortion.GetCost(this.rateMultiplier, bestMaskRate, 0);
                    wedgeMode = mode;
                    wedgeIndex = bestMaskIndex;
                }
            }

            selectedIntra = this.PrepareInterIntraPlane(macroBlock, blockOrigin, blockSize, Av1Plane.Y, wedgeMode);
            Av1WedgeMask.Fill(mask, width, blockSize, wedgeIndex, wedgeSign: false, 0, 0, invert: true);
            int wedgeSyntaxRate = modeCosts.GetInterIntraMode(blockSize, wedgeMode) +
                modeCosts.GetWedgeIndex(blockSize, wedgeIndex) + modeCosts.GetWedgeInterIntra(blockSize, 1);
            Av1MotionVector wedgeVector = vector;
            int wedgeMotionRate = motionRate;
            if (searchMotion)
            {
                // Keep the intra predictor and mask fixed while refining the moving reference.
                // Motion scratch uses an idle chroma prediction plane; the fixed predictor stays intact.
                Av1MotionSearchSettings motionSettings = this.picture.Parent.MotionSearchSettings;
                Av1MotionVectorCosts costs = this.blockWorkspace.GetMotionVectorCosts(this.picture.Parent.FrameHeader.MotionVectorPrecision);
                int sourceOrigin = ((sourcePlane.Bounds.Y + blockOrigin.Y) * sourcePlane.Stride) + sourcePlane.Bounds.X + blockOrigin.X;
                int referenceOrigin = ((referencePlane.Bounds.Y + blockOrigin.Y) * referencePlane.Stride) + referencePlane.Bounds.X + blockOrigin.X;
                Size size = new(width, height);
                Size frameSize = new(
                    this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                    this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

                Rectangle bounds = Av1MotionVector.GetFrameSearchBounds(
                    new Rectangle(blockOrigin, size), frameSize, Math.Min(referencePlane.Bounds.X, referencePlane.Bounds.Y));
                Av1MotionSearchSettings.FullPixelSearchMethod method = motionSettings.GetFullPixelMethod(blockSize);
                Av1MotionSearchBase.FullPixelSearch<TSample, TOperator> fullSearch = new(
                    sourcePlane.Buffer.DangerousGetSingleSpan()[sourceOrigin..],
                    sourcePlane.Stride,
                    referencePlane.Buffer.DangerousGetSingleSpan(),
                    referencePlane.Stride,
                    referenceOrigin,
                    size,
                    referenceVector.GetFullPixelSearchBounds(bounds),
                    referenceVector,
                    costs,
                    this.bitDepth,
                    Av1RateDistortion.GetMotionSearchSadPerBit(this.quantization.QIndex[0], this.bitDepth),
                    this.rateMultiplier,
                    selectedIntra,
                    mask);

                Av1MotionSearchBase.FullPixelResult integer = fullSearch.Search(
                    new Point(
                        (vector.Column + 3 + (vector.Column >= 0 ? 1 : 0)) >> 3,
                        (vector.Row + 3 + (vector.Row >= 0 ? 1 : 0)) >> 3),
                    5,
                    method,
                    this.blockWorkspace.GetMotionSearchSites(method, referencePlane.Stride),
                    motionSettings,
                    false,
                    false,
                    false,
                    [],
                    out _);

                Av1MotionVector trialVector = new(integer.Vector.Y * 8, integer.Vector.X * 8);
                if (!this.picture.Parent.FrameHeader.ForceIntegerMotionVector)
                {
                    Av1MotionSearchBase.FractionalSearch<TSample, TOperator> fractionalSearch = new(
                        sourcePlane.Buffer.DangerousGetSingleSpan()[sourceOrigin..],
                        sourcePlane.Stride,
                        referencePlane.Buffer.DangerousGetSingleSpan(),
                        referencePlane.Stride,
                        referenceOrigin,
                        workspace.BluePrediction,
                        size,
                        referenceVector.GetSubpixelSearchBounds(bounds),
                        referenceVector,
                        costs,
                        this.bitDepth,
                        this.rateMultiplier,
                        selectedIntra,
                        mask);

                    fractionalSearch.Search(
                        trialVector,
                        integer,
                        motionSettings.FractionalMethod,
                        Av1MotionSearchSettings.SearchPrecision.EighthSample,
                        this.picture.Parent.FrameHeader.AllowHighPrecisionMotionVector,
                        motionSettings.FractionalIterationsPerStep,
                        motionSettings.FractionalInterpolationTaps,
                        [],
                        [],
                        out Av1MotionSearchBase.FractionalResult fractional);

                    trialVector = fractional.Vector;
                }

                if (trialVector != vector)
                {
                    int trialColumnQ4 = (blockOrigin.X << 4) + (trialVector.Column << 1);
                    int trialRowQ4 = (blockOrigin.Y << 4) + (trialVector.Row << 1);
                    TOperator.PrepareTranslationalInterPrediction(
                        sourcePlane,
                        blockOrigin,
                        referencePlane,
                        new Point(trialColumnQ4 >> 4, trialRowQ4 >> 4),
                        horizontalFilter,
                        verticalFilter,
                        trialColumnQ4 & 15,
                        trialRowQ4 & 15,
                        blendedPrediction,
                        intraResidual,
                        workspace.PredictionScratch,
                        blockSize,
                        this.bitDepth);

                    TOperator.BlendInterIntraPrediction(blendedPrediction, selectedIntra, mask, width, height);
                    TOperator.GetMoments(
                        Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin),
                        sourcePlane.Stride,
                        blendedPrediction,
                        width,
                        visible.Width,
                        visible.Height,
                        out _,
                        out long error);

                    if (shift != 0)
                    {
                        error = (error + (1L << (shift - 1))) >> shift;
                    }

                    Av1RateDistortion.ModelPredictionError(
                        blockSize,
                        error,
                        visible.Width * visible.Height,
                        acQuantizer,
                        this.bitDepth,
                        this.rateMultiplier,
                        out int rate,
                        out long distortion);

                    int trialMotionRate = ((costs.GetCost(trialVector, referenceVector) * 108) + 64) >> 7;
                    long trialCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate + wedgeSyntaxRate + trialMotionRate, distortion);
                    long originalCost = bestWedgeResidualCost + Av1RateDistortion.GetCost(this.rateMultiplier, wedgeSyntaxRate + motionRate, 0);
                    if (trialCost < originalCost)
                    {
                        wedgeVector = trialVector;
                        wedgeMotionRate = trialMotionRate;
                    }
                }
            }

            // Only an accepted motion refinement can replace the original inter component.
            // Rejected trials restore it before the fixed-transform comparison against the smooth blend.
            if (wedgeVector == vector)
            {
                interPrediction.CopyTo(blendedPrediction);
                TOperator.BlendInterIntraPrediction(blendedPrediction, selectedIntra, mask, width, height);
            }

            int selectedWedgeRate = wedgeMotionRate + wedgeSyntaxRate;
            long wedgeBound = smoothCost - Av1RateDistortion.GetCost(this.rateMultiplier, selectedWedgeRate, 0);
            long wedgeCost = this.EstimateInterPredictionResidual(
                writer, macroBlock, blockOrigin, blockSize, tileIndex, selectedWedgeRate, wedgeBound, out _);

            if (wedgeCost < smoothCost)
            {
                vector = wedgeVector;
                selectedMode = wedgeMode;
                selectedWedge = true;
                selectedWedgeIndex = wedgeIndex;
            }

            return true;
        }

        /// <summary>
        /// Evaluates a selected blend with the fixed luma transform estimate and block skip syntax.
        /// </summary>
        private long EstimateInterPredictionResidual(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            int modeRate,
            long residualBound,
            out Av1RateDistortionStatistics residualStatistics)
        {
            residualStatistics = Av1RateDistortionStatistics.Invalid;
            if (residualBound < 0)
            {
                return long.MaxValue;
            }

            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            TOperator.SubtractPrediction(
                this.source.GetPlane(Av1Plane.Y), blockOrigin, workspace.LumaPrediction, workspace.Residual, width, height);

            Av1TransformSize transformSize = blockSize.GetMaximumTransformSize();
            Av1NeighborArrayUnit<byte> coefficientContexts = this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex];
            ReadOnlySpan<byte> above = coefficientContexts.Top.Slice(coefficientContexts.GetTopIndex(blockOrigin), blockSize.Get4x4WideCount());
            ReadOnlySpan<byte> left = coefficientContexts.Left.Slice(coefficientContexts.GetLeftIndex(blockOrigin), blockSize.Get4x4HighCount());
            int transformRate = 0;
            if (this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select)
            {
                Av1NeighborArrayUnit<byte> contexts = this.picture.TransformFunctionContexts[tileIndex];
                int context = Av1SymbolContextHelper.GetTransformPartitionContext(
                    contexts.Top[contexts.GetTopIndex(blockOrigin)], contexts.Left[contexts.GetLeftIndex(blockOrigin)], blockSize, transformSize);

                transformRate = writer.GetTransformPartitionCost(false, context);
            }

            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            int noSkipRate = writer.GetSkipCost(false, skipContext);
            int skipRate = writer.GetSkipCost(true, skipContext);
            long cost = Av1TransformBlockEncoder.EstimateInterTransform(
                this.blockWorkspace,
                workspace.Residual,
                width,
                workspace.TransformCoefficients,
                writer,
                above,
                left,
                blockSize,
                GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0),
                transformSize,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[0],
                this.bitDepth,
                0,
                this.picture.Parent.FrameHeader.CodedLossless,
                this.rateMultiplier,
                transformRate,
                noSkipRate,
                skipRate,
                residualBound,
                out Av1RateDistortionStatistics statistics,
                out _,
                out bool skip);

            if (cost == long.MaxValue)
            {
                return long.MaxValue;
            }

            residualStatistics = new Av1RateDistortionStatistics(
                this.rateMultiplier, skip ? skipRate : statistics.Rate + noSkipRate, statistics.Distortion);

            return Av1RateDistortion.GetCost(this.rateMultiplier, modeRate + residualStatistics.Rate, residualStatistics.Distortion);
        }

        /// <summary>
        /// Builds the intra component of one inter-intra plane from its reconstructed boundary.
        /// </summary>
        private Span<TSample> PrepareInterIntraPlane(
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1Plane plane,
            Av1InterIntraMode interIntraMode)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            this.blockWorkspace.GetInterIntraStorage<TSample>(
                out Span<TSample> intraPrediction, out Span<TSample> aboveStorage, out Span<TSample> leftStorage);

            int planeIndex = (int)plane;
            int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            Av1PredictionMode intraMode = interIntraMode switch
            {
                Av1InterIntraMode.Vertical => Av1PredictionMode.Vertical,
                Av1InterIntraMode.Horizontal => Av1PredictionMode.Horizontal,
                Av1InterIntraMode.Smooth => Av1PredictionMode.Smooth,
                _ => Av1PredictionMode.DC,
            };

            int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
            int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
            Point planeOrigin = planeIndex == 0
                ? blockOrigin
                : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
            Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subX != 0, subY != 0);
            Av1TransformSize transformSize = planeIndex == 0
                ? blockSize.GetMaximumTransformSize()
                : blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int sampleCount = transformSize.GetSize2d();
            bool hasLeft = macroBlock.IsLeftAvailable;
            bool hasAbove = macroBlock.IsUpAvailable;
            if (subX != 0 && blockSize.Get4x4WideCount() < Av1BlockSize.Block8x8.Get4x4WideCount())
            {
                hasLeft = modeInfoColumn - 1 > macroBlock.Tile.ModeInfoColumnStart;
            }

            if (subY != 0 && blockSize.Get4x4HighCount() < Av1BlockSize.Block8x8.Get4x4HighCount())
            {
                hasAbove = modeInfoRow - 1 > macroBlock.Tile.ModeInfoRowStart;
            }

            bool rightAvailable = modeInfoColumn + (transformSize.Get4x4WideCount() << subX) < macroBlock.Tile.ModeInfoColumnEnd;

            // Samples below the transform exist only while coded rows remain below it.
            int rowsBelow = (macroBlock.ToBottomEdge >> (3 + subY)) + planeBlockSize.GetHeight() - height;
            bool bottomAvailable = rowsBelow > 0 &&
                modeInfoRow + (transformSize.Get4x4HighCount() << subY) < macroBlock.Tile.ModeInfoRowEnd;

            // Availability tables describe prediction blocks. Subsampled chroma of a luma block narrower or
            // shorter than eight samples belongs to the enclosing 8x8 region, so its geometry uses that region.
            Av1BlockSize availabilityBlockSize = Av1IntraReferenceAvailability.ScaleChromaBlockSize(blockSize, subX != 0, subY != 0);
            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                availabilityBlockSize,
                modeInfoRow,
                modeInfoColumn,
                hasAbove,
                rightAvailable,
                macroBlock.GetRelativeModeInfo(0).Block.PartitionType,
                transformSize,
                0,
                0,
                subX,
                subY);
            bool hasBottomLeft = Av1IntraReferenceAvailability.HasBottomLeft(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                availabilityBlockSize,
                modeInfoRow,
                modeInfoColumn,
                bottomAvailable,
                hasLeft,
                macroBlock.GetRelativeModeInfo(0).Block.PartitionType,
                transformSize,
                0,
                0,
                subX,
                subY);

            // An intra prediction reads the reconstruction of its neighbors, and which neighbors
            // exist depends on where the block sits in its superblock and on the scan order. The
            // four availability tests above answer that, and the preparation below then supplies
            // the edge the predictor reads, extending it where a neighbor is missing.
            PrepareReferenceSamples(
                this.reconstruction.GetPlane(plane),
                planeOrigin,
                planeBlockSize.GetWidth(),
                planeBlockSize.GetHeight(),
                hasLeft,
                hasAbove,
                hasTopRight,
                hasBottomLeft,
                this.bitDepth,
                aboveStorage,
                leftStorage);

            TOperator.PrepareIntra(
                this.blockWorkspace,
                this.source.GetPlane(plane),
                planeOrigin,
                intraPrediction,
                width,
                aboveStorage.Slice(1, width + height),
                leftStorage.Slice(1, width + height),
                hasLeft,
                hasAbove,
                intraMode,
                0,
                this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                this.UseSmoothIntraEdges(macroBlock, blockOrigin, blockSize, plane),
                workspace.Residual,
                transformSize,
                this.bitDepth);

            return intraPrediction[..sampleCount];
        }

        /// <summary>
        /// Builds every active plane of the selected inter-intra prediction.
        /// </summary>
        private void PrepareInterIntraPrediction(
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool hasChroma,
            Av1ReferenceFrameType referenceFrame,
            Av1MotionVector vector,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Av1InterIntraMode interIntraMode,
            bool useWedge,
            int wedgeIndex)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int planeCount = hasChroma ? 3 : 1;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Point planeOrigin = planeIndex == 0 ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
                Av1BlockSize planeSize = blockSize.GetSubsampled(subX != 0, subY != 0);
                int width = planeSize.GetWidth();
                int height = planeSize.GetHeight();
                int sampleCount = width * height;
                Av1TransformSize transformSize = planeSize.GetMaximumTransformSize();
                Span<TSample> interPrediction = plane switch
                {
                    Av1Plane.Y => workspace.LumaPrediction[..sampleCount],
                    Av1Plane.U => workspace.BluePrediction[..sampleCount],
                    _ => workspace.RedPrediction[..sampleCount],
                };
                int columnQ4 = (planeOrigin.X << 4) + (vector.Column << (1 - subX));
                int rowQ4 = (planeOrigin.Y << 4) + (vector.Row << (1 - subY));
                Av1EncoderFrame<TSample>.PlanarView reference = this.references.Span[(int)referenceFrame].CodedView;
                TOperator.PrepareTranslationalInterPrediction(
                    this.source.GetPlane(plane),
                    planeOrigin,
                    reference.GetPlane(plane),
                    new Point(columnQ4 >> 4, rowQ4 >> 4),
                    horizontalFilter,
                    verticalFilter,
                    columnQ4 & 15,
                    rowQ4 & 15,
                    interPrediction,
                    workspace.Residual,
                    workspace.PredictionScratch,
                    transformSize.ToBlockSize(),
                    this.bitDepth);

                Span<TSample> intraPrediction = this.PrepareInterIntraPlane(macroBlock, blockOrigin, blockSize, plane, interIntraMode);

                Span<byte> mask = this.blockWorkspace.GetCompoundPredictionMask();
                if (useWedge)
                {
                    Av1WedgeMask.Fill(mask, width, blockSize, wedgeIndex, wedgeSign: false, subX, subY, invert: true);
                }
                else
                {
                    Av1InterIntraMaskBuilder.FillInterIntraMask(mask, width, width, height, interIntraMode, invert: true);
                }

                TOperator.BlendInterIntraPrediction(interPrediction, intraPrediction[..sampleCount], mask, width, height);
            }
        }

        /// <summary>
        /// Rejects near-motion modes when adjacent reference pairs do not meet the quantizer-dependent threshold.
        /// </summary>
        private bool ShouldPruneNearMode(
            Av1MacroBlockD macroBlock,
            Av1ReferenceFrameType primary,
            Av1ReferenceFrameType secondary,
            long bestCost)
        {
            int level = this.picture.Parent.SpeedSettings.NearNeighborPruningLevel;
            if (level == 0 || bestCost == long.MaxValue || !macroBlock.IsLeftAvailable || !macroBlock.IsUpAvailable)
            {
                return false;
            }

            ReadOnlySpan<byte> thresholds = [1, 0, 0, 1, 1, 0, 2, 1, 0];
            int threshold = thresholds[((level - 1) * 3) + (this.quantization.QIndex[0] * 3 / 256)];
            Av1EncoderBlockModeInfo left = macroBlock.GetRelativeModeInfo(-1).Block;
            Av1EncoderBlockModeInfo above = macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block;
            int matches = left.ReferenceFrame == primary && left.SecondaryReferenceFrame == secondary ? 1 : 0;
            matches += above.ReferenceFrame == primary && above.SecondaryReferenceFrame == secondary ? 1 : 0;
            return matches < threshold;
        }

        /// <summary>
        /// Ranks compound references from complete translation costs followed by modeled filter costs.
        /// </summary>
        private InlineArray4<byte> GetCompoundReferenceMasks(uint searchedModes)
        {
            InlineArray4<byte> masks = default;
            int level = this.picture.Parent.SpeedSettings.CompoundSingleResultPruningLevel;
            if (level == 0)
            {
                masks[..].Fill(byte.MaxValue);
                return masks;
            }

            ReadOnlySpan<long> simple = this.blockWorkspace.SingleReferenceSimpleCosts;
            ReadOnlySpan<long> modeled = this.blockWorkspace.SingleReferenceFilterCosts;
            InlineArray4<long> simpleCosts = default;
            InlineArray4<long> modelCosts = default;
            InlineArray4<byte> simpleReferences = default;
            InlineArray4<byte> modelReferences = default;
            InlineArray4<byte> orderedReferences = default;
            InlineArray4<bool> simpleValid = default;
            InlineArray4<bool> modelValid = default;
            for (int direction = 0; direction < 2; direction++)
            {
                int firstReference = direction == 0 ? (int)Av1ReferenceFrameType.Last : (int)Av1ReferenceFrameType.Golden + 1;
                int lastReference = direction == 0 ? (int)Av1ReferenceFrameType.Golden : (int)Av1ReferenceFrameType.Alternate;
                long bestSimple = long.MaxValue;
                long bestModel = long.MaxValue;
                for (int reference = firstReference; reference <= lastReference; reference++)
                {
                    for (int mode = (int)Av1PredictionMode.GlobalMotionVector; mode <= (int)Av1PredictionMode.NewMotionVector; mode++)
                    {
                        for (int index = 0; index < 3; index++)
                        {
                            int offset = ((((mode - (int)Av1PredictionMode.InterModeStart) * 3) + index) *
                                Av1Constants.ReferenceFrameCount) + reference;

                            bestSimple = Math.Min(bestSimple, simple[offset]);
                            bestModel = Math.Min(bestModel, modeled[offset]);
                        }
                    }
                }

                for (int mode = 0; mode < 4; mode++)
                {
                    int count = 0;
                    for (int reference = firstReference; reference <= lastReference; reference++)
                    {
                        if ((searchedModes & (1U << ((mode * Av1Constants.ReferenceFrameCount) + reference))) == 0)
                        {
                            continue;
                        }

                        long simpleCost = long.MaxValue;
                        long modelCost = long.MaxValue;
                        for (int index = 0; index < 3; index++)
                        {
                            int offset = (((mode * 3) + index) * Av1Constants.ReferenceFrameCount) + reference;
                            simpleCost = Math.Min(simpleCost, simple[offset]);
                            modelCost = Math.Min(modelCost, modeled[offset]);
                        }

                        // Equal costs retain reference traversal order in each independently sorted list.
                        int position = count;
                        while (position > 0 && simpleCosts[position - 1] > simpleCost)
                        {
                            simpleCosts[position] = simpleCosts[position - 1];
                            simpleReferences[position] = simpleReferences[position - 1];
                            position--;
                        }

                        simpleCosts[position] = simpleCost;
                        simpleReferences[position] = (byte)reference;
                        position = count;
                        while (position > 0 && modelCosts[position - 1] > modelCost)
                        {
                            modelCosts[position] = modelCosts[position - 1];
                            modelReferences[position] = modelReferences[position - 1];
                            position--;
                        }

                        modelCosts[position] = modelCost;
                        modelReferences[position] = (byte)reference;
                        count++;
                    }

                    int factor = level >= 2 ? 6 : 5;
                    for (int index = 0; index < count; index++)
                    {
                        // Each mode keeps its own best reference even when another mode has a lower bound.
                        simpleValid[index] = index == 0 || simpleCosts[index] == long.MaxValue ||
                            (simpleCosts[index] >> 3) * factor <= bestSimple;
                        modelValid[index] = index == 0 || modelCosts[index] == long.MaxValue ||
                            (modelCosts[index] >> 3) * factor <= bestModel;
                    }

                    int orderedCount = 0;
                    for (int index = 0; index < count && simpleCosts[index] != long.MaxValue; index++)
                    {
                        if (simpleValid[index])
                        {
                            orderedReferences[orderedCount++] = simpleReferences[index];
                        }
                    }

                    for (int index = 0; index < count && orderedCount < count && modelCosts[index] != long.MaxValue; index++)
                    {
                        if (!modelValid[index])
                        {
                            continue;
                        }

                        byte reference = modelReferences[index];
                        bool valid = true;
                        for (int previous = 0; previous < orderedCount; previous++)
                        {
                            valid &= orderedReferences[previous] != reference;
                        }

                        for (int previous = 0; previous < count; previous++)
                        {
                            if (simpleReferences[previous] == reference)
                            {
                                valid &= simpleValid[previous];
                                break;
                            }
                        }

                        if (valid)
                        {
                            orderedReferences[orderedCount++] = reference;
                        }
                    }

                    int candidates = level >= 2 ? Math.Min(2, orderedCount) : orderedCount;
                    if (level >= 3 &&
                        ((count != 0 && simpleCosts[0] != long.MaxValue && modelCosts[0] != long.MaxValue &&
                            simpleReferences[0] == modelReferences[0]) ||
                        mode == (int)Av1PredictionMode.NearMotionVector - (int)Av1PredictionMode.InterModeStart ||
                        mode == (int)Av1PredictionMode.GlobalMotionVector - (int)Av1PredictionMode.InterModeStart))
                    {
                        candidates = Math.Min(1, candidates);
                    }

                    if (level >= 4)
                    {
                        candidates = Math.Min(1, candidates);
                    }

                    for (int index = 0; index < candidates; index++)
                    {
                        masks[mode] |= (byte)(1 << orderedReferences[index]);
                    }
                }
            }

            return masks;
        }

        /// <summary>
        /// Searches ordered compound modes using retained single-reference decisions.
        /// </summary>
        private void SelectCompoundBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1ReferenceFrameType primaryReference,
            Av1ReferenceFrameType secondaryReference,
            bool nearestOnly,
            Span<long> topAverageCosts,
            Span<int> compoundMaskHistory,
            ReadOnlySpan<Av1PredictionMode> bestSingleModes,
            uint searchedSingleModes,
            ReadOnlySpan<Av1ReferenceMotionVectors> singleReferenceVectors,
            ReadOnlySpan<InlineArray3<Av1MotionVector>> newMotionVectors,
            ReadOnlySpan<byte> newMotionVectorMasks,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1RateDistortionStatistics selectedStatistics,
            ref Av1MotionVector selectedVector,
            ref Av1MotionVector selectedSecondaryVector,
            ref InlineArray128<Av1EncoderTransformBlockState> selectedStates)
        {
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            if ((this.picture.Parent.AvailableReferenceMask & (1 << (int)primaryReference)) == 0 ||
                (this.picture.Parent.AvailableReferenceMask & (1 << (int)secondaryReference)) == 0 ||
                frameHeader.ReferenceMode == ObuReferenceMode.SingleReference ||
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) < 8)
            {
                return;
            }

            Point modeInfoPosition = new(
                blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

            ref Av1ReferenceMotionVectors referenceMotionVectors = ref this.blockWorkspace.ReferenceMotionVectors;
            referenceMotionVectors.Build(
                this.picture,
                macroBlock,
                modeInfoPosition,
                blockSize,
                modeInfo.Block.PartitionType,
                this.picture.Sequence.SequenceHeader,
                frameHeader,
                primaryReference,
                secondaryReference);

            Span<byte> referenceCounts = stackalloc byte[Av1Constants.ReferenceFrameCount];
            Av1TileWriter.CollectNeighborReferenceCounts(macroBlock, referenceCounts);
            int commonPredictionRate = writer.GetIsInterCost(
                isInter: true,
                Av1TileWriter.GetIntraInterContext(macroBlock)) +
                writer.GetCompoundReferenceCost(
                    primaryReference,
                    secondaryReference,
                    Av1SymbolContextHelper.GetReferenceModeContext(macroBlock),
                    Av1SymbolContextHelper.GetCompoundReferenceTypeContext(macroBlock),
                    referenceCounts);

            InlineArray16<Av1MotionVector> primaryVectors = default;
            InlineArray16<Av1MotionVector> secondaryVectors = default;
            InlineArray16<Av1PredictionMode> modes = default;
            InlineArray16<byte> referenceIndices = default;
            int candidateCount = 0;
            primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(0);
            secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(1);
            modes[candidateCount++] = Av1PredictionMode.NearestNearestMotionVector;

            int maximumNearIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 2));
            for (int referenceIndex = 0; referenceIndex <= maximumNearIndex; referenceIndex++)
            {
                primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 0);
                secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 1);
                modes[candidateCount] = Av1PredictionMode.NearNearMotionVector;
                referenceIndices[candidateCount++] = (byte)referenceIndex;
            }

            ReadOnlySpan<Av1MotionVector> primaryNewVectors = newMotionVectors[(int)primaryReference];
            ReadOnlySpan<Av1MotionVector> secondaryNewVectors = newMotionVectors[(int)secondaryReference];
            byte primaryNewVectorMask = newMotionVectorMasks[(int)primaryReference];
            byte secondaryNewVectorMask = newMotionVectorMasks[(int)secondaryReference];
            int sharedNewMask = primaryNewVectorMask & secondaryNewVectorMask;
            int maximumNewIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 1));
            for (int referenceIndex = 0; referenceIndex <= maximumNewIndex; referenceIndex++)
            {
                if ((sharedNewMask & (1 << referenceIndex)) != 0)
                {
                    primaryVectors[candidateCount] = primaryNewVectors[referenceIndex];
                    secondaryVectors[candidateCount] = secondaryNewVectors[referenceIndex];
                    modes[candidateCount] = Av1PredictionMode.NewNewMotionVector;
                    referenceIndices[candidateCount++] = (byte)referenceIndex;
                }
            }

            if ((primaryNewVectorMask & 1) != 0)
            {
                primaryVectors[candidateCount] = primaryNewVectors[0];
                secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(1);
                modes[candidateCount++] = Av1PredictionMode.NewNearestMotionVector;
            }

            if ((secondaryNewVectorMask & 1) != 0)
            {
                primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(0);
                secondaryVectors[candidateCount] = secondaryNewVectors[0];
                modes[candidateCount++] = Av1PredictionMode.NearestNewMotionVector;
            }

            // Complete one syntax mode's DRL entries before moving to the next mode. The searched
            // vector cache uses the signaled index; only a NEAR predictor advances in the reference stack.
            for (int referenceIndex = 0; referenceIndex <= maximumNearIndex; referenceIndex++)
            {
                if ((primaryNewVectorMask & (1 << referenceIndex)) != 0)
                {
                    primaryVectors[candidateCount] = primaryNewVectors[referenceIndex];
                    secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 1);
                    modes[candidateCount] = Av1PredictionMode.NewNearMotionVector;
                    referenceIndices[candidateCount++] = (byte)referenceIndex;
                }
            }

            for (int referenceIndex = 0; referenceIndex <= maximumNearIndex; referenceIndex++)
            {
                if ((secondaryNewVectorMask & (1 << referenceIndex)) != 0)
                {
                    primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 0);
                    secondaryVectors[candidateCount] = secondaryNewVectors[referenceIndex];
                    modes[candidateCount] = Av1PredictionMode.NearNewMotionVector;
                    referenceIndices[candidateCount++] = (byte)referenceIndex;
                }
            }

            primaryVectors[candidateCount] = frameHeader
                .GetGlobalMotionParameters()[(int)primaryReference - 1]
                .GetMotionVector(frameHeader.AllowHighPrecisionMotionVector, blockSize, modeInfoPosition, frameHeader.ForceIntegerMotionVector);

            secondaryVectors[candidateCount] = frameHeader
                .GetGlobalMotionParameters()[(int)secondaryReference - 1]
                .GetMotionVector(frameHeader.AllowHighPrecisionMotionVector, blockSize, modeInfoPosition, frameHeader.ForceIntegerMotionVector);

            modes[candidateCount++] = Av1PredictionMode.GlobalGlobalMotionVector;

            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Av1InterpolationFilter filter = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable
                ? Av1InterpolationFilter.Regular : frameHeader.InterpolationFilter;
            bool maskedCompoundEnabled = this.picture.Sequence.SequenceHeader.EnableMaskedCompound &&
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8;
            int compoundGroupContext = Av1TileWriter.GetCompoundGroupIndexContext(macroBlock);
            bool jointCompoundEnabled = this.picture.Sequence.SequenceHeader.OrderHintInfo.EnableJointCompound;
            int compoundIndexContext = jointCompoundEnabled
                ? Av1SymbolContextHelper.GetCompoundIndexContext(
                    this.picture.Sequence.SequenceHeader.OrderHintInfo,
                    frameHeader,
                    primaryReference,
                    secondaryReference,
                    macroBlock)
                : 0;
            ReadOnlySpan<Av1PredictionMode> primaryModes =
            [
                Av1PredictionMode.NearestMotionVector, Av1PredictionMode.NearMotionVector,
                Av1PredictionMode.NearestMotionVector, Av1PredictionMode.NewMotionVector,
                Av1PredictionMode.NearMotionVector, Av1PredictionMode.NewMotionVector,
                Av1PredictionMode.GlobalMotionVector, Av1PredictionMode.NewMotionVector
            ];

            ReadOnlySpan<Av1PredictionMode> secondaryModes =
            [
                Av1PredictionMode.NearestMotionVector, Av1PredictionMode.NearMotionVector,
                Av1PredictionMode.NewMotionVector, Av1PredictionMode.NearestMotionVector,
                Av1PredictionMode.NewMotionVector, Av1PredictionMode.NearMotionVector,
                Av1PredictionMode.GlobalMotionVector, Av1PredictionMode.NewMotionVector
            ];

            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            InlineArray4<byte> referenceMasks = this.GetCompoundReferenceMasks(searchedSingleModes);
            bool primaryNeighborMatch = false;
            bool secondaryNeighborMatch = false;
            for (int neighborIndex = 0; neighborIndex < 2; neighborIndex++)
            {
                if (neighborIndex == 0 ? !macroBlock.IsLeftAvailable : !macroBlock.IsUpAvailable)
                {
                    continue;
                }

                Av1EncoderBlockModeInfo neighbor = macroBlock.GetRelativeModeInfo(
                    neighborIndex == 0 ? -1 : -macroBlock.ModeInfoStride).Block;

                if (neighbor.ReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    primaryNeighborMatch |= neighbor.ReferenceFrame == primaryReference ||
                        neighbor.SecondaryReferenceFrame == primaryReference;
                    secondaryNeighborMatch |= neighbor.ReferenceFrame == secondaryReference ||
                        neighbor.SecondaryReferenceFrame == secondaryReference;
                }
            }

            InlineArray2<Av1MotionVector> previousPrimary = default;
            InlineArray2<Av1MotionVector> previousSecondary = default;
            Av1PredictionMode previousMode = Av1PredictionMode.PredictionModeCount;
            byte previousVectorMask = 0;
            bool modeImproved = false;
            bool rejectMode = false;
            int candidateMask = 0;
            InlineArray3<long> translationCosts = default;
            InlineArray4<Av1RateDistortionStatistics> planeStatistics = default;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1MotionVector candidatePrimary = primaryVectors[candidateIndex];
                Av1MotionVector candidateSecondary = secondaryVectors[candidateIndex];
                Av1PredictionMode mode = modes[candidateIndex];
                if ((mode == Av1PredictionMode.NearestNearestMotionVector) != nearestOnly)
                {
                    continue;
                }

                bool startsMode = mode != previousMode;
                if (startsMode)
                {
                    previousMode = mode;
                    previousVectorMask = 0;
                    modeImproved = false;
                    rejectMode = Av1ModeThresholds.ShouldSkip(
                        this.blockWorkspace.ModeThresholdFactors,
                        this.blockWorkspace.ModeThresholdQuantizerFactor,
                        this.blockWorkspace.ModeThresholdSkipMultiplier,
                        blockSize,
                        mode,
                        primaryReference,
                        secondaryReference,
                        Math.Min(this.blockCostLimit, selectedStatistics.Cost),
                        modeInfo.Block.Skip);
                    rejectMode |= mode == Av1PredictionMode.NearNearMotionVector &&
                        this.ShouldPruneNearMode(
                            macroBlock, primaryReference, secondaryReference, Math.Min(this.blockCostLimit, selectedStatistics.Cost));

                    int modeIndex = (int)mode - (int)Av1PredictionMode.CompoundInterModeStart;
                    for (int component = 0; component < 2; component++)
                    {
                        Av1PredictionMode singleMode = component == 0 ? primaryModes[modeIndex] : secondaryModes[modeIndex];
                        int singleOffset = (int)singleMode - (int)Av1PredictionMode.InterModeStart;
                        Av1ReferenceFrameType reference = component == 0 ? primaryReference : secondaryReference;
                        uint searchedBit = 1U << ((singleOffset * Av1Constants.ReferenceFrameCount) + (int)reference);
                        if ((searchedSingleModes & searchedBit) == 0 || (referenceMasks[singleOffset] & (1 << (int)reference)) != 0)
                        {
                            continue;
                        }

                        // Neighbor-derived stacks differ between single and compound syntax. A single
                        // result can reject that component only when every predictor being compared agrees.
                        bool samePredictors = true;
                        if (singleMode == Av1PredictionMode.NearestMotionVector)
                        {
                            samePredictors = singleReferenceVectors[(int)reference].Nearest ==
                                referenceMotionVectors.GetCompoundNearestReference(component);
                        }
                        else if (singleMode == Av1PredictionMode.NearMotionVector)
                        {
                            for (int index = 0; index <= maximumNearIndex; index++)
                            {
                                samePredictors &= singleReferenceVectors[(int)reference].GetNearReference(index) ==
                                    referenceMotionVectors.GetCompoundNearReference(index, component);
                            }
                        }

                        rejectMode |= samePredictors;
                    }
                }

                if (startsMode && !rejectMode)
                {
                    bool nearMode = mode is Av1PredictionMode.NearNearMotionVector or
                        Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;
                    int referenceCount = nearMode ? maximumNearIndex + 1 :
                        mode == Av1PredictionMode.NewNewMotionVector ? maximumNewIndex + 1 : 1;

                    candidateMask = (1 << referenceCount) - 1;
                    if (referenceCount > 1)
                    {
                        bool modelTranslation = nearMode && settings.PruneNearMotionByTranslation &&
                            blockSize.GetWidth() * blockSize.GetHeight() > 64;

                        translationCosts[..].Fill(long.MaxValue);
                        long bestTranslation = long.MaxValue;
                        Av1EncoderBlockModeInfo prediction = modeInfo.Block;
                        prediction.Mode = mode;
                        prediction.ReferenceFrame = primaryReference;
                        prediction.SecondaryReferenceFrame = secondaryReference;
                        prediction.CompoundType = Av1CompoundType.Average;
                        prediction.CompoundGroupIndex = false;
                        prediction.CompoundIndex = true;
                        for (int index = candidateIndex; index < candidateCount && modes[index] == mode; index++)
                        {
                            int translationModeRate = this.GetCompoundInterModeRate(
                                writer, mode, primaryVectors[index], secondaryVectors[index], referenceIndices[index], in referenceMotionVectors) -
                                this.GetCompoundMotionRate(
                                    mode, primaryVectors[index], secondaryVectors[index], referenceIndices[index], in referenceMotionVectors);
                            int drlRate = translationModeRate - writer.GetInterCompoundModeCost(mode, referenceMotionVectors.ModeContext);

                            if (Av1RateDistortion.GetCost(this.rateMultiplier, commonPredictionRate + drlRate, 0) > Math.Min(this.blockCostLimit, selectedStatistics.Cost))
                            {
                                candidateMask &= ~(1 << referenceIndices[index]);
                                continue;
                            }

                            if (modelTranslation &&
                                Av1RateDistortion.GetCost(this.rateMultiplier, commonPredictionRate + translationModeRate, 0) <= Math.Min(this.blockCostLimit, selectedStatistics.Cost))
                            {
                                Av1RateDistortionStatistics estimate = this.GetInterFilterModelCost(
                                    primaryVectors[index],
                                    secondaryVectors[index],
                                    prediction,
                                    blockOrigin,
                                    blockSize,
                                    false,
                                    filter,
                                    filter,
                                    commonPredictionRate + translationModeRate,
                                    long.MaxValue,
                                    100,
                                    0,
                                    workspace.LumaPrediction,
                                    workspace.BluePrediction,
                                    workspace.RedPrediction,
                                    planeStatistics);

                                translationCosts[referenceIndices[index]] = estimate.Cost;
                                bestTranslation = Math.Min(bestTranslation, estimate.Cost);
                            }
                        }

                        if (modelTranslation)
                        {
                            for (int index = 0; index < referenceCount; index++)
                            {
                                if (!((double)translationCosts[index] / bestTranslation < 1.05 &&
                                    (double)translationCosts[index] / Math.Min(this.blockCostLimit, selectedStatistics.Cost) < 5))
                                {
                                    candidateMask &= ~(1 << index);
                                }
                            }
                        }
                    }
                }

                if (rejectMode || (candidateMask & (1 << referenceIndices[candidateIndex])) == 0)
                {
                    continue;
                }

                bool mixedMode = mode is Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NewNearestMotionVector or
                    Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;

                if (mixedMode)
                {
                    if (settings.SkipMixedNearCompound &&
                        mode is Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector)
                    {
                        continue;
                    }

                    int matchingNeighbors = (primaryNeighborMatch ? 1 : 0) + (secondaryNeighborMatch ? 1 : 0);
                    if (matchingNeighbors < settings.CompoundNeighborPruningLevel)
                    {
                        continue;
                    }

                    Av1ReferenceFrameType searchedReference = mode is Av1PredictionMode.NewNearestMotionVector or Av1PredictionMode.NewNearMotionVector
                        ? primaryReference : secondaryReference;

                    if (settings.PruneMixedCompoundBySingleWinner &&
                        bestSingleModes[(int)searchedReference] != Av1PredictionMode.NewMotionVector)
                    {
                        continue;
                    }
                }

                int modeRate = commonPredictionRate + this.GetCompoundInterModeRate(
                    writer, mode, candidatePrimary, candidateSecondary, referenceIndices[candidateIndex], in referenceMotionVectors);

                if (mode != Av1PredictionMode.NearestNearestMotionVector &&
                    Av1RateDistortion.GetCost(this.rateMultiplier, modeRate, 0) > Math.Min(this.blockCostLimit, selectedStatistics.Cost))
                {
                    continue;
                }

                int referenceIndex = referenceIndices[candidateIndex];
                if (settings.CompoundReferenceIndexPruningLevel != 0)
                {
                    bool similar = false;
                    int threshold = 2 << (settings.CompoundReferenceIndexPruningLevel + 1);
                    for (int index = 0; index < referenceIndex && !modeImproved; index++)
                    {
                        if ((previousVectorMask & (1 << index)) != 0)
                        {
                            int distance = Math.Abs(previousPrimary[index].Row - candidatePrimary.Row) +
                                Math.Abs(previousPrimary[index].Column - candidatePrimary.Column) +
                                Math.Abs(previousSecondary[index].Row - candidateSecondary.Row) +
                                Math.Abs(previousSecondary[index].Column - candidateSecondary.Column);

                            similar |= distance <= threshold;
                        }
                    }

                    if (similar)
                    {
                        continue;
                    }

                    // Retain the vectors before blend refinement. A later DRL entry is compared with
                    // the same starting predictions, and an improving mode keeps all remaining entries.
                    if (referenceIndex < 2)
                    {
                        previousPrimary[referenceIndex] = candidatePrimary;
                        previousSecondary[referenceIndex] = candidateSecondary;
                        previousVectorMask |= (byte)(1 << referenceIndex);
                    }
                }

                if (!this.SelectCompoundBlend(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    Math.Min(this.blockCostLimit, selectedStatistics.Cost),
                    commonPredictionRate,
                    modes[candidateIndex],
                    primaryReference,
                    secondaryReference,
                    filter,
                    referenceIndices[candidateIndex],
                    in referenceMotionVectors,
                    topAverageCosts,
                    compoundMaskHistory,
                    ref candidatePrimary,
                    ref candidateSecondary,
                    out Av1CompoundType compoundType,
                    out int wedgeIndex,
                    out bool wedgeSign,
                    out Av1DifferenceWeightedMaskType maskType))
                {
                    continue;
                }

                // The blend search above chose how the two predictions combine. The two flags below
                // are how that choice reaches the bitstream: the group flag separates the masked
                // blends from the rest, and the index then separates a plain average from a
                // distance weighted one.
                Av1EncoderBlockModeInfo candidateMode = modeInfo.Block;
                candidateMode.ReferenceFrame = primaryReference;
                candidateMode.SecondaryReferenceFrame = secondaryReference;
                candidateMode.Mode = modes[candidateIndex];
                candidateMode.CompoundType = compoundType;
                candidateMode.CompoundGroupIndex = compoundType is Av1CompoundType.Wedge or Av1CompoundType.DifferenceWeighted;
                candidateMode.CompoundIndex = compoundType == Av1CompoundType.Average;
                candidateMode.CompoundWedgeIndex = (byte)wedgeIndex;
                candidateMode.CompoundWedgeSign = wedgeSign;
                candidateMode.DifferenceWeightedMaskType = maskType;
                candidateMode.HorizontalInterpolationFilter = filter;
                candidateMode.VerticalInterpolationFilter = filter;

                // A compound mode names one single-reference mode for each of its two references.
                // The costs of those single-reference searches are already known, so the cheaper of
                // the two gives this candidate a bound: a compound prediction that cannot approach
                // the better of its own halves is not worth a filter search.
                //
                // The two indices address one table laid out as mode, then list index, then
                // reference frame, so each step multiplies by the size of the level below it.
                int compoundModeIndex = (int)modes[candidateIndex] - (int)Av1PredictionMode.CompoundInterModeStart;
                int primaryCostIndex = (((((int)primaryModes[compoundModeIndex] - (int)Av1PredictionMode.InterModeStart) * 3) +
                    referenceIndices[candidateIndex]) * Av1Constants.ReferenceFrameCount) + (int)primaryReference;
                int secondaryCostIndex = (((((int)secondaryModes[compoundModeIndex] - (int)Av1PredictionMode.InterModeStart) * 3) +
                    referenceIndices[candidateIndex]) * Av1Constants.ReferenceFrameCount) + (int)secondaryReference;

                long singleReferenceCost = Math.Min(
                    this.blockWorkspace.SingleReferenceFilterCosts[primaryCostIndex],
                    this.blockWorkspace.SingleReferenceFilterCosts[secondaryCostIndex]);

                bool preparedPrediction = this.SelectInterFilters(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    block.HasChroma,
                    candidatePrimary,
                    candidateSecondary,
                    ref candidateMode,
                    Math.Min(this.blockCostLimit, selectedStatistics.Cost),
                    singleReferenceCost,
                    out int filterRate,
                    out long filterModelCost);

                // The filter search prices the prediction with a model rather than a transform, so
                // the thresholds below are deliberately loose. The shift by three removes the
                // fixed-point scale the model carries before the two ratios are applied: against
                // the block winner when the speed setting allows it, and always against the better
                // half of this compound pair.
                if (filterModelCost == long.MaxValue ||
                    (Math.Min(this.blockCostLimit, selectedStatistics.Cost) != long.MaxValue &&
                        ((this.picture.Parent.SpeedSettings.ModelBasedInterpolationBreakout &&
                            (filterModelCost >> 3) * 3 > Math.Min(this.blockCostLimit, selectedStatistics.Cost)) ||
                        (filterModelCost >> 3) * 6 > singleReferenceCost)))
                {
                    continue;
                }

                int blendRate = writer.GetCompoundBlendCost(
                    blockSize,
                    compoundType,
                    compoundGroupContext,
                    compoundIndexContext,
                    wedgeIndex,
                    maskedCompoundEnabled,
                    jointCompoundEnabled);

                Av1RateDistortionStatistics candidateStatistics = this.EvaluateInterCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    Math.Min(this.blockCostLimit, selectedStatistics.Cost),
                    block.HasChroma,
                    commonPredictionRate + filterRate,
                    primaryReference,
                    candidatePrimary,
                    candidateSecondary,
                    modes[candidateIndex],
                    secondaryReference,
                    usePreparedPrediction: preparedPrediction,
                    compoundType,
                    wedgeIndex,
                    wedgeSign,
                    maskType,
                    blendRate,
                    candidateMode.HorizontalInterpolationFilter,
                    candidateMode.VerticalInterpolationFilter,
                    referenceIndices[candidateIndex],
                    false,
                    default,
                    false,
                    0,
                    in referenceMotionVectors,
                    workspace.LumaCandidateReconstruction,
                    workspace.LumaCandidateCoefficients,
                    workspace.BlueCandidateReconstruction,
                    workspace.BlueCandidateCoefficients,
                    workspace.RedCandidateReconstruction,
                    workspace.RedCandidateCoefficients,
                    out bool candidateSkip,
                    out InlineArray64<Av1EncoderTransformBlockState> candidateLumaStates,
                    out InlineArray16<Av1TransformSize> candidateLumaSizes,
                    out InlineArray16<Av1EncoderTransformBlockState> candidateBlueState,
                    out InlineArray16<Av1EncoderTransformBlockState> candidateRedState);

                if (candidateStatistics.Cost >= Math.Min(this.blockCostLimit, selectedStatistics.Cost))
                {
                    continue;
                }

                modeImproved = true;
                selectedStatistics = candidateStatistics;
                selectedVector = candidatePrimary;
                selectedSecondaryVector = candidateSecondary;
                candidateLumaStates[..].CopyTo(selectedStates);
                candidateLumaSizes[..].CopyTo(modeInfo.Block.InterTransformSizes);
                candidateBlueState[..].CopyTo(selectedStates[64..80]);
                candidateRedState[..].CopyTo(selectedStates[80..96]);
                modeInfo.Block.ReferenceFrame = primaryReference;
                modeInfo.Block.SecondaryReferenceFrame = secondaryReference;
                modeInfo.Block.Mode = modes[candidateIndex];
                modeInfo.Block.TransformSize = frameHeader.CodedLossless ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize();
                modeInfo.Block.Skip = candidateSkip;
                modeInfo.Block.CompoundGroupIndex = compoundType is Av1CompoundType.Wedge or Av1CompoundType.DifferenceWeighted;
                modeInfo.Block.CompoundIndex = compoundType == Av1CompoundType.Average;
                modeInfo.Block.CompoundType = compoundType;
                modeInfo.Block.CompoundWedgeIndex = (byte)wedgeIndex;
                modeInfo.Block.CompoundWedgeSign = wedgeSign;
                modeInfo.Block.DifferenceWeightedMaskType = maskType;
                modeInfo.Block.HorizontalInterpolationFilter = candidateMode.HorizontalInterpolationFilter;
                modeInfo.Block.VerticalInterpolationFilter = candidateMode.VerticalInterpolationFilter;
                block.ReferenceMotionVectorIndex = referenceIndices[candidateIndex];
            }
        }

        /// <summary>
        /// Selects compound syntax using luma estimates before full residual coding.
        /// </summary>
        private bool SelectCompoundBlend(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            long bestCost,
            int commonPredictionRate,
            Av1PredictionMode mode,
            Av1ReferenceFrameType primaryReference,
            Av1ReferenceFrameType secondaryReference,
            Av1InterpolationFilter filter,
            int referenceIndex,
            in Av1ReferenceMotionVectors referenceMotionVectors,
            Span<long> topAverageCosts,
            Span<int> maskHistory,
            ref Av1MotionVector primary,
            ref Av1MotionVector secondary,
            out Av1CompoundType selectedType,
            out int selectedWedgeIndex,
            out bool selectedWedgeSign,
            out Av1DifferenceWeightedMaskType selectedMaskType)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int count = width * height;
            Span<TSample> firstPrediction = workspace.LumaCandidateReconstruction[..count];
            Span<TSample> secondPrediction = workspace.BlueCandidateReconstruction[..count];
            Span<byte> mask = this.blockWorkspace.GetCompoundPredictionMask()[..count];
            bool masked = this.picture.Sequence.SequenceHeader.EnableMaskedCompound && Math.Min(width, height) >= 8;
            bool jointCompound = this.picture.Sequence.SequenceHeader.OrderHintInfo.EnableJointCompound;
            bool supportsWedge = blockSize is Av1BlockSize.Block8x8 or Av1BlockSize.Block8x16 or Av1BlockSize.Block16x8 or
                Av1BlockSize.Block16x16 or Av1BlockSize.Block16x32 or Av1BlockSize.Block32x16 or Av1BlockSize.Block32x32 or
                Av1BlockSize.Block8x32 or Av1BlockSize.Block32x8;

            int groupContext = Av1TileWriter.GetCompoundGroupIndexContext(macroBlock);
            int indexContext = jointCompound ? Av1SymbolContextHelper.GetCompoundIndexContext(
                this.picture.Sequence.SequenceHeader.OrderHintInfo,
                frameHeader,
                primaryReference,
                secondaryReference,
                macroBlock) : 0;

            bool hasNew = mode is Av1PredictionMode.NewNewMotionVector or Av1PredictionMode.NewNearestMotionVector or
                Av1PredictionMode.NewNearMotionVector or Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NearNewMotionVector;

            Av1MotionVector initialPrimary = primary;
            Av1MotionVector initialSecondary = secondary;
            int initialMotionRate = this.GetCompoundMotionRate(mode, primary, secondary, referenceIndex, in referenceMotionVectors);
            int totalModeRate = commonPredictionRate + this.GetCompoundInterModeRate(
                writer, mode, primary, secondary, referenceIndex, in referenceMotionVectors);

            bool primaryGlobal = mode == Av1PredictionMode.GlobalGlobalMotionVector &&
                frameHeader.GetGlobalMotionParameters()[(int)primaryReference - 1].Type > Av1GlobalMotionType.Translation;
            bool secondaryGlobal = mode == Av1PredictionMode.GlobalGlobalMotionVector &&
                frameHeader.GetGlobalMotionParameters()[(int)secondaryReference - 1].Type > Av1GlobalMotionType.Translation;
            Span<Av1CompoundSearchRecord> records = this.blockWorkspace.CompoundSearchRecords;
            Av1CompoundSearchRecord record = new()
            {
                Primary = primary,
                Secondary = secondary,
                PrimaryReference = primaryReference,
                SecondaryReference = secondaryReference,
                Mode = mode,
                Filter = filter,
                ReferenceIndex = referenceIndex,
                PrimaryGlobal = primaryGlobal,
                SecondaryGlobal = secondaryGlobal
            };

            record.Rates[..].Fill(int.MaxValue);
            record.Distortions[..].Fill(long.MaxValue);
            record.ModelRates[..].Fill(int.MaxValue);
            record.ModelDistortions[..].Fill(long.MaxValue);
            record.BlendRates[..].Fill(int.MaxValue);
            int matchIndex = -1;
            for (int index = 0; index < this.compoundSearchRecordCount; index++)
            {
                ref Av1CompoundSearchRecord previous = ref records[index];
                if (previous.Primary != primary || previous.Secondary != secondary || previous.Filter != filter ||
                    previous.PrimaryReference != primaryReference || previous.SecondaryReference != secondaryReference ||
                    previous.PrimaryGlobal != primaryGlobal || previous.SecondaryGlobal != secondaryGlobal)
                {
                    continue;
                }

                matchIndex = index;
                bool previousHasNew = previous.Mode is Av1PredictionMode.NewNewMotionVector or Av1PredictionMode.NewNearestMotionVector or
                    Av1PredictionMode.NewNearMotionVector or Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NearNewMotionVector;

                for (int typeIndex = 0; typeIndex < 4; typeIndex++)
                {
                    Av1CompoundType type = (Av1CompoundType)typeIndex;
                    bool reuse = type is Av1CompoundType.Average or Av1CompoundType.DistanceWeighted ||
                        (!hasNew && !previousHasNew) ||
                        (type == Av1CompoundType.Wedge ? !settings.RefineWedgeMotion : settings.CompoundMotionSearchLevel != 0);
                    if (reuse)
                    {
                        record.Rates[typeIndex] = previous.Rates[typeIndex];
                        record.Distortions[typeIndex] = previous.Distortions[typeIndex];
                        record.ModelRates[typeIndex] = previous.ModelRates[typeIndex];
                        record.ModelDistortions[typeIndex] = previous.ModelDistortions[typeIndex];
                        record.BlendRates[typeIndex] = previous.BlendRates[typeIndex];
                    }
                }

                break;
            }

            if (matchIndex >= 0 && settings.ReuseCompoundTypeDecision)
            {
                ref Av1CompoundSearchRecord previous = ref records[matchIndex];
                selectedType = previous.SelectedType;
                selectedWedgeIndex = previous.WedgeIndex;
                selectedWedgeSign = previous.WedgeSign;
                selectedMaskType = previous.MaskType;
                int typeIndex = (int)selectedType;
                if (record.Rates[typeIndex] == int.MaxValue)
                {
                    return false;
                }

                long estimate = Av1RateDistortion.GetCost(
                    this.rateMultiplier,
                    record.BlendRates[typeIndex] + initialMotionRate + record.Rates[typeIndex],
                    record.Distortions[typeIndex]);

                return bestCost == long.MaxValue || (estimate >> 4) * 11 <= bestCost;
            }

            long threshold = bestCost < 11 * (long.MaxValue / 16) ? (bestCost / 11) * 16 : long.MaxValue;
            long bestEstimate = long.MaxValue;
            long bestModel = long.MaxValue;
            bool predictorsReady = false;
            selectedType = Av1CompoundType.Average;
            selectedWedgeIndex = 0;
            selectedWedgeSign = false;
            selectedMaskType = Av1DifferenceWeightedMaskType.Type38;
            ReadOnlySpan<Av1CompoundType> types =
                [Av1CompoundType.Average, Av1CompoundType.DistanceWeighted, Av1CompoundType.Wedge, Av1CompoundType.DifferenceWeighted];

            foreach (Av1CompoundType type in types)
            {
                bool isWedge = type == Av1CompoundType.Wedge;
                bool isMasked = isWedge || type == Av1CompoundType.DifferenceWeighted;
                if ((type == Av1CompoundType.DistanceWeighted && !jointCompound) ||
                    (isMasked && !masked) ||
                    (isWedge && (!supportsWedge || this.interSourceVariance <= settings.InterInterWedgeVarianceThreshold ||
                                maskHistory[3] == (int)Av1CompoundType.Average)))
                {
                    continue;
                }

                long currentBest = Math.Min(bestCost, bestEstimate);
                int baseBlendRate = writer.GetCompoundBlendCost(blockSize, type, groupContext, indexContext, 0, masked, jointCompound);
                if (isWedge)
                {
                    baseBlendRate -= writer.ModeCosts.GetWedgeIndex(blockSize, 0) + 512;
                }
                else if (type == Av1CompoundType.DifferenceWeighted)
                {
                    baseBlendRate -= 512;
                }

                if (Av1RateDistortion.GetCost(this.rateMultiplier, baseBlendRate + totalModeRate, 0) >= currentBest)
                {
                    continue;
                }

                if (isMasked && !predictorsReady)
                {
                    // Rounded predictors are retained only during blend selection. Full coding regenerates
                    // its selected high-precision compound predictor after these scratch spans are released.
                    this.PrepareInterPlanePrediction(
                        initialPrimary,
                        default,
                        Av1Plane.Y,
                        Av1PredictionMode.NearestMotionVector,
                        primaryReference,
                        Av1ReferenceFrameType.None,
                        false,
                        Av1CompoundType.Average,
                        0,
                        false,
                        default,
                        filter,
                        filter,
                        this.references.Span[(int)primaryReference].CodedView.GetPlane(Av1Plane.Y),
                        default,
                        blockOrigin,
                        0,
                        0,
                        blockSize,
                        firstPrediction,
                        workspace.Residual);

                    this.PrepareInterPlanePrediction(
                        initialSecondary,
                        default,
                        Av1Plane.Y,
                        Av1PredictionMode.NearestMotionVector,
                        secondaryReference,
                        Av1ReferenceFrameType.None,
                        false,
                        Av1CompoundType.Average,
                        0,
                        false,
                        default,
                        filter,
                        filter,
                        this.references.Span[(int)secondaryReference].CodedView.GetPlane(Av1Plane.Y),
                        default,
                        blockOrigin,
                        0,
                        0,
                        blockSize,
                        secondPrediction,
                        workspace.Residual);

                    predictorsReady = true;
                }

                bool modelMask = isMasked && (isWedge ? settings.UseCompoundWedgeModel : settings.CompoundMotionSearchLevel != 0);
                int wedgeIndex = 0;
                bool wedgeSign = false;
                Av1DifferenceWeightedMaskType maskType = Av1DifferenceWeightedMaskType.Type38;
                long maskModel = long.MaxValue;
                if (modelMask)
                {
                    if (!isWedge && bestEstimate != long.MaxValue)
                    {
                        ReadOnlySpan<int> multipliers = [1, 11, 12];
                        ReadOnlySpan<int> divisors = [3, 16, 16];
                        int level = settings.CompoundTypePruningLevel;
                        if ((bestEstimate / divisors[level]) * multipliers[level] >= currentBest)
                        {
                            continue;
                        }
                    }

                    maskModel = this.SelectCompoundMask(
                        writer,
                        blockOrigin,
                        blockSize,
                        type,
                        firstPrediction,
                        secondPrediction,
                        out wedgeIndex,
                        out wedgeSign,
                        out maskType,
                        out long modelError);

                    int selectedBlendRate = writer.GetCompoundBlendCost(
                        blockSize, type, groupContext, indexContext, wedgeIndex, masked, jointCompound);

                    if (Av1RateDistortion.GetCost(this.rateMultiplier, selectedBlendRate + totalModeRate, 0) > Math.Min(bestEstimate, threshold) ||
                        !this.ShouldSearchCompoundTransforms(selectedBlendRate + initialMotionRate, modelError << 4))
                    {
                        continue;
                    }
                }
                else if (isWedge && settings.SkipWedgeForSimilarPredictors)
                {
                    TOperator.GetMoments(firstPrediction, width, secondPrediction, width, width, height, out _, out long error);
                    int shift = (this.bitDepth.GetBitCount() - 8) * 2;
                    if (shift != 0)
                    {
                        error = (error + (1L << (shift - 1))) >> shift;
                    }

                    if ((error + (count / 2)) / count < 512)
                    {
                        continue;
                    }
                }

                int maskCount = !isMasked || modelMask ? 1 : isWedge ? 32 : 2;
                bool reuseMask = isMasked && !modelMask && maskHistory[isWedge ? 0 : 2] >= 0;
                if (reuseMask)
                {
                    maskCount = 1;
                    wedgeIndex = isWedge ? maskHistory[0] : 0;
                    wedgeSign = isWedge && maskHistory[1] != 0;
                    maskType = isWedge ? default : (Av1DifferenceWeightedMaskType)maskHistory[2];
                }

                long typeEstimate = long.MaxValue;
                long typeModel = long.MaxValue;
                Av1MotionVector typePrimary = initialPrimary;
                Av1MotionVector typeSecondary = initialSecondary;
                int typeWedgeIndex = wedgeIndex;
                bool typeWedgeSign = wedgeSign;
                Av1DifferenceWeightedMaskType typeMaskType = maskType;
                for (int maskCandidate = 0; maskCandidate < maskCount; maskCandidate++)
                {
                    // Prune between mask pairs, including pairs whose two signs were both rejected.
                    // An oblique winner admits only its asymmetric pair; an axis winner finishes here.
                    if (isWedge && !modelMask && !reuseMask && settings.FastWedgeMaskSearch && maskCandidate == 16)
                    {
                        if (typeWedgeIndex > 3)
                        {
                            break;
                        }

                        ReadOnlySpan<int> asymmetricStarts = [8, 12, 14, 10];
                        maskCandidate = asymmetricStarts[typeWedgeIndex] * 2;
                        maskCount = maskCandidate + 4;
                    }

                    // A wedge is one of sixteen shapes with a sign that says which side each
                    // prediction fills, so the candidate index carries the sign in its low bit and
                    // the shape above it. A difference weighted mask has no sign and the index is
                    // the mask type itself.
                    if (isWedge && !modelMask && !reuseMask)
                    {
                        wedgeIndex = maskCandidate >> 1;
                        wedgeSign = (maskCandidate & 1) != 0;
                    }
                    else if (isMasked && !modelMask && !reuseMask)
                    {
                        maskType = (Av1DifferenceWeightedMaskType)maskCandidate;
                    }

                    int blendRate = writer.GetCompoundBlendCost(
                        blockSize, type, groupContext, indexContext, wedgeIndex, masked, jointCompound);

                    // The syntax of a wedge is priced before its prediction is formed. Half of the
                    // current winner is the allowance: a shape whose syntax alone already reaches
                    // it leaves no room for the distortion that will follow.
                    if (isWedge && !modelMask &&
                        Av1RateDistortion.GetCost(this.rateMultiplier, blendRate + totalModeRate, 0) >= currentBest / 2)
                    {
                        continue;
                    }

                    // A mask changes which samples each reference contributes, so the vectors that
                    // suited an average are no longer the best pair. Only a mode that codes its own
                    // vectors can be refined, and the speed settings decide whether the refinement
                    // is worth its cost for this blend.
                    Av1MotionVector trialPrimary = initialPrimary;
                    Av1MotionVector trialSecondary = initialSecondary;
                    bool refine = hasNew && (isWedge ? settings.RefineWedgeMotion : isMasked ? !modelMask :
                        settings.CompoundMotionSearchLevel < 2 || mode == Av1PredictionMode.NewNewMotionVector);

                    if (isWedge)
                    {
                        Av1WedgeMask.Fill(mask, width, blockSize, wedgeIndex, wedgeSign, 0, 0, false);
                    }

                    if (refine)
                    {
                        Span<byte> motionMask = isMasked ? mask : [];
                        if (type == Av1CompoundType.DifferenceWeighted)
                        {
                            mask.Fill(maskType == Av1DifferenceWeightedMaskType.Type38 ? (byte)38 : (byte)26);
                        }

                        if (type == Av1CompoundType.DistanceWeighted)
                        {
                            Av1CompoundDistanceWeights.Derive(
                                this.picture.Sequence.SequenceHeader.OrderHintInfo,
                                frameHeader,
                                primaryReference,
                                secondaryReference,
                                out int firstWeight,
                                out _);

                            mask.Fill((byte)(firstWeight * 4));
                            motionMask = mask;
                        }

                        this.RefineCompoundVectors(
                            blockOrigin,
                            blockSize,
                            primaryReference,
                            secondaryReference,
                            mode,
                            referenceIndex,
                            in referenceMotionVectors,
                            motionMask,
                            isWedge,
                            ref trialPrimary,
                            ref trialSecondary);
                    }

                    int motionRate = this.GetCompoundMotionRate(mode, trialPrimary, trialSecondary, referenceIndex, in referenceMotionVectors);
                    if (!isMasked || refine || (!isWedge && !modelMask))
                    {
                        this.PrepareInterPlanePrediction(
                            trialPrimary,
                            trialSecondary,
                            Av1Plane.Y,
                            mode,
                            primaryReference,
                            secondaryReference,
                            false,
                            type,
                            wedgeIndex,
                            wedgeSign,
                            maskType,
                            filter,
                            filter,
                            this.references.Span[(int)primaryReference].CodedView.GetPlane(Av1Plane.Y),
                            this.references.Span[(int)secondaryReference].CodedView.GetPlane(Av1Plane.Y),
                            blockOrigin,
                            0,
                            0,
                            blockSize,
                            workspace.LumaPrediction,
                            workspace.Residual);
                    }
                    else
                    {
                        firstPrediction.CopyTo(workspace.LumaPrediction);
                        TOperator.BlendInterIntraPrediction(workspace.LumaPrediction, secondPrediction, mask, width, height);
                    }

                    int typeIndex = (int)type;
                    bool useCachedEstimate = record.Rates[typeIndex] != int.MaxValue &&
                        (modelMask || (!isMasked && settings.CompoundMotionSearchLevel == 2 && mode != Av1PredictionMode.NewNewMotionVector));

                    long model = this.GetCompoundPredictionModelCost(
                        blockOrigin,
                        blockSize,
                        blendRate + motionRate,
                        out long predictionError,
                        out int modelRate,
                        out long modelDistortion);
                    if (modelMask && refine && model >= maskModel + Av1RateDistortion.GetCost(
                        this.rateMultiplier, blendRate + initialMotionRate, 0))
                    {
                        trialPrimary = initialPrimary;
                        trialSecondary = initialSecondary;
                        motionRate = initialMotionRate;
                        firstPrediction.CopyTo(workspace.LumaPrediction);
                        TOperator.BlendInterIntraPrediction(workspace.LumaPrediction, secondPrediction, mask, width, height);
                        model = maskModel + Av1RateDistortion.GetCost(this.rateMultiplier, blendRate + motionRate, 0);
                    }

                    if ((modelMask && !useCachedEstimate && settings.PruneCompoundTypeByModel && model > bestModel) ||
                        (!modelMask && !useCachedEstimate && !this.ShouldSearchCompoundTransforms(blendRate + motionRate, predictionError << 4)))
                    {
                        continue;
                    }

                    long residualBound = isWedge && !modelMask ? Math.Min(typeEstimate, currentBest) :
                        refine && !isMasked ? long.MaxValue :
                        Math.Min(bestEstimate, threshold) - Av1RateDistortion.GetCost(this.rateMultiplier, blendRate + motionRate, 0);

                    long estimate;
                    if (useCachedEstimate)
                    {
                        estimate = Av1RateDistortion.GetCost(
                            this.rateMultiplier, blendRate + motionRate + record.Rates[typeIndex], record.Distortions[typeIndex]);

                        model = Av1RateDistortion.GetCost(
                            this.rateMultiplier, blendRate + motionRate + record.ModelRates[typeIndex], record.ModelDistortions[typeIndex]);
                    }
                    else
                    {
                        estimate = this.EstimateInterPredictionResidual(
                            writer,
                            macroBlock,
                            blockOrigin,
                            blockSize,
                            tileIndex,
                            blendRate + motionRate,
                            residualBound,
                            out Av1RateDistortionStatistics residualStatistics);

                        if (estimate != long.MaxValue && (!isMasked || modelMask))
                        {
                            record.Rates[typeIndex] = residualStatistics.Rate;
                            record.Distortions[typeIndex] = residualStatistics.Distortion;
                            record.ModelRates[typeIndex] = modelRate;
                            record.ModelDistortions[typeIndex] = modelDistortion;
                            record.BlendRates[typeIndex] = blendRate;
                        }
                    }

                    if (estimate < typeEstimate)
                    {
                        typeEstimate = estimate;
                        typeModel = model;
                        typePrimary = trialPrimary;
                        typeSecondary = trialSecondary;
                        typeWedgeIndex = wedgeIndex;
                        typeWedgeSign = wedgeSign;
                        typeMaskType = maskType;
                    }
                }

                if (isWedge && !modelMask && !reuseMask &&
                    (settings.ReuseCompoundMaskResults || mode == Av1PredictionMode.NewNewMotionVector))
                {
                    maskHistory[0] = typeWedgeIndex;
                    maskHistory[1] = typeWedgeSign ? 1 : 0;
                }

                if (isMasked && !isWedge && !modelMask && !reuseMask && mode == Av1PredictionMode.NewNewMotionVector)
                {
                    maskHistory[2] = (int)typeMaskType;
                }

                if (typeEstimate < bestEstimate)
                {
                    bestEstimate = typeEstimate;
                    bestModel = typeModel;
                    primary = typePrimary;
                    secondary = typeSecondary;
                    selectedType = type;
                    selectedWedgeIndex = typeWedgeIndex;
                    selectedWedgeSign = typeWedgeSign;
                    selectedMaskType = typeMaskType;
                }

                int retainedCount = settings.CompoundAverageCandidateCount;
                if (type == Av1CompoundType.Average && retainedCount != 0)
                {
                    for (int index = 0; index < retainedCount; index++)
                    {
                        if (typeEstimate < topAverageCosts[index])
                        {
                            for (int move = retainedCount - 1; move > index; move--)
                            {
                                topAverageCosts[move] = topAverageCosts[move - 1];
                            }

                            topAverageCosts[index] = typeEstimate;
                            break;
                        }
                    }

                    if (currentBest != long.MaxValue && topAverageCosts[retainedCount - 1] != long.MaxValue &&
                        typeEstimate > topAverageCosts[retainedCount - 1])
                    {
                        return false;
                    }
                }
            }

            if (mode == Av1PredictionMode.NewNewMotionVector)
            {
                maskHistory[3] = (int)selectedType;
            }

            if (matchIndex < 0 && this.compoundSearchRecordCount < Av1CompoundSearchRecord.Capacity)
            {
                record.SelectedType = selectedType;
                record.WedgeIndex = (byte)selectedWedgeIndex;
                record.WedgeSign = selectedWedgeSign;
                record.MaskType = selectedMaskType;
                records[this.compoundSearchRecordCount++] = record;
            }

            return bestEstimate != long.MaxValue && (bestCost == long.MaxValue || (bestEstimate >> 4) * 11 <= bestCost);
        }

        /// <summary>
        /// Gates luma-only compound transform estimates against the best completed inter prediction.
        /// </summary>
        private bool ShouldSearchCompoundTransforms(int syntaxRate, long predictionError)
        {
            int level = this.picture.Parent.SpeedSettings.InterTransformGateLevel;
            if (level == 0 || this.bestInterLumaPredictionCost == long.MaxValue)
            {
                return true;
            }

            ReadOnlySpan<int> quantizerThresholds = [0, 0, 0, 80, 100, 140];
            ReadOnlySpan<int> scales = [int.MaxValue, 4, 3, 2, 2, 1];
            ReadOnlySpan<int> lumaMultipliers = [int.MaxValue, 32, 29, 17, 17, 17];
            int factor = 4;
            if (this.bestInterLumaPredictionCost > this.interSourceVarianceCost &&
                this.quantization.QIndex[0] >= quantizerThresholds[level])
            {
                factor *= scales[level];
            }

            long threshold = (this.bestInterLumaPredictionCost * factor * lumaMultipliers[level]) >> 6;
            return Av1RateDistortion.GetCost(this.rateMultiplier, syntaxRate, predictionError) <= threshold;
        }

        /// <summary>
        /// Selects a mask from rounded single predictors while retaining six-bit blend precision in its error estimate.
        /// </summary>
        private long SelectCompoundMask(
            Av1SymbolEncoder writer,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1CompoundType compoundType,
            ReadOnlySpan<TSample> firstPrediction,
            ReadOnlySpan<TSample> secondPrediction,
            out int wedgeIndex,
            out bool wedgeSign,
            out Av1DifferenceWeightedMaskType maskType,
            out long selectedError)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int count = width * height;
            Span<short> residualStorage = MemoryMarshal.Cast<int, short>(workspace.BlueCandidateCoefficients);
            Span<short> firstResidual = residualStorage[..count];
            Span<short> secondResidual = residualStorage.Slice(count, count);
            Span<short> difference = workspace.Residual[..count];
            Span<byte> mask = this.blockWorkspace.GetCompoundPredictionMask()[..count];
            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            TOperator.SubtractPrediction(sourcePlane, blockOrigin, firstPrediction, firstResidual, width, height);
            TOperator.SubtractPrediction(sourcePlane, blockOrigin, secondPrediction, secondResidual, width, height);
            long firstEnergy = 0;
            long secondEnergy = 0;
            for (int i = 0; i < count; i++)
            {
                int first = firstResidual[i];
                int second = secondResidual[i];
                firstEnergy += (long)first * first;
                secondEnergy += (long)second * second;
                difference[i] = (short)(first - second);
            }

            bool fixedSign = false;
            int shift = (this.bitDepth.GetBitCount() - 8) * 2;
            if (compoundType == Av1CompoundType.Wedge && this.picture.Parent.SpeedSettings.FastWedgeSignEstimation)
            {
                // Opposite quadrants choose one orientation for all masks. Normalize each quadrant's
                // squared error separately; rounding their signed difference would change boundary ties.
                long topFirst = 0;
                long topSecond = 0;
                long bottomFirst = 0;
                long bottomSecond = 0;
                for (int y = 0; y < height / 2; y++)
                {
                    for (int x = 0; x < width / 2; x++)
                    {
                        int top = (y * width) + x;
                        int bottom = ((y + (height / 2)) * width) + x + (width / 2);
                        topFirst += (long)firstResidual[top] * firstResidual[top];
                        topSecond += (long)secondResidual[top] * secondResidual[top];
                        bottomFirst += (long)firstResidual[bottom] * firstResidual[bottom];
                        bottomSecond += (long)secondResidual[bottom] * secondResidual[bottom];
                    }
                }

                if (shift != 0)
                {
                    long rounding = 1L << (shift - 1);
                    topFirst = (topFirst + rounding) >> shift;
                    topSecond = (topSecond + rounding) >> shift;
                    bottomFirst = (bottomFirst + rounding) >> shift;
                    bottomSecond = (bottomSecond + rounding) >> shift;
                }

                // A wedge divides the block, so the sign says which reference fills which side.
                // Comparing the two references over one diagonal half against the other tells which
                // way round they fit, which removes the sign from the search below.
                fixedSign = topFirst - topSecond + bottomSecond - bottomFirst > 0;
            }

            // Each mask candidate is scored from how much better one reference predicts each
            // sample than the other. Holding that difference per sample lets every candidate be
            // scored by summing over the samples its mask selects, instead of forming a blended
            // prediction for each one.
            long signLimit = (firstEnergy - secondEnergy) * 32;
            for (int i = 0; i < count; i++)
            {
                int delta = (firstResidual[i] * firstResidual[i]) - (secondResidual[i] * secondResidual[i]);
                firstResidual[i] = (short)Math.Clamp(delta, short.MinValue, short.MaxValue);
            }

            // The first residual buffer now stores saturated differences of squared errors.
            // It remains disjoint from the second residual and predictor difference used by the model.
            int acQuantizer = Av1QuantizationLookup.GetAcQuant(
                this.quantization.QIndex[0], this.quantization.DeltaQAc[0], this.bitDepth);

            long bestCost = long.MaxValue;
            selectedError = 0;
            wedgeIndex = 0;
            wedgeSign = false;
            maskType = Av1DifferenceWeightedMaskType.Type38;
            int candidates = compoundType == Av1CompoundType.Wedge ? 16 : 2;
            for (int index = 0; index < candidates; index++)
            {
                bool sign = fixedSign;
                if (compoundType == Av1CompoundType.Wedge)
                {
                    Av1WedgeMask.Fill(mask, width, blockSize, index, false, 0, 0, false);
                    if (!this.picture.Parent.SpeedSettings.FastWedgeSignEstimation)
                    {
                        long weightedDelta = 0;
                        for (int i = 0; i < count; i++)
                        {
                            weightedDelta += (long)firstResidual[i] * mask[i];
                        }

                        sign = weightedDelta > signLimit;
                    }

                    if (sign)
                    {
                        Av1WedgeMask.Fill(mask, width, blockSize, index, true, 0, 0, false);
                    }
                }
                else
                {
                    TOperator.BuildCompoundDifferenceMask(
                        mask,
                        firstPrediction,
                        secondPrediction,
                        new Size(width, height),
                        this.bitDepth,
                        (Av1DifferenceWeightedMaskType)index);
                }

                long error = 0;
                for (int i = 0; i < count; i++)
                {
                    int weightedResidual = (secondResidual[i] * 64) + (difference[i] * mask[i]);
                    weightedResidual = Math.Clamp(weightedResidual, short.MinValue, short.MaxValue);
                    error += (long)weightedResidual * weightedResidual;
                }

                error = (error + 2048) >> 12;
                if (shift != 0)
                {
                    error = (error + (1L << (shift - 1))) >> shift;
                }

                Av1RateDistortion.ModelPredictionError(
                    blockSize,
                    error,
                    count,
                    acQuantizer,
                    this.bitDepth,
                    this.rateMultiplier,
                    out int rate,
                    out long distortion);

                int maskRate = compoundType == Av1CompoundType.Wedge ? writer.ModeCosts.GetWedgeIndex(blockSize, index) : 0;
                long cost = Av1RateDistortion.GetCost(this.rateMultiplier, rate + maskRate, distortion);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    selectedError = error;
                    wedgeIndex = index;
                    wedgeSign = sign;
                    maskType = (Av1DifferenceWeightedMaskType)index;
                }
            }

            if (compoundType == Av1CompoundType.Wedge)
            {
                Av1WedgeMask.Fill(mask, width, blockSize, wedgeIndex, wedgeSign, 0, 0, false);
                bestCost -= Av1RateDistortion.GetCost(this.rateMultiplier, writer.ModeCosts.GetWedgeIndex(blockSize, wedgeIndex), 0);
                maskType = Av1DifferenceWeightedMaskType.Type38;
            }
            else
            {
                TOperator.BuildCompoundDifferenceMask(
                    mask, firstPrediction, secondPrediction, new Size(width, height), this.bitDepth, maskType);

                wedgeIndex = 0;
                wedgeSign = false;
            }

            return bestCost;
        }

        /// <summary>
        /// Measures a rounded luma predictor in the curve-fit model's normalized error domain.
        /// </summary>
        private long GetCompoundPredictionModelCost(
            Point blockOrigin, Av1BlockSize blockSize, int syntaxRate, out long error, out int rate, out long distortion)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Size visible = this.GetPredictionModelSize(blockOrigin, blockSize, 0, 0);
            TOperator.GetMoments(
                Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin),
                sourcePlane.Stride,
                workspace.LumaPrediction,
                blockSize.GetWidth(),
                visible.Width,
                visible.Height,
                out _,
                out error);

            int shift = (this.bitDepth.GetBitCount() - 8) * 2;
            if (shift != 0)
            {
                error = (error + (1L << (shift - 1))) >> shift;
            }

            int acQuantizer = Av1QuantizationLookup.GetAcQuant(
                this.quantization.QIndex[0], this.quantization.DeltaQAc[0], this.bitDepth);

            Av1RateDistortion.ModelPredictionError(
                blockSize,
                error,
                visible.Width * visible.Height,
                acQuantizer,
                this.bitDepth,
                this.rateMultiplier,
                out rate,
                out distortion);

            return Av1RateDistortion.GetCost(this.rateMultiplier, syntaxRate + rate, distortion);
        }

        /// <summary>
        /// Refines the searched components of a compound predictor while holding its blend mask fixed.
        /// </summary>
        private void RefineCompoundVectors(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType primaryReference,
            Av1ReferenceFrameType secondaryReference,
            Av1PredictionMode mode,
            int referenceIndex,
            in Av1ReferenceMotionVectors referenceMotionVectors,
            Span<byte> mask,
            bool wedge,
            ref Av1MotionVector primary,
            ref Av1MotionVector secondary)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            Av1MotionSearchSettings motionSettings = this.picture.Parent.MotionSearchSettings;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Av1MotionVectorCosts costs = this.blockWorkspace.GetMotionVectorCosts(frameHeader.MotionVectorPrecision);
            bool joint = mode == Av1PredictionMode.NewNewMotionVector;
            bool primaryNew = mode is Av1PredictionMode.NewNearestMotionVector or Av1PredictionMode.NewNearMotionVector;
            int iterations = joint ? settings.CompoundMotionSearchLevel == 2 ? 2 : 4 : 1;
            Av1MotionVector originalPrimary = primary;
            Av1MotionVector originalSecondary = secondary;
            int primaryBestCost = int.MaxValue;
            int secondaryBestCost = int.MaxValue;
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            Size size = new(width, height);
            Size frameSize = new(
                this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> source = Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin);
            int stackIndex = mode is Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector
                ? referenceIndex + 1 : referenceIndex;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int component = joint ? iteration & 1 : primaryNew ? 0 : 1;
                Av1MotionVector current = component == 0 ? primary : secondary;
                Av1MotionVector other = component == 0 ? secondary : primary;
                Av1MotionVector initial = component == 0 ? originalPrimary : originalSecondary;
                Av1MotionVector initialOther = component == 0 ? originalSecondary : originalPrimary;
                if (iteration >= 2 && other == initialOther &&
                    (current == initial || ((current.Row >> 3) == (initial.Row >> 3) && (current.Column >> 3) == (initial.Column >> 3))))
                {
                    break;
                }

                Buffer2DRegion<TSample> movingPlane = this.references.Span[(int)(component == 0 ? primaryReference : secondaryReference)].CodedView.GetPlane(Av1Plane.Y);
                Buffer2DRegion<TSample> fixedPlane = this.references.Span[(int)(component == 0 ? secondaryReference : primaryReference)].CodedView.GetPlane(Av1Plane.Y);
                Av1MotionVector referenceVector = referenceMotionVectors.GetCompoundNewReference(stackIndex, component);
                int columnQ4 = (blockOrigin.X << 4) + (other.Column << 1);
                int rowQ4 = (blockOrigin.Y << 4) + (other.Row << 1);
                TOperator.PrepareTranslationalInterPrediction(
                    sourcePlane,
                    blockOrigin,
                    fixedPlane,
                    new Point(columnQ4 >> 4, rowQ4 >> 4),
                    Av1InterpolationFilter.Regular,
                    Av1InterpolationFilter.Regular,
                    columnQ4 & 15,
                    rowQ4 & 15,
                    workspace.BluePrediction,
                    workspace.Residual,
                    workspace.PredictionScratch,
                    blockSize,
                    this.bitDepth);

                // The mask describes the first reference. Reverse it only while the second reference
                // moves, then restore it before the caller builds the final compound prediction.
                if (component != 0)
                {
                    for (int i = 0; i < mask.Length; i++)
                    {
                        mask[i] = (byte)(64 - mask[i]);
                    }
                }

                int referenceOrigin = ((movingPlane.Bounds.Y + blockOrigin.Y) * movingPlane.Stride) + movingPlane.Bounds.X + blockOrigin.X;
                Rectangle bounds = Av1MotionVector.GetFrameSearchBounds(
                    new Rectangle(blockOrigin, size), frameSize, Math.Min(movingPlane.Bounds.X, movingPlane.Bounds.Y));

                Av1MotionSearchBase.FullPixelSearch<TSample, TOperator> fullSearch = new(
                    source,
                    sourcePlane.Stride,
                    movingPlane.Buffer.DangerousGetSingleSpan(),
                    movingPlane.Stride,
                    referenceOrigin,
                    size,
                    referenceVector.GetFullPixelSearchBounds(bounds),
                    referenceVector,
                    costs,
                    this.bitDepth,
                    Av1RateDistortion.GetMotionSearchSadPerBit(this.quantization.QIndex[0], this.bitDepth),
                    this.rateMultiplier,
                    workspace.BluePrediction,
                    mask);

                Point start = new(
                    (current.Column + 3 + (current.Column >= 0 ? 1 : 0)) >> 3,
                    (current.Row + 3 + (current.Row >= 0 ? 1 : 0)) >> 3);

                Point? second = null;
                Av1MotionSearchBase.FullPixelResult integer;
                int searchCost;
                if (joint && (settings.UseLocalJointMotionSearch || wedge))
                {
                    Point selected = fullSearch.RefineCompound(start, out searchCost);
                    integer = fullSearch.GetVarianceResult(selected);
                }
                else
                {
                    Av1MotionSearchSettings.FullPixelSearchMethod method = motionSettings.GetFullPixelMethod(blockSize);
                    integer = fullSearch.Search(
                        start,
                        5,
                        method,
                        this.blockWorkspace.GetMotionSearchSites(method, movingPlane.Stride),
                        motionSettings,
                        false,
                        false,
                        false,
                        [],
                        out second);

                    searchCost = integer.Cost;
                }

                Av1MotionVector trial = new(integer.Vector.Y * 8, integer.Vector.X * 8);
                if (!frameHeader.ForceIntegerMotionVector)
                {
                    Av1MotionSearchBase.FractionalSearch<TSample, TOperator> fractionalSearch = new(
                        source,
                        sourcePlane.Stride,
                        movingPlane.Buffer.DangerousGetSingleSpan(),
                        movingPlane.Stride,
                        referenceOrigin,
                        workspace.RedPrediction,
                        size,
                        referenceVector.GetSubpixelSearchBounds(bounds),
                        referenceVector,
                        costs,
                        this.bitDepth,
                        this.rateMultiplier,
                        workspace.BluePrediction,
                        mask);

                    searchCost = fractionalSearch.Search(
                        trial,
                        joint ? null : integer,
                        motionSettings.FractionalMethod,
                        Av1MotionSearchSettings.SearchPrecision.EighthSample,
                        frameHeader.AllowHighPrecisionMotionVector,
                        motionSettings.FractionalIterationsPerStep,
                        motionSettings.FractionalInterpolationTaps,
                        [],
                        [],
                        out Av1MotionSearchBase.FractionalResult fractional);

                    trial = fractional.Vector;
                    if (joint && second.HasValue && second.Value != integer.Vector &&
                        motionSettings.SecondCandidateSelection == Av1MotionSearchSettings.CandidateSelection.RateDistortion)
                    {
                        Av1MotionVector secondVector = new(second.Value.Y * 8, second.Value.X * 8);
                        Rectangle fractionalBounds = referenceVector.GetSubpixelSearchBounds(bounds);
                        if (fractionalBounds.Contains(secondVector.Column, secondVector.Row))
                        {
                            int secondCost = fractionalSearch.Search(
                                secondVector,
                                null,
                                motionSettings.FractionalMethod,
                                Av1MotionSearchSettings.SearchPrecision.EighthSample,
                                frameHeader.AllowHighPrecisionMotionVector,
                                motionSettings.FractionalIterationsPerStep,
                                motionSettings.FractionalInterpolationTaps,
                                [],
                                [],
                                out Av1MotionSearchBase.FractionalResult secondResult);

                            if (secondCost < searchCost)
                            {
                                searchCost = secondCost;
                                trial = secondResult.Vector;
                            }
                        }
                    }
                }

                if (component != 0)
                {
                    for (int i = 0; i < mask.Length; i++)
                    {
                        mask[i] = (byte)(64 - mask[i]);
                    }
                }

                int previousCost = component == 0 ? primaryBestCost : secondaryBestCost;
                if (searchCost >= previousCost)
                {
                    break;
                }

                if (component == 0)
                {
                    primary = trial;
                    primaryBestCost = searchCost;
                }
                else
                {
                    secondary = trial;
                    secondaryBestCost = searchCost;
                }
            }
        }

        /// <summary>
        /// Reconstructs the selected inter mode after intra trials have reused its arithmetic storage.
        /// </summary>
        /// <param name="writer">The coefficient entropy costs.</param>
        /// <param name="macroBlock">The block geometry and neighboring transform contexts.</param>
        /// <param name="tileIndex">The tile containing the coefficient neighbors.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The selected prediction and interpolation syntax.</param>
        /// <param name="block">The selected block parameters.</param>
        /// <param name="vector">The selected motion vector in eighth-luma-sample units.</param>
        /// <param name="secondaryVector">The selected compound secondary vector, or zero for a single-reference block.</param>
        /// <param name="states">The selected transform choices, indexed by plane.</param>
        /// <summary>
        /// Reconstructs the selected inter transforms in coding order, carrying each plane's coefficient contexts forward.
        /// </summary>
        /// <returns>Whether any transform block kept a coefficient.</returns>
        private bool ReconstructSelectedInterBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            ushort tileIndex,
            Point blockOrigin,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            ReadOnlySpan<Av1EncoderTransformBlockState> states)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            bool isInterIntra = modeInfo.Block.SecondaryReferenceFrame == Av1ReferenceFrameType.Intra;
            if (isInterIntra)
            {
                this.PrepareInterIntraPrediction(
                    macroBlock,
                    blockOrigin,
                    modeInfo.Block.BlockSize,
                    block.HasChroma,
                    modeInfo.Block.ReferenceFrame,
                    vector,
                    modeInfo.Block.HorizontalInterpolationFilter,
                    modeInfo.Block.VerticalInterpolationFilter,
                    modeInfo.Block.InterIntraMode,
                    modeInfo.Block.UseInterIntraWedge,
                    modeInfo.Block.InterIntraWedgeIndex);
            }

            Av1EncoderFrame<TSample>.PlanarView primaryReference = modeInfo.Block.UseIntraBlockCopy
                ? this.reconstruction : this.references.Span[(int)modeInfo.Block.ReferenceFrame].CodedView;
            Av1EncoderFrame<TSample>.PlanarView secondaryReference = modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                ? this.references.Span[(int)modeInfo.Block.SecondaryReferenceFrame].CodedView : primaryReference;
            bool lossless = this.picture.Parent.FrameHeader.CodedLossless;
            int planeCount = block.HasChroma ? 3 : 1;
            bool coded = false;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Size frameContextSize = new(
                    this.picture.Parent.FrameHeader.ModeInfoColumnCount >> subX,
                    this.picture.Parent.FrameHeader.ModeInfoRowCount >> subY);

                Point planeOrigin = planeIndex == 0 ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
                Av1BlockSize planeBlockSize = modeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0);
                Av1TransformSize transformSize = lossless
                    ? Av1TransformSize.Size4x4
                    : planeIndex == 0
                        ? modeInfo.Block.TransformSize
                        : modeInfo.Block.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);
                Av1TransformSize rootSize = planeIndex == 0 && !lossless ? planeBlockSize.GetMaximumTransformSize() : transformSize;
                int width = planeBlockSize.GetWidth();
                int transformWidth = transformSize.GetWidth();
                int transformHeight = transformSize.GetHeight();
                int sampleCount = transformSize.GetSize2d();
                Size extent = GetCodedTransformExtent(macroBlock, planeBlockSize, Av1TransformSize.Size4x4, subX, subY);
                Span<TSample> prediction = plane switch
                {
                    Av1Plane.Y => workspace.LumaPrediction,
                    Av1Plane.U => workspace.BluePrediction,
                    _ => workspace.RedPrediction,
                };

                // Keep motion interpolation and compound masks in full-block coordinates. Transform
                // leaves consume strided views, while coefficients remain packed in coding order.
                this.PrepareInterPlanePrediction(
                    vector,
                    secondaryVector,
                    plane,
                    modeInfo.Block.Mode,
                    modeInfo.Block.ReferenceFrame,
                    modeInfo.Block.SecondaryReferenceFrame,
                    isInterIntra,
                    modeInfo.Block.CompoundType,
                    modeInfo.Block.CompoundWedgeIndex,
                    modeInfo.Block.CompoundWedgeSign,
                    modeInfo.Block.DifferenceWeightedMaskType,
                    modeInfo.Block.HorizontalInterpolationFilter,
                    modeInfo.Block.VerticalInterpolationFilter,
                    primaryReference.GetPlane(plane),
                    secondaryReference.GetPlane(plane),
                    new Point(planeOrigin.X << subX, planeOrigin.Y << subY),
                    subX,
                    subY,
                    modeInfo.Block.BlockSize,
                    prediction,
                    workspace.Residual);

                Av1NeighborArrayUnit<byte> neighbors = plane switch
                {
                    Av1Plane.Y => this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                    Av1Plane.U => this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                    _ => this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
                };
                int contextWidth = planeBlockSize.Get4x4WideCount();
                int contextHeight = planeBlockSize.Get4x4HighCount();
                Span<byte> topContexts = workspace.TransformContexts[..contextWidth];
                Span<byte> leftContexts = workspace.TransformContexts.Slice(contextWidth, contextHeight);
                neighbors.Top.Slice(neighbors.GetTopIndex(planeOrigin), contextWidth).CopyTo(topContexts);
                neighbors.Left.Slice(neighbors.GetLeftIndex(planeOrigin), contextHeight).CopyTo(leftContexts);
                int codedArea = planeIndex == 0 ? this.codedAreaLuma : this.codedAreaChroma;
                int stateOffset = planeIndex == 0 ? 0 : 64 + ((planeIndex - 1) * 16);
                Av1TransformSize traversalSize = planeIndex == 0 ? rootSize.GetSubSize().GetSubSize() : transformSize;
                int leafCount = planeBlockSize.GetWidth() * planeBlockSize.GetHeight() / traversalSize.GetSize2d();
                int coefficientOffset = codedArea;
                int transformIndex = 0;
                for (int leaf = 0; leaf < leafCount; leaf++)
                {
                    Point offset = rootSize.GetBlockPartitionOrigin(planeBlockSize, traversalSize, leaf, subX, subY);
                    if (offset.X >= extent.Width || offset.Y >= extent.Height)
                    {
                        continue;
                    }

                    if (planeIndex == 0)
                    {
                        transformSize = modeInfo.Block.InterTransformSizes[modeInfo.Block.GetInterTransformSizeIndex(offset.Y >> 2, offset.X >> 2)];
                        transformWidth = transformSize.GetWidth();
                        transformHeight = transformSize.GetHeight();
                        sampleCount = transformSize.GetSize2d();
                        if ((offset.X % transformWidth) != 0 || (offset.Y % transformHeight) != 0)
                        {
                            continue;
                        }
                    }

                    Span<byte> top = topContexts.Slice(offset.X >> 2, transformSize.Get4x4WideCount());
                    Span<byte> left = leftContexts.Slice(offset.Y >> 2, transformSize.Get4x4HighCount());
                    Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
                        planeIndex == 0 ? Av1ComponentType.Luminance : Av1ComponentType.Chroma,
                        top,
                        left,
                        planeBlockSize,
                        transformSize);

                    int inputOffset = (offset.Y * width) + offset.X;
                    this.ReconstructSelectedTransform(
                        writer,
                        context,
                        true,
                        planeOrigin + new Size(offset.X, offset.Y),
                        plane,
                        transformSize,
                        prediction[inputOffset..],
                        workspace.Residual[inputOffset..],
                        width,
                        lossless ? default : states[stateOffset + transformIndex],
                        modeInfo.Block.Skip,
                        coefficientOffset);

                    Av1EncoderTransformBlockState state = this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane)[
                        coefficientOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];
                    coded |= state.EndOfBlock != 0;
                    byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                        this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, plane)[coefficientOffset..],
                        transformSize,
                        state.TransformType,
                        state.EndOfBlock);

                    Av1TileWriter.UpdateCoefficientContexts(
                        top,
                        left,
                        coefficientContext,
                        planeOrigin + new Size(offset.X, offset.Y),
                        frameContextSize);

                    coefficientOffset += sampleCount;
                    transformIndex++;
                }
            }

            return coded;
        }

        /// <summary>
        /// Gets the source extent used by prediction-error models at the coded frame boundary.
        /// </summary>
        private Size GetPredictionModelSize(Point blockOrigin, Av1BlockSize blockSize, int subsamplingX, int subsamplingY)
        {
            Av1BlockSize planeSize = blockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
            int width = planeSize.GetWidth();
            int height = planeSize.GetHeight();
            int right = blockOrigin.X + blockSize.GetWidth() - this.source.Width;
            int bottom = blockOrigin.Y + blockSize.GetHeight() - this.source.Height;
            if (right > 0)
            {
                // Round the out-of-frame luma count before subtracting it from the plane size.
                // The coded frame boundary and the chroma block must use the same rounding direction.
                width = Math.Max(0, width - ((right + subsamplingX) >> subsamplingX));
            }

            if (bottom > 0)
            {
                height = Math.Max(0, height - ((bottom + subsamplingY) >> subsamplingY));
            }

            return new Size(width, height);
        }

        /// <summary>
        /// Selects interpolation filters with per-block reuse and frame-history pruning.
        /// </summary>
        /// <returns>Whether all selected prediction planes are prepared in the shared workspace.</returns>
        private bool SelectInterFilters(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool hasChroma,
            Av1MotionVector primary,
            Av1MotionVector secondary,
            ref Av1EncoderBlockModeInfo modeInfo,
            long bestCandidateCost,
            long singleReferenceCost,
            out int filterRate,
            out long modelCost)
        {
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            bool winnerSearch = settings.UseWinnerInterpolation && this.blockWorkspace.EvaluationStage == Av1EncoderEvaluationStage.Winner;
            bool compound = modeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra;
            bool dual = this.picture.Sequence.SequenceHeader.EnableDualFilter;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            bool writesFilters = Av1TileWriter.UsesSwitchableInterpolation(frameHeader, modeInfo);
            int verticalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo, macroBlock, 0);
            int horizontalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo, macroBlock, 1);
            Span<Av1InterpolationSearchRecord> records = this.blockWorkspace.InterpolationSearchRecords;
            int reuseLevel = winnerSearch ? 0 : settings.InterpolationReuseLevel;
            int match = -1;
            int bestDifference = int.MaxValue;
            if (writesFilters && reuseLevel != 0)
            {
                int threshold = reuseLevel == 1 ? 0 : compound ? 7 : 3;
                for (int index = 0; index < this.interpolationSearchRecordCount; index++)
                {
                    ref Av1InterpolationSearchRecord record = ref records[index];
                    if (record.PrimaryReference != modeInfo.ReferenceFrame ||
                        (compound && record.SecondaryReference != modeInfo.SecondaryReferenceFrame) ||
                        (compound && reuseLevel == 1 &&
                            (record.CompoundType != modeInfo.CompoundType || record.CompoundIndex != modeInfo.CompoundIndex)))
                    {
                        continue;
                    }

                    int difference = Math.Abs(record.Primary.Row - primary.Row) + Math.Abs(record.Primary.Column - primary.Column);
                    if (compound)
                    {
                        difference += Math.Abs(record.Secondary.Row - secondary.Row) + Math.Abs(record.Secondary.Column - secondary.Column);
                    }

                    if (difference == 0)
                    {
                        match = index;
                        break;
                    }

                    if (difference < bestDifference && difference <= threshold)
                    {
                        bestDifference = difference;
                        match = index;
                    }
                }
            }

            if (match >= 0)
            {
                modelCost = records[match].Cost;
                modeInfo.HorizontalInterpolationFilter = records[match].HorizontalFilter;
                modeInfo.VerticalInterpolationFilter = records[match].VerticalFilter;
                filterRate = writer.GetSwitchableInterpolationFilterCost(modeInfo.VerticalInterpolationFilter, verticalContext) +
                    (dual ? writer.GetSwitchableInterpolationFilterCost(modeInfo.HorizontalInterpolationFilter, horizontalContext) : 0);

                // Cached decisions retain syntax, not pixels. The caller rebuilds the current vectors'
                // prediction; approximate vector matches must never reuse another candidate's samples.
                return false;
            }

            const int filterCount = Av1InterpolationProbabilities.FilterCount;
            int allowedMask = winnerSearch
                ? (1 << (int)Av1InterpolationFilter.Regular) |
                    (1 << (int)(settings.WinnerInterpolationUsesSharp ? Av1InterpolationFilter.Sharp : Av1InterpolationFilter.Smooth))
                : settings.InterpolationPruningLevel != 0 ? this.blockWorkspace.InterpolationSearchMask : (1 << filterCount) - 1;
            if (!winnerSearch && !dual && settings.InterpolationPruningLevel == 2)
            {
                ReadOnlySpan<byte> thresholds = [0, 8, 8, 8, 8, 0, 8];
                int threshold = thresholds[(int)this.picture.Parent.FrameUpdateType];
                int offset = (int)this.picture.Parent.FrameUpdateType * Av1InterpolationProbabilities.FrameLength;
                ReadOnlySpan<int> probabilities = this.blockWorkspace.InterpolationProbabilities[offset..];
                for (int filter = 0; filter < filterCount; filter++)
                {
                    if (probabilities[(verticalContext * filterCount) + filter] < threshold &&
                        probabilities[(horizontalContext * filterCount) + filter] < threshold)
                    {
                        allowedMask &= ~(1 << filter);
                    }
                }
            }

            bool modelChroma = hasChroma && !winnerSearch && !settings.SkipInterpolationChromaModel;
            int horizontalSkip = modelChroma ? 3 : 1;
            int verticalSkip = horizontalSkip;
            const int interpolationExtension = 4;
            for (int referenceIndex = 0; referenceIndex < (compound ? 2 : 1); referenceIndex++)
            {
                Av1MotionVector vector = referenceIndex == 0 ? primary : secondary;
                for (int planeIndex = 0; planeIndex < (modelChroma ? 2 : 1); planeIndex++)
                {
                    int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                    int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                    Av1BlockSize planeSize = blockSize.GetSubsampled(subX != 0, subY != 0);
                    int horizontalBorder = (interpolationExtension + planeSize.GetWidth()) << 4;
                    int verticalBorder = (interpolationExtension + planeSize.GetHeight()) << 4;

                    // Once every tap lies outside an edge, border replication makes the fractional
                    // displacement irrelevant. Clamp in sixteenth-sample plane units before testing phase.
                    int column = Math.Clamp(
                        vector.Column << (1 - subX),
                        (macroBlock.ToLeftEdge << (1 - subX)) - horizontalBorder,
                        (macroBlock.ToRightEdge << (1 - subX)) + horizontalBorder - 16);
                    int row = Math.Clamp(
                        vector.Row << (1 - subY),
                        (macroBlock.ToTopEdge << (1 - subY)) - verticalBorder,
                        (macroBlock.ToBottomEdge << (1 - subY)) + verticalBorder - 16);

                    if ((column & 15) != 0)
                    {
                        horizontalSkip &= ~(1 << planeIndex);
                    }

                    if ((row & 15) != 0)
                    {
                        verticalSkip &= ~(1 << planeIndex);
                    }
                }
            }

            int defaultSkip = modelChroma ? 3 : 1;
            bool horizontalPhase = horizontalSkip != defaultSkip;
            bool verticalPhase = verticalSkip != defaultSkip;
            int predictionSkip = horizontalSkip & verticalSkip;
            int predictedFilter = -1;
            if (!winnerSearch && !dual && settings.UseNeighborInterpolation && (horizontalPhase || verticalPhase) &&
                ((((blockOrigin.Y >> 2) + (blockOrigin.X >> 2)) >> (System.Numerics.BitOperations.Log2((uint)blockSize.GetWidth()) - 2)) +
                    this.blockWorkspace.EncodedFrameCount & 1) != 0 &&
                macroBlock.IsUpAvailable && macroBlock.IsLeftAvailable)
            {
                Av1EncoderBlockModeInfo above = macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block;
                Av1EncoderBlockModeInfo left = macroBlock.GetRelativeModeInfo(-1).Block;
                if (above.ReferenceFrame > Av1ReferenceFrameType.Intra && left.ReferenceFrame > Av1ReferenceFrameType.Intra &&
                    above.HorizontalInterpolationFilter == left.HorizontalInterpolationFilter &&
                    above.VerticalInterpolationFilter == left.VerticalInterpolationFilter)
                {
                    predictedFilter = (int)above.VerticalInterpolationFilter;
                }
            }

            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int lumaCount = blockSize.GetWidth() * blockSize.GetHeight();
            Av1BlockSize chromaSize = blockSize.GetSubsampled(this.source.ChromaSubsamplingX != 0, this.source.ChromaSubsamplingY != 0);
            int chromaCount = hasChroma ? chromaSize.GetWidth() * chromaSize.GetHeight() : 0;
            Span<TSample> bestLuma = workspace.LumaPrediction[..lumaCount];
            Span<TSample> trialLuma = workspace.LumaCandidateReconstruction[..lumaCount];
            Span<TSample> bestBlue = workspace.BluePrediction[..chromaCount];
            Span<TSample> trialBlue = workspace.BlueCandidateReconstruction[..chromaCount];
            Span<TSample> bestRed = workspace.RedPrediction[..chromaCount];
            Span<TSample> trialRed = workspace.RedCandidateReconstruction[..chromaCount];
            Av1InterpolationFilter initialFilter = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable
                ? Av1InterpolationFilter.Regular : frameHeader.InterpolationFilter;
            int regularRate = writesFilters
                ? writer.GetSwitchableInterpolationFilterCost(initialFilter, verticalContext) +
                    (dual ? writer.GetSwitchableInterpolationFilterCost(initialFilter, horizontalContext) : 0)
                : 0;
            InlineArray4<Av1RateDistortionStatistics> bestPlaneStatistics = default;
            InlineArray4<Av1RateDistortionStatistics> trialPlaneStatistics = default;
            Av1RateDistortionStatistics regular = this.GetInterFilterModelCost(
                primary,
                secondary,
                modeInfo,
                blockOrigin,
                blockSize,
                modelChroma,
                initialFilter,
                initialFilter,
                regularRate,
                long.MaxValue,
                100,
                0,
                bestLuma,
                bestBlue,
                bestRed,
                bestPlaneStatistics);

            long bestCost = regular.Cost;
            bool lumaUsesWorkspace = true;
            bool chromaUsesWorkspace = true;
            modeInfo.HorizontalInterpolationFilter = initialFilter;
            modeInfo.VerticalInterpolationFilter = initialFilter;
            filterRate = regularRate;
            if (writesFilters && compound && bestCandidateCost != long.MaxValue && (bestCost >> 1) > singleReferenceCost)
            {
                modelCost = long.MaxValue;
                return false;
            }

            bool sharpMatchesRegular = blockSize == Av1BlockSize.Block4x4 ||
                (blockSize.GetWidth() == 4 && !verticalPhase) || (blockSize.GetHeight() == 4 && !horizontalPhase);
            int pairCount = writesFilters ? dual ? filterCount * filterCount : filterCount : 1;
            for (int pairIndex = 1; pairIndex < pairCount; pairIndex++)
            {
                // Full dual search and the four-wide equivalent-kernel case visit sharp first.
                // Other non-dual blocks visit smooth first so its winner can terminate the search.
                int pair = !winnerSearch && (dual || sharpMatchesRegular) ? pairCount - pairIndex : pairIndex;
                int vertical = dual ? pair / filterCount : pair;
                int horizontal = dual ? pair % filterCount : pair;
                if (!dual && (((allowedMask & (1 << vertical)) == 0) || (predictedFilter >= 0 && vertical != predictedFilter)))
                {
                    continue;
                }

                bool sharp = vertical == (int)Av1InterpolationFilter.Sharp || horizontal == (int)Av1InterpolationFilter.Sharp;
                int scale = !winnerSearch && sharp && settings.PreferSharpInterpolation ? 90 : 100;
                int trialRate = writer.GetSwitchableInterpolationFilterCost((Av1InterpolationFilter)vertical, verticalContext) +
                    (dual ? writer.GetSwitchableInterpolationFilterCost((Av1InterpolationFilter)horizontal, horizontalContext) : 0);

                if (Av1RateDistortion.GetCost(this.rateMultiplier, trialRate, 0) * scale / 100 > bestCost)
                {
                    continue;
                }

                // Keep unchanged plane estimates and samples in their existing views. Luma can
                // be integer-phase while subsampled chroma remains fractional, so reuse is per plane.
                int skipPlanes = winnerSearch ? 0 : !dual && sharp && sharpMatchesRegular ? defaultSkip : predictionSkip;
                bestPlaneStatistics[..].CopyTo(trialPlaneStatistics);
                Av1RateDistortionStatistics trial = this.GetInterFilterModelCost(
                    primary,
                    secondary,
                    modeInfo,
                    blockOrigin,
                    blockSize,
                    modelChroma,
                    (Av1InterpolationFilter)horizontal,
                    (Av1InterpolationFilter)vertical,
                    trialRate,
                    bestCost,
                    scale,
                    skipPlanes,
                    trialLuma,
                    trialBlue,
                    trialRed,
                    trialPlaneStatistics);

                if (trial.Cost != long.MaxValue && trial.Cost * scale / 100 < bestCost)
                {
                    bestCost = trial.Cost;
                    modeInfo.HorizontalInterpolationFilter = (Av1InterpolationFilter)horizontal;
                    modeInfo.VerticalInterpolationFilter = (Av1InterpolationFilter)vertical;
                    filterRate = trialRate;
                    trialPlaneStatistics[..].CopyTo(bestPlaneStatistics);
                    if ((skipPlanes & 1) == 0)
                    {
                        Span<TSample> previousLuma = bestLuma;
                        bestLuma = trialLuma;
                        trialLuma = previousLuma;
                        lumaUsesWorkspace = !lumaUsesWorkspace;
                    }

                    if (modelChroma && (skipPlanes & 2) == 0)
                    {
                        Span<TSample> previousBlue = bestBlue;
                        bestBlue = trialBlue;
                        trialBlue = previousBlue;
                        Span<TSample> previousRed = bestRed;
                        bestRed = trialRed;
                        trialRed = previousRed;
                        chromaUsesWorkspace = !chromaUsesWorkspace;
                    }
                }

                if (!winnerSearch && !dual && !sharpMatchesRegular && settings.SkipSharpInterpolationAfterSmooth && (horizontalPhase || verticalPhase) &&
                    modeInfo.VerticalInterpolationFilter == Av1InterpolationFilter.Smooth)
                {
                    break;
                }
            }

            if (!lumaUsesWorkspace)
            {
                bestLuma.CopyTo(workspace.LumaPrediction);
            }

            if (modelChroma && !chromaUsesWorkspace)
            {
                bestBlue.CopyTo(workspace.BluePrediction);
                bestRed.CopyTo(workspace.RedPrediction);
            }

            if (hasChroma && !modelChroma)
            {
                // Difference-weighted chroma consumes the luma mask. Rebuild that mask for the winner
                // after filter trials, then prepare only the chroma planes omitted by the luma-only model.
                int firstPlane = compound && modeInfo.CompoundType == Av1CompoundType.DifferenceWeighted ? 0 : 1;
                for (int planeIndex = firstPlane; planeIndex < 3; planeIndex++)
                {
                    Av1Plane plane = (Av1Plane)planeIndex;
                    int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                    int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                    Span<TSample> prediction = planeIndex == 0 ? workspace.LumaPrediction :
                        planeIndex == 1 ? workspace.BluePrediction : workspace.RedPrediction;
                    Buffer2DRegion<TSample> reference = this.references.Span[(int)modeInfo.ReferenceFrame].CodedView.GetPlane(plane);
                    Buffer2DRegion<TSample> secondaryReference = modeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                        ? this.references.Span[(int)modeInfo.SecondaryReferenceFrame].CodedView.GetPlane(plane)
                        : default;

                    this.PrepareInterPlanePrediction(
                        primary,
                        secondary,
                        plane,
                        modeInfo.Mode,
                        modeInfo.ReferenceFrame,
                        modeInfo.SecondaryReferenceFrame,
                        false,
                        modeInfo.CompoundType,
                        modeInfo.CompoundWedgeIndex,
                        modeInfo.CompoundWedgeSign,
                        modeInfo.DifferenceWeightedMaskType,
                        modeInfo.HorizontalInterpolationFilter,
                        modeInfo.VerticalInterpolationFilter,
                        reference,
                        secondaryReference,
                        blockOrigin,
                        subX,
                        subY,
                        blockSize,
                        prediction,
                        workspace.Residual);
                }
            }

            if (writesFilters && reuseLevel != 0 && this.interpolationSearchRecordCount < Av1InterpolationSearchRecord.Capacity)
            {
                records[this.interpolationSearchRecordCount++] = new Av1InterpolationSearchRecord
                {
                    Primary = primary,
                    Secondary = secondary,
                    PrimaryReference = modeInfo.ReferenceFrame,
                    SecondaryReference = modeInfo.SecondaryReferenceFrame,
                    CompoundType = modeInfo.CompoundType,
                    CompoundIndex = modeInfo.CompoundIndex,
                    HorizontalFilter = modeInfo.HorizontalInterpolationFilter,
                    VerticalFilter = modeInfo.VerticalInterpolationFilter,
                    Cost = bestCost
                };
            }

            modelCost = bestCost;
            return true;
        }

        /// <summary>
        /// Ranks a filter pair from visible prediction error without transforming samples.
        /// </summary>
        private Av1RateDistortionStatistics GetInterFilterModelCost(
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1EncoderBlockModeInfo modeInfo,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool modelChroma,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int filterRate,
            long bestCost,
            int comparisonScale,
            int skipPlanes,
            Span<TSample> lumaPrediction,
            Span<TSample> bluePrediction,
            Span<TSample> redPrediction,
            Span<Av1RateDistortionStatistics> planeStatistics)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int planeCount = modelChroma ? 3 : 1;
            bool simpleModel = this.picture.Parent.SpeedSettings.UseSimpleInterpolationModel;
            int rate = filterRate;
            long distortion = 0;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                if (planeIndex > 0 && Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion) * comparisonScale / 100 >= bestCost)
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                if ((skipPlanes & (planeIndex == 0 ? 1 : 2)) != 0)
                {
                    rate += planeStatistics[planeIndex].Rate;
                    distortion += planeStatistics[planeIndex].Distortion;
                    continue;
                }

                Av1Plane plane = (Av1Plane)planeIndex;
                int subsamplingX = plane == Av1Plane.Y ? 0 : this.source.ChromaSubsamplingX;
                int subsamplingY = plane == Av1Plane.Y ? 0 : this.source.ChromaSubsamplingY;
                Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
                int width = planeBlockSize.GetWidth();
                int height = planeBlockSize.GetHeight();
                Span<TSample> prediction = plane == Av1Plane.Y ? lumaPrediction : plane == Av1Plane.U ? bluePrediction : redPrediction;
                Span<short> residual = workspace.Residual[..(width * height)];
                Buffer2DRegion<TSample> primaryReferencePlane = this.references.Span[(int)modeInfo.ReferenceFrame].CodedView.GetPlane(plane);
                Buffer2DRegion<TSample> secondaryReferencePlane = modeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                    ? this.references.Span[(int)modeInfo.SecondaryReferenceFrame].CodedView.GetPlane(plane)
                    : default;

                this.PrepareInterPlanePrediction(
                    vector,
                    secondaryVector,
                    plane,
                    modeInfo.Mode,
                    modeInfo.ReferenceFrame,
                    modeInfo.SecondaryReferenceFrame,
                    false,
                    modeInfo.CompoundType,
                    modeInfo.CompoundWedgeIndex,
                    modeInfo.CompoundWedgeSign,
                    modeInfo.DifferenceWeightedMaskType,
                    horizontalFilter,
                    verticalFilter,
                    primaryReferencePlane,
                    secondaryReferencePlane,
                    blockOrigin,
                    subsamplingX,
                    subsamplingY,
                    blockSize,
                    prediction,
                    residual);

                Size visible = simpleModel ? new Size(width, height) : this.GetPredictionModelSize(blockOrigin, blockSize, subsamplingX, subsamplingY);
                int visibleWidth = visible.Width;
                int visibleHeight = visible.Height;
                long squaredError = 0;

                // The source view includes samples extended to the coded dimensions. Reduce complete rows together;
                // a partial right edge needs separate row reductions to exclude samples beyond the source view.
                if (visibleWidth == width)
                {
                    squaredError = Av1ResidualBuilder.SumSquares(residual[..(width * visibleHeight)]);
                }
                else
                {
                    for (int row = 0; row < visibleHeight; row++)
                    {
                        squaredError += Av1ResidualBuilder.SumSquares(residual.Slice(row * width, visibleWidth));
                    }
                }

                int normalizationShift = (this.bitDepth.GetBitCount() - 8) * 2;
                if (normalizationShift != 0)
                {
                    squaredError = (squaredError + (1L << (normalizationShift - 1))) >> normalizationShift;
                }

                int acQuantizer = Av1QuantizationLookup.GetAcQuant(
                    this.quantization.QIndex[0],
                    this.quantization.DeltaQAc[planeIndex],
                    this.bitDepth);

                int planeRate;
                long planeDistortion;
                if (simpleModel)
                {
                    // The linear model uses complete padded blocks and eight-bit-normalized error.
                    // Quantizer scaling removes transform precision before the rate and distortion shifts.
                    int quantizer = acQuantizer >> (this.bitDepth.GetBitCount() - 5);
                    planeRate = quantizer < 120 ? (int)Math.Min((squaredError * (280 - quantizer)) >> 7, int.MaxValue) : 0;
                    planeDistortion = ((squaredError * quantizer) >> 8) << 4;
                }
                else
                {
                    Av1RateDistortion.ModelPredictionError(
                        planeBlockSize,
                        squaredError,
                        visibleWidth * visibleHeight,
                        acQuantizer,
                        this.bitDepth,
                        this.rateMultiplier,
                        out planeRate,
                        out planeDistortion);
                }

                planeStatistics[planeIndex] = new Av1RateDistortionStatistics(this.rateMultiplier, planeRate, planeDistortion);
                rate += planeRate;
                distortion += planeDistortion;
            }

            return new Av1RateDistortionStatistics(this.rateMultiplier, rate, distortion);
        }

        /// <summary>
        /// Evaluates one inter mode through prediction, transform, coefficient, skip, and distortion selection.
        /// </summary>
        private Av1RateDistortionStatistics EvaluateInterCandidate(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            long bestCost,
            bool hasChroma,
            int commonPredictionRate,
            Av1ReferenceFrameType referenceFrame,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1PredictionMode mode,
            Av1ReferenceFrameType secondaryReferenceFrame,
            bool usePreparedPrediction,
            Av1CompoundType compoundType,
            int compoundWedgeIndex,
            bool compoundWedgeSign,
            Av1DifferenceWeightedMaskType differenceWeightedMaskType,
            int compoundBlendRate,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int referenceMotionVectorIndex,
            bool isInterIntra,
            Av1InterIntraMode interIntraMode,
            bool useInterIntraWedge,
            int interIntraWedgeIndex,
            in Av1ReferenceMotionVectors referenceMotionVectors,
            Span<TSample> lumaReconstruction,
            Span<int> lumaCoefficients,
            Span<TSample> blueReconstruction,
            Span<int> blueCoefficients,
            Span<TSample> redReconstruction,
            Span<int> redCoefficients,
            out bool skip,
            out InlineArray64<Av1EncoderTransformBlockState> lumaStates,
            out InlineArray16<Av1TransformSize> lumaTransformSizes,
            out InlineArray16<Av1EncoderTransformBlockState> blueState,
            out InlineArray16<Av1EncoderTransformBlockState> redState)
        {
            bool isCompound = secondaryReferenceFrame > Av1ReferenceFrameType.Intra;
            skip = false;
            lumaStates = default;
            lumaTransformSizes = default;
            blueState = default;
            redState = default;

            // An intra candidate reaching this method carries no inter mode syntax, so it keeps the
            // rate its caller already priced. Everything else adds the blend it chose and the mode
            // itself, which differ between one reference and two.
            int predictionRate = commonPredictionRate;
            if (referenceFrame != Av1ReferenceFrameType.Intra)
            {
                predictionRate += compoundBlendRate + (isCompound
                    ? this.GetCompoundInterModeRate(
                        writer,
                        mode,
                        vector,
                        secondaryVector,
                        referenceMotionVectorIndex,
                        in referenceMotionVectors)
                    : this.GetInterModeRate(
                        writer,
                        mode,
                        vector,
                        referenceMotionVectorIndex,
                        in referenceMotionVectors));
            }

            // The second reference position carries three different meanings. A compound block
            // names its second reference frame there, an inter-intra block marks the slot as intra
            // so that the blend knows to build an intra prediction for it, and a plain single
            // reference block leaves it empty.
            Av1EncoderBlockModeInfo predictionModeInfo = new()
            {
                BlockSize = blockSize,
                Mode = mode,
                ReferenceFrame = referenceFrame,
                SecondaryReferenceFrame = isCompound ? secondaryReferenceFrame :
                    isInterIntra ? Av1ReferenceFrameType.Intra : Av1ReferenceFrameType.None,
                HorizontalInterpolationFilter = horizontalFilter,
                VerticalInterpolationFilter = verticalFilter,
                CompoundType = compoundType,
                CompoundGroupIndex = compoundType is Av1CompoundType.Wedge or Av1CompoundType.DifferenceWeighted,
                CompoundIndex = compoundType == Av1CompoundType.Average,
                CompoundWedgeIndex = (byte)compoundWedgeIndex,
                CompoundWedgeSign = compoundWedgeSign,
                DifferenceWeightedMaskType = differenceWeightedMaskType
            };

            if (isInterIntra)
            {
                predictionModeInfo.InterIntraMode = interIntraMode;
                predictionModeInfo.UseInterIntraWedge = useInterIntraWedge;
                predictionModeInfo.InterIntraWedgeIndex = (byte)interIntraWedgeIndex;
            }

            Av1InterModeCandidate candidate = new()
            {
                ModeInfo = predictionModeInfo,
                Vector = vector,
                SecondaryVector = secondaryVector,
                PredictionRate = predictionRate,
                ReferenceIndex = referenceMotionVectorIndex,
                SearchIndex = this.interCandidateCount
            };

            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Av1EncoderFrame<TSample>.PlanarView primaryReference = referenceFrame == Av1ReferenceFrameType.Intra
                ? this.reconstruction
                : this.references.Span[(int)referenceFrame].CodedView;

            // The prediction error is only accumulated when something later reads it: either this
            // pass is estimating candidates rather than coding them, or a speed setting uses the
            // error to decide whether the transform search runs at all.
            bool measurePrediction = this.estimateInterCandidates ||
                (referenceFrame != Av1ReferenceFrameType.Intra && this.picture.Parent.SpeedSettings.InterTransformGateLevel != 0);
            long predictionError = 0;
            long estimatedDistortion = 0;
            int estimatedRate = 0;
            int planeCount = hasChroma ? 3 : 1;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Av1BlockSize planeSize = blockSize.GetSubsampled(subX != 0, subY != 0);
                Span<TSample> prediction = planeIndex == 0 ? workspace.LumaPrediction :
                    planeIndex == 1 ? workspace.BluePrediction : workspace.RedPrediction;

                this.PrepareInterPlanePrediction(
                    vector,
                    secondaryVector,
                    plane,
                    mode,
                    referenceFrame,
                    secondaryReferenceFrame,
                    usePreparedPrediction,
                    compoundType,
                    compoundWedgeIndex,
                    compoundWedgeSign,
                    differenceWeightedMaskType,
                    horizontalFilter,
                    verticalFilter,
                    primaryReference.GetPlane(plane),
                    (isCompound ? this.references.Span[(int)secondaryReferenceFrame].CodedView : primaryReference).GetPlane(plane),
                    blockOrigin,
                    subX,
                    subY,
                    blockSize,
                    prediction,
                    workspace.Residual);

                if (measurePrediction)
                {
                    Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                    Point planeOrigin = planeIndex == 0 ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
                    Size extent = this.picture.Parent.SpeedSettings.InterModeEstimation == 2 && this.estimateInterCandidates
                        ? this.GetPredictionModelSize(blockOrigin, blockSize, subX, subY)
                        : new Size(planeSize.GetWidth(), planeSize.GetHeight());
                    TOperator.GetMoments(
                        Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, planeOrigin),
                        sourcePlane.Stride,
                        prediction,
                        planeSize.GetWidth(),
                        extent.Width,
                        extent.Height,
                        out _,
                        out long squaredError);

                    int shift = (this.bitDepth.GetBitCount() - 8) * 2;
                    if (shift != 0)
                    {
                        squaredError = (squaredError + (1L << (shift - 1))) >> shift;
                    }

                    predictionError += squaredError << 4;
                    if (planeIndex == 0)
                    {
                        candidate.LumaPredictionError = squaredError << 4;
                    }

                    if (this.estimateInterCandidates && this.picture.Parent.SpeedSettings.InterModeEstimation == 2)
                    {
                        int acQuantizer = Av1QuantizationLookup.GetAcQuant(
                            this.quantization.QIndex[0], this.quantization.DeltaQAc[planeIndex], this.bitDepth);

                        Av1RateDistortion.ModelPredictionError(
                            planeSize,
                            squaredError,
                            extent.Width * extent.Height,
                            acQuantizer,
                            this.bitDepth,
                            this.rateMultiplier,
                            out int planeRate,
                            out long planeDistortion);

                        estimatedRate += planeRate;
                        estimatedDistortion += planeDistortion;
                    }
                }
            }

            candidate.PredictionError = predictionError;
            if (this.estimateInterCandidates)
            {
                if (this.picture.Parent.SpeedSettings.InterModeEstimation == 1)
                {
                    this.blockWorkspace.InterModeModels[(int)blockSize].Estimate(
                        predictionError, out estimatedRate, out estimatedDistortion);
                }

                Av1RateDistortionStatistics estimate = new(this.rateMultiplier, predictionRate + estimatedRate, estimatedDistortion)
                {
                    ResidualRate = estimatedRate,
                    PredictionDistortion = predictionError
                };

                // Retain predictions close enough to the best estimate for the later transform pass.
                // Syntax and vectors are sufficient to rebuild them; no candidate owns a pixel buffer.
                if (estimate.Cost * 0.80 > this.bestInterEstimate)
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                if (estimate.Cost < this.bestInterEstimate)
                {
                    this.bestInterEstimate = estimate.Cost;
                    this.bestInterLumaPredictionCost = Av1RateDistortion.GetCost(
                        this.rateMultiplier, predictionRate, candidate.LumaPredictionError);
                }

                candidate.EstimatedCost = estimate.Cost;
                this.blockWorkspace.InterModeCandidates[this.interCandidateCount++] = candidate;
                return estimate;
            }

            if (referenceFrame != Av1ReferenceFrameType.Intra && !this.ShouldSearchInterTransforms(candidate))
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            return this.EvaluatePreparedInterCandidate(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                bestCost,
                hasChroma,
                candidate,
                lumaReconstruction,
                lumaCoefficients,
                blueReconstruction,
                blueCoefficients,
                redReconstruction,
                redCoefficients,
                out skip,
                out lumaStates,
                out lumaTransformSizes,
                out blueState,
                out redState);
        }

        /// <summary>
        /// Compares prediction-only error with the retained inter result before searching transforms.
        /// </summary>
        private bool ShouldSearchInterTransforms(Av1InterModeCandidate candidate)
        {
            int level = this.picture.Parent.SpeedSettings.InterTransformGateLevel;
            if (level == 0 || this.bestInterPredictionCost == long.MaxValue)
            {
                return true;
            }

            int quantizer = this.quantization.QIndex[0];
            int factor = level <= 2 ? 4 * Math.Max(1, (((255 - quantizer) * 2) + 128) >> 8) : 4;
            ReadOnlySpan<int> quantizerThresholds = [0, 0, 0, 80, 100, 140];
            if (this.bestInterPredictionCost > this.interSourceVarianceCost && quantizer >= quantizerThresholds[level])
            {
                ReadOnlySpan<int> scales = [int.MaxValue, 4, 3, 2, 2, 1];
                factor *= scales[level];
            }
            else if (level <= 1)
            {
                factor = (factor >> 2) * 6;
            }

            // A low quantizer or a poorly predicted source needs a wider margin. Compare the same
            // prediction-only rate/error scale for both modes, before coefficient coding changes it.
            long threshold = (this.bestInterPredictionCost * factor * 16) >> 6;
            long predictionCost = Av1RateDistortion.GetCost(this.rateMultiplier, candidate.PredictionRate, candidate.PredictionError);
            return predictionCost <= threshold;
        }

        /// <summary>
        /// Searches residuals for a prepared prediction and records completed searches in the tile model.
        /// </summary>
        private Av1RateDistortionStatistics EvaluatePreparedInterCandidate(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            long bestCost,
            bool hasChroma,
            Av1InterModeCandidate candidate,
            Span<TSample> lumaReconstruction,
            Span<int> lumaCoefficients,
            Span<TSample> blueReconstruction,
            Span<int> blueCoefficients,
            Span<TSample> redReconstruction,
            Span<int> redCoefficients,
            out bool skip,
            out InlineArray64<Av1EncoderTransformBlockState> lumaStates,
            out InlineArray16<Av1TransformSize> lumaTransformSizes,
            out InlineArray16<Av1EncoderTransformBlockState> blueState,
            out InlineArray16<Av1EncoderTransformBlockState> redState)
        {
            skip = false;
            lumaStates = default;
            lumaTransformSizes = default;
            blueState = default;
            redState = default;
            Av1EncoderBlockModeInfo predictionModeInfo = candidate.ModeInfo;
            Av1BlockSize blockSize = predictionModeInfo.BlockSize;
            Av1PredictionMode mode = predictionModeInfo.Mode;
            Av1ReferenceFrameType referenceFrame = predictionModeInfo.ReferenceFrame;
            Av1MotionVector vector = candidate.Vector;
            Av1MotionVector secondaryVector = candidate.SecondaryVector;
            Av1CompoundType compoundType = predictionModeInfo.CompoundType;
            int compoundWedgeIndex = predictionModeInfo.CompoundWedgeIndex;
            bool compoundWedgeSign = predictionModeInfo.CompoundWedgeSign;
            Av1DifferenceWeightedMaskType differenceWeightedMaskType = predictionModeInfo.DifferenceWeightedMaskType;
            Av1InterpolationFilter horizontalFilter = predictionModeInfo.HorizontalInterpolationFilter;
            Av1InterpolationFilter verticalFilter = predictionModeInfo.VerticalInterpolationFilter;
            bool usePreparedPrediction = true;
            int predictionRate = candidate.PredictionRate;
            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            int noSkipCost = writer.GetSkipCost(false, skipContext);
            int skipCost = writer.GetSkipCost(true, skipContext);
            if (Av1RateDistortion.GetCost(this.rateMultiplier, predictionRate + Math.Min(noSkipCost, skipCost), 0) > bestCost)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            long transformCostLimit = bestCost == long.MaxValue
                ? long.MaxValue
                : bestCost - Av1RateDistortion.GetCost(this.rateMultiplier, predictionRate, 0);

            Av1TransformSize lumaTransformSize = this.picture.Parent.FrameHeader.CodedLossless
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaximumTransformSize();
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Av1EncoderBlockModeInfo lumaModeInfo = new()
            {
                BlockSize = blockSize,
                TransformSize = lumaTransformSize,
                Mode = mode,
                UseIntraBlockCopy = referenceFrame == Av1ReferenceFrameType.Intra
            };

            Av1RateDistortionStatistics lumaStatistics = this.EvaluateInterLumaTree(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                ref lumaModeInfo,
                lumaStates,
                transformCostLimit,
                out int lumaStateCount);

            if (lumaStatistics.Cost == long.MaxValue)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            lumaModeInfo.InterTransformSizes.CopyTo(lumaTransformSizes);
            int lumaRate = lumaStatistics.Rate;
            long lumaDistortion = lumaStatistics.Distortion;
            long lumaPredictionDistortion = lumaStatistics.PredictionDistortion;

            long codedLumaCost = Av1RateDistortion.GetCost(
                this.rateMultiplier, predictionRate + noSkipCost + lumaRate, lumaDistortion);
            long skippedLumaCost = Av1RateDistortion.GetCost(
                this.rateMultiplier, predictionRate + skipCost, lumaPredictionDistortion);

            if (Math.Min(codedLumaCost, skippedLumaCost) > bestCost)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            int blueRate = 0;
            int redRate = 0;
            long blueDistortion = 0;
            long redDistortion = 0;
            long bluePredictionDistortion = 0;
            long redPredictionDistortion = 0;
            bool blueHasCoefficients = false;
            bool redHasCoefficients = false;
            if (hasChroma)
            {
                long chromaCostLimit = this.picture.Parent.SpeedSettings.UseLumaCostForChromaBound && bestCost != long.MaxValue
                    ? bestCost - Math.Min(codedLumaCost, skippedLumaCost)
                    : bestCost;

                bool blueValid = this.EvaluateInterChromaPlane(
                    writer,
                    macroBlock,
                    tileIndex,
                    chromaCostLimit,
                    blockOrigin,
                    blockSize,
                    Av1Plane.U,
                    referenceFrame,
                    predictionModeInfo.SecondaryReferenceFrame,
                    vector,
                    secondaryVector,
                    mode,
                    usePreparedPrediction,
                    compoundType,
                    compoundWedgeIndex,
                    compoundWedgeSign,
                    differenceWeightedMaskType,
                    horizontalFilter,
                    verticalFilter,
                    lumaTransformSizes,
                    lumaStates[..lumaStateCount],
                    blueReconstruction,
                    blueCoefficients,
                    out blueState,
                    out blueRate,
                    out blueDistortion,
                    out bluePredictionDistortion,
                    out blueHasCoefficients);

                if (!blueValid)
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                long blueCost = Math.Min(
                    Av1RateDistortion.GetCost(this.rateMultiplier, blueRate, blueDistortion),
                    Av1RateDistortion.GetCost(this.rateMultiplier, 0, bluePredictionDistortion));
                if (blueCost > chromaCostLimit)
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                long redCostLimit = this.picture.Parent.SpeedSettings.UseLumaCostForChromaBound && chromaCostLimit != long.MaxValue
                    ? chromaCostLimit - blueCost
                    : chromaCostLimit;

                bool redValid = this.EvaluateInterChromaPlane(
                    writer,
                    macroBlock,
                    tileIndex,
                    redCostLimit,
                    blockOrigin,
                    blockSize,
                    Av1Plane.V,
                    referenceFrame,
                    predictionModeInfo.SecondaryReferenceFrame,
                    vector,
                    secondaryVector,
                    mode,
                    usePreparedPrediction,
                    compoundType,
                    compoundWedgeIndex,
                    compoundWedgeSign,
                    differenceWeightedMaskType,
                    horizontalFilter,
                    verticalFilter,
                    lumaTransformSizes,
                    lumaStates[..lumaStateCount],
                    redReconstruction,
                    redCoefficients,
                    out redState,
                    out redRate,
                    out redDistortion,
                    out redPredictionDistortion,
                    out redHasCoefficients);

                long chromaCost = Math.Min(
                    Av1RateDistortion.GetCost(this.rateMultiplier, blueRate + redRate, blueDistortion + redDistortion),
                    Av1RateDistortion.GetCost(this.rateMultiplier, 0, bluePredictionDistortion + redPredictionDistortion));

                if (!redValid || chromaCost > chromaCostLimit)
                {
                    return Av1RateDistortionStatistics.Invalid;
                }
            }

            int codedRate = predictionRate +
                writer.GetSkipCost(false, skipContext) +
                lumaRate +
                blueRate +
                redRate;

            long codedDistortion = lumaDistortion + blueDistortion + redDistortion;
            Av1RateDistortionStatistics selectedStatistics = new(this.rateMultiplier, codedRate, codedDistortion);
            int skipRate = writer.GetSkipCost(true, skipContext);
            long skipDistortion = lumaPredictionDistortion + bluePredictionDistortion + redPredictionDistortion;

            // All-empty residuals omit the transform tree. Nonempty residuals can also be discarded when
            // prediction alone costs no more; shared prediction syntax must not affect the rounded comparison.
            bool allEmpty = !lumaStatistics.HasCoefficients && !blueHasCoefficients && !redHasCoefficients;

            if (this.picture.Parent.FrameHeader.CodedLossless)
            {
                // Preserve coefficient presence independently of rounded distortion. A small high-bit-depth
                // residual can have zero normalized error but still requires reversible coefficients.
                skip = allEmpty;
            }
            else
            {
                // The recursive luma search marks its result skippable by comparing the costs, not by the
                // emptiness of its transforms, while chroma is skippable only when it codes nothing. Skip is
                // taken outright when both are, and otherwise when leaving the residual uncoded costs no more.
                // Reference: the skip_txfm result of select_inter_block_yrd(), merged with av1_txfm_uvrd()'s,
                // and the choose_skip_txfm comparison of av1_txfm_search().
                bool lumaSkippable = lumaStatistics.SkipPredicted || (this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select
                    ? Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, lumaPredictionDistortion) <=
                        Av1RateDistortion.GetCost(this.rateMultiplier, lumaRate + writer.GetSkipCost(false, skipContext), lumaDistortion)
                    : !lumaStatistics.HasCoefficients);
                skip = (lumaSkippable && !blueHasCoefficients && !redHasCoefficients) ||
                    Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, skipDistortion) <=
                    Av1RateDistortion.GetCost(this.rateMultiplier, codedRate - predictionRate, codedDistortion);
            }

            if (skip)
            {
                selectedStatistics = new(
                    this.rateMultiplier,
                    predictionRate + skipRate,
                    skipDistortion);
                int lumaSampleCount = blockSize.GetWidth() * blockSize.GetHeight();
                workspace.LumaPrediction[..lumaSampleCount].CopyTo(lumaReconstruction);
                lumaCoefficients[..lumaSampleCount].Clear();
                lumaStates = default;
                lumaTransformSizes[..].Fill(lumaTransformSize);
                if (hasChroma)
                {
                    Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                        this.source.ChromaSubsamplingX != 0, this.source.ChromaSubsamplingY != 0);
                    int chromaSampleCount = chromaBlockSize.GetWidth() * chromaBlockSize.GetHeight();
                    workspace.BluePrediction[..chromaSampleCount].CopyTo(blueReconstruction);
                    workspace.RedPrediction[..chromaSampleCount].CopyTo(redReconstruction);
                    blueCoefficients[..chromaSampleCount].Clear();
                    redCoefficients[..chromaSampleCount].Clear();
                    blueState = default;
                    redState = default;
                }
            }

            selectedStatistics.LumaCost = skip ? skippedLumaCost : codedLumaCost;
            selectedStatistics.HasCoefficients = !skip;
            selectedStatistics.AllTransformsEmpty = allEmpty;
            selectedStatistics.ResidualRate = selectedStatistics.Rate - predictionRate;
            selectedStatistics.PredictionDistortion = skipDistortion;
            if (referenceFrame != Av1ReferenceFrameType.Intra && selectedStatistics.Cost < bestCost)
            {
                this.bestInterPredictionCost = Av1RateDistortion.GetCost(this.rateMultiplier, predictionRate, candidate.PredictionError);
                this.bestInterLumaPredictionCost = Av1RateDistortion.GetCost(
                    this.rateMultiplier, predictionRate, candidate.LumaPredictionError);
            }

            if (referenceFrame != Av1ReferenceFrameType.Intra &&
                this.picture.Parent.SpeedSettings.InterModeEstimation == 1 &&
                blockSize.GetWidth() >= 8 && blockSize.GetHeight() >= 8)
            {
                this.blockWorkspace.InterModeModels[(int)blockSize].Add(
                    skipDistortion, selectedStatistics.Distortion, selectedStatistics.ResidualRate);
            }

            return selectedStatistics;
        }

        /// <summary>
        /// Measures compound mode and dynamic-reference-list syntax for one candidate.
        /// </summary>
        private int GetCompoundInterModeRate(
            Av1SymbolEncoder writer,
            Av1PredictionMode mode,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            int referenceMotionVectorIndex,
            in Av1ReferenceMotionVectors referenceMotionVectors)
        {
            int rate = writer.GetInterCompoundModeCost(mode, referenceMotionVectors.ModeContext);
            bool usesNear = mode is Av1PredictionMode.NearNearMotionVector or
                Av1PredictionMode.NearNewMotionVector or
                Av1PredictionMode.NewNearMotionVector;

            bool usesNewNew = mode == Av1PredictionMode.NewNewMotionVector;
            if (usesNear)
            {
                // Compound modes containing NEARMV use the same one-based DRL walk as single-reference NEARMV.
                // The stored index remains zero-based from the first near pair, matching decoder reconstruction.
                for (int index = 1; index < 3 && referenceMotionVectors.Count > index + 1; index++)
                {
                    bool advance = referenceMotionVectorIndex >= index;
                    int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                    rate += writer.GetDynamicReferenceListCost(advance, context);
                    if (!advance)
                    {
                        break;
                    }
                }
            }
            else if (usesNewNew)
            {
                for (int index = 0; index < 2 && referenceMotionVectors.Count > index + 1; index++)
                {
                    bool advance = referenceMotionVectorIndex > index;
                    int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                    rate += writer.GetDynamicReferenceListCost(advance, context);
                    if (!advance)
                    {
                        break;
                    }
                }
            }

            return rate + this.GetCompoundMotionRate(mode, vector, secondaryVector, referenceMotionVectorIndex, in referenceMotionVectors);
        }

        /// <summary>
        /// Measures differential motion syntax for the searched components of a compound mode.
        /// </summary>
        private int GetCompoundMotionRate(
            Av1PredictionMode mode,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            int referenceMotionVectorIndex,
            in Av1ReferenceMotionVectors referenceMotionVectors)
        {
            int rate = 0;
            bool usesNear = mode is Av1PredictionMode.NearNearMotionVector or
                Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;

            int newReferenceIndex = usesNear ? referenceMotionVectorIndex + 1 : referenceMotionVectorIndex;
            Av1MotionVectorCosts costs = this.blockWorkspace.GetMotionVectorCosts(this.picture.Parent.FrameHeader.MotionVectorPrecision);
            if (mode is Av1PredictionMode.NewNearestMotionVector or
                Av1PredictionMode.NewNearMotionVector or
                Av1PredictionMode.NewNewMotionVector)
            {
                Av1MotionVector reference = referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 0);
                rate += ((costs.GetCost(vector, reference) * 108) + 64) >> 7;
            }

            if (mode is Av1PredictionMode.NearestNewMotionVector or
                Av1PredictionMode.NearNewMotionVector or
                Av1PredictionMode.NewNewMotionVector)
            {
                Av1MotionVector reference = referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 1);
                rate += ((costs.GetCost(secondaryVector, reference) * 108) + 64) >> 7;
            }

            return rate;
        }

        /// <summary>
        /// Measures the complete mode, dynamic-reference-list, and differential-vector syntax for one candidate.
        /// </summary>
        /// <param name="writer">The live tile entropy model.</param>
        /// <param name="mode">The candidate single-reference inter mode.</param>
        /// <param name="vector">The candidate motion vector.</param>
        /// <param name="referenceMotionVectorIndex">The selected dynamic-reference-list entry.</param>
        /// <param name="referenceMotionVectors">The current spatial candidate stack.</param>
        /// <returns>The syntax rate in 1/512-bit units.</returns>
        private int GetInterModeRate(
            Av1SymbolEncoder writer,
            Av1PredictionMode mode,
            Av1MotionVector vector,
            int referenceMotionVectorIndex,
            in Av1ReferenceMotionVectors referenceMotionVectors)
        {
            int rate = writer.GetInterModeCost(mode, referenceMotionVectors.ModeContext);
            if (mode == Av1PredictionMode.NearMotionVector)
            {
                for (int index = 1; index < 3 && referenceMotionVectors.Count > index + 1; index++)
                {
                    bool advance = referenceMotionVectorIndex >= index;
                    int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                    rate += writer.GetDynamicReferenceListCost(advance, context);
                    if (!advance)
                    {
                        break;
                    }
                }

                return rate;
            }

            if (mode != Av1PredictionMode.NewMotionVector)
            {
                return rate;
            }

            for (int index = 0; index < 2 && referenceMotionVectors.Count > index + 1; index++)
            {
                bool advance = referenceMotionVectorIndex > index;
                int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                rate += writer.GetDynamicReferenceListCost(advance, context);
                if (!advance)
                {
                    break;
                }
            }

            Av1MotionVector reference = referenceMotionVectors.GetNewReference(referenceMotionVectorIndex);
            Av1MotionVectorCosts costs = this.blockWorkspace.GetMotionVectorCosts(this.picture.Parent.FrameHeader.MotionVectorPrecision);

            // Mode selection discounts motion syntax to 108/128 of its estimated rate. Apply the rounded
            // weight to the vector alone; mode and dynamic-reference-list symbols retain their full rate.
            return rate + (((costs.GetCost(vector, reference) * 108) + 64) >> 7);
        }

        /// <summary>
        /// Evaluates chroma transforms using full-plane prediction and sequential coefficient contexts.
        /// </summary>
        private bool EvaluateInterChromaPlane(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            ushort tileIndex,
            long costLimit,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1Plane plane,
            Av1ReferenceFrameType referenceFrame,
            Av1ReferenceFrameType secondaryReferenceFrame,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1PredictionMode predictionMode,
            bool usePreparedPrediction,
            Av1CompoundType compoundType,
            int compoundWedgeIndex,
            bool compoundWedgeSign,
            Av1DifferenceWeightedMaskType differenceWeightedMaskType,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            ReadOnlySpan<Av1TransformSize> lumaTransformSizes,
            ReadOnlySpan<Av1EncoderTransformBlockState> lumaStates,
            Span<TSample> selectedReconstruction,
            Span<int> selectedCoefficients,
            out InlineArray16<Av1EncoderTransformBlockState> states,
            out int rate,
            out long distortion,
            out long predictionDistortion,
            out bool hasCoefficients)
        {
            int subX = this.source.ChromaSubsamplingX;
            int subY = this.source.ChromaSubsamplingY;
            Size frameContextSize = new(
                this.picture.Parent.FrameHeader.ModeInfoColumnCount >> subX,
                this.picture.Parent.FrameHeader.ModeInfoRowCount >> subY);

            Point planeOrigin = Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
            Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subX != 0, subY != 0);
            bool lossless = this.picture.Parent.FrameHeader.CodedLossless;
            Av1TransformSize transformSize = lossless ? Av1TransformSize.Size4x4 : blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);
            int width = planeBlockSize.GetWidth();
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int sampleCount = transformSize.GetSize2d();
            Size extent = GetCodedTransformExtent(macroBlock, planeBlockSize, transformSize, subX, subY);
            Av1TransformSize lumaRootSize = blockSize.GetMaximumTransformSize();
            Av1TransformSize traversalSize = lumaRootSize.GetSubSize().GetSubSize();
            Size lumaExtent = GetCodedTransformExtent(macroBlock, blockSize, Av1TransformSize.Size4x4, 0, 0);
            int lumaLeafCount = blockSize.GetWidth() * blockSize.GetHeight() / traversalSize.GetSize2d();
            Av1TransformSize cellSize = lumaRootSize.GetSubSize();
            int cellWidth = cellSize.GetWidth();
            int cellHeight = cellSize.GetHeight();
            int cellStride = blockSize.GetWidth() / cellWidth;
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Span<TSample> prediction = plane == Av1Plane.U ? workspace.BluePrediction : workspace.RedPrediction;
            Av1EncoderFrame<TSample>.PlanarView primaryReference = referenceFrame == Av1ReferenceFrameType.Intra
                ? this.reconstruction
                : this.references.Span[(int)referenceFrame].CodedView;

            // Build the whole plane so interpolation and compound masks retain their block coordinates.
            // Transform origins then select strided views without repacking the predicted pixels.
            this.PrepareInterPlanePrediction(
                vector,
                secondaryVector,
                plane,
                predictionMode,
                referenceFrame,
                secondaryReferenceFrame,
                usePreparedPrediction,
                compoundType,
                compoundWedgeIndex,
                compoundWedgeSign,
                differenceWeightedMaskType,
                horizontalFilter,
                verticalFilter,
                primaryReference.GetPlane(plane),
                (secondaryReferenceFrame > Av1ReferenceFrameType.Intra
                    ? this.references.Span[(int)secondaryReferenceFrame].CodedView : primaryReference).GetPlane(plane),
                new Point(planeOrigin.X << subX, planeOrigin.Y << subY),
                subX,
                subY,
                blockSize,
                prediction,
                workspace.Residual);

            // Coefficient coding reads the neighboring contexts of this plane, and the search
            // changes them as it prices each transform. Taking a copy leaves the tile contexts
            // untouched, so a candidate that loses leaves nothing behind.
            Av1NeighborArrayUnit<byte> neighbors = plane == Av1Plane.U
                ? this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex]
                : this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex];
            int contextWidth = planeBlockSize.Get4x4WideCount();
            int contextHeight = planeBlockSize.Get4x4HighCount();
            Span<byte> topContexts = workspace.TransformContexts[..contextWidth];
            Span<byte> leftContexts = workspace.TransformContexts.Slice(contextWidth, contextHeight);
            neighbors.Top.Slice(neighbors.GetTopIndex(planeOrigin), contextWidth).CopyTo(topContexts);
            neighbors.Left.Slice(neighbors.GetLeftIndex(planeOrigin), contextHeight).CopyTo(leftContexts);
            Av1TransformSetType transformSet = Av1SymbolContextHelper.GetExtendedTransformSetType(
                transformSize, isInter: true, this.picture.Parent.FrameHeader.UseReducedTransformSet);

            states = default;
            rate = 0;
            distortion = 0;
            predictionDistortion = 0;
            hasCoefficients = false;
            int transformIndex = 0;
            long currentCost = 0;
            Av1TransformSize chromaRootSize = transformSize;
            int chromaLeafCount = planeBlockSize.GetWidth() * planeBlockSize.GetHeight() / sampleCount;
            for (int chromaLeaf = 0; chromaLeaf < chromaLeafCount; chromaLeaf++)
            {
                Point chromaOffset = chromaRootSize.GetBlockPartitionOrigin(planeBlockSize, transformSize, chromaLeaf, subX, subY);
                int x = chromaOffset.X;
                int y = chromaOffset.Y;
                if (x < extent.Width && y < extent.Height)
                {
                    if (currentCost > costLimit)
                    {
                        return false;
                    }

                    // Chroma inherits the luma type at its top-left sample. Luma states follow the
                    // transform tree and omit leaves outside the coded frame, so advance only for coded leaves.
                    Av1EncoderTransformBlockState lumaState = default;
                    int lumaIndex = 0;
                    for (int leaf = 0; !lossless && leaf < lumaLeafCount; leaf++)
                    {
                        Point offset = lumaRootSize.GetBlockPartitionOrigin(blockSize, traversalSize, leaf, 0, 0);
                        if (offset.X >= lumaExtent.Width || offset.Y >= lumaExtent.Height)
                        {
                            continue;
                        }

                        Av1TransformSize lumaTransformSize =
                            lumaTransformSizes[((offset.Y / cellHeight) * cellStride) + (offset.X / cellWidth)];
                        if ((offset.X % lumaTransformSize.GetWidth()) != 0 || (offset.Y % lumaTransformSize.GetHeight()) != 0)
                        {
                            continue;
                        }

                        if ((x << subX) >= offset.X && (x << subX) < offset.X + lumaTransformSize.GetWidth() &&
                            (y << subY) >= offset.Y && (y << subY) < offset.Y + lumaTransformSize.GetHeight())
                        {
                            lumaState = lumaStates[lumaIndex];
                            break;
                        }

                        lumaIndex++;
                    }

                    Av1TransformType transformType = lumaState.EndOfBlock == 0 || !lumaState.TransformType.IsExtendedSetUsed(transformSet)
                        ? Av1TransformType.DctDct
                        : lumaState.TransformType;
                    Span<byte> top = topContexts.Slice(x >> 2, transformSize.Get4x4WideCount());
                    Span<byte> left = leftContexts.Slice(y >> 2, transformSize.Get4x4HighCount());
                    Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
                        Av1ComponentType.Chroma,
                        top,
                        left,
                        planeBlockSize,
                        transformSize);

                    int inputOffset = (y * width) + x;
                    int coefficientOffset = transformIndex * sampleCount;
                    Span<int> coefficients = selectedCoefficients.Slice(coefficientOffset, sampleCount);
                    this.EvaluateInterTransform(
                        writer,
                        plane,
                        Av1ComponentType.Chroma,
                        predictionMode,
                        planeOrigin + new Size(x, y),
                        transformSize,
                        transformType,
                        context,
                        prediction[inputOffset..],
                        workspace.Residual[inputOffset..],
                        width,
                        costLimit - currentCost,
                        workspace.TransformReconstruction,
                        workspace.TransformCoefficients,
                        selectedReconstruction.Slice(coefficientOffset, sampleCount),
                        coefficients,
                        out Av1EncoderTransformBlockState state,
                        out int transformRate,
                        out long transformDistortion,
                        out long transformPredictionDistortion);

                    if (!lossless)
                    {
                        states[transformIndex] = state;
                    }

                    transformIndex++;
                    hasCoefficients |= state.EndOfBlock != 0;
                    rate += transformRate;
                    distortion += transformDistortion;
                    predictionDistortion += transformPredictionDistortion;
                    long codedCost = Av1RateDistortion.GetCost(this.rateMultiplier, transformRate, transformDistortion);
                    long skippedCost = Av1RateDistortion.GetCost(this.rateMultiplier, 0, transformPredictionDistortion);
                    currentCost += Math.Min(codedCost, skippedCost);

                    byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                        coefficients,
                        transformSize,
                        state.TransformType,
                        state.EndOfBlock);

                    Av1TileWriter.UpdateCoefficientContexts(
                        top,
                        left,
                        coefficientContext,
                        planeOrigin + new Size(x, y),
                        frameContextSize);
                }
            }

            return true;
        }

        /// <summary>
        /// Builds the complete plane prediction and residual in contiguous block rows.
        /// </summary>
        private void PrepareInterPlanePrediction(
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1Plane plane,
            Av1PredictionMode predictionMode,
            Av1ReferenceFrameType primaryReferenceFrame,
            Av1ReferenceFrameType secondaryReferenceFrame,
            bool usePreparedPrediction,
            Av1CompoundType compoundType,
            int compoundWedgeIndex,
            bool compoundWedgeSign,
            Av1DifferenceWeightedMaskType differenceWeightedMaskType,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Buffer2DRegion<TSample> referencePlane,
            Buffer2DRegion<TSample> secondaryReferencePlane,
            Point lumaOrigin,
            int subsamplingX,
            int subsamplingY,
            Av1BlockSize blockSize,
            Span<TSample> prediction,
            Span<short> residual)
        {
            Point planeOrigin = new(lumaOrigin.X >> subsamplingX, lumaOrigin.Y >> subsamplingY);
            int sourceColumnQ4 = (planeOrigin.X << 4) + (vector.Column << (1 - subsamplingX));
            int sourceRowQ4 = (planeOrigin.Y << 4) + (vector.Row << (1 - subsamplingY));
            Point predictionOrigin = new(sourceColumnQ4 >> 4, sourceRowQ4 >> 4);
            Av1BlockSize predictionSize = blockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
            int sampleCount = predictionSize.GetWidth() * predictionSize.GetHeight();
            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            if (usePreparedPrediction)
            {
                TOperator.SubtractPrediction(
                    sourcePlane, planeOrigin, prediction[..sampleCount], residual[..sampleCount], predictionSize.GetWidth(), predictionSize.GetHeight());
            }
            else if (predictionMode >= Av1PredictionMode.CompoundInterModeStart)
            {
                int firstWeight = 8;
                int secondWeight = 8;
                if (compoundType == Av1CompoundType.DistanceWeighted)
                {
                    Av1CompoundDistanceWeights.Derive(
                        this.picture.Sequence.SequenceHeader.OrderHintInfo,
                        this.picture.Parent.FrameHeader,
                        primaryReferenceFrame,
                        secondaryReferenceFrame,
                        out firstWeight,
                        out secondWeight);
                }

                int secondarySourceColumnQ4 = (planeOrigin.X << 4) + (secondaryVector.Column << (1 - subsamplingX));
                int secondarySourceRowQ4 = (planeOrigin.Y << 4) + (secondaryVector.Row << (1 - subsamplingY));
                this.blockWorkspace.GetCompoundPredictionIntermediates(
                    out Span<ushort> firstIntermediate,
                    out Span<ushort> secondIntermediate);

                TOperator.PrepareCompoundInterPrediction(
                    sourcePlane,
                    planeOrigin,
                    referencePlane,
                    predictionOrigin,
                    sourceColumnQ4 & 15,
                    sourceRowQ4 & 15,
                    secondaryReferencePlane,
                    new Point(secondarySourceColumnQ4 >> 4, secondarySourceRowQ4 >> 4),
                    secondarySourceColumnQ4 & 15,
                    secondarySourceRowQ4 & 15,
                    horizontalFilter,
                    verticalFilter,
                    prediction[..sampleCount],
                    residual[..sampleCount],
                    firstIntermediate,
                    secondIntermediate,
                    this.blockWorkspace.GetCompoundPredictionMask(),
                    this.blockWorkspace.GetInterPredictionWorkspace<TSample>().PredictionScratch,
                    predictionSize,
                    this.bitDepth,
                    plane,
                    blockSize,
                    compoundType,
                    firstWeight,
                    secondWeight,
                    subsamplingX,
                    subsamplingY,
                    compoundWedgeIndex,
                    compoundWedgeSign,
                    differenceWeightedMaskType);
            }
            else if (predictionMode >= Av1PredictionMode.InterModeStart)
            {
                Span<short> predictionScratch = this.blockWorkspace
                    .GetInterPredictionWorkspace<TSample>()
                    .PredictionScratch;

                // Reference-frame modes use the complete interpolation pipeline even when the current zero-phase
                // global vector reduces to a SIMD copy. Later fractional vectors therefore share decoder arithmetic.
                TOperator.PrepareTranslationalInterPrediction(
                    sourcePlane,
                    planeOrigin,
                    referencePlane,
                    predictionOrigin,
                    horizontalFilter,
                    verticalFilter,
                    sourceColumnQ4 & 15,
                    sourceRowQ4 & 15,
                    prediction,
                    residual,
                    predictionScratch,
                    predictionSize,
                    this.picture.Sequence.SequenceHeader.ColorConfig.BitDepth);
            }
            else
            {
                // Intra-block copy has its own bilinear half-sample rules and reads the current reconstruction.
                TOperator.PrepareIntraBlockCopyPrediction(
                    sourcePlane,
                    planeOrigin,
                    referencePlane,
                    predictionOrigin,
                    (sourceColumnQ4 & 15) != 0,
                    (sourceRowQ4 & 15) != 0,
                    prediction[..sampleCount],
                    residual[..sampleCount],
                    predictionSize);
            }
        }

        /// <summary>
        /// Evaluates transform types over strided prediction and residual samples at one transform origin.
        /// </summary>
        private void EvaluateInterTransform(
            Av1SymbolEncoder writer,
            Av1Plane plane,
            Av1ComponentType componentType,
            Av1PredictionMode predictionMode,
            Point planeOrigin,
            Av1TransformSize transformSize,
            Av1TransformType transformTypeSelection,
            Av1TransformBlockContext blockContext,
            ReadOnlySpan<TSample> prediction,
            ReadOnlySpan<short> residual,
            int inputStride,
            long costLimit,
            Span<TSample> transformReconstruction,
            Span<int> transformCoefficients,
            Span<TSample> selectedReconstruction,
            Span<int> selectedCoefficients,
            out Av1EncoderTransformBlockState selectedState,
            out int selectedRate,
            out long selectedDistortion,
            out long predictionDistortion)
        {
            if (this.picture.Parent.FrameHeader.CodedLossless)
            {
                transformTypeSelection = Av1TransformType.DctDct;
            }

            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            int sampleCount = transformSize.GetSize2d();

            // Prediction-only error remains available even when every transform quantizes to nonzero coefficients.
            // Normalize squared sample precision with rounding before adding four fractional distortion bits.
            int width = transformSize.GetWidth();
            int visibleWidth = Math.Min(width, sourcePlane.Width - planeOrigin.X);
            int visibleHeight = Math.Min(transformSize.GetHeight(), sourcePlane.Height - planeOrigin.Y);
            long predictionSquaredError = 0;
            if (visibleWidth == inputStride)
            {
                predictionSquaredError = Av1ResidualBuilder.SumSquares(residual[..(width * visibleHeight)]);
            }
            else
            {
                for (int row = 0; row < visibleHeight; row++)
                {
                    predictionSquaredError += Av1ResidualBuilder.SumSquares(residual.Slice(row * inputStride, visibleWidth));
                }
            }

            int normalizationShift = (this.bitDepth.GetBitCount() - 8) * 2;
            predictionDistortion = normalizationShift == 0
                ? predictionSquaredError << 4
                : ((predictionSquaredError + (1L << (normalizationShift - 1))) >> normalizationShift) << 4;

            // Motion compensation and subtraction are shared by all transform types for this prediction.
            Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
                transformSize,
                isInter: true,
                this.picture.Parent.FrameHeader.UseReducedTransformSet);

            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            if (plane == Av1Plane.Y && transformTypeSelection == Av1TransformType.AllTransformTypes &&
                this.blockWorkspace.EvaluationStage == Av1EncoderEvaluationStage.Candidate)
            {
                int threshold = settings.InterTransformTypeProbabilityThreshold;
                if (threshold == 0)
                {
                    transformTypeSelection = Av1TransformType.DctDct;
                }
                else if (threshold != int.MaxValue)
                {
                    int probabilityOffset = ((int)this.picture.Parent.FrameUpdateType * Av1TransformTypeProbabilities.FrameLength) +
                        ((int)transformSize * Av1TransformTypeProbabilities.TypeCount);
                    ReadOnlySpan<int> probabilities = this.blockWorkspace.TransformTypeProbabilities.Slice(
                        probabilityOffset, Av1TransformTypeProbabilities.TypeCount);

                    if (probabilities[0] > threshold)
                    {
                        transformTypeSelection = Av1TransformType.DctDct;
                    }
                    else
                    {
                        const int alternateTypeThresholdOffset = 100;
                        int bestType = 0;
                        int bestProbability = 0;
                        for (int type = 1; type < Av1TransformTypeProbabilities.TypeCount; type++)
                        {
                            if (probabilities[type] > bestProbability)
                            {
                                bestProbability = probabilities[type];
                                bestType = type;
                            }
                        }

                        if (bestProbability > threshold + alternateTypeThresholdOffset)
                        {
                            transformTypeSelection = (Av1TransformType)bestType;
                        }
                    }
                }
            }

            InlineArray16<Av1TransformType> transformOrder = default;
            ushort allowedMask = 0;
            int allowedCount = 0;
            for (int index = 0; index < 16; index++)
            {
                Av1TransformType type = (Av1TransformType)index;
                transformOrder[index] = type;
                if (type.IsExtendedSetUsed(transformSetType) &&
                    (transformTypeSelection == Av1TransformType.AllTransformTypes || type == transformTypeSelection))
                {
                    allowedMask |= (ushort)(1 << index);
                    allowedCount++;
                }
            }

            if (plane == Av1Plane.Y && transformTypeSelection == Av1TransformType.AllTransformTypes &&
                settings.TransformTypeProbabilityPruning != 0 && allowedCount > 1)
            {
                int probabilityOffset = ((int)this.picture.Parent.FrameUpdateType * Av1TransformTypeProbabilities.FrameLength) +
                    ((int)transformSize * Av1TransformTypeProbabilities.TypeCount);
                allowedMask = Av1TransformTypeProbabilities.Prune(
                    this.blockWorkspace.TransformTypeProbabilities.Slice(probabilityOffset, Av1TransformTypeProbabilities.TypeCount),
                    allowedMask,
                    settings.TransformTypeProbabilityPruning,
                    this.picture.Parent.FrameUpdateType);
                allowedCount = System.Numerics.BitOperations.PopCount((uint)allowedMask);
            }

            int pruningLevel = this.blockWorkspace.EvaluationStage switch
            {
                Av1EncoderEvaluationStage.Candidate => settings.CandidateInterTransformTypePruning,
                Av1EncoderEvaluationStage.Winner => settings.WinnerInterTransformTypePruning,
                _ => settings.DefaultInterTransformTypePruning
            };
            int minimumCandidates = pruningLevel >= 4 ? 1 : 5;
            if (plane == Av1Plane.Y && settings.EstimateTransformTypeRateDistortion && allowedCount > 2)
            {
                allowedMask = this.PruneTransformTypesByEstimatedCost(
                    writer,
                    residual,
                    inputStride,
                    transformSize,
                    blockContext,
                    predictionMode,
                    Av1FilterIntraMode.AllFilterIntraModes,
                    true,
                    allowedMask,
                    pruningLevel,
                    costLimit,
                    transformCoefficients,
                    transformOrder);
            }
            else if (plane == Av1Plane.Y && pruningLevel > 0 && allowedCount > minimumCandidates)
            {
                allowedMask = PruneInterTransformTypes(
                    residual, inputStride, transformSize, transformSetType, pruningLevel, allowedMask, transformOrder);
            }

            // A forced type can be absent from the legal set for this size. Preserve the canonical fallback.
            if (allowedMask == 0)
            {
                allowedMask = 1;
            }

            long bestCost = long.MaxValue;
            selectedState = default;
            selectedRate = 0;
            selectedDistortion = 0;

            // Alternate candidate and best spans on improvement. The winning storage stays intact during
            // later trials, with at most one normalization copy into the caller's destination after the search.
            Span<TSample> candidateReconstruction = transformReconstruction[..sampleCount];
            Span<int> candidateCoefficients = transformCoefficients[..sampleCount];
            Span<TSample> bestReconstruction = selectedReconstruction[..sampleCount];
            Span<int> bestCoefficients = selectedCoefficients[..sampleCount];
            bool bestUsesSelectedStorage = true;
            for (int transformIndex = 0; transformIndex < 16; transformIndex++)
            {
                Av1TransformType transformType = transformOrder[transformIndex];
                if (transformType == Av1TransformType.Invalid || (allowedMask & (1 << (int)transformType)) == 0)
                {
                    continue;
                }

                Av1EncoderTransformBlockState candidateState = default;
                long candidateDistortion = TOperator.EncodePredictionCandidate(
                    this.blockWorkspace,
                    writer,
                    blockContext,
                    this.rateMultiplier,
                    true,
                    this.picture.Sequence.SequenceHeader.IsStillPicture,
                    sourcePlane,
                    planeOrigin,
                    prediction,
                    residual,
                    inputStride,
                    candidateReconstruction,
                    transformSize.GetWidth(),
                    candidateCoefficients,
                    transformSize,
                    transformType,
                    plane,
                    this.quantization.QIndex[0],
                    this.quantization.DeltaQDc[(int)plane],
                    this.quantization.DeltaQAc[(int)plane],
                    this.bitDepth,
                    ref candidateState,
                    out _);

                int candidateRate = writer.GetCoefficientCost(
                    transformSize,
                    transformType,
                    predictionMode,
                    candidateCoefficients,
                    componentType,
                    blockContext,
                    candidateState.EndOfBlock,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet,
                    Av1FilterIntraMode.AllFilterIntraModes,
                    usesInterTransformSet: true);

                long candidateCost = Av1RateDistortion.GetCost(
                    this.rateMultiplier,
                    candidateRate,
                    candidateDistortion);

                if (candidateCost < bestCost)
                {
                    Span<TSample> previousBestReconstruction = bestReconstruction;
                    bestReconstruction = candidateReconstruction;
                    candidateReconstruction = previousBestReconstruction;

                    Span<int> previousBestCoefficients = bestCoefficients;
                    bestCoefficients = candidateCoefficients;
                    candidateCoefficients = previousBestCoefficients;
                    bestUsesSelectedStorage = !bestUsesSelectedStorage;
                    bestCost = candidateCost;
                    selectedState = candidateState;
                    selectedRate = candidateRate;
                    selectedDistortion = candidateDistortion;
                }
            }

            // Callers retain the designated selected spans after this scratch workspace is reused by the
            // next plane or motion vector, so normalize only when the final best result occupies scratch.
            if (!bestUsesSelectedStorage)
            {
                bestReconstruction.CopyTo(selectedReconstruction);
                bestCoefficients.CopyTo(selectedCoefficients);
            }
        }
    }
}

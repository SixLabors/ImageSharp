// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;
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
        /// Gets a value indicating whether the image tune biases inter costs toward intra prediction. Reference: the
        /// AOM_TUNE_IQ test of adjust_rdcost() and adjust_cost().
        /// </summary>
        private readonly bool BiasesInterCosts => this.blockWorkspace.EncoderOptions.Tuning.IsImageTuning();

        /// <summary>
        /// Gets a value indicating whether 8-bit sharpness 3 adds a charge to a block that is smoother than its source.
        /// A high bit depth uses different rules. Intra frames, golden frames and alternate reference frames get no charge.
        /// Reference: the sharpness and frame_is_kf_gf_arf() tests of adjust_rdcost() and adjust_cost(), after their high bit depth branch.
        /// </summary>
        private readonly bool ChargesSmoothing =>
            this.blockWorkspace.EncoderOptions.Sharpness == 3 && !this.UsesHighBitDepthSharpness && !this.picture.Parent.FrameHeader.IsIntra &&
            this.picture.Parent.FrameUpdateType is not (Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate);

        /// <summary>
        /// Gets a value indicating whether the high bit depth sharpness 3 rules apply.
        /// Reference: the xd->bd &gt; 8 &amp;&amp; sharpness == 3 tests of adjust_rdcost(), adjust_cost(), av1_get_tx_skip_dist() and their callers.
        /// </summary>
        private readonly bool UsesHighBitDepthSharpness => this.bitDepth.GetBitCount() > 8 && this.blockWorkspace.EncoderOptions.Sharpness == 3;

        /// <summary>
        /// Gets a value indicating whether high bit depth sharpness 3 adds a texture-loss charge to a block cost.
        /// Only intra frames get no charge. Golden frames and alternate reference frames get the charge.
        /// Reference: the frame_is_intra_only() return of the high bit depth branch of adjust_rdcost() and adjust_cost().
        /// </summary>
        private readonly bool ChargesHighBitDepthTextureLoss => this.UsesHighBitDepthSharpness && !this.picture.Parent.FrameHeader.IsIntra;

        /// <summary>
        /// Gets a value indicating whether high bit depth sharpness 3 adds a penalty to a skipped transform.
        /// Intra frames, golden frames and alternate reference frames get no penalty.
        /// Reference: the frame_is_kf_gf_arf() tests of av1_is_skip_txfm_penalized() and av1_get_tx_skip_dist().
        /// </summary>
        private readonly bool PenalizesHighBitDepthSkip =>
            this.UsesHighBitDepthSharpness && !this.picture.Parent.FrameHeader.IsIntra &&
            this.picture.Parent.FrameUpdateType is not (Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate);

        /// <summary>
        /// Gets the Q12 mode threshold multiplier of the block, from 2.5 at quantizer zero to 1 at quantizer 255 when
        /// skippable inter modes are pruned, else 1. Reference: mode_threshold_mul_factor[x->qindex] in
        /// av1_rd_pick_inter_mode(), with the table's nearest-integer rounding.
        /// </summary>
        private readonly int ModeThresholdSkipMultiplier => this.picture.Parent.SpeedSettings.PruneSkippableInterModes
            ? 10240 - (((this.blockQIndex * 6144) + 127) / 255)
            : 4096;

        /// <summary>
        /// Gets the quantizer scale of the mode thresholds of the block's segment. Reference: the segment_id index of
        /// rd->threshes in av1_rd_pick_inter_mode().
        /// </summary>
        private readonly int ModeThresholdQuantizerFactor => this.blockWorkspace.ModeThresholdQuantizerFactors[this.blockSegmentId];

        /// <summary>
        /// Compares legal same-frame displacements with the retained intra winner.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="regularStatistics">The rate and distortion of the intra winner.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        /// <returns>The rate and distortion of the winner.</returns>
        private Av1RateDistortionStatistics SelectIntraBlockCopy(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
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
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                macroBlock,
                modeInfoPosition,
                blockSize,
                modeInfo.Block.PartitionType,
                referenceCandidates,
                referenceWeights);

            InlineArray2<Av1MotionVector> candidatesStorage = default;
            Span<Av1MotionVector> candidates = candidatesStorage;
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
                this.blockQIndex,
                this.rateMultiplier,
                this.picture.Parent.MotionSearchStepParameter,
                settings,
                this.blockWorkspace.GetMotionSearchSites(settings.GetFullPixelMethod(blockSize), this.reconstruction.GetPlane(Av1Plane.Y).Stride),
                candidates);

            Av1RateDistortionStatistics selectedStatistics = regularStatistics;
            Av1MotionVector selectedVector = default;
            InlineArray128<Av1EncoderTransformBlockState> selectedStatesStorage = default;
            Span<Av1EncoderTransformBlockState> selectedStates = selectedStatesStorage;
            InlineArray16<Av1TransformSize> selectedSizes = default;
            bool selected = false;
            bool selectedSkip = false;
            this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
            ref Av1ReferenceMotionVectors referenceMotionVectors = ref this.blockWorkspace.ReferenceMotionVectors;

            // Every candidate signals the copy flag with the same rate.
            int useCopyRate = Av1SymbolEncoder.GetUseIntraBlockCopyCost(tables.ModeCosts, true);
            for (int index = 0; index < candidateCount; index++)
            {
                Av1MotionVector vector = candidates[index];
                int predictionRate = useCopyRate + costs.GetDisplacementVectorCost(vector, reference);
                Av1RateDistortionStatistics statistics = this.EvaluateInterCandidate(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    in motionVectorCosts,
                    transformPrediction,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
                    blockOrigin,
                    blockSize,
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
                    referenceMotionVectors,
                    interWorkspace.LumaCandidateReconstruction,
                    interWorkspace.LumaCandidateCoefficients,
                    interWorkspace.BlueCandidateReconstruction,
                    interWorkspace.BlueCandidateCoefficients,
                    interWorkspace.RedCandidateReconstruction,
                    interWorkspace.RedCandidateCoefficients,
                    out bool skip,
                    out InlineArray64<Av1EncoderTransformBlockState> lumaStates,
                    out InlineArray16<Av1TransformSize> sizes,
                    out InlineArray16<Av1EncoderTransformBlockState> blueStates,
                    out InlineArray16<Av1EncoderTransformBlockState> redStates);

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
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    encoderSegmentMap,
                    previousSegmentMap,
                    macroBlock,
                    blockOrigin,
                    modeInfo,
                    block,
                    selectedVector,
                    default,
                    selectedStates);

                this.picture.SetDisplacementVector(displacementVectors, modeInfoPosition, selectedVector);
            }

            return selectedStatistics;
        }

        /// <summary>
        /// Returns whether a zero-vector global mode whose references all have identity models is dropped because
        /// its prediction error exceeds that of the best new vectors of the references, by a quarter at level one.
        /// A compound mode compares the sums over both references. Reference: prune_zero_mv_with_sse().
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="primaryReference">The first reference.</param>
        /// <param name="secondaryReference">The second reference, or <see cref="Av1ReferenceFrameType.None"/>.</param>
        /// <returns><see langword="true"/> when the mode is dropped.</returns>
        private readonly bool PrunesZeroVectorWithSse(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType primaryReference,
            Av1ReferenceFrameType secondaryReference = Av1ReferenceFrameType.None)
        {
            int level = this.picture.Parent.SpeedSettings.ZeroVectorSsePruningLevel;
            if (level == 0)
            {
                return false;
            }

            int referenceCount = secondaryReference > Av1ReferenceFrameType.Intra ? 2 : 1;
            for (int index = 0; index < referenceCount; index++)
            {
                Av1ReferenceFrameType reference = index == 0 ? primaryReference : secondaryReference;
                if (this.picture.Parent.FrameHeader.GetGlobalMotionParameters()[(int)reference - 1].Type != Av1GlobalMotionType.Identity ||
                    this.bestSingleReferenceSses[(int)reference] == int.MaxValue)
                {
                    return false;
                }
            }

            // The identity model predicts with the reference block at the same place, and the variance function of
            // the block size measures its error, rounded down to 8-bit precision. The sums wrap as unsigned values.
            // Reference: the fn_ptr[bsize].vf call.
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            int shift = 2 * (this.bitDepth.GetBitCount() - 8);
            uint sseSum = 0;
            uint bestSseSum = 0;
            ReadOnlySpan<Av1EncoderFrame<TSample>> references = this.references.Span;
            ReadOnlySpan<TSample> sourceBlock = Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin);
            for (int index = 0; index < referenceCount; index++)
            {
                Av1ReferenceFrameType reference = index == 0 ? primaryReference : secondaryReference;
                Av1PlaneRegion<TSample> referencePlane = references[(int)reference].CodedView.GetPlane(Av1Plane.Y);
                TOperator.GetMoments(
                    sourceBlock,
                    sourcePlane.Stride,
                    Av1TransformBlockEncoder.GetPlaneSpan(referencePlane, this.GetReferenceBlockOrigin(reference, blockOrigin)),
                    referencePlane.Stride,
                    blockSize.GetWidth(),
                    blockSize.GetHeight(),
                    out _,
                    out long squares);

                sseSum += shift == 0 ? (uint)squares : (uint)((squares + (1L << (shift - 1))) >> shift);
                bestSseSum += this.bestSingleReferenceSses[(int)reference];
            }

            double multiplier = level > 1 ? 1.00 : 1.25;
            return sseSum > multiplier * bestSseSum;
        }

        /// <summary>
        /// Evaluates reference-frame modes and retains the winning syntax and transform choices.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="selectedVector">The motion vector of the winner.</param>
        /// <param name="selectedSecondaryVector">The second motion vector of a compound winner.</param>
        /// <param name="selectedStates">The transform states of the winner.</param>
        /// <returns>The rate and distortion of the winner.</returns>
        private Av1RateDistortionStatistics SelectInterBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<Av1EncoderReferenceContext> referenceContexts,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
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
            this.useWarpedPrediction = false;
            this.useObmcPrediction = false;
            this.motionModeWinnerCount = 0;
            this.motionModeWinnerLimit = this.picture.Parent.SpeedSettings.GetMotionModeWinnerCount(this.picture.Parent.FrameUpdateType);
            this.interSourceVarianceCost = (long)this.interSourceVariance * blockSize.GetWidth() * blockSize.GetHeight() * 128;
            Span<long> topAverageCosts = stackalloc long[5];
            topAverageCosts.Fill(long.MaxValue);

            // The saved wedge index, wedge sign and difference-weighted mask type are shared by every compound
            // reference pair of the block; the last compound type is kept per pair. Reference: wedge_index,
            // wedge_sign, diffwtd_index and cmp_mode[] of HandleInterModeArgs in av1_rd_pick_inter_mode().
            Span<int> compoundMaskHistory = stackalloc int[64];
            compoundMaskHistory.Fill(-1);
            modeInfo.Block.MotionMode = Av1MotionMode.SimpleTranslation;
            Av1MacroBlockModeInfo initialModeInfo = modeInfo;
            Av1EncoderBlockStruct initialBlock = block;
            InlineArray8<Av1ReferenceMotionVectors> singleReferenceVectorsStorage = default;
            InlineArray8<int> interIntraModesStorage = default;
            InlineArray8<long> bestSingleCostsStorage = default;
            InlineArray8<Av1PredictionMode> bestSingleModesStorage = default;
            Span<Av1ReferenceMotionVectors> singleReferenceVectors = singleReferenceVectorsStorage;
            Span<int> interIntraModes = interIntraModesStorage;
            Span<long> bestSingleCosts = bestSingleCostsStorage;
            Span<Av1PredictionMode> bestSingleModes = bestSingleModesStorage;
            bestSingleCosts[..].Fill(long.MaxValue);
            bestSingleModes[..].Fill(Av1PredictionMode.PredictionModeCount);
            interIntraModes[..].Fill(-1);
            InlineArray8<InlineArray3<Av1MotionVector>> newMotionVectors = default;
            InlineArray8<byte> newMotionVectorMasksStorage = default;
            Span<byte> newMotionVectorMasks = newMotionVectorMasksStorage;
            uint searchedSingleModes = 0;

            // Reference: the best_single_sse_in_refs reset of init_inter_mode_search_state().
            this.bestSingleReferenceSses[..].Fill(int.MaxValue);

            // Reference: the x->pred_sse reset at the start of av1_rd_pick_inter_mode().
            this.predictionSses[..].Fill(int.MaxValue);
            this.leftoverInterEstimate = Av1RateDistortionStatistics.Invalid;
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
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    macroBlock,
                    modeInfoPosition,
                    blockSize,
                    initialModeInfo.Block.PartitionType,
                    this.picture.Sequence.SequenceHeader,
                    this.picture.Parent.FrameHeader,
                    reference,
                    Av1ReferenceFrameType.None);
            }

            this.skipReferenceFrameMask = this.GetSkipReferenceFrameMask(blockOrigin, blockSize, initialModeInfo.Block.PartitionType);
            this.SetInterModeSkipMasks(blockOrigin, blockSize, singleReferenceVectors[..]);
            this.PrepareTplInterModePruning(blockOrigin, blockSize);

            // Search the same syntax mode across the available references before advancing to the
            // next mode. NEWMV's complete DRL search is retained for the later compound candidates.
            ReadOnlySpan<Av1PredictionMode> modeOrder =
            [
                Av1PredictionMode.NearestMotionVector, Av1PredictionMode.NewMotionVector,
                Av1PredictionMode.NearMotionVector, Av1PredictionMode.GlobalMotionVector
            ];

            Span<int> modeThresholdFactors = this.blockWorkspace.ModeThresholdFactors;
            Span<long> singleReferenceFilterCosts = this.blockWorkspace.SingleReferenceFilterCosts;
            foreach (Av1PredictionMode mode in modeOrder)
            {
                foreach (Av1ReferenceFrameType reference in referenceOrder)
                {
                    // Reference: the skip_txfm reset at the start of each iteration of the av1_rd_pick_inter_mode()
                    // mode loop.
                    this.transformSearchSkip = false;
                    int index = (int)reference;

                    // A reference that the cached decision of an asymmetric sub-block needs is not removed by the
                    // partition's reference mask, and a mode the cache leaves searches motion modes only when the
                    // cache holds that single-reference mode itself.
                    int cacheDecision = this.GetInterModeCacheDecision(mode, reference, Av1ReferenceFrameType.None);
                    bool skipMotionModes = cacheDecision == 2 ||
                        (cacheDecision == 0 && (this.skipReferenceFrameMask & (1 << index)) != 0);

                    if ((availableReferences & (1 << index)) == 0 ||
                        (this.interModeSkipMasks[index] & (1u << (int)mode)) != 0 ||
                        (cacheDecision == 0 && this.IsSingleReferenceSkipped(index)) ||
                        this.PrunesReferenceBySelectiveReferenceFrame(reference, Av1ReferenceFrameType.None))
                    {
                        continue;
                    }

                    // The last argument is the search's all-empty result, not the skip choice of the block.
                    // Reference: the best_mode_skippable of the search state, which update_search_state() takes
                    // from the skip_txfm of the winner's RD_STATS.
                    if (Av1ModeThresholds.ShouldSkip(
                        modeThresholdFactors,
                        this.ModeThresholdQuantizerFactor,
                        this.ModeThresholdSkipMultiplier,
                        blockSize,
                        mode,
                        reference,
                        Av1ReferenceFrameType.None,
                        Math.Min(this.blockCostLimit, selectedStatistics.Cost),
                        selectedStatistics.AllTransformsEmpty))
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

                        long previousCost = singleReferenceFilterCosts[previousIndex];
                        int context = singleReferenceVectors[index].ModeContext;
                        if (previousCost != long.MaxValue &&
                            Av1SymbolEncoder.GetInterModeCost(tables.ModeCosts, mode, context) >
                            Av1SymbolEncoder.GetInterModeCost(tables.ModeCosts, compareMode, context))
                        {
                            // Identical vectors have identical prediction error. Retain the modeled cost
                            // for compound comparisons, but do not search the more expensive single syntax.
                            int currentIndex = (((int)mode - (int)Av1PredictionMode.InterModeStart) * 3 *
                                Av1Constants.ReferenceFrameCount) + (int)reference;

                            singleReferenceFilterCosts[currentIndex] = previousCost;
                            continue;
                        }
                    }

                    // The cache test follows the repeated-vector test, and a single mode kept for a cached compound
                    // passes the neighbor test unconditionally. Reference: the order of
                    // inter_mode_search_order_independent_skip(), which returns before prune_nearmv_using_neighbors.
                    if (cacheDecision == 1)
                    {
                        continue;
                    }

                    if (mode == Av1PredictionMode.NearMotionVector && cacheDecision != 2 &&
                        this.ShouldPruneNearMode(
                            modeInfoGrid,
                            modeInfoAllocation,
                            macroBlock,
                            reference,
                            Av1ReferenceFrameType.None,
                            Math.Min(this.blockCostLimit, selectedStatistics.Cost)))
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

                    if (mode == Av1PredictionMode.GlobalMotionVector && this.PrunesZeroVectorWithSse(blockOrigin, blockSize, reference))
                    {
                        continue;
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

                    this.transformSearchReset = false;
                    Av1RateDistortionStatistics candidateStatistics = this.SelectSingleReferenceMode(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        macroBlock,
                        blockOrigin,
                        Math.Min(this.blockCostLimit, selectedStatistics.Cost),
                        reference,
                        mode,
                        ref singleReferenceVectors[index],
                        ref interIntraModes[index],
                        this.motionModeWinnerLimit == 0 && !skipMotionModes,
                        ref candidateModeInfo,
                        ref candidateBlock,
                        out Av1MotionVector candidateVector,
                        out InlineArray128<Av1EncoderTransformBlockState> candidateStates,
                        ref newVectors,
                        ref newVectorMask);

                    // A completed search leaves the skip flag of its best motion mode. Reference: the best_xskip_txfm
                    // restore at the end of handle_inter_mode().
                    if (candidateStatistics.Cost != long.MaxValue)
                    {
                        this.transformSearchSkip = candidateModeInfo.Block.Skip;
                    }
                    else if (this.transformSearchReset)
                    {
                        // A search whose every entry failed leaves the flag its trials cleared.
                        this.transformSearchSkip = false;
                    }

                    if (candidateStatistics.Cost < bestSingleCosts[(int)reference])
                    {
                        bestSingleCosts[(int)reference] = candidateStatistics.Cost;
                        bestSingleModes[(int)reference] = mode;
                    }

                    // A reference kept only because a compound pair uses it searches no motion mode.
                    // Reference: the skip_motion_mode result of inter_mode_search_order_independent_skip().
                    if (!skipMotionModes)
                    {
                        this.RecordMotionModeWinner(candidateStatistics.Cost, false, candidateModeInfo, candidateBlock, candidateVector);
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

            // The compound iterations of the mode loop follow the single ones, and each clears the skip flag.
            if (this.picture.Parent.FrameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect)
            {
                this.transformSearchSkip = false;
            }

            // After every single-reference mode, a pair is searched only when one of its references is within ten
            // percent of the best single reference. Reference: find_top_ref() and in_single_ref_cutoff().
            long singleReferenceCutoff = long.MaxValue;
            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                singleReferenceCutoff = Math.Min(singleReferenceCutoff, bestSingleCosts[reference]);
            }

            if (singleReferenceCutoff != long.MaxValue)
            {
                singleReferenceCutoff = 110 * singleReferenceCutoff / 100;
            }

            // An alternate reference frame of the base layer searches no compound mode at the speeds that skip them.
            // Reference: the skip_arf_compound test of inter_mode_search_order_independent_skip().
            bool skipsCompound = this.picture.Parent.PrunesAllCompoundReferences ||
                (this.picture.Parent.SpeedSettings.SkipAlternateReferenceCompound &&
                 this.picture.Parent.FrameUpdateType == Av1FrameUpdateType.Alternate);

            // Nearest pairs establish a bound across all admitted references first. The remaining
            // motion families then complete each pair in order, retaining that pair's mask history.
            for (int phase = 0; !skipsCompound && phase < 2; phase++)
            {
                for (int index = 0; index < compoundSearchOrder.Length; index++)
                {
                    int pairIndex = phase == 0 ? index : compoundSearchOrder[index];
                    bool outsideSingleReferenceCutoff = this.picture.Parent.SpeedSettings.PruneCompoundUsingSingleReference &&
                        bestSingleCosts[(int)compoundReferences[pairIndex * 2]] > singleReferenceCutoff &&
                        bestSingleCosts[(int)compoundReferences[(pairIndex * 2) + 1]] > singleReferenceCutoff;

                    this.SelectCompoundBlock(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in interWorkspace,
                        in motionVectorCosts,
                        transformPrediction,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        referenceContexts,
                        macroBlock,
                        blockOrigin,
                        compoundReferences[pairIndex * 2],
                        compoundReferences[(pairIndex * 2) + 1],
                        phase == 0,
                        topAverageCosts,
                        compoundMaskHistory[..3],
                        compoundMaskHistory.Slice(3 + pairIndex, 1),
                        outsideSingleReferenceCutoff,
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

            this.EvaluateMotionModeWinners(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                macroBlock,
                blockOrigin,
                singleReferenceVectors[..],
                ref modeInfo,
                ref block,
                ref selectedStatistics,
                ref selectedVector,
                ref selectedSecondaryVector,
                ref selectedStates);

            // The winner's luma cost gates the intra modes, unless the retained candidates are searched below.
            this.interLumaThreshold = selectedStatistics.LumaCost;
            if (this.estimateInterCandidates)
            {
                Av1RateDistortionStatistics bestEstimate = selectedStatistics;
                selectedStatistics = this.SearchRetainedInterCandidates(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
                    blockOrigin,
                    ref modeInfo,
                    ref block,
                    out selectedVector,
                    out selectedSecondaryVector,
                    out selectedStates,
                    out this.interLumaThreshold);

                if (selectedStatistics.Cost == long.MaxValue)
                {
                    this.leftoverInterEstimate = bestEstimate;
                }
            }

            return selectedStatistics;
        }

        /// <summary>
        /// Returns whether the frame drops a compound pair. When the references lie on both sides of the frame, a pair
        /// with both references on one side is dropped, and from selective level four a pair with ALTREF2 is dropped
        /// when BWDREF is a nearer future reference. Reference: the second branch of setup_prune_ref_frame_mask(),
        /// which prune_ref_frame() reads.
        /// </summary>
        /// <param name="first">The first reference of the pair.</param>
        /// <param name="second">The second reference of the pair.</param>
        /// <returns><see langword="true"/> when the pair is not searched.</returns>
        private bool PrunesCompoundReferencePair(Av1ReferenceFrameType first, Av1ReferenceFrameType second)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            int level = parent.SpeedSettings.SelectiveReferenceFrameLevel;
            if (level < 1)
            {
                return false;
            }

            int[] distances = parent.ReferenceDistances;
            if (!parent.AllOneSidedReferences && (distances[(int)first] > 0) == (distances[(int)second] > 0))
            {
                return true;
            }

            int alternate2Distance = distances[(int)Av1ReferenceFrameType.Alternate2];
            int backwardDistance = distances[(int)Av1ReferenceFrameType.Backward];
            return level >= 4 &&
                (first == Av1ReferenceFrameType.Alternate2 || second == Av1ReferenceFrameType.Alternate2) &&
                (parent.AvailableReferenceMask & (1 << (int)Av1ReferenceFrameType.Backward)) != 0 &&
                alternate2Distance > 0 && backwardDistance > 0 && backwardDistance <= alternate2Distance;
        }

        /// <summary>
        /// Returns the combined type of a reference or reference pair. A pair of a forward and a backward reference
        /// indexes the forward-by-backward table, and a pair on one side indexes the unidirectional pairs after it.
        /// Reference: av1_ref_frame_type() with get_uni_comp_ref_idx().
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference, or none for a single reference.</param>
        /// <returns>The combined reference type.</returns>
        private static int GetReferenceFrameType(Av1ReferenceFrameType first, Av1ReferenceFrameType second)
        {
            if (second <= Av1ReferenceFrameType.Intra)
            {
                return (int)first;
            }

            for (int index = 0; index < 9; index++)
            {
                (Av1ReferenceFrameType pairFirst, Av1ReferenceFrameType pairSecond) = GetUnidirectionalPair(index);
                if (first == pairFirst && second == pairSecond)
                {
                    return Av1Constants.ReferenceFrameCount + (4 * 3) + index;
                }
            }

            return Av1Constants.ReferenceFrameCount + ((int)first - (int)Av1ReferenceFrameType.Last) +
                (((int)second - (int)Av1ReferenceFrameType.Backward) * 4);
        }

        /// <summary>
        /// Returns the reference pair of a combined compound type. Reference: ref_frame_map.
        /// </summary>
        /// <param name="type">The combined reference type, at least the single-reference count.</param>
        /// <returns>The two references.</returns>
        private static (Av1ReferenceFrameType First, Av1ReferenceFrameType Second) GetReferenceFramePair(int type)
        {
            int index = type - Av1Constants.ReferenceFrameCount;
            if (index >= 4 * 3)
            {
                return GetUnidirectionalPair(index - (4 * 3));
            }

            return ((Av1ReferenceFrameType)((int)Av1ReferenceFrameType.Last + (index % 4)),
                (Av1ReferenceFrameType)((int)Av1ReferenceFrameType.Backward + (index / 4)));
        }

        /// <summary>
        /// Returns a unidirectional compound pair. Reference: comp_ref0() and comp_ref1().
        /// </summary>
        /// <param name="index">The pair index, from zero to eight.</param>
        /// <returns>The two references.</returns>
        private static (Av1ReferenceFrameType First, Av1ReferenceFrameType Second) GetUnidirectionalPair(int index)
            => index switch
            {
                0 => (Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last2),
                1 => (Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last3),
                2 => (Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Golden),
                3 => (Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Alternate),
                4 => (Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Last3),
                5 => (Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Golden),
                6 => (Av1ReferenceFrameType.Last3, Av1ReferenceFrameType.Golden),
                7 => (Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Alternate2),
                _ => (Av1ReferenceFrameType.Alternate2, Av1ReferenceFrameType.Alternate)
            };

        /// <summary>
        /// Returns the largest full-sample magnitude of the first two reference predictors, which sets the block's
        /// spatial search range. Clamping at the last potentially visible interpolation tap bounds padded reads
        /// without changing their prediction. Reference: x->max_mv_context from av1_find_best_ref_mvs().
        /// </summary>
        private static int GetSpatialMotionMagnitude(
            Av1ReferenceMotionVectors referenceMotionVectors,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Size frameSize)
        {
            int spatialMagnitude = 0;
            for (int index = 0; index < 2; index++)
            {
                Av1MotionVector spatial = referenceMotionVectors.GetNewReference(index);
                int column = Math.Clamp(spatial.Column, -(blockOrigin.X + blockSize.GetWidth() + 4) * 8, (frameSize.Width - blockOrigin.X + 4) * 8);
                int row = Math.Clamp(spatial.Row, -(blockOrigin.Y + blockSize.GetHeight() + 4) * 8, (frameSize.Height - blockOrigin.Y + 4) * 8);
                spatialMagnitude = Math.Max(spatialMagnitude, Math.Max(Math.Abs(row), Math.Abs(column)) >> 3);
            }

            return spatialMagnitude;
        }

        /// <summary>
        /// Evaluates a motion mode other than simple translation for a single-reference prediction, from the
        /// decisions of its simple-translation result. OBMC re-searches a new vector against its blended target, and
        /// warped motion refines one against its local model. A pruned mode, an invalid model, or a new vector equal
        /// to its reference returns an invalid result. Reference: the OBMC_CAUSAL and WARPED_CAUSAL iterations of the
        /// mode_index loop of motion_mode_rd().
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the block entry read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction workspace, which the block entry read once.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the OBMC motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="motionMode">The motion mode to evaluate.</param>
        /// <param name="lastAllowed">The last motion mode the block may code.</param>
        /// <param name="baseRate">The prediction rate without the motion mode and filter syntax.</param>
        /// <param name="costLimit">The cost above which the transform search stops.</param>
        /// <param name="hasChroma">Whether the block codes chroma.</param>
        /// <param name="referenceIndex">The dynamic reference list index of the prediction.</param>
        /// <param name="referenceMotionVectors">The reference vector list of the reference frame.</param>
        /// <param name="candidate">The block decisions. The motion mode and, for warped motion, the filters are set.</param>
        /// <param name="vector">The motion vector, which a new vector search may change.</param>
        /// <param name="lumaReconstruction">The luma reconstruction scratch.</param>
        /// <param name="lumaCoefficients">The luma coefficient scratch.</param>
        /// <param name="blueReconstruction">The blue reconstruction scratch.</param>
        /// <param name="blueCoefficients">The blue coefficient scratch.</param>
        /// <param name="redReconstruction">The red reconstruction scratch.</param>
        /// <param name="redCoefficients">The red coefficient scratch.</param>
        /// <param name="skip">Whether the result codes no residual.</param>
        /// <param name="lumaStates">The luma transform states.</param>
        /// <param name="lumaSizes">The luma transform sizes.</param>
        /// <param name="blueStates">The blue transform states.</param>
        /// <param name="redStates">The red transform states.</param>
        /// <returns>The result, or an invalid result.</returns>
        private Av1RateDistortionStatistics EvaluateMotionMode(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1MotionMode motionMode,
            Av1MotionMode lastAllowed,
            int baseRate,
            long costLimit,
            bool hasChroma,
            int referenceIndex,
            Av1ReferenceMotionVectors referenceMotionVectors,
            ref Av1EncoderBlockModeInfo candidate,
            ref Av1MotionVector vector,
            Span<TSample> lumaReconstruction,
            Span<int> lumaCoefficients,
            Span<TSample> blueReconstruction,
            Span<int> blueCoefficients,
            Span<TSample> redReconstruction,
            Span<int> redCoefficients,
            out bool skip,
            out InlineArray64<Av1EncoderTransformBlockState> lumaStates,
            out InlineArray16<Av1TransformSize> lumaSizes,
            out InlineArray16<Av1EncoderTransformBlockState> blueStates,
            out InlineArray16<Av1EncoderTransformBlockState> redStates)
        {
            skip = false;
            lumaStates = default;
            lumaSizes = default;
            blueStates = default;
            redStates = default;
            this.unbiasedInterTrialCost = long.MaxValue;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Av1BlockSize blockSize = candidate.BlockSize;

            // Sharpness 3 searches neither OBMC nor warped motion. Reference: the sharpness test of the mode_index
            // loop of motion_mode_rd().
            if (this.blockWorkspace.EncoderOptions.Sharpness == 3)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            candidate.MotionMode = motionMode;
            int motionModeRate;
            int filterRate = 0;
            if (motionMode == Av1MotionMode.Obmc)
            {
                // A block size that rarely picks OBMC does not search it. Reference: the prune_obmc test of
                // motion_mode_rd().
                if (this.picture.Parent.ObmcProbabilities[(int)blockSize] < this.picture.Parent.SpeedSettings.ObmcProbabilityThreshold)
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                this.SetWarpedPrediction(macroBlock, blockOrigin, candidate.PartitionType, candidate, vector);
                if (candidate.Mode == Av1PredictionMode.NewMotionVector)
                {
                    // A new vector is searched again against the OBMC target, and one equal to its reference
                    // codes no difference. Reference: the av1_single_motion_search() call of motion_mode_rd(),
                    // then av1_check_newmv_joint_nonzero().
                    Av1MotionVector referenceVector = referenceMotionVectors.GetNewReference(referenceIndex);
                    Size frameSize = new(
                        this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                        this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

                    int spatialMagnitude = GetSpatialMotionMagnitude(referenceMotionVectors, blockOrigin, blockSize, frameSize);
                    vector = this.SearchObmcVector(
                        motionSearchPrediction,
                        interWorkspace.FilterRows,
                        in motionVectorCosts,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        blockOrigin,
                        blockSize,
                        candidate.ReferenceFrame,
                        vector,
                        referenceVector,
                        spatialMagnitude);

                    if (vector == referenceVector)
                    {
                        this.useWarpedPrediction = false;
                        this.useObmcPrediction = false;
                        return Av1RateDistortionStatistics.Invalid;
                    }
                }

                motionModeRate = lastAllowed == Av1MotionMode.Warped
                    ? tables.ModeCosts.GetMotionMode(blockSize, Av1MotionMode.Obmc)
                    : tables.ModeCosts.GetObmc(blockSize, Av1MotionMode.Obmc);

                if (Av1TileWriter.UsesSwitchableInterpolation(frameHeader, candidate))
                {
                    filterRate = Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(
                        tables.ModeCosts,
                        candidate.VerticalInterpolationFilter,
                        Av1SymbolContextHelper.GetSwitchableInterpolationContext(candidate, modeInfoGrid, modeInfoAllocation, macroBlock, 0));

                    if (this.picture.Sequence.SequenceHeader.EnableDualFilter)
                    {
                        filterRate += Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(
                            tables.ModeCosts,
                            candidate.HorizontalInterpolationFilter,
                            Av1SymbolContextHelper.GetSwitchableInterpolationContext(candidate, modeInfoGrid, modeInfoAllocation, macroBlock, 1));
                    }
                }
            }
            else
            {
                // A warped block signals no filter and predicts any small chroma plane with the frame's fixed filter.
                // Reference: av1_unswitchable_filter().
                Av1InterpolationFilter warpedFilter = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable
                    ? Av1InterpolationFilter.Regular
                    : frameHeader.InterpolationFilter;

                candidate.HorizontalInterpolationFilter = warpedFilter;
                candidate.VerticalInterpolationFilter = warpedFilter;
                if (!this.SetWarpedPrediction(macroBlock, blockOrigin, candidate.PartitionType, candidate, vector))
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                if (candidate.Mode == Av1PredictionMode.NewMotionVector)
                {
                    Av1MotionVector referenceVector = referenceMotionVectors.GetNewReference(referenceIndex);
                    vector = this.RefineWarpedVector(in interWorkspace, in motionVectorCosts, macroBlock, blockOrigin, candidate, vector, referenceVector);

                    // A new vector equal to its reference codes no difference. Reference: av1_check_newmv_joint_nonzero().
                    if (vector == referenceVector)
                    {
                        this.useWarpedPrediction = false;
                        this.useObmcPrediction = false;
                        return Av1RateDistortionStatistics.Invalid;
                    }
                }

                motionModeRate = tables.ModeCosts.GetMotionMode(blockSize, Av1MotionMode.Warped);
            }

            this.transformSearchReset = true;
            Av1RateDistortionStatistics statistics = this.EvaluateInterCandidate(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in interWorkspace,
                in motionVectorCosts,
                transformPrediction,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                macroBlock,
                blockOrigin,
                blockSize,
                costLimit,
                hasChroma,
                baseRate + motionModeRate + filterRate,
                candidate.ReferenceFrame,
                vector,
                default,
                candidate.Mode,
                Av1ReferenceFrameType.None,
                usePreparedPrediction: false,
                Av1CompoundType.Average,
                0,
                false,
                Av1DifferenceWeightedMaskType.Type38,
                0,
                candidate.HorizontalInterpolationFilter,
                candidate.VerticalInterpolationFilter,
                referenceIndex,
                false,
                default,
                false,
                0,
                referenceMotionVectors,
                lumaReconstruction,
                lumaCoefficients,
                blueReconstruction,
                blueCoefficients,
                redReconstruction,
                redCoefficients,
                out skip,
                out lumaStates,
                out lumaSizes,
                out blueStates,
                out redStates);

            this.useWarpedPrediction = false;
            this.useObmcPrediction = false;
            return statistics;
        }

        /// <summary>
        /// Retains a mode-loop result for the motion-mode search after the loop. Reference: the handle_winner_cand()
        /// call of av1_rd_pick_inter_mode().
        /// </summary>
        /// <param name="cost">The rate-distortion cost of the result.</param>
        /// <param name="isCompound">Whether the result predicts from two references.</param>
        /// <param name="modeInfo">The block decisions of the result.</param>
        /// <param name="block">The block state of the result.</param>
        /// <param name="vector">The motion vector of the result.</param>
        private void RecordMotionModeWinner(
            long cost,
            bool isCompound,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector)
        {
            if (this.motionModeWinnerLimit == 0 || cost == long.MaxValue)
            {
                return;
            }

            Av1MotionModeWinner winner = new()
            {
                Cost = cost,
                IsCompound = isCompound,
                ModeInfo = modeInfo,
                Block = block,
                Vector = vector
            };

            Av1MotionModeWinner.Insert(this.motionModeWinners[..], ref this.motionModeWinnerCount, this.motionModeWinnerLimit, winner);
        }

        /// <summary>
        /// Searches the motion modes other than simple translation for the single-reference winners of the mode
        /// loop. Reference: evaluate_motion_mode_for_winner_candidates(), with the WARPED_CAUSAL branch of
        /// motion_mode_rd().
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="singleReferenceVectors">The reference vector lists of the single references.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="selectedStatistics">The rate and distortion of the winner.</param>
        /// <param name="selectedVector">The motion vector of the winner.</param>
        /// <param name="selectedSecondaryVector">The second motion vector of a compound winner.</param>
        /// <param name="selectedStates">The transform states of the winner.</param>
        private void EvaluateMotionModeWinners(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ReadOnlySpan<Av1ReferenceMotionVectors> singleReferenceVectors,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1RateDistortionStatistics selectedStatistics,
            ref Av1MotionVector selectedVector,
            ref Av1MotionVector selectedSecondaryVector,
            ref InlineArray128<Av1EncoderTransformBlockState> selectedStates)
        {
            if (this.motionModeWinnerCount == 0)
            {
                return;
            }

            this.evaluatingMotionModeWinners = true;

            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            ObuSequenceHeader sequenceHeader = this.picture.Sequence.SequenceHeader;
            Point position = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Span<byte> referenceCounts = stackalloc byte[Av1Constants.ReferenceFrameCount];
            Av1TileWriter.CollectNeighborReferenceCounts(modeInfoGrid, modeInfoAllocation, macroBlock, referenceCounts);
            int intraInterContext = Av1TileWriter.GetIntraInterContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int intraInterRate = Av1SymbolEncoder.GetIsInterCost(tables.ModeCosts, isInter: true, intraInterContext);
            Av1ModeCosts modeCosts = tables.ModeCosts;
            for (int index = 0; index < this.motionModeWinnerCount; index++)
            {
                Av1MotionModeWinner winner = this.motionModeWinners[index];
                if (winner.IsCompound)
                {
                    continue;
                }

                // Reference: the skip_txfm reset of each candidate in evaluate_motion_mode_for_winner_candidates().
                this.transformSearchSkip = false;

                Av1MotionMode lastAllowed = Av1EncoderMotionVariation.GetLastAllowedMotionMode(
                    this.picture, macroBlock, position, winner.ModeInfo.Block);

                long candidateBestCost = long.MaxValue;
                bool candidateBestSkip = false;

                // A completed trial bounds the later trials of the candidate by its cost before the image tune bias.
                // Reference: the ref_best_rd update of motion_mode_rd().
                long candidateTrialCost = long.MaxValue;

                // OBMC first, then warped motion, each from the simple-translation decisions of the winner.
                // Reference: the mode_index loop of motion_mode_rd() from update_mode_start_end_index().
                for (Av1MotionMode motionMode = Av1MotionMode.Obmc; motionMode <= lastAllowed; motionMode++)
                {
                    Av1MacroBlockModeInfo candidateModeInfo = winner.ModeInfo;
                    Av1EncoderBlockStruct candidateBlock = winner.Block;
                    ref Av1EncoderBlockModeInfo candidate = ref candidateModeInfo.Block;
                    Av1BlockSize blockSize = candidate.BlockSize;
                    Av1MotionVector vector = winner.Vector;

                    // The prediction syntax of the simple-translation result, without its motion mode and filters.
                    int baseRate = intraInterRate + Av1SymbolEncoder.GetSingleReferenceCost(tables.ModeCosts, candidate.ReferenceFrame, referenceCounts);
                    if (frameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect &&
                        Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8)
                    {
                        baseRate += modeCosts.GetCompInter(Av1SymbolContextHelper.GetReferenceModeContext(modeInfoGrid, modeInfoAllocation, macroBlock), 0);
                    }

                    if (sequenceHeader.EnableInterIntraCompound && blockSize is >= Av1BlockSize.Block8x8 and <= Av1BlockSize.Block32x32)
                    {
                        baseRate += Av1SymbolEncoder.GetInterIntraCost(tables.ModeCosts, blockSize, false, default, false, 0);
                    }

                    long costLimit = Math.Min(this.blockCostLimit, selectedStatistics.Cost);
                    Av1RateDistortionStatistics statistics = this.EvaluateMotionMode(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        macroBlock,
                        blockOrigin,
                        motionMode,
                        lastAllowed,
                        baseRate,
                        Math.Min(costLimit, candidateTrialCost),
                        candidateBlock.HasChroma,
                        candidateBlock.ReferenceMotionVectorIndex,
                        singleReferenceVectors[(int)candidate.ReferenceFrame],
                        ref candidate,
                        ref vector,
                        interWorkspace.LumaCandidateReconstruction,
                        interWorkspace.LumaCandidateCoefficients,
                        interWorkspace.BlueCandidateReconstruction,
                        interWorkspace.BlueCandidateCoefficients,
                        interWorkspace.RedCandidateReconstruction,
                        interWorkspace.RedCandidateCoefficients,
                        out bool skip,
                        out InlineArray64<Av1EncoderTransformBlockState> lumaStates,
                        out InlineArray16<Av1TransformSize> lumaSizes,
                        out InlineArray16<Av1EncoderTransformBlockState> blueStates,
                        out InlineArray16<Av1EncoderTransformBlockState> redStates);

                    candidateTrialCost = Math.Min(candidateTrialCost, this.unbiasedInterTrialCost);
                    if (statistics.Cost < candidateBestCost)
                    {
                        candidateBestCost = statistics.Cost;
                        candidateBestSkip = skip;
                    }

                    if (statistics.Cost >= costLimit)
                    {
                        continue;
                    }

                    // Setting the block transform size resets every transform size, so the searched tree is copied after it.
                    candidate.Skip = skip;
                    candidate.TransformSize = frameHeader.CodedLossless ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize();
                    lumaSizes[..].CopyTo(candidate.InterTransformSizes);
                    lumaStates[..].CopyTo(selectedStates);
                    blueStates[..].CopyTo(selectedStates[64..80]);
                    redStates[..].CopyTo(selectedStates[80..96]);
                    modeInfo = candidateModeInfo;
                    block = candidateBlock;
                    selectedStatistics = statistics;
                    selectedVector = vector;
                    selectedSecondaryVector = default;
                }

                // The best motion mode of the candidate leaves its skip flag. Reference: the best_xskip_txfm restore at the
                // end of motion_mode_rd().
                if (candidateBestCost != long.MaxValue)
                {
                    this.transformSearchSkip = candidateBestSkip;
                }
            }

            this.evaluatingMotionModeWinners = false;
        }

        /// <summary>
        /// Refines the new vector of a warped block in unit steps, re-deriving the warped model at each step and
        /// keeping the vector with the smallest luma variance plus vector cost. The retained model is left in
        /// <see cref="warpedModel"/>. Reference: av1_refine_warped_mv() with compute_motion_cost().
        /// </summary>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="mode">The block decisions, including the partition type.</param>
        /// <param name="vector">The searched vector, whose warped model is the current one.</param>
        /// <param name="referenceVector">The reference of the new vector.</param>
        /// <returns>The refined vector.</returns>
        private Av1MotionVector RefineWarpedVector(
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1EncoderBlockModeInfo mode,
            Av1MotionVector vector,
            Av1MotionVector referenceVector)
        {
            // The diamond visits the four edge neighbors and the square adds the corners. A neighbor that the last
            // step made redundant is skipped. Reference: warp_search_info.
            ReadOnlySpan<sbyte> rows = [0, 1, 0, -1, 1, 1, -1, -1];
            ReadOnlySpan<sbyte> columns = [-1, 0, 1, 0, -1, 1, -1, 1];
            ReadOnlySpan<byte> diamondMasks = [0b1011, 0b0111, 0b1110, 0b1101];
            ReadOnlySpan<byte> squareMasks = [0b01010001, 0b00110010, 0b10100100, 0b11001000, 0b01110011, 0b10110110, 0b11011001, 0b11101100];
            bool diamond = this.picture.Parent.SpeedSettings.Speed >= HeifEncodingSpeed.Level5;
            int neighborCount = diamond ? 4 : 8;
            ReadOnlySpan<byte> masks = diamond ? diamondMasks : squareMasks;
            const int iterations = 8;

            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            int step = frameHeader.AllowHighPrecisionMotionVector ? 1 : 2;
            Av1BlockSize blockSize = mode.BlockSize;
            Point position = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Span<Point> sourcePoints = stackalloc Point[Av1EncoderMotionVariation.MaximumSampleCount];
            Span<Point> referencePoints = stackalloc Point[Av1EncoderMotionVariation.MaximumSampleCount];
            int count = Av1EncoderMotionVariation.FindSamples(this.picture, macroBlock, position, mode, sourcePoints, referencePoints);

            Av1PlaneRegion<TSample> referencePlane = this.references.Span[(int)mode.ReferenceFrame].CodedView.GetPlane(Av1Plane.Y);
            Size frameSize = new(
                this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

            Rectangle bounds = referenceVector.GetSubpixelSearchBounds(Av1MotionVector.GetFrameSearchBounds(
                new Rectangle(blockOrigin, new Size(blockSize.GetWidth(), blockSize.GetHeight())),
                frameSize,
                Math.Min(referencePlane.Bounds.X, referencePlane.Bounds.Y)));

            Av1GlobalMotionParameters bestModel = this.warpedModel;
            long bestCost = this.GetWarpedMotionCost(
                in interWorkspace,
                in motionVectorCosts,
                blockOrigin,
                blockSize,
                mode.ReferenceFrame,
                vector,
                referenceVector);

            int validNeighbors = 0xFF;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int bestIndex = -1;
                for (int index = 0; index < neighborCount; index++)
                {
                    if ((validNeighbors & (1 << index)) == 0)
                    {
                        continue;
                    }

                    Av1MotionVector candidate = new(vector.Row + (rows[index] * step), vector.Column + (columns[index] * step));
                    if (!bounds.Contains(candidate.Column, candidate.Row))
                    {
                        continue;
                    }

                    Av1GlobalMotionParameters model = Av1GlobalMotionParameters.DeriveLocalProjection(
                        sourcePoints[..count], referencePoints[..count], blockSize, candidate, position);

                    if (model.IsInvalid)
                    {
                        continue;
                    }

                    this.warpedModel = model;
                    long cost = this.GetWarpedMotionCost(
                        in interWorkspace,
                        in motionVectorCosts,
                        blockOrigin,
                        blockSize,
                        mode.ReferenceFrame,
                        candidate,
                        referenceVector);

                    if (cost < bestCost)
                    {
                        bestIndex = index;
                        bestModel = model;
                        bestCost = cost;
                    }
                }

                if (bestIndex == -1)
                {
                    break;
                }

                vector = new Av1MotionVector(vector.Row + (rows[bestIndex] * step), vector.Column + (columns[bestIndex] * step));
                validNeighbors = masks[bestIndex];
            }

            this.warpedModel = bestModel;
            return vector;
        }

        /// <summary>
        /// Measures the luma variance of the current warped prediction plus the cost of its vector.
        /// Reference: compute_motion_cost().
        /// </summary>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="referenceFrame">The reference frame of the prediction.</param>
        /// <param name="vector">The motion vector to measure.</param>
        /// <param name="referenceVector">The reference of the new vector.</param>
        /// <returns>The luma variance plus the vector cost.</returns>
        private long GetWarpedMotionCost(
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType referenceFrame,
            Av1MotionVector vector,
            Av1MotionVector referenceVector)
        {
            Av1EncoderFrame<TSample> reference = this.references.Span[(int)referenceFrame];
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            Span<TSample> prediction = interWorkspace.LumaPrediction[..(width * height)];
            TOperator.PrepareWarpedInterPrediction(
                reference.CodedView.GetPlane(Av1Plane.Y),
                reference.Width,
                reference.Height,
                blockOrigin,
                width,
                height,
                0,
                0,
                this.warpedModel,
                prediction,
                interWorkspace.FilterRows,
                this.bitDepth);

            // The prediction is the first operand, so high-bit-depth rounding matches the reference variance.
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            TOperator.GetMoments(
                prediction,
                width,
                Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin),
                sourcePlane.Stride,
                width,
                height,
                out int sum,
                out long squares);

            int precisionShift = this.bitDepth.GetBitCount() - 8;
            if (precisionShift != 0)
            {
                sum = (sum + (1 << (precisionShift - 1))) >> precisionShift;
                int squaredShift = precisionShift * 2;
                squares = (squares + (1L << (squaredShift - 1))) >> squaredShift;
            }

            long variance = Math.Max(squares - (((long)sum * sum) / (width * height)), 0);
            return variance + Av1RateDistortion.GetMotionSearchCost(this.rateMultiplier, motionVectorCosts.GetCost(vector, referenceVector), 0);
        }

        /// <summary>
        /// Runs complete transform search on ranked predictions before publishing the inter winner.
        /// Reference: tx_search_best_inter_candidates().
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the block entry read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction workspace, which the block entry read once.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block, which also holds the inter-intra prediction.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound or inter-intra prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor context of the block.</param>
        /// <param name="blockOrigin">The luma origin of the block.</param>
        /// <param name="modeInfo">The block decisions, which receive the winner.</param>
        /// <param name="block">The block coding state, which receives the winner.</param>
        /// <param name="selectedVector">The first vector of the winner.</param>
        /// <param name="selectedSecondaryVector">The second vector of the winner.</param>
        /// <param name="selectedStates">The transform states of the winner.</param>
        /// <param name="lumaThreshold">
        /// The luma cost of the cheapest candidate whose transform search completed, even one above the block budget,
        /// or the maximum when none completed. Reference: the *yrd of best_rd_in_this_partition.
        /// </param>
        /// <returns>The winner, or invalid when no candidate is below the block budget.</returns>
        private Av1RateDistortionStatistics SearchRetainedInterCandidates(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            out Av1MotionVector selectedVector,
            out Av1MotionVector selectedSecondaryVector,
            out InlineArray128<Av1EncoderTransformBlockState> selectedStates,
            out long lumaThreshold)
        {
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
            lumaThreshold = long.MaxValue;
            long bestPartitionCost = long.MaxValue;
            this.estimateInterCandidates = false;
            this.searchingRetainedCandidates = true;
            Span<Av1InterModeCandidate> candidates = this.blockWorkspace.InterModeCandidates[..this.interCandidateCount];
            candidates.Sort();
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            int candidateCount = Math.Min(candidates.Length, settings.MaximumInterTransformCandidates);
            long firstEstimate = candidateCount == 0 ? long.MaxValue : candidates[0].EstimatedCost;
            Av1RateDistortionStatistics selected = Av1RateDistortionStatistics.Invalid;
            selectedVector = default;
            selectedSecondaryVector = default;
            selectedStates = default;
            InlineArray36<int> searchedModesStorage = default;
            Span<int> searchedModes = searchedModesStorage;
            int searchCount = 0;
            bool searchedNewMotion = false;
            ReadOnlySpan<Av1EncoderFrame<TSample>> references = this.references.Span;
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

                // Reference: the skip_txfm reset of each candidate in tx_search_best_inter_candidates().
                this.transformSearchSkip = false;
                if (!this.ShouldSearchInterTransforms(candidate))
                {
                    continue;
                }

                // Recreate only the predictions that reach transform search. Inter-intra owns its complete
                // three-plane blend; other modes use the same plane builder as the initial estimation pass.
                if (predictionModeInfo.SecondaryReferenceFrame == Av1ReferenceFrameType.Intra)
                {
                    this.PrepareInterIntraPrediction(
                        transformWorkspace,
                        in interWorkspace,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        compoundMask,
                        modeInfoGrid,
                        modeInfoAllocation,
                        macroBlock,
                        blockOrigin,
                        predictionModeInfo.BlockSize,
                        block.HasChroma,
                        predictionModeInfo.Mode,
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
                    this.SetWarpedPrediction(macroBlock, blockOrigin, modeInfo.Block.PartitionType, predictionModeInfo, candidate.Vector);
                    Av1EncoderFrame<TSample>.PlanarView primaryReference = references[(int)predictionModeInfo.ReferenceFrame].CodedView;
                    int planeCount = block.HasChroma ? 3 : 1;
                    for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                    {
                        Av1Plane plane = (Av1Plane)planeIndex;
                        int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                        int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                        Av1BlockSize planeSize = predictionModeInfo.BlockSize.GetSubsampled(subX != 0, subY != 0);
                        Span<TSample> prediction = planeIndex == 0 ? interWorkspace.LumaPrediction :
                            planeIndex == 1 ? interWorkspace.BluePrediction : interWorkspace.RedPrediction;

                        Av1PlaneRegion<TSample> secondaryReference = predictionModeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                            ? references[(int)predictionModeInfo.SecondaryReferenceFrame].CodedView.GetPlane(plane)
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
                            interWorkspace.Residual,
                            interWorkspace.FilterRows,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors);
                    }

                    // The rebuilt luma goes into pd->dst, which is the frame. Reference: av1_enc_build_inter_predictor() in
                    // tx_search_best_inter_candidates().
                    if (!this.useObmcPrediction && !this.useWarpedPrediction)
                    {
                        this.WriteInterLumaDestination(lumaFrame, blockOrigin, predictionModeInfo.BlockSize, interWorkspace.LumaPrediction);
                    }
                }

                searchCount++;
                searchedModes[modeIndex]++;
                searchedNewMotion |= mode is Av1PredictionMode.NewMotionVector or Av1PredictionMode.NewNewMotionVector or
                    Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector or
                    Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NewNearestMotionVector;

                Av1RateDistortionStatistics statistics = this.EvaluatePreparedInterCandidate(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    transformPrediction,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
                    blockOrigin,
                    Math.Min(this.blockCostLimit, selected.Cost),
                    block.HasChroma,
                    candidate,
                    interWorkspace.LumaCandidateReconstruction,
                    interWorkspace.LumaCandidateCoefficients,
                    interWorkspace.BlueCandidateReconstruction,
                    interWorkspace.BlueCandidateCoefficients,
                    interWorkspace.RedCandidateReconstruction,
                    interWorkspace.RedCandidateCoefficients,
                    out bool skip,
                    out InlineArray64<Av1EncoderTransformBlockState> lumaStates,
                    out InlineArray16<Av1TransformSize> sizes,
                    out InlineArray16<Av1EncoderTransformBlockState> blueStates,
                    out InlineArray16<Av1EncoderTransformBlockState> redStates);

                this.useWarpedPrediction = false;
                this.useObmcPrediction = false;
                if (statistics.Cost == long.MaxValue)
                {
                    continue;
                }

                // Every completed search competes for the intra threshold, whether or not it fits the budget.
                if (statistics.Cost < bestPartitionCost)
                {
                    bestPartitionCost = statistics.Cost;
                    lumaThreshold = statistics.LumaCost;
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
                            candidateCount = Math.Min(candidateCount, limits[(5 * this.blockQIndex) >> 8]);
                        }
                        else if (predictionModeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                        {
                            ReadOnlySpan<int> limits = settings.InterModeTransformBreakout == 1 ? [10, 7, 5, 4] : [10, 7, 5, 3];
                            candidateCount = Math.Min(candidateCount, limits[(4 * this.blockQIndex) >> 8]);
                        }
                    }
                }

                if (searchCount > settings.InterModeCandidateLimit && searchedNewMotion)
                {
                    break;
                }
            }

            this.searchingRetainedCandidates = false;
            return selected;
        }

        /// <summary>
        /// Re-evaluates a selected prediction's residual while retaining its prediction syntax rate.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="vector">The motion vector of the winner.</param>
        /// <param name="secondaryVector">The second motion vector of a compound winner.</param>
        /// <param name="selectedStatistics">The rate and distortion of the winner.</param>
        /// <param name="selectedStates">The transform states of the winner.</param>
        private void RefineInterTransformSize(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
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
                int originalFilterRate = Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(
                    tables.ModeCosts,
                    modeInfo.Block.VerticalInterpolationFilter,
                    Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo.Block, modeInfoGrid, modeInfoAllocation, macroBlock, 0));

                if (this.picture.Sequence.SequenceHeader.EnableDualFilter)
                {
                    originalFilterRate += Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(
                        tables.ModeCosts,
                        modeInfo.Block.HorizontalInterpolationFilter,
                        Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo.Block, modeInfoGrid, modeInfoAllocation, macroBlock, 1));
                }

                // This search ranks luma only. Chroma is prepared below after the selected luma tree
                // supplies its transform types, so no temporary chroma prediction needs to survive here.
                preparedPrediction = this.SelectInterFilters(
                    in tables,
                    in interWorkspace,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
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
                this.PrepareSelectedInterLumaPrediction(
                    transformWorkspace,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
                    blockOrigin,
                    candidateModeInfo,
                    block,
                    vector,
                    secondaryVector);
            }

            InlineArray64<Av1EncoderTransformBlockState> candidateStates = default;
            candidateModeInfo.Block.TransformSize = maximumTransformSize;
            candidateModeInfo.Block.Skip = false;
            Av1RateDistortionStatistics candidateStatistics = this.EvaluateInterLumaTree(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in interWorkspace,
                transformPrediction,
                in transformEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                ref candidateModeInfo.Block,
                candidateStates,
                long.MaxValue,
                out int stateCount);

            if (candidateStatistics.Cost == long.MaxValue)
            {
                return;
            }

            Av1RateDistortionStatistics lumaStatistics = candidateStatistics;

            // Chroma must use the type at its own luma origin after the tree changes.
            Av1RateDistortionStatistics chromaStatistics = this.EvaluateRefinedInterChroma(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in interWorkspace,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                macroBlock,
                blockOrigin,
                candidateModeInfo,
                block,
                vector,
                secondaryVector,
                candidateStates[..stateCount],
                out InlineArray16<Av1EncoderTransformBlockState> blueStates,
                out InlineArray16<Av1EncoderTransformBlockState> redStates);

            candidateStatistics.Add(this.rateMultiplier, chromaStatistics);
            int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            Av1ModeCosts modeCosts = tables.ModeCosts;
            int noSkipRate = Av1SymbolEncoder.GetSkipCost(modeCosts, false, skipContext);
            int skipRate = Av1SymbolEncoder.GetSkipCost(modeCosts, true, skipContext);
            bool allEmpty = !candidateStatistics.HasCoefficients;

            // Skipping removes the entire transform tree, including every partition and coefficient
            // symbol. Compare that complete syntax once both luma and chroma have been evaluated. An empty
            // residual still codes as non-skip when its skip flag costs more. Any sharpness keeps the residual of a
            // compound prediction. Reference: the skip_blk test of refine_winner_mode_tx().
            bool compound = candidateModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra;

            // High bit depth sharpness 3 adds a skip penalty to the comparison only.
            // Reference: the av1_get_tx_skip_dist() call before the skip_blk test of refine_winner_mode_tx().
            long comparedCodedDistortion = candidateStatistics.Distortion;
            long comparedSkipDistortion = candidateStatistics.PredictionDistortion;
            if (this.UsesHighBitDepthSharpness)
            {
                this.PenalizeTransformSkip(
                    blockOrigin, modeInfo.Block.BlockSize, interWorkspace.LumaPrediction, ref comparedCodedDistortion, ref comparedSkipDistortion);
            }

            bool skip = (this.blockWorkspace.EncoderOptions.Sharpness == 0 || !compound) &&
                Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, comparedSkipDistortion) <
                Av1RateDistortion.GetCost(this.rateMultiplier, noSkipRate + candidateStatistics.Rate, comparedCodedDistortion);

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

            if (this.UsesHighBitDepthSharpness)
            {
                bool refinedSkip = modeInfo.Block.Skip || skip;
                this.ChargeRefinedInterTextureLoss(ref refinedStatistics, blockOrigin, modeInfo.Block, vector, interWorkspace.LumaPrediction, refinedSkip);
            }

            // The winner is priced again from its rate and distortion, which drops the cost bias of the image tune but
            // keeps its distortion bias. Reference: the best_rd of refine_winner_mode_tx().
            if (refinedStatistics.Cost >= Av1RateDistortion.GetCost(this.rateMultiplier, selectedStatistics.Rate, selectedStatistics.Distortion))
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
        /// Sets the cost of a refined inter winner at high bit depth sharpness 3, as the reference does. The cost starts at zero.
        /// The image tunes add one eighth to the distortion and to the zero cost. Else the texture-loss charge sets a cost only when its offset is positive.
        /// A refined winner with a cost of zero always replaces the selection.
        /// Reference: the tmp_rd_stats branch of refine_winner_mode_tx(). Its av1_init_rd_stats() sets rdcost to zero before adjust_rdcost().
        /// </summary>
        /// <param name="statistics">The refined statistics.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="block">The mode information of the winner.</param>
        /// <param name="vector">The first motion vector of the winner.</param>
        /// <param name="prediction">The luma prediction of the winner.</param>
        /// <param name="skip">True when the winner or its refinement skips the transform.</param>
        private readonly void ChargeRefinedInterTextureLoss(
            ref Av1RateDistortionStatistics statistics,
            Point blockOrigin,
            Av1EncoderBlockModeInfo block,
            Av1MotionVector vector,
            ReadOnlySpan<TSample> prediction,
            bool skip)
        {
            statistics.Cost = 0;
            if (this.BiasesInterCosts)
            {
                statistics.AddInterCostBias();
                return;
            }

            if (!this.ChargesHighBitDepthTextureLoss)
            {
                return;
            }

            bool compound = block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra;
            bool interIntra = block.SecondaryReferenceFrame == Av1ReferenceFrameType.Intra;
            this.GetVarianceStatistics(blockOrigin, block.BlockSize, prediction, block.BlockSize.GetWidth(), out long sourceVariance, out long sampleVariance);
            long offset = GetTextureLossOffset(sourceVariance, sampleVariance, block.BlockSize, skip, compound || interIntra, !compound, vector);
            this.ChargeTextureLoss(ref statistics, 0, offset, blockOrigin, block.BlockSize, true, compound, false);
        }

        /// <summary>
        /// Sets the cost of a refined intra winner in an inter frame at high bit depth sharpness 3, as the reference does.
        /// The cost starts at zero. The texture-loss charge sets a cost only when its offset is positive.
        /// The measure reads the frame luma that the last transform trial of the refinement left. An intra mode never skips.
        /// Reference: the tmp_rd_stats branch of refine_winner_mode_tx() with is_inter_mode() false.
        /// </summary>
        /// <param name="statistics">The refined statistics.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="mode">The refined luma mode.</param>
        private readonly void ChargeRefinedIntraTextureLoss(
            ref Av1RateDistortionStatistics statistics, Point blockOrigin, Av1BlockSize blockSize, Av1PredictionMode mode)
        {
            statistics.Cost = 0;
            if (!this.ChargesHighBitDepthTextureLoss)
            {
                return;
            }

            bool smoothMode = IsSmoothTextureMode(mode);
            Av1PlaneRegion<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> lumaFrameBlock = Av1TransformBlockEncoder.GetPlaneSpan(lumaFrame, blockOrigin);
            this.GetVarianceStatistics(blockOrigin, blockSize, lumaFrameBlock, lumaFrame.Stride, out long sourceVariance, out long sampleVariance);
            long offset = GetTextureLossOffset(sourceVariance, sampleVariance, blockSize, false, smoothMode, false, default);
            this.ChargeTextureLoss(ref statistics, 0, offset, blockOrigin, blockSize, false, false, smoothMode);
        }

        /// <summary>
        /// Evaluates both chroma planes using the transform types at their corresponding luma positions.
        /// </summary>
        /// <param name="writer">The current entropy cost model.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the block entry read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction workspace, which the block entry read once.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The coded block and its frame-edge geometry.</param>
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
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
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

            for (int planeIndex = 1; planeIndex < 3; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                this.EvaluateInterChromaPlane(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
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
                    plane == Av1Plane.U ? interWorkspace.BlueCandidateReconstruction : interWorkspace.RedCandidateReconstruction,
                    plane == Av1Plane.U ? interWorkspace.BlueCandidateCoefficients : interWorkspace.RedCandidateCoefficients,
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

                statistics.Add(this.rateMultiplier, planeStatistics);
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

        /// <summary>
        /// Writes the luma prediction of the selected inter block into the frame: the inter-intra blend, or the single
        /// or compound inter prediction.
        /// </summary>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="transformPrediction">The intra prediction storage of an inter-intra blend.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="vector">The motion vector.</param>
        /// <param name="secondaryVector">The second motion vector of a compound prediction.</param>
        private void PrepareSelectedInterLumaPrediction(
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector)
        {
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
            if (modeInfo.Block.SecondaryReferenceFrame == Av1ReferenceFrameType.Intra)
            {
                this.PrepareInterIntraPrediction(
                    transformWorkspace,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    modeInfo.Block.BlockSize,
                    block.HasChroma,
                    modeInfo.Block.Mode,
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
            Span<TSample> prediction = interWorkspace.LumaPrediction[..sampleCount];
            Span<short> residual = interWorkspace.Residual[..sampleCount];
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

                Av1PlaneRegion<TSample> primaryPlane = this.references.Span[(int)modeInfo.Block.ReferenceFrame].CodedView.GetPlane(Av1Plane.Y);
                Av1PlaneRegion<TSample> secondaryPlane = this.references.Span[(int)modeInfo.Block.SecondaryReferenceFrame].CodedView.GetPlane(Av1Plane.Y);
                bool primaryWarped = this.TryPrepareGlobalCompoundIntermediate(
                    modeInfo.Block.Mode,
                    modeInfo.Block.ReferenceFrame,
                    predictionSize,
                    primaryPlane,
                    blockOrigin,
                    predictionSize,
                    0,
                    0,
                    firstIntermediate,
                    interWorkspace.FilterRows);

                bool secondaryWarped = this.TryPrepareGlobalCompoundIntermediate(
                    modeInfo.Block.Mode,
                    modeInfo.Block.SecondaryReferenceFrame,
                    predictionSize,
                    secondaryPlane,
                    blockOrigin,
                    predictionSize,
                    0,
                    0,
                    secondIntermediate,
                    interWorkspace.FilterRows);

                int primaryColumnQ4 = (blockOrigin.X << 4) + (vector.Column << 1);
                int primaryRowQ4 = (blockOrigin.Y << 4) + (vector.Row << 1);
                int secondaryColumnQ4 = (blockOrigin.X << 4) + (secondaryVector.Column << 1);
                int secondaryRowQ4 = (blockOrigin.Y << 4) + (secondaryVector.Row << 1);
                TOperator.PrepareCompoundInterPrediction(
                    this.source.GetPlane(Av1Plane.Y),
                    blockOrigin,
                    primaryPlane,
                    new Point(primaryColumnQ4 >> 4, primaryRowQ4 >> 4),
                    primaryColumnQ4 & 15,
                    primaryRowQ4 & 15,
                    secondaryPlane,
                    new Point(secondaryColumnQ4 >> 4, secondaryRowQ4 >> 4),
                    secondaryColumnQ4 & 15,
                    secondaryRowQ4 & 15,
                    modeInfo.Block.HorizontalInterpolationFilter,
                    modeInfo.Block.VerticalInterpolationFilter,
                    prediction,
                    residual,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    interWorkspace.FilterRows,
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
                    modeInfo.Block.DifferenceWeightedMaskType,
                    primaryWarped,
                    secondaryWarped);

                // The rebuilt luma goes into pd->dst, which is the frame. Reference: av1_enc_build_inter_predictor() in
                // refine_winner_mode_tx().
                this.WriteInterLumaDestination(lumaFrame, blockOrigin, predictionSize, prediction);
                return;
            }

            Av1EncoderFrame<TSample>.PlanarView reference = this.references.Span[(int)modeInfo.Block.ReferenceFrame].CodedView;
            bool warped = this.useWarpedPrediction;
            Av1GlobalMotionParameters warpModel = this.warpedModel;
            if (!warped && this.TryGetGlobalWarpModel(modeInfo.Block.Mode, modeInfo.Block.ReferenceFrame, modeInfo.Block.BlockSize, out Av1GlobalMotionParameters global))
            {
                warped = true;
                warpModel = global;
            }

            if (warped)
            {
                // A warped block of at least 8x8 luma samples predicts with its local model, and a GLOBALMV block
                // with the model of its reference. Reference: av1_init_warp_params().
                Av1EncoderFrame<TSample> referenceFrame = this.references.Span[(int)modeInfo.Block.ReferenceFrame];
                int width = predictionSize.GetWidth();
                int height = predictionSize.GetHeight();
                TOperator.PrepareWarpedInterPrediction(
                    reference.GetPlane(Av1Plane.Y),
                    referenceFrame.Width,
                    referenceFrame.Height,
                    blockOrigin,
                    width,
                    height,
                    0,
                    0,
                    warpModel,
                    prediction,
                    interWorkspace.FilterRows,
                    this.bitDepth);

                Av1PlaneRegion<TSample> lumaSource = this.source.GetPlane(Av1Plane.Y);
                TOperator.SubtractPrediction(
                    Av1TransformBlockEncoder.GetPlaneSpan(lumaSource, blockOrigin),
                    lumaSource.Stride,
                    prediction[..(width * height)],
                    residual[..(width * height)],
                    width,
                    height);

                // A global warp is allowed at sharpness 3, so its rebuilt luma goes into the frame. A local warp is not.
                // Reference: av1_enc_build_inter_predictor() in refine_winner_mode_tx().
                if (!this.useWarpedPrediction)
                {
                    this.WriteInterLumaDestination(lumaFrame, blockOrigin, predictionSize, prediction);
                }

                return;
            }

            if (this.IsScaledReference(modeInfo.Block.ReferenceFrame))
            {
                this.PrepareScaledInterPrediction(
                    modeInfo.Block.ReferenceFrame,
                    Av1Plane.Y,
                    blockOrigin,
                    0,
                    0,
                    vector,
                    modeInfo.Block.HorizontalInterpolationFilter,
                    modeInfo.Block.VerticalInterpolationFilter,
                    predictionSize,
                    prediction,
                    residual,
                    interWorkspace.FilterRows);
            }
            else
            {
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
                    interWorkspace.FilterRows,
                    predictionSize,
                    this.bitDepth);
            }

            if (this.useObmcPrediction)
            {
                this.ApplyObmcPrediction(
                    Av1Plane.Y,
                    0,
                    0,
                    prediction,
                    residual,
                    interWorkspace.FilterRows,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors);

                return;
            }

            // Reference: av1_enc_build_inter_predictor() in refine_winner_mode_tx().
            this.WriteInterLumaDestination(lumaFrame, blockOrigin, predictionSize, prediction);
        }

        /// <summary>
        /// Evaluates a prepared luma prediction with local coefficient and transform edge contexts.
        /// At high bit depth sharpness 3, the trellis of each luma trial can treat the block as a noise pattern.
        /// During an inter residual search, the destination holds the prediction, so the test reads the prediction.
        /// Reference: is_noise_pattern in av1_optimize_txb(), from av1_get_variance_stats() on pd->dst.
        /// </summary>
        /// <param name="writer">The symbol encoder that supplies the coefficient costs.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the block entry read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction workspace, which the block entry read once.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The neighborhood of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The mode information of the block, which gets the transform sizes.</param>
        /// <param name="states">Gets the luma transform states.</param>
        /// <param name="costLimit">The cost above which the search fails.</param>
        /// <param name="stateCount">Gets the number of transform states written.</param>
        /// <returns>The luma statistics.</returns>
        private Av1RateDistortionStatistics EvaluateInterLumaTree(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> transformPrediction,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            Span<Av1EncoderTransformBlockState> states,
            long costLimit,
            out int stateCount)
        {
            bool noisePattern = false;
            if (this.UsesHighBitDepthSharpness)
            {
                Av1BlockSize blockSize = modeInfo.BlockSize;
                ReadOnlySpan<TSample> prediction = interWorkspace.LumaPrediction;
                this.GetVarianceStatistics(blockOrigin, blockSize, prediction, blockSize.GetWidth(), out long sourceVariance, out long sampleVariance);
                noisePattern = sourceVariance > sampleVariance && sourceVariance / (blockSize.GetWidth() * blockSize.GetHeight()) < 64;
            }

            this.blockWorkspace.LumaNoisePattern = noisePattern;
            Av1RateDistortionStatistics statistics =
                this.EvaluateInterLumaTreeCore(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    transformPrediction,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    ref modeInfo,
                    states,
                    costLimit,
                    out stateCount);

            this.blockWorkspace.LumaNoisePattern = false;
            return statistics;
        }

        /// <summary>
        /// Evaluates a prepared luma prediction with local coefficient and transform edge contexts.
        /// </summary>
        /// <param name="writer">The symbol encoder that supplies the coefficient costs.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the block entry read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction workspace, which the block entry read once.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The neighborhood of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The mode information of the block, which gets the transform sizes.</param>
        /// <param name="states">Gets the luma transform states.</param>
        /// <param name="costLimit">The cost above which the search fails.</param>
        /// <param name="stateCount">Gets the number of transform states written.</param>
        /// <returns>The luma statistics.</returns>
        private Av1RateDistortionStatistics EvaluateInterLumaTreeCore(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> transformPrediction,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            Span<Av1EncoderTransformBlockState> states,
            long costLimit,
            out int stateCount)
        {
            int width4 = modeInfo.BlockSize.Get4x4WideCount();
            int height4 = modeInfo.BlockSize.Get4x4HighCount();
            Span<byte> coefficientAbove = interWorkspace.TransformContexts[..width4];
            Span<byte> coefficientLeft = interWorkspace.TransformContexts.Slice(width4, height4);
            Span<byte> transformAbove = interWorkspace.TransformContexts.Slice(width4 + height4, width4);
            Span<byte> transformLeft = interWorkspace.TransformContexts.Slice((2 * width4) + height4, height4);
            lumaCoefficientEdges.Top.Slice(lumaCoefficientEdges.GetTopIndex(blockOrigin), width4).CopyTo(coefficientAbove);
            lumaCoefficientEdges.Left.Slice(lumaCoefficientEdges.GetLeftIndex(blockOrigin), height4).CopyTo(coefficientLeft);
            transformEdges.Top.Slice(transformEdges.GetTopIndex(blockOrigin), width4).CopyTo(transformAbove);
            transformEdges.Left.Slice(transformEdges.GetLeftIndex(blockOrigin), height4).CopyTo(transformLeft);

            stateCount = 0;
            int initialDepth = this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select &&
                !modeInfo.Skip &&
                (!this.picture.Parent.SpeedSettings.DeferTransformSizeSearch ||
                    this.blockWorkspace.EvaluationStage != Av1EncoderEvaluationStage.Candidate)
                ? this.picture.Parent.SpeedSettings.InterTransformSearchInitialDepth
                : Av1Constants.MaxVarTransform;

            int blockWidth = modeInfo.BlockSize.GetWidth();
            int blockHeight = modeInfo.BlockSize.GetHeight();
            Span<short> residual = interWorkspace.Residual[..(blockWidth * blockHeight)];
            Av1PlaneRegion<TSample> lumaSource = this.source.GetPlane(Av1Plane.Y);
            bool residualPrepared = false;

            // The recursive search exits early when the modeled luma cost is already well above its bound.
            // Reference: model_based_tx_search_prune() in av1_pick_recursive_tx_size_type_yrd().
            if (costLimit != long.MaxValue && initialDepth != Av1Constants.MaxVarTransform &&
                this.picture.Parent.SpeedSettings.GetModelBasedTransformPruneLevel(this.picture.Parent.FrameUpdateType) != 0)
            {
                TOperator.SubtractPrediction(
                    Av1TransformBlockEncoder.GetPlaneSpan(lumaSource, blockOrigin),
                    lumaSource.Stride,
                    interWorkspace.LumaPrediction,
                    residual,
                    blockWidth,
                    blockHeight);

                residualPrepared = true;
                if (this.PrunesTransformSearchByModel(blockOrigin, modeInfo.BlockSize, modeInfo.ReferenceFrame, residual, costLimit))
                {
                    return Av1RateDistortionStatistics.Invalid;
                }
            }

            // A block inside the tile reuses the result of an earlier search of the same residual in the superblock,
            // unless the search has no cost bound. Reference: the use_mb_rd_hash lookup of
            // av1_pick_recursive_tx_size_type_yrd() and av1_pick_uniform_tx_size_type_yrd().
            Av1MacroblockRateDistortionRecord record = this.blockWorkspace.MacroblockRateDistortionRecord;
            bool useRecord = this.picture.Parent.SpeedSettings.UseMacroblockRateDistortionHash &&
                macroBlock.ToBottomEdge > 0 && macroBlock.ToRightEdge > 0;

            uint hash = 0;
            if (useRecord)
            {
                if (!residualPrepared)
                {
                    TOperator.SubtractPrediction(
                        Av1TransformBlockEncoder.GetPlaneSpan(lumaSource, blockOrigin),
                        lumaSource.Stride,
                        interWorkspace.LumaPrediction,
                        residual,
                        blockWidth,
                        blockHeight);
                }

                hash = Av1MacroblockRateDistortionRecord.GetHash(residual, modeInfo.BlockSize);
                int match = record.Find(costLimit, hash);
                if (match >= 0)
                {
                    ref readonly Av1MacroblockRateDistortionRecord.Entry entry = ref record.Get(match);
                    modeInfo.TransformSize = entry.TransformSize;
                    ((ReadOnlySpan<Av1TransformSize>)entry.InterTransformSizes).CopyTo(modeInfo.InterTransformSizes);
                    stateCount = entry.StateCount;
                    ((ReadOnlySpan<Av1EncoderTransformBlockState>)entry.States)[..stateCount].CopyTo(states);
                    return entry.Statistics;
                }
            }

            // A residual predicted to quantize to nothing takes the largest transforms with every coefficient
            // zero, and the transform search is not run. Reference: the predict_skip_txfm() and set_skip_txfm()
            // step that opens av1_pick_recursive_tx_size_type_yrd() and av1_pick_uniform_tx_size_type_yrd().
            int skipPredictionLevel = this.GetSkipPredictionLevel();
            if (skipPredictionLevel != 0 && !this.picture.Parent.FrameHeader.CodedLossless &&
                this.PredictSkipTransform(
                    transformCoefficients,
                    transformWorkspace,
                    in interWorkspace,
                    macroBlock,
                    blockOrigin,
                    modeInfo.BlockSize,
                    skipPredictionLevel,
                    out long predictedDistortion))
            {
                Av1RateDistortionStatistics skipStatistics = this.SetSkipTransform(
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

                if (useRecord)
                {
                    record.Save(hash, skipStatistics, states[..stateCount], modeInfo);
                }

                return skipStatistics;
            }

            Av1ModeCosts modeCosts = tables.ModeCosts;
            int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int skipRate = Av1SymbolEncoder.GetSkipCost(modeCosts, true, skipContext);
            int codedRate = Av1SymbolEncoder.GetSkipCost(modeCosts, false, skipContext);
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

            // The uniform search of the largest transform accumulates the cheaper of coding and skipping each
            // block after the cheaper skip flag, and stops once that sum exceeds the budget. Reference:
            // choose_largest_tx_size() with the current_rd of av1_txfm_rd_in_plane() and block_rd_txfm().
            bool uniformSearch = initialDepth == Av1Constants.MaxVarTransform;

            // Lossless coding starts at zero. Reference: choose_smallest_tx_size().
            long uniformCurrentCost = this.picture.Parent.FrameHeader.CodedLossless ? 0 : Math.Min(skipCost, codedCost);
            bool uniformExited = false;

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
                    // A block that follows the end of the uniform search leaves the plane incomplete. The uniform
                    // search records even an incomplete result. Reference: save_mb_rd_info() in
                    // av1_pick_uniform_tx_size_type_yrd().
                    if (uniformExited)
                    {
                        if (useRecord)
                        {
                            record.Save(hash, Av1RateDistortionStatistics.Invalid, states[..stateCount], modeInfo);
                        }

                        return Av1RateDistortionStatistics.Invalid;
                    }

                    long remainingCost = costLimit == long.MaxValue
                        ? long.MaxValue
                        : costLimit - (uniformSearch ? uniformCurrentCost : Math.Min(skipCost, codedCost));

                    Av1RateDistortionStatistics rootStatistics = this.SelectInterTransformNode(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        interWorkspace.LumaPrediction,
                        interWorkspace.Residual,
                        interWorkspace.TransformReconstruction,
                        interWorkspace.TransformCoefficients,
                        interWorkspace.LumaCandidateReconstruction,
                        interWorkspace.LumaCandidateCoefficients,
                        transformPrediction,
                        macroBlock,
                        blockOrigin,
                        in lumaCoefficientEdges,
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
                        // Only the uniform search records a failed result. Reference: the rd == INT64_MAX return of
                        // av1_pick_recursive_tx_size_type_yrd(), before save_mb_rd_info().
                        if (useRecord && uniformSearch)
                        {
                            record.Save(hash, Av1RateDistortionStatistics.Invalid, states[..stateCount], modeInfo);
                        }

                        return Av1RateDistortionStatistics.Invalid;
                    }

                    statistics.Add(this.rateMultiplier, rootStatistics);
                    skipCost = Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, statistics.PredictionDistortion);
                    codedCost = Av1RateDistortion.GetCost(this.rateMultiplier, codedRate + statistics.Rate, statistics.Distortion);
                    if (uniformSearch)
                    {
                        uniformCurrentCost += Math.Min(
                            rootStatistics.Cost,
                            Av1RateDistortion.GetCost(this.rateMultiplier, 0, rootStatistics.PredictionDistortion));

                        uniformExited = costLimit != long.MaxValue && uniformCurrentCost > costLimit;
                    }
                }
            }

            // When a skip of the whole luma residual costs no more than coding it, the largest-transform search reports a skip.
            // The skip has the prediction error and no rate. At high bit depth sharpness 3, the skip gets a penalty first.
            // The largest transform signals no size, so the coded cost has no size rate. The record keeps the result.
            // Reference: the forced skip test at the end of uniform_txfm_yrd() with TX_MODE_LARGEST.
            // Also save_mb_rd_info() in av1_pick_uniform_tx_size_type_yrd().
            if (uniformSearch && statistics.HasCoefficients && !this.picture.Parent.FrameHeader.CodedLossless)
            {
                long codedDistortion = statistics.Distortion;
                long skipDistortion = statistics.PredictionDistortion;
                if (this.UsesHighBitDepthSharpness)
                {
                    this.PenalizeTransformSkip(blockOrigin, modeInfo.BlockSize, interWorkspace.LumaPrediction, ref codedDistortion, ref skipDistortion);
                }

                long forcedSkipCost = Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, skipDistortion);
                if (forcedSkipCost <= Av1RateDistortion.GetCost(this.rateMultiplier, codedRate + statistics.Rate, codedDistortion))
                {
                    statistics = new(this.rateMultiplier, 0, statistics.PredictionDistortion) { PredictionDistortion = statistics.PredictionDistortion };
                }
            }

            if (useRecord)
            {
                record.Save(hash, statistics, states[..stateCount], modeInfo);
            }

            return statistics;
        }

        /// <summary>
        /// Gets the predicted-skip level of the current evaluation stage.
        /// Reference: predict_skip_levels, indexed by use_skip_flag_prediction and the mode evaluation type.
        /// </summary>
        private int GetSkipPredictionLevel()
        {
            // Rows of predict_skip_levels: { 0, 0, 0 }, { 1, 1, 1 } and { 1, 2, 1 }.
            int level = this.picture.Parent.SpeedSettings.SkipFlagPredictionLevel;
            return level == 0 ? 0 : this.blockWorkspace.EvaluationStage == Av1EncoderEvaluationStage.Candidate ? level : 1;
        }

        /// <summary>
        /// Predicts whether the whole luma residual of an inter or copied block quantizes to nothing.
        /// Reference: predict_skip_txfm().
        /// </summary>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block, whose luma prediction is current.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="level">The predicted-skip level of the evaluation stage.</param>
        /// <param name="distortion">The distortion of the skipped residual.</param>
        /// <returns><see langword="true"/> when the residual is predicted to quantize to nothing.</returns>
        private bool PredictSkipTransform(
            Span<int> transformCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int level,
            out long distortion)
        {
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();

            // High bit depth sharpness 3 never predicts a skip when the prediction is smoother than a source with low detail.
            // Reference: av1_is_skip_txfm_penalized() at the start of predict_skip_txfm().
            if (this.PenalizesHighBitDepthSkip)
            {
                this.GetVarianceStatistics(blockOrigin, blockSize, interWorkspace.LumaPrediction, width, out long sourceVariance, out long sampleVariance);
                if (sourceVariance > sampleVariance && sourceVariance / (width * height) < 64)
                {
                    distortion = 0;
                    return false;
                }
            }

            Span<short> residual = interWorkspace.Residual[..(width * height)];
            Av1PlaneRegion<TSample> lumaSource = this.source.GetPlane(Av1Plane.Y);
            TOperator.SubtractPrediction(
                Av1TransformBlockEncoder.GetPlaneSpan(lumaSource, blockOrigin), lumaSource.Stride, interWorkspace.LumaPrediction, residual, width, height);

            // The whole block is subtracted with the DCT_DCT border padding. Reference: av1_subtract_plane().
            Av1TransformBlockEncoder.PadBorderResidual(
                this.blockWorkspace, Av1Plane.Y, blockOrigin, residual, width, width, height, Av1TransformType.DctDct);

            // The distortion covers the visible samples only. Reference: av1_pixel_diff_dist().
            Size visibleSize = this.blockWorkspace.GetVisibleSize(Av1Plane.Y, blockOrigin, width, height);
            int visibleWidth = visibleSize.Width;
            int visibleHeight = visibleSize.Height;
            distortion = Av1ResidualBuilder.SumSquares(residual, width, visibleWidth, visibleHeight);

            int qIndex = this.blockQIndex;
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
            int coefficientCount = transformWidth * transformHeight;
            for (int row = 0; row < height; row += transformHeight)
            {
                for (int column = 0; column < width; column += transformWidth)
                {
                    Av1ForwardTransformer.Transform2d(
                        residual[((row * width) + column)..],
                        transformCoefficients,
                        (uint)width,
                        Av1TransformType.DctDct,
                        transformSize,
                        this.bitDepth.GetBitCount(),
                        transformWorkspace);

                    if (((uint)Math.Abs(transformCoefficients[0]) << 7) >= dcThreshold)
                    {
                        return false;
                    }

                    // Any AC coefficient at or above its threshold fails, so the largest magnitude decides.
                    if (((uint)Av1CoefficientMeasures.GetMaximumAbsolute(transformCoefficients[1..coefficientCount]) << 7) >= acThreshold)
                    {
                        return false;
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

            int zeroRate = Av1SymbolEncoder.GetTransformBlockSkipCost(
                writer.CoefficientCosts, true, Av1SymbolContextHelper.GetTransformSizeContext(transformSize), firstContext.SkipContext);

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
        /// <param name="writer">The tile symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the root of the tree reads once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="lumaPrediction">The luma prediction of the whole block.</param>
        /// <param name="residual">The residual buffer of one transform block.</param>
        /// <param name="transformReconstruction">The reconstruction storage of the candidate.</param>
        /// <param name="candidateCoefficients">The coefficient storage of the candidate.</param>
        /// <param name="lumaCandidateReconstruction">The reconstruction storage of the winner.</param>
        /// <param name="lumaCandidateCoefficients">The coefficient storage of the winner.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="macroBlock">The block and its neighbor availability.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfo">The mode information of the block, which records the selected transform sizes.</param>
        /// <param name="transformSize">The transform size of this node.</param>
        /// <param name="row">The node row in four-sample units.</param>
        /// <param name="column">The node column in four-sample units.</param>
        /// <param name="depth">The split depth of this node.</param>
        /// <param name="coefficientAbove">The trial top coefficient contexts.</param>
        /// <param name="coefficientLeft">The trial left coefficient contexts.</param>
        /// <param name="transformAbove">The trial top transform-size contexts.</param>
        /// <param name="transformLeft">The trial left transform-size contexts.</param>
        /// <param name="states">The transform block states of the tree.</param>
        /// <param name="stateCount">The number of states that the tree holds.</param>
        /// <param name="parentCost">The cost of the parent node, which bounds the split.</param>
        /// <param name="costLimit">The cost at which the search of this node stops.</param>
        /// <param name="rootIndex">The index of the root transform that holds this node.</param>
        /// <returns>The statistics of the best choice for this node, or invalid statistics when the limit stopped it.</returns>
        private Av1RateDistortionStatistics SelectInterTransformNode(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<TSample> lumaPrediction,
            Span<short> residual,
            Span<TSample> transformReconstruction,
            Span<int> candidateCoefficients,
            Span<TSample> lumaCandidateReconstruction,
            Span<int> lumaCandidateCoefficients,
            Span<TSample> transformPrediction,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
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

            // The largest-transform search of mode evaluation codes each root transform once, without a split.
            // Reference: choose_largest_tx_size() against select_tx_block().
            bool uniformSearch = rootIndex >= 0 && depth == Av1Constants.MaxVarTransform;
            InlineArray32<byte> savedCoefficientAbove = default;
            InlineArray32<byte> savedCoefficientLeft = default;
            InlineArray32<byte> savedTransformAbove = default;
            InlineArray32<byte> savedTransformLeft = default;
            coefficientAbove.CopyTo(savedCoefficientAbove);
            coefficientLeft.CopyTo(savedCoefficientLeft);
            transformAbove.CopyTo(savedTransformAbove);
            transformLeft.CopyTo(savedTransformLeft);

            int sourceStride = blockSize.GetWidth();
            int sourceOffset = ((row << 2) * sourceStride) + (column << 2);
            for (int y = 0; y < height; y++)
            {
                lumaPrediction.Slice(sourceOffset + (y * sourceStride), width).CopyTo(transformPrediction.Slice(y * width, width));
            }

            Point origin = blockOrigin + new Size(column << 2, row << 2);
            Av1PlaneRegion<TSample> lumaSource = this.source.GetPlane(Av1Plane.Y);
            TOperator.SubtractPrediction(
                Av1TransformBlockEncoder.GetPlaneSpan(lumaSource, origin),
                lumaSource.Stride,
                transformPrediction,
                residual,
                transformSize.GetWidth(),
                transformSize.GetHeight());

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
                        // Reference: the aom_get_blk_sse_sum() call of get_blk_var_dev().
                        long subSquaredSum = Av1ResidualBuilder.SumAndSumSquares(
                            residual[((y * width) + x)..], width, subWidth, subHeight, out long blockSum);

                        int subSum = (int)blockSum;
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
                    this.blockQIndex, this.quantization.DeltaQDc[0], this.bitDepth) >> 3;

                int acQuantizer = Av1QuantizationLookup.GetAcQuant(
                    this.blockQIndex, this.quantization.DeltaQAc[0], this.bitDepth) >> 3;

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
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    Av1Plane.Y,
                    modeInfo.Mode,
                    origin,
                    transformSize,
                    Av1TransformType.AllTransformTypes,
                    context,
                    Av1TileWriter.GetTransformBlockContexts(Av1ComponentType.Luminance, in lumaCoefficientEdges, blockOrigin, blockSize, transformSize),
                    transformPrediction,
                    residual,
                    width,
                    costLimit,
                    transformReconstruction,
                    candidateCoefficients,
                    lumaCandidateReconstruction,
                    lumaCandidateCoefficients,
                    out noSplitState,
                    out int rate,
                    out long distortion,
                    out long predictionDistortion);

                int zeroRate = writer.GetCoefficientCost(
                    tables,
                    transformSize,
                    Av1TransformType.DctDct,
                    modeInfo.Mode,
                    lumaCandidateCoefficients,
                    Av1ComponentType.Luminance,
                    context,
                    0,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet,
                    Av1FilterIntraMode.AllFilterIntraModes,
                    usesInterTransformSet: true);

                // The neighbors of this transform read the entropy context of its coded result, even when the
                // empty residual below wins. Reference: txb_entropy_ctx, which the zero_blk_rd branch of
                // try_tx_block_no_split() leaves as quantization set it.
                coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                    lumaCandidateCoefficients, transformSize, noSplitState.TransformType, noSplitState.EndOfBlock);

                // An empty residual can win even when quantization retained coefficients. Its distortion
                // is the prediction error, and its syntax consists only of the transform-skip symbol. The
                // uniform search of the largest transform keeps the coded result and its searched type.
                // Reference: the zero_blk_rd branch of try_tx_block_no_split(), which choose_largest_tx_size()
                // does not have.
                if (!uniformSearch && (noSplitState.EndOfBlock == 0 ||
                    (!this.picture.Parent.FrameHeader.CodedLossless &&
                    Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion) >=
                    Av1RateDistortion.GetCost(this.rateMultiplier, zeroRate, predictionDistortion))))
                {
                    noSplitState.EndOfBlock = 0;
                    noSplitState.TransformType = Av1TransformType.DctDct;
                    rate = zeroRate;
                    distortion = predictionDistortion;
                }

                noSplitState.EntropyContext = (byte)(context.SkipContext | (context.DcSignContext << 4));
                if (transformSize > Av1TransformSize.Size4x4 && depth < Av1Constants.MaxVarTransform)
                {
                    rate += Av1SymbolEncoder.GetTransformPartitionCost(tables.ModeCosts, false, partitionContext);
                }

                noSplit = new(this.rateMultiplier, rate, distortion)
                {
                    PredictionDistortion = predictionDistortion,
                    HasCoefficients = noSplitState.EndOfBlock != 0
                };

                if (uniformSearch)
                {
                    // The block keeps its result even above the budget; the plane search ends after it instead,
                    // and fails only when another block follows. Reference: the exit_early and incomplete_exit
                    // handling of block_rd_txfm() and av1_txfm_rd_in_plane() for inter blocks.
                    return this.CommitInterTransformNode(
                        ref modeInfo,
                        transformSize,
                        row,
                        column,
                        coefficientAbove,
                        coefficientLeft,
                        transformAbove,
                        transformLeft,
                        states,
                        ref stateCount,
                        noSplitState,
                        coefficientContext,
                        origin,
                        visibleWidth4,
                        visibleHeight4,
                        noSplit);
                }

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
                int splitScore = PredictTransformSplit(residual, transformSize);
                canSplit = splitScore >= -this.picture.Parent.SpeedSettings.InterTransformSplitThreshold;
            }

            Av1RateDistortionStatistics split = Av1RateDistortionStatistics.Invalid;
            if (canSplit)
            {
                split = new(this.rateMultiplier, Av1SymbolEncoder.GetTransformPartitionCost(tables.ModeCosts, true, partitionContext), 0);
                Av1TransformSize childSize = transformSize.GetSubSize();
                int childWidth4 = childSize.Get4x4WideCount();
                int childHeight4 = childSize.Get4x4HighCount();
                int childCount = (width4 / childWidth4) * (height4 / childHeight4);
                long splitLimit = Math.Min(noSplit.Cost, costLimit);

                // The split cost counts only after the first child: the partition rate is added without updating
                // rdcost, so the first child receives the whole budget. Reference: the ref_best_rd -
                // split_rd_stats->rdcost of try_tx_block_split().
                long consumedCost = 0;
                for (int y = 0; y < height4 && row + y < visibleHeight4; y += childHeight4)
                {
                    for (int x = 0; x < width4 && column + x < visibleWidth4; x += childWidth4)
                    {
                        Av1RateDistortionStatistics child = this.SelectInterTransformNode(
                            writer,
                            in tables,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            lumaPrediction,
                            residual,
                            transformReconstruction,
                            candidateCoefficients,
                            lumaCandidateReconstruction,
                            lumaCandidateCoefficients,
                            transformPrediction,
                            macroBlock,
                            blockOrigin,
                            in lumaCoefficientEdges,
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
                            splitLimit - consumedCost,
                            -1);

                        if (child.Cost == long.MaxValue)
                        {
                            split = Av1RateDistortionStatistics.Invalid;
                            break;
                        }

                        split.Add(this.rateMultiplier, child);
                        consumedCost = split.Cost;
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
            return this.CommitInterTransformNode(
                ref modeInfo,
                transformSize,
                row,
                column,
                coefficientAbove,
                coefficientLeft,
                transformAbove,
                transformLeft,
                states,
                ref stateCount,
                noSplitState,
                coefficientContext,
                origin,
                visibleWidth4,
                visibleHeight4,
                noSplit);
        }

        /// <summary>
        /// Publishes an unsplit transform: its state, its coefficient edge contexts, and its size.
        /// </summary>
        private Av1RateDistortionStatistics CommitInterTransformNode(
            ref Av1EncoderBlockModeInfo modeInfo,
            Av1TransformSize transformSize,
            int row,
            int column,
            Span<byte> coefficientAbove,
            Span<byte> coefficientLeft,
            Span<byte> transformAbove,
            Span<byte> transformLeft,
            Span<Av1EncoderTransformBlockState> states,
            ref int stateCount,
            Av1EncoderTransformBlockState noSplitState,
            byte coefficientContext,
            Point origin,
            int visibleWidth4,
            int visibleHeight4,
            Av1RateDistortionStatistics noSplit)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int width4 = transformSize.Get4x4WideCount();
            int height4 = transformSize.Get4x4HighCount();
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

        /// <summary>
        /// Returns whether the reference lists that skip mode reads exist for the block. A pair that the rectangular
        /// partition mask drops, unless the cached decision of an asymmetric sub-block uses it, or that the frame or
        /// the selective reference search prunes, has no list of its own, and skip mode then builds it only when both
        /// single references have theirs. Reference: the ref_mv_count UINT8_MAX return of rd_pick_skip_mode(), with
        /// the lists that set_params_rd_pick_inter_mode() builds under prune_ref_frame().
        /// </summary>
        /// <param name="first">The first skip-mode reference.</param>
        /// <param name="second">The second skip-mode reference.</param>
        /// <returns><see langword="true"/> when skip mode is searched.</returns>
        private bool HasSkipModeReferenceLists(Av1ReferenceFrameType first, Av1ReferenceFrameType second)
        {
            bool pairListBuilt = ((this.skipReferenceFrameMask & (1 << GetReferenceFrameType(first, second))) == 0 ||
                    this.IsCachedCompoundPair(first, second)) &&
                !this.picture.Parent.PrunesAllCompoundReferences &&
                !this.PrunesCompoundReferencePair(first, second) &&
                !this.PrunesReferenceBySelectiveReferenceFrame(first, second);

            return pairListBuilt || (!this.IsSingleReferenceSkipped((int)first) && !this.IsSingleReferenceSkipped((int)second));
        }

        /// <summary>
        /// Prices the non-skip-mode symbol of the block winner and tries skip mode against it. Reference:
        /// rd_pick_skip_mode().
        /// </summary>
        /// <param name="tables">The rate tables that price the syntax.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor context of the block.</param>
        /// <param name="blockOrigin">The luma origin of the block.</param>
        /// <param name="skipModeContext">The skip mode symbol context.</param>
        /// <param name="nonSkipModeStatistics">The cost of coding skip mode as off.</param>
        /// <param name="modeInfo">The block decisions, updated when skip mode wins.</param>
        /// <param name="block">The block coding state, updated when skip mode wins.</param>
        /// <param name="selectedStatistics">The block winner, which receives the non-skip-mode cost and any skip mode win.</param>
        /// <param name="selectedVector">The first vector of the winner.</param>
        /// <param name="selectedSecondaryVector">The second vector of the winner.</param>
        /// <param name="selectedStates">The transform states of the winner.</param>
        private void SelectSkipModeBlock(
            in Av1CoefficientTables tables,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<Av1EncoderReferenceContext> referenceContexts,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            int skipModeContext,
            Av1RateDistortionStatistics nonSkipModeStatistics,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1RateDistortionStatistics selectedStatistics,
            ref Av1MotionVector selectedVector,
            ref Av1MotionVector selectedSecondaryVector,
            ref InlineArray128<Av1EncoderTransformBlockState> selectedStates)
        {
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Av1ReferenceFrameType primaryReference = frameHeader.SkipModeParameters.FirstReferenceFrame;
            Av1ReferenceFrameType secondaryReference = frameHeader.SkipModeParameters.SecondReferenceFrame;
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            ref Av1ReferenceMotionVectors referenceMotionVectors = ref this.blockWorkspace.ReferenceMotionVectors;
            referenceMotionVectors.Build(
                this.picture,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
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

            // Skip mode is not searched when its vectors fail the build_cur_mv() test, which only the second vector
            // decides; see CompoundVectorsInFrameSearchBounds(). Reference: the build_cur_mv() return of
            // rd_pick_skip_mode().
            Av1PlaneRegion<TSample> boundsPlane = this.references.Span[(int)primaryReference].CodedView.GetPlane(Av1Plane.Y);
            Rectangle frameBounds = Av1MotionVector.GetFrameSearchBounds(
                new Rectangle(blockOrigin, new Size(blockSize.GetWidth(), blockSize.GetHeight())),
                new Size(
                    this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                    this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2),
                Math.Min(boundsPlane.Bounds.X, boundsPlane.Bounds.Y));

            if (!secondaryVector.IsInFrameSearchBounds(frameBounds))
            {
                return;
            }

            // The winner takes the cost of coding skip mode as off only once skip mode is tried. Reference: the
            // skip_mode_cost[skip_mode_ctx][0] update of rd_pick_skip_mode().
            if (selectedStatistics.Cost != long.MaxValue)
            {
                selectedStatistics.Add(this.rateMultiplier, nonSkipModeStatistics);
            }

            Av1InterpolationFilter filter = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable
                ? Av1InterpolationFilter.Regular
                : frameHeader.InterpolationFilter;

            int rate = Av1SymbolEncoder.GetSkipModeCost(tables.ModeCosts, true, skipModeContext);
            int normalizationShift = (this.bitDepth.GetBitCount() - 8) * 2;
            long distortion = 0;
            bool exactPrediction = true;
            int planeCount = block.HasChroma ? 3 : 1;
            Av1EncoderFrame<TSample>.PlanarView primaryView = this.references.Span[(int)primaryReference].CodedView;
            Av1EncoderFrame<TSample>.PlanarView secondaryView = this.references.Span[(int)secondaryReference].CodedView;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Point origin = planeIndex == 0 ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
                Av1BlockSize planeSize = blockSize.GetSubsampled(subX != 0, subY != 0);
                Span<TSample> prediction = plane switch
                {
                    Av1Plane.Y => interWorkspace.LumaPrediction,
                    Av1Plane.U => interWorkspace.BluePrediction,
                    _ => interWorkspace.RedPrediction
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
                    primaryView.GetPlane(plane),
                    secondaryView.GetPlane(plane),
                    new Point(origin.X << subX, origin.Y << subY),
                    subX,
                    subY,
                    blockSize,
                    prediction,
                    interWorkspace.Residual,
                    interWorkspace.FilterRows,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors);

                // The luma prediction goes into pd->dst, which is the frame. Reference: av1_enc_build_inter_predictor() in skip_mode_rd().
                if (plane == Av1Plane.Y)
                {
                    this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, prediction);
                }

                // Skip mode carries no coefficient or transform-choice syntax. Accumulate visible
                // prediction error directly and abandon later planes once the candidate cannot win.
                // A frame that pads its border measures the samples inside the frame only. Reference:
                // av1_pixel_diff_dist() in skip_mode_rd().
                Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                Size visibleSize = this.blockWorkspace.BorderPad
                    ? this.blockWorkspace.GetVisibleSize(plane, origin, planeSize.GetWidth(), planeSize.GetHeight())
                    : new Size(
                        Math.Min(planeSize.GetWidth(), sourcePlane.Width - origin.X),
                        Math.Min(planeSize.GetHeight(), sourcePlane.Height - origin.Y));

                int width = visibleSize.Width;
                int height = visibleSize.Height;
                long squaredError = Av1ResidualBuilder.SumSquares(interWorkspace.Residual, planeSize.GetWidth(), width, height);

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

            // Skip mode codes no residual, so the block is skippable. Reference: the best_mode_skippable update of
            // rd_pick_skip_mode().
            selectedStatistics = new(this.rateMultiplier, rate, distortion)
            {
                PredictionDistortion = distortion,
                AllTransformsEmpty = true
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
            modeInfo.Block.MotionMode = Av1MotionMode.SimpleTranslation;
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
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="bestCost">The cost of the best block result so far.</param>
        /// <param name="referenceFrame">The reference frame of the mode.</param>
        /// <param name="requestedMode">The single-reference mode to search.</param>
        /// <param name="referenceMotionVectors">The reference vector list of the reference frame.</param>
        /// <param name="cachedInterIntraMode">The inter-intra mode that an earlier search of the reference chose.</param>
        /// <param name="searchMotionModes">Whether each candidate searches OBMC and warped motion after simple translation.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="selectedVector">The motion vector of the winner.</param>
        /// <param name="selectedStates">The transform states of the winner.</param>
        /// <param name="searchedNewVectors">The new vectors that earlier searches of the reference found, by list index.</param>
        /// <param name="searchedNewVectorMask">The list indices whose new vector is in <paramref name="searchedNewVectors"/>.</param>
        /// <returns>The rate and distortion of the winner.</returns>
        private Av1RateDistortionStatistics SelectSingleReferenceMode(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            long bestCost,
            Av1ReferenceFrameType referenceFrame,
            Av1PredictionMode requestedMode,
            ref Av1ReferenceMotionVectors referenceMotionVectors,
            ref int cachedInterIntraMode,
            bool searchMotionModes,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            out Av1MotionVector selectedVector,
            out InlineArray128<Av1EncoderTransformBlockState> selectedStates,
            ref InlineArray3<Av1MotionVector> searchedNewVectors,
            ref byte searchedNewVectorMask)
        {
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
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
            modeInfo.Block.MotionMode = Av1MotionMode.SimpleTranslation;
            block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 0;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 0;
            block.PredictionUnit.ChromaFromLumaIndex = 0;
            block.PredictionUnit.ChromaFromLumaSigns = 0;

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
            InlineArray3<Av1MotionVector> candidateVectorsStorage = default;
            InlineArray3<byte> candidateReferenceIndicesStorage = default;
            Span<Av1MotionVector> candidateVectors = candidateVectorsStorage;
            Span<byte> candidateReferenceIndices = candidateReferenceIndicesStorage;
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
            Span<TSample> selectedLumaReconstruction = interWorkspace.SelectedLumaReconstruction;
            Span<TSample> candidateLumaReconstruction = interWorkspace.LumaCandidateReconstruction;
            Span<TSample> selectedBlueReconstruction = interWorkspace.SelectedBlueReconstruction;
            Span<TSample> candidateBlueReconstruction = interWorkspace.BlueCandidateReconstruction;
            Span<TSample> selectedRedReconstruction = interWorkspace.SelectedRedReconstruction;
            Span<TSample> candidateRedReconstruction = interWorkspace.RedCandidateReconstruction;
            Span<int> selectedLumaCoefficients = interWorkspace.SelectedLumaCoefficients;
            Span<int> candidateLumaCoefficients = interWorkspace.LumaCandidateCoefficients;
            Span<int> selectedBlueCoefficients = interWorkspace.SelectedBlueCoefficients;
            Span<int> candidateBlueCoefficients = interWorkspace.BlueCandidateCoefficients;
            Span<int> selectedRedCoefficients = interWorkspace.SelectedRedCoefficients;
            Span<int> candidateRedCoefficients = interWorkspace.RedCandidateCoefficients;
            int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            Span<byte> referenceCounts = stackalloc byte[Av1Constants.ReferenceFrameCount];
            Av1TileWriter.CollectNeighborReferenceCounts(modeInfoGrid, modeInfoAllocation, macroBlock, referenceCounts);

            // Every candidate prices its reference and skip syntax with the same rates.
            Av1ModeCosts modeCosts = tables.ModeCosts;

            // Every candidate of this reference pays the same syntax: the flag that says the block
            // is inter, and the reference selection itself. Pricing it once keeps it out of the
            // candidate loop.
            int intraInterContext = Av1TileWriter.GetIntraInterContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int commonPredictionRate = Av1SymbolEncoder.GetIsInterCost(tables.ModeCosts, isInter: true, intraInterContext) +
                Av1SymbolEncoder.GetSingleReferenceCost(tables.ModeCosts, referenceFrame, referenceCounts);

            // A frame that lets each block choose its reference mode codes one flag saying whether
            // the block is compound. A block with a side below eight samples cannot be compound, so
            // it codes no flag and pays nothing.
            if (frameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect &&
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8)
            {
                commonPredictionRate += modeCosts.GetCompInter(Av1SymbolContextHelper.GetReferenceModeContext(modeInfoGrid, modeInfoAllocation, macroBlock), 0);
            }

            // A frame that selects its transform size codes, for each block, whether the largest
            // transform is split. This search never splits, so the cost of the unsplit flag is
            // common to every candidate. The smallest transform cannot split and codes nothing.
            // Mode evaluation that defers the transform size search codes the largest transform, which signals no
            // split flag. Reference: the tx_select test of av1_estimate_txfm_yrd().
            int transformPartitionRate = 0;
            if (frameHeader.TransformMode == Av1TransformMode.Select && lumaTransformSize != Av1TransformSize.Size4x4 &&
                (!this.picture.Parent.SpeedSettings.DeferTransformSizeSearch ||
                    this.blockWorkspace.EvaluationStage != Av1EncoderEvaluationStage.Candidate))
            {
                int topIndex = transformEdges.GetTopIndex(blockOrigin);
                int leftIndex = transformEdges.GetLeftIndex(blockOrigin);
                int transformPartitionContext = Av1SymbolContextHelper.GetTransformPartitionContext(
                    transformEdges.Top[topIndex], transformEdges.Left[leftIndex], blockSize, lumaTransformSize);

                transformPartitionRate = Av1SymbolEncoder.GetTransformPartitionCost(tables.ModeCosts, false, transformPartitionContext);
            }

            Av1RateDistortionStatistics selectedStatistics = Av1RateDistortionStatistics.Invalid;
            selectedVector = default;
            selectedStates = default;
            Av1PredictionMode selectedMode = default;
            int selectedReferenceIndex = 0;
            bool selectedSkip = false;
            bool selectedInterIntra = false;
            Av1MotionMode selectedMotionMode = Av1MotionMode.SimpleTranslation;
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
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);

            // The motion search reads a reference of another size through its copy resized to the frame size.
            // Reference: the scaled_ref_frame of av1_single_motion_search().
            Av1PlaneRegion<TSample> referencePlane = this.searchReferences.Span[(int)referenceFrame].CodedView.GetPlane(Av1Plane.Y);
            int sourceOrigin = ((sourcePlane.Bounds.Y + blockOrigin.Y) * sourcePlane.Stride) + sourcePlane.Bounds.X + blockOrigin.X;
            int referenceOrigin = ((referencePlane.Bounds.Y + blockOrigin.Y) * referencePlane.Stride) + referencePlane.Bounds.X + blockOrigin.X;
            Size frameSize = new(
                this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

            Rectangle frameBounds = Av1MotionVector.GetFrameSearchBounds(
                new Rectangle(blockOrigin, new Size(blockSize.GetWidth(), blockSize.GetHeight())),
                frameSize,
                Math.Min(referencePlane.Bounds.X, referencePlane.Bounds.Y));

            ReadOnlySpan<byte> aboveContexts = lumaCoefficientEdges.Top.Slice(lumaCoefficientEdges.GetTopIndex(blockOrigin), blockSize.Get4x4WideCount());
            ReadOnlySpan<byte> leftContexts = lumaCoefficientEdges.Left.Slice(lumaCoefficientEdges.GetLeftIndex(blockOrigin), blockSize.Get4x4HighCount());

            // Frame owners provide contiguous padded planes. Borrow those spans without copying source blocks
            // or reconstructing border samples, and keep the search scratch disjoint from retained inter winners.
            Av1MotionSearchBase.SingleReferenceSearch<TSample, TOperator> motionSearch = new(
                sourcePlane.Samples[sourceOrigin..],
                sourcePlane.Stride,
                referencePlane.Samples,
                referencePlane.Stride,
                referenceOrigin,
                blockSize,
                blockOrigin,
                frameBounds,
                new Size(frameHeader.FrameSize.FrameWidth, frameHeader.FrameSize.FrameHeight),
                this.blockWorkspace,
                motionSearchPrediction,
                this.reconstruction.GetPlane(Av1Plane.Y),
                blockResidual,
                interWorkspace.FilterRows,
                interWorkspace.TransformCoefficients,
                writer,
                aboveContexts,
                leftContexts,
                this.bitDepth,
                this.blockQIndex,
                this.quantization.DeltaQDc[0],
                this.blockWorkspace.EncoderOptions.Sharpness,
                frameHeader.CodedLossless,
                this.rateMultiplier,
                transformPartitionRate,
                Av1SymbolEncoder.GetSkipCost(modeCosts, false, skipContext),
                Av1SymbolEncoder.GetSkipCost(modeCosts, true, skipContext),
                motionVectorCosts,
                this.GetScaledSearchReference(referenceFrame, blockOrigin, interWorkspace.FilterRows));

            int spatialMagnitude = GetSpatialMotionMagnitude(referenceMotionVectors, blockOrigin, blockSize, frameSize);

            Av1MotionSearchSettings motionSettings = this.picture.Parent.MotionSearchSettings;
            Av1MotionSearchBase.SingleReferenceState motionState = default;

            // The spatial start and one candidate per 16x16 model block of the largest block. Reference: the cand
            // array of av1_single_motion_search(), MAX_TPL_BLK_IN_SB squared plus one.
            Span<Av1MotionSearchBase.StartingCandidate> motionStarts =
                stackalloc Av1MotionSearchBase.StartingCandidate[Av1EncoderBlockWorkspace.TplSuperblockBlockCount + 1];

            // The model prunes no entry of the mode when a neighbor shares its reference. Reference: the
            // ref_match_found_in_above_nb and ref_match_found_in_left_nb setup of handle_inter_mode().
            bool tplPruning = this.tplInterModePruning &&
                !this.HasNeighborReferenceMatch(
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    referenceFrame,
                    Av1ReferenceFrameType.None);

            int candidateMask = (1 << candidateCount) - 1;

            // A list or global vector that points beyond the frame displacement region is not searched, and neither
            // is it modeled among the near entries. A new-motion entry searches from its reference instead.
            // Reference: build_cur_mv() in handle_inter_mode() and ref_mv_idx_to_search().
            if (requestedMode != Av1PredictionMode.NewMotionVector)
            {
                for (int index = 0; index < candidateCount; index++)
                {
                    if (!candidateVectors[index].IsInFrameSearchBounds(frameBounds))
                    {
                        candidateMask &= ~(1 << index);
                    }
                }
            }

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
                        candidateReferenceIndices[index] >= (this.blockQIndex * 3 / 256) + 1;

                    if ((secondaryPastReference || (distantNewReference && pruneByQuantizer)) &&
                        referenceMotionVectors.Weights[weightIndex] < Av1ReferenceMotionVectors.NearestCandidateWeight)
                    {
                        candidateMask &= ~(1 << index);
                    }
                }
            }

            if (requestedMode == Av1PredictionMode.NearMotionVector && candidateCount > 1)
            {
                InlineArray3<long> translationCostsStorage = default;
                Span<long> translationCosts = translationCostsStorage;
                translationCosts[..].Fill(long.MaxValue);
                InlineArray4<Av1RateDistortionStatistics> planeStatisticsStorage = default;
                Span<Av1RateDistortionStatistics> planeStatistics = planeStatisticsStorage;

                // A scaled reference is not pruned. Reference: the av1_is_scaled() test of ref_mv_idx_to_search().
                bool modelTranslation = this.picture.Parent.SpeedSettings.PruneNearMotionByTranslation &&
                    blockSize.GetWidth() * blockSize.GetHeight() > 64 &&
                    !this.IsScaledReference(modeInfo.Block.ReferenceFrame);

                modeInfo.Block.Mode = requestedMode;
                long bestTranslation = long.MaxValue;
                for (int index = 0; index < candidateCount; index++)
                {
                    if ((candidateMask & (1 << index)) == 0)
                    {
                        continue;
                    }

                    int modeRate = GetInterModeRate(
                        in tables, in motionVectorCosts, requestedMode, candidateVectors[index], candidateReferenceIndices[index], referenceMotionVectors);

                    // Every candidate of this mode pays the mode itself, so removing it leaves the
                    // part that separates one list entry from another. A candidate whose syntax
                    // alone already costs more than the block winner cannot win at any distortion,
                    // so it is dropped before any prediction is formed.
                    int drlRate = modeRate - Av1SymbolEncoder.GetInterModeCost(tables.ModeCosts, requestedMode, referenceMotionVectors.ModeContext);

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
                        long translationLumaSquaredError = 0;
                        Av1RateDistortionStatistics estimate = this.GetInterFilterModelCost(
                            in interWorkspace,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
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
                            interWorkspace.LumaPrediction,
                            interWorkspace.BluePrediction,
                            interWorkspace.RedPrediction,
                            planeStatistics,
                            ref translationLumaSquaredError);

                        // The estimate predicts luma into pd->dst, which is the frame. Reference: av1_enc_build_inter_predictor() in
                        // simple_translation_pred_rd().
                        this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, interWorkspace.LumaPrediction);

                        // Reference: the plane 0 pred_sse store of model_rd_for_sb_with_curvfit() in
                        // simple_translation_pred_rd().
                        this.SetPredictionSse(modeInfo.Block.ReferenceFrame, translationLumaSquaredError);
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

            // The block keeps the interpolation families of the previous list entry until the next entry searches
            // its filters, so a new-motion search predicts with them. Reference: set_default_interp_filters() in
            // init_mbmi(), and the mbmi left by av1_interpolation_filter_search() between the ref_mv_idx passes of
            // handle_inter_mode().
            Av1InterpolationFilter heldHorizontalFilter = defaultFilter;
            Av1InterpolationFilter heldVerticalFilter = defaultFilter;

            // A new-motion entry keeps its motion modes only when the vector that its last trial leaves in the block
            // differs from the reference; otherwise the entry yields nothing and the block keeps the filters of that
            // trial. The winner from before the entry is held in the spare buffers until the test, which runs when
            // the next entry starts and after the last. Reference: the av1_check_newmv_joint_nonzero() test at the
            // end of motion_mode_rd().
            Span<TSample> spareLumaReconstruction = interWorkspace.SpareLumaReconstruction;
            Span<TSample> spareBlueReconstruction = interWorkspace.SpareBlueReconstruction;
            Span<TSample> spareRedReconstruction = interWorkspace.SpareRedReconstruction;
            Span<int> spareLumaCoefficients = interWorkspace.SpareLumaCoefficients;
            Span<int> spareBlueCoefficients = interWorkspace.SpareBlueCoefficients;
            Span<int> spareRedCoefficients = interWorkspace.SpareRedCoefficients;
            bool entryPending = false;
            bool winnerHeld = false;
            Av1MotionVector entryReference = default;
            Av1MotionVector lastTrialVector = default;
            Av1InterpolationFilter lastTrialHorizontalFilter = defaultFilter;
            Av1InterpolationFilter lastTrialVerticalFilter = defaultFilter;
            Av1RateDistortionStatistics priorWinnerStatistics = default;
            Av1MotionVector priorWinnerVector = default;
            Av1PredictionMode priorWinnerMode = default;
            int priorWinnerReferenceIndex = default;
            bool priorWinnerSkip = default;
            bool priorWinnerInterIntra = default;
            Av1MotionMode priorWinnerMotionMode = default;
            Av1InterIntraMode priorWinnerInterIntraMode = default;
            bool priorWinnerInterIntraWedge = default;
            int priorWinnerInterIntraWedgeIndex = default;
            InlineArray64<Av1EncoderTransformBlockState> priorWinnerLumaStates = default;
            InlineArray16<Av1TransformSize> priorWinnerLumaSizes = default;
            InlineArray16<Av1EncoderTransformBlockState> priorWinnerBlueState = default;
            InlineArray16<Av1EncoderTransformBlockState> priorWinnerRedState = default;
            Av1InterpolationFilter priorWinnerVerticalFilter = default;
            Av1InterpolationFilter priorWinnerHorizontalFilter = default;

            // Rank interpolation families with prediction-error modeling before running a full transform search.
            // The selected inter reconstruction remains untouched while two existing prediction views alternate.
            Span<long> singleReferenceFilterCosts = this.blockWorkspace.SingleReferenceFilterCosts;
            Span<long> singleReferenceSimpleCosts = this.blockWorkspace.SingleReferenceSimpleCosts;
            for (int candidateIndex = 0; candidateIndex <= candidateCount; candidateIndex++)
            {
                if (entryPending)
                {
                    entryPending = false;
                    if (lastTrialVector == entryReference)
                    {
                        heldHorizontalFilter = lastTrialHorizontalFilter;
                        heldVerticalFilter = lastTrialVerticalFilter;
                        if (winnerHeld)
                        {
                            Span<TSample> discardedLumaReconstruction = selectedLumaReconstruction;
                            selectedLumaReconstruction = spareLumaReconstruction;
                            spareLumaReconstruction = discardedLumaReconstruction;
                            Span<TSample> discardedBlueReconstruction = selectedBlueReconstruction;
                            selectedBlueReconstruction = spareBlueReconstruction;
                            spareBlueReconstruction = discardedBlueReconstruction;
                            Span<TSample> discardedRedReconstruction = selectedRedReconstruction;
                            selectedRedReconstruction = spareRedReconstruction;
                            spareRedReconstruction = discardedRedReconstruction;
                            Span<int> discardedLumaCoefficients = selectedLumaCoefficients;
                            selectedLumaCoefficients = spareLumaCoefficients;
                            spareLumaCoefficients = discardedLumaCoefficients;
                            Span<int> discardedBlueCoefficients = selectedBlueCoefficients;
                            selectedBlueCoefficients = spareBlueCoefficients;
                            spareBlueCoefficients = discardedBlueCoefficients;
                            Span<int> discardedRedCoefficients = selectedRedCoefficients;
                            selectedRedCoefficients = spareRedCoefficients;
                            spareRedCoefficients = discardedRedCoefficients;
                            selectedStatistics = priorWinnerStatistics;
                            selectedVector = priorWinnerVector;
                            selectedMode = priorWinnerMode;
                            selectedReferenceIndex = priorWinnerReferenceIndex;
                            selectedSkip = priorWinnerSkip;
                            selectedInterIntra = priorWinnerInterIntra;
                            selectedMotionMode = priorWinnerMotionMode;
                            selectedInterIntraMode = priorWinnerInterIntraMode;
                            selectedInterIntraWedge = priorWinnerInterIntraWedge;
                            selectedInterIntraWedgeIndex = priorWinnerInterIntraWedgeIndex;
                            selectedLumaStates = priorWinnerLumaStates;
                            selectedLumaSizes = priorWinnerLumaSizes;
                            selectedBlueState = priorWinnerBlueState;
                            selectedRedState = priorWinnerRedState;
                            selectedVerticalFilter = priorWinnerVerticalFilter;
                            selectedHorizontalFilter = priorWinnerHorizontalFilter;
                        }
                    }

                    winnerHeld = false;
                }

                if (candidateIndex == candidateCount)
                {
                    break;
                }

                if ((candidateMask & (1 << candidateIndex)) == 0)
                {
                    continue;
                }

                if (tplPruning && this.PrunesInterModeByTpl(
                    Math.Min(bestCost, selectedStatistics.Cost),
                    referenceFrame,
                    Av1ReferenceFrameType.None,
                    candidateReferenceIndices[candidateIndex],
                    requestedMode))
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
                        drlRate += Av1SymbolEncoder.GetDynamicReferenceListCost(tables.ModeCosts, advance, context);
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
                            // Every earlier list entry is compared, searched or not, by the vector of the list
                            // itself; the candidate array holds the search result of an entry already searched.
                            // Reference: the av1_get_ref_mv_from_stack() call of handle_newmv().
                            Av1MotionVector previousReference = referenceMotionVectors.GetNewReference(index);
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
                            Av1MotionVector matchReference = referenceMotionVectors.GetNewReference(bestMatch);
                            int displacement = Math.Max(
                                Math.Abs(previous.Vector.Row - matchReference.Row),
                                Math.Abs(previous.Vector.Column - matchReference.Column));

                            searchRange = (minimumDifference + displacement + 4) >> 3;
                        }
                    }

                    Point startVector = new(
                        (referenceVector.Column + 3 + (referenceVector.Column >= 0 ? 1 : 0)) >> 3,
                        (referenceVector.Row + 3 + (referenceVector.Row >= 0 ? 1 : 0)) >> 3);

                    // The temporal dependency vectors of the covered 16x16 blocks join the spatial start, unless the
                    // speed limits the full-pixel search to the spatial start. Reference: the full_pixel_search_level
                    // test and get_mv_candidate_from_tpl() in av1_single_motion_search().
                    motionStarts[0] = new Av1MotionSearchBase.StartingCandidate(startVector, 0);
                    int startCount = 1;
                    int startWeight = 0;
                    if (!motionSettings.LimitFullPixelStartingCandidates)
                    {
                        startCount = Av1MotionSearchBase.CollectStartingCandidates(
                            this.blockWorkspace.TplSuperblockVectors,
                            this.tplSuperblockBlockCount,
                            this.tplSuperblockStride,
                            this.picture.Sequence.SequenceHeader.SuperblockSize.Get4x4WideCount(),
                            modeInfoPosition,
                            blockSize,
                            (int)referenceFrame - (int)Av1ReferenceFrameType.Last,
                            motionStarts,
                            out startWeight);
                    }

                    if (!motionSearch.Search(
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        motionSettings,
                        this.picture.Parent.MotionSearchStepParameter,
                        spatialMagnitude,
                        frameHeader.ShowFrame,
                        searchRange,
                        frameHeader.ForceIntegerMotionVector,
                        frameHeader.AllowHighPrecisionMotionVector,
                        fineMeshInterval: this.UsesFineSearchInterval,
                        referenceIndex,
                        referenceVector,
                        drlRate,
                        heldHorizontalFilter,
                        heldVerticalFilter,
                        motionStarts[..startCount],
                        startWeight,
                        ref motionState,
                        out Av1MotionSearchBase.FractionalResult searchResult))
                    {
                        continue;
                    }

                    // A search result is kept for the compound modes even when the entry is then skipped as a repeat
                    // of an earlier one. Reference: the single_newmv_valid update before the mode_info skip return
                    // in handle_newmv().
                    searchedNewVectors[referenceIndex] = searchResult.Vector;
                    searchedNewVectorMask |= (byte)(1 << referenceIndex);
                    if (motionState.References[referenceIndex].Skip)
                    {
                        continue;
                    }

                    candidateVectors[candidateIndex] = searchResult.Vector;

                    // The prediction error of the search is kept for the zero-vector pruning. Reference: the
                    // best_single_sse_in_refs update after handle_newmv() in handle_inter_mode(), which reads the
                    // pred_sse that the fractional search leaves. A forced integer vector skips that search, so the
                    // value an earlier candidate of the reference left is read. Reference: use_fractional_mv in
                    // av1_single_motion_search().
                    if (!frameHeader.ForceIntegerMotionVector)
                    {
                        this.SetPredictionSse(referenceFrame, searchResult.SquaredError);
                    }

                    uint searchSse = this.predictionSses[(int)referenceFrame];
                    if (searchSse < this.bestSingleReferenceSses[(int)referenceFrame])
                    {
                        this.bestSingleReferenceSses[(int)referenceFrame] = searchSse;
                    }
                }

                modeInfo.Block.Mode = requestedMode;

                // Estimated mode evaluation keeps the budget of the entry for every motion mode trial; a full search
                // lowers it to each trial it completes. Reference: the ref_best_rd update of the full transform
                // search path of motion_mode_rd(), which the estimation path does not have.
                long candidateLimit = Math.Min(bestCost, selectedStatistics.Cost);

                // A completed trial bounds the later trials of the entry by its cost before the image tune bias.
                // Reference: the ref_best_rd update of motion_mode_rd().
                long entryTrialCost = long.MaxValue;
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
                        in tables,
                        in interWorkspace,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
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

                    singleReferenceFilterCosts[filterCostIndex] = filterModelCost;
                    heldHorizontalFilter = modeInfo.Block.HorizontalInterpolationFilter;
                    heldVerticalFilter = modeInfo.Block.VerticalInterpolationFilter;
                    if (settings.ModelBasedInterpolationBreakout &&
                        candidateLimit != long.MaxValue && (filterModelCost >> 3) * 3 > candidateLimit)
                    {
                        continue;
                    }
                }
                else if (Av1TileWriter.UsesSwitchableInterpolation(frameHeader, modeInfo.Block))
                {
                    int verticalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(
                        modeInfo.Block,
                        modeInfoGrid,
                        modeInfoAllocation,
                        macroBlock,
                        0);

                    filterRate = Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(tables.ModeCosts, defaultFilter, verticalContext);

                    if (sequenceHeader.EnableDualFilter)
                    {
                        int horizontalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(
                            modeInfo.Block,
                            modeInfoGrid,
                            modeInfoAllocation,
                            macroBlock,
                            1);

                        filterRate += Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(tables.ModeCosts, defaultFilter, horizontalContext);
                    }
                }

                // A new vector equal to its reference codes no difference, so simple translation skips it; the other
                // motion modes search their own vectors. Reference: av1_check_newmv_joint_nonzero() in the mode_index
                // loop of motion_mode_rd().
                bool newEntry = requestedMode == Av1PredictionMode.NewMotionVector;
                Av1MotionVector newReference = newEntry
                    ? referenceMotionVectors.GetNewReference(candidateReferenceIndices[candidateIndex])
                    : default;

                bool simpleSkipped = newEntry && candidateVectors[candidateIndex] == newReference;
                if (simpleSkipped && !searchMotionModes)
                {
                    continue;
                }

                Av1InterpolationFilter horizontalFilter = modeInfo.Block.HorizontalInterpolationFilter;
                Av1InterpolationFilter verticalFilter = modeInfo.Block.VerticalInterpolationFilter;
                entryPending = newEntry;
                entryReference = newReference;
                lastTrialVector = candidateVectors[candidateIndex];
                lastTrialHorizontalFilter = horizontalFilter;
                lastTrialVerticalFilter = verticalFilter;
                if (entryPending)
                {
                    priorWinnerStatistics = selectedStatistics;
                    priorWinnerVector = selectedVector;
                    priorWinnerMode = selectedMode;
                    priorWinnerReferenceIndex = selectedReferenceIndex;
                    priorWinnerSkip = selectedSkip;
                    priorWinnerInterIntra = selectedInterIntra;
                    priorWinnerMotionMode = selectedMotionMode;
                    priorWinnerInterIntraMode = selectedInterIntraMode;
                    priorWinnerInterIntraWedge = selectedInterIntraWedge;
                    priorWinnerInterIntraWedgeIndex = selectedInterIntraWedgeIndex;
                    priorWinnerLumaStates = selectedLumaStates;
                    priorWinnerLumaSizes = selectedLumaSizes;
                    priorWinnerBlueState = selectedBlueState;
                    priorWinnerRedState = selectedRedState;
                    priorWinnerVerticalFilter = selectedVerticalFilter;
                    priorWinnerHorizontalFilter = selectedHorizontalFilter;
                }

                int simplePredictionRate = commonPredictionRate + filterRate +
                    (interIntraEligible ? Av1SymbolEncoder.GetInterIntraCost(tables.ModeCosts, blockSize, false, default, false, 0) : 0) +
                    this.GetSimpleTranslationRate(in tables, macroBlock, blockOrigin, blockSize, referenceFrame, requestedMode);

                Av1RateDistortionStatistics candidateStatistics = Av1RateDistortionStatistics.Invalid;
                bool candidateSkip = false;
                InlineArray64<Av1EncoderTransformBlockState> candidateLumaStatesStorage = default;
                InlineArray16<Av1TransformSize> candidateLumaSizesStorage = default;
                InlineArray16<Av1EncoderTransformBlockState> candidateBlueStateStorage = default;
                InlineArray16<Av1EncoderTransformBlockState> candidateRedStateStorage = default;
                Span<Av1EncoderTransformBlockState> candidateLumaStates = candidateLumaStatesStorage;
                Span<Av1TransformSize> candidateLumaSizes = candidateLumaSizesStorage;
                Span<Av1EncoderTransformBlockState> candidateBlueState = candidateBlueStateStorage;
                Span<Av1EncoderTransformBlockState> candidateRedState = candidateRedStateStorage;
                if (!simpleSkipped)
                {
                    this.transformSearchReset = true;
                    candidateStatistics = this.EvaluateInterCandidate(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in interWorkspace,
                        in motionVectorCosts,
                        transformPrediction,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        Math.Min(bestCost, selectedStatistics.Cost),
                        block.HasChroma,
                        simplePredictionRate,
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
                        referenceMotionVectors,
                        candidateLumaReconstruction,
                        candidateLumaCoefficients,
                        candidateBlueReconstruction,
                        candidateBlueCoefficients,
                        candidateRedReconstruction,
                        candidateRedCoefficients,
                        out candidateSkip,
                        out candidateLumaStatesStorage,
                        out candidateLumaSizesStorage,
                        out candidateBlueStateStorage,
                        out candidateRedStateStorage);

                    entryTrialCost = this.unbiasedInterTrialCost;
                }

                singleReferenceSimpleCosts[filterCostIndex] = candidateStatistics.Cost;

                // The block leaves this entry with the filters of its best motion mode, or of its last motion mode
                // trial when none succeeds; a warped trial holds the regular filter. Reference: the best_mbmi
                // restore and the base_mbmi reset of each mode_index in motion_mode_rd().
                long motionModeCost = candidateStatistics.Cost;

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
                    selectedMotionMode = Av1MotionMode.SimpleTranslation;
                    selectedLumaStates = candidateLumaStatesStorage;
                    selectedLumaSizes = candidateLumaSizesStorage;
                    selectedBlueState = candidateBlueStateStorage;
                    selectedRedState = candidateRedStateStorage;

                    // The first winner of a new-motion entry keeps the earlier winner in the spare buffers until the entry's
                    // final vector test.
                    if (entryPending && !winnerHeld)
                    {
                        Span<TSample> spareLumaReconstructionBuffer = spareLumaReconstruction;
                        spareLumaReconstruction = candidateLumaReconstruction;
                        candidateLumaReconstruction = spareLumaReconstructionBuffer;
                        Span<TSample> spareBlueReconstructionBuffer = spareBlueReconstruction;
                        spareBlueReconstruction = candidateBlueReconstruction;
                        candidateBlueReconstruction = spareBlueReconstructionBuffer;
                        Span<TSample> spareRedReconstructionBuffer = spareRedReconstruction;
                        spareRedReconstruction = candidateRedReconstruction;
                        candidateRedReconstruction = spareRedReconstructionBuffer;
                        Span<int> spareLumaCoefficientsBuffer = spareLumaCoefficients;
                        spareLumaCoefficients = candidateLumaCoefficients;
                        candidateLumaCoefficients = spareLumaCoefficientsBuffer;
                        Span<int> spareBlueCoefficientsBuffer = spareBlueCoefficients;
                        spareBlueCoefficients = candidateBlueCoefficients;
                        candidateBlueCoefficients = spareBlueCoefficientsBuffer;
                        Span<int> spareRedCoefficientsBuffer = spareRedCoefficients;
                        spareRedCoefficients = candidateRedCoefficients;
                        candidateRedCoefficients = spareRedCoefficientsBuffer;
                        winnerHeld = true;
                    }
                }

                // Without motion_mode_for_winner_cand, each candidate searches OBMC and warped motion after simple
                // translation. A reference kept only because a compound pair uses it searches no other motion mode,
                // and a simple translation whose luma search failed ends the candidate. Reference: the mode_index
                // loop of motion_mode_rd(), with the skip_motion_mode test and the mode_index 0 return.
                if (searchMotionModes && !simpleSkipped && this.lumaSearchFailed)
                {
                    continue;
                }

                if (searchMotionModes)
                {
                    Av1MotionMode lastAllowed = Av1EncoderMotionVariation.GetLastAllowedMotionMode(
                        this.picture, macroBlock, modeInfoPosition, modeInfo.Block);

                    int baseRate = commonPredictionRate +
                        (interIntraEligible ? Av1SymbolEncoder.GetInterIntraCost(tables.ModeCosts, blockSize, false, default, false, 0) : 0);

                    for (Av1MotionMode motionMode = Av1MotionMode.Obmc; motionMode <= lastAllowed; motionMode++)
                    {
                        Av1EncoderBlockModeInfo motionModeInfo = modeInfo.Block;
                        Av1MotionVector motionVector = candidateVectors[candidateIndex];
                        Av1RateDistortionStatistics motionStatistics = this.EvaluateMotionMode(
                            writer,
                            in tables,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            in interWorkspace,
                            motionSearchPrediction,
                            in motionVectorCosts,
                            transformPrediction,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            in transformEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            macroBlock,
                            blockOrigin,
                            motionMode,
                            lastAllowed,
                            baseRate,
                            this.estimateInterCandidates ? candidateLimit : Math.Min(Math.Min(bestCost, selectedStatistics.Cost), entryTrialCost),
                            block.HasChroma,
                            candidateReferenceIndices[candidateIndex],
                            referenceMotionVectors,
                            ref motionModeInfo,
                            ref motionVector,
                            candidateLumaReconstruction,
                            candidateLumaCoefficients,
                            candidateBlueReconstruction,
                            candidateBlueCoefficients,
                            candidateRedReconstruction,
                            candidateRedCoefficients,
                            out bool motionSkip,
                            out InlineArray64<Av1EncoderTransformBlockState> motionLumaStates,
                            out InlineArray16<Av1TransformSize> motionLumaSizes,
                            out InlineArray16<Av1EncoderTransformBlockState> motionBlueState,
                            out InlineArray16<Av1EncoderTransformBlockState> motionRedState);

                        entryTrialCost = Math.Min(entryTrialCost, this.unbiasedInterTrialCost);
                        lastTrialVector = motionVector;
                        lastTrialHorizontalFilter = motionModeInfo.HorizontalInterpolationFilter;
                        lastTrialVerticalFilter = motionModeInfo.VerticalInterpolationFilter;
                        if (motionModeCost == long.MaxValue || motionStatistics.Cost < motionModeCost)
                        {
                            motionModeCost = motionStatistics.Cost;
                            heldHorizontalFilter = motionModeInfo.HorizontalInterpolationFilter;
                            heldVerticalFilter = motionModeInfo.VerticalInterpolationFilter;
                        }

                        if (motionStatistics.Cost >= selectedStatistics.Cost)
                        {
                            continue;
                        }

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
                        selectedStatistics = motionStatistics;
                        selectedVector = motionVector;
                        selectedMode = requestedMode;
                        selectedHorizontalFilter = motionModeInfo.HorizontalInterpolationFilter;
                        selectedVerticalFilter = motionModeInfo.VerticalInterpolationFilter;
                        selectedReferenceIndex = candidateReferenceIndices[candidateIndex];
                        selectedSkip = motionSkip;
                        selectedInterIntra = false;
                        selectedMotionMode = motionMode;
                        selectedLumaStates = motionLumaStates;
                        selectedLumaSizes = motionLumaSizes;
                        selectedBlueState = motionBlueState;
                        selectedRedState = motionRedState;

                        // The first winner of a new-motion entry keeps the earlier winner in the spare buffers until the entry's
                        // final vector test.
                        if (entryPending && !winnerHeld)
                        {
                            Span<TSample> spareLumaReconstructionBuffer = spareLumaReconstruction;
                            spareLumaReconstruction = candidateLumaReconstruction;
                            candidateLumaReconstruction = spareLumaReconstructionBuffer;
                            Span<TSample> spareBlueReconstructionBuffer = spareBlueReconstruction;
                            spareBlueReconstruction = candidateBlueReconstruction;
                            candidateBlueReconstruction = spareBlueReconstructionBuffer;
                            Span<TSample> spareRedReconstructionBuffer = spareRedReconstruction;
                            spareRedReconstruction = candidateRedReconstruction;
                            candidateRedReconstruction = spareRedReconstructionBuffer;
                            Span<int> spareLumaCoefficientsBuffer = spareLumaCoefficients;
                            spareLumaCoefficients = candidateLumaCoefficients;
                            candidateLumaCoefficients = spareLumaCoefficientsBuffer;
                            Span<int> spareBlueCoefficientsBuffer = spareBlueCoefficients;
                            spareBlueCoefficients = candidateBlueCoefficients;
                            candidateBlueCoefficients = spareBlueCoefficientsBuffer;
                            Span<int> spareRedCoefficientsBuffer = spareRedCoefficients;
                            spareRedCoefficients = candidateRedCoefficients;
                            candidateRedCoefficients = spareRedCoefficientsBuffer;
                            winnerHeld = true;
                        }
                    }
                }

                if (!interIntraEligible || !searchMotionModes)
                {
                    continue;
                }

                int interIntraMotionRate = 0;
                if (requestedMode == Av1PredictionMode.NewMotionVector)
                {
                    Av1MotionVector referenceVector = referenceMotionVectors.GetNewReference(candidateReferenceIndices[candidateIndex]);
                    interIntraMotionRate = ((motionVectorCosts.GetCost(candidateVectors[candidateIndex], referenceVector) * 108) + 64) >> 7;
                }

                // The inter-intra trial keeps the interpolation filters.
                if (motionModeCost == long.MaxValue)
                {
                    heldHorizontalFilter = horizontalFilter;
                    heldVerticalFilter = verticalFilter;
                }

                lastTrialVector = candidateVectors[candidateIndex];
                lastTrialHorizontalFilter = horizontalFilter;
                lastTrialVerticalFilter = verticalFilter;

                Av1MotionVector interIntraVector = candidateVectors[candidateIndex];
                if (!this.SelectInterIntraBlend(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in interWorkspace,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    requestedMode,
                    referenceFrame,
                    ref interIntraVector,
                    referenceMotionVectors.GetNewReference(candidateReferenceIndices[candidateIndex]),
                    requestedMode == Av1PredictionMode.NewMotionVector,
                    horizontalFilter,
                    verticalFilter,
                    interIntraMotionRate,
                    this.estimateInterCandidates ? candidateLimit : Math.Min(Math.Min(bestCost, selectedStatistics.Cost), entryTrialCost),
                    ref cachedInterIntraMode,
                    out Av1InterIntraMode interIntraMode,
                    out bool useWedge,
                    out int wedgeIndex))
                {
                    continue;
                }

                // A new vector that the blend search leaves at its reference codes no difference. Reference:
                // av1_check_newmv_joint_nonzero() after av1_handle_inter_intra_mode() in motion_mode_rd().
                lastTrialVector = interIntraVector;
                if (newEntry && interIntraVector == newReference)
                {
                    continue;
                }

                this.PrepareInterIntraPrediction(
                    transformWorkspace,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    block.HasChroma,
                    requestedMode,
                    referenceFrame,
                    interIntraVector,
                    horizontalFilter,
                    verticalFilter,
                    interIntraMode,
                    useWedge,
                    wedgeIndex);

                int interIntraRate = Av1SymbolEncoder.GetInterIntraCost(tables.ModeCosts, blockSize, true, interIntraMode, useWedge, wedgeIndex);

                this.transformSearchReset = true;
                Av1RateDistortionStatistics interIntraStatistics = this.EvaluateInterCandidate(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    in motionVectorCosts,
                    transformPrediction,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    this.estimateInterCandidates ? candidateLimit : Math.Min(Math.Min(bestCost, selectedStatistics.Cost), entryTrialCost),
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
                    referenceMotionVectors,
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

                if (interIntraStatistics.Cost < motionModeCost)
                {
                    heldHorizontalFilter = horizontalFilter;
                    heldVerticalFilter = verticalFilter;
                }

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
                selectedMotionMode = Av1MotionMode.SimpleTranslation;
                selectedInterIntraMode = interIntraMode;
                selectedInterIntraWedge = useWedge;
                selectedInterIntraWedgeIndex = wedgeIndex;
                selectedLumaStates = interIntraLumaStates;
                selectedLumaSizes = interIntraLumaSizes;
                selectedBlueState = interIntraBlueState;
                selectedRedState = interIntraRedState;

                // The first winner of a new-motion entry keeps the earlier winner in the spare buffers until the entry's
                // final vector test.
                if (entryPending && !winnerHeld)
                {
                    Span<TSample> spareLumaReconstructionBuffer = spareLumaReconstruction;
                    spareLumaReconstruction = candidateLumaReconstruction;
                    candidateLumaReconstruction = spareLumaReconstructionBuffer;
                    Span<TSample> spareBlueReconstructionBuffer = spareBlueReconstruction;
                    spareBlueReconstruction = candidateBlueReconstruction;
                    candidateBlueReconstruction = spareBlueReconstructionBuffer;
                    Span<TSample> spareRedReconstructionBuffer = spareRedReconstruction;
                    spareRedReconstruction = candidateRedReconstruction;
                    candidateRedReconstruction = spareRedReconstructionBuffer;
                    Span<int> spareLumaCoefficientsBuffer = spareLumaCoefficients;
                    spareLumaCoefficients = candidateLumaCoefficients;
                    candidateLumaCoefficients = spareLumaCoefficientsBuffer;
                    Span<int> spareBlueCoefficientsBuffer = spareBlueCoefficients;
                    spareBlueCoefficients = candidateBlueCoefficients;
                    candidateBlueCoefficients = spareBlueCoefficientsBuffer;
                    Span<int> spareRedCoefficientsBuffer = spareRedCoefficients;
                    spareRedCoefficients = candidateRedCoefficients;
                    candidateRedCoefficients = spareRedCoefficientsBuffer;
                    winnerHeld = true;
                }
            }

            // Candidate pixels and coefficients remain scratch. Preserve the transform decisions so final
            // reconstruction can regenerate only the winner after other mode families reuse this storage.
            selectedLumaStates[..].CopyTo(selectedStates);
            selectedLumaSizes[..].CopyTo(modeInfo.Block.InterTransformSizes);
            selectedBlueState[..].CopyTo(selectedStates[64..80]);
            selectedRedState[..].CopyTo(selectedStates[80..96]);

            modeInfo.Block.Mode = selectedMode;
            modeInfo.Block.Skip = selectedSkip;
            modeInfo.Block.MotionMode = selectedMotionMode;
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
        /// Gets the rate of signaling simple translation for a single-reference block that may use OBMC or warped
        /// motion. Reference: the motion_mode_cost and motion_mode_cost1 terms of motion_mode_rd().
        /// </summary>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="referenceFrame">The reference frame of the block.</param>
        /// <param name="mode">The single-reference mode.</param>
        /// <returns>The rate in 1/512-bit units.</returns>
        private int GetSimpleTranslationRate(
            in Av1CoefficientTables tables,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType referenceFrame,
            Av1PredictionMode mode)
        {
            Av1EncoderBlockModeInfo candidate = new()
            {
                BlockSize = blockSize,
                ReferenceFrame = referenceFrame,
                SecondaryReferenceFrame = Av1ReferenceFrameType.None,
                Mode = mode
            };

            Point position = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            return Av1EncoderMotionVariation.GetLastAllowedMotionMode(this.picture, macroBlock, position, candidate) switch
            {
                Av1MotionMode.Warped => tables.ModeCosts.GetMotionMode(blockSize, Av1MotionMode.SimpleTranslation),
                Av1MotionMode.Obmc => tables.ModeCosts.GetObmc(blockSize, Av1MotionMode.SimpleTranslation),
                _ => 0
            };
        }

        /// <summary>
        /// Selects the intra mode and blend mask before the complete inter residual search.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The intra prediction storage of the blend.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="compoundMask">The blend mask storage.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="predictionMode">The single-reference mode.</param>
        /// <param name="referenceFrame">The reference frame.</param>
        /// <param name="vector">The motion vector, which the wedge search may refine.</param>
        /// <param name="referenceVector">The reference of a new vector.</param>
        /// <param name="searchMotion">Whether the wedge blend refines the vector.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="motionRate">The differential vector rate of the candidate.</param>
        /// <param name="bestCost">The cost above which the search stops.</param>
        /// <param name="cachedMode">The inter-intra mode that an earlier search of the reference chose.</param>
        /// <param name="selectedMode">The selected inter-intra mode.</param>
        /// <param name="selectedWedge">Whether the blend uses a wedge mask.</param>
        /// <param name="selectedWedgeIndex">The wedge mask index.</param>
        /// <returns><see langword="true"/> when a blend is selected.</returns>
        private bool SelectInterIntraBlend(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PredictionMode predictionMode,
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
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            Av1ModeCosts modeCosts = tables.ModeCosts;
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int sampleCount = width * height;
            Span<TSample> interPrediction = interWorkspace.TransformReconstruction[..sampleCount];
            Span<TSample> blendedPrediction = interWorkspace.LumaPrediction[..sampleCount];
            Span<short> intraResidual = interWorkspace.Residual[..sampleCount];
            Span<short> interResidual = interWorkspace.Residual.Slice(sampleCount, sampleCount);
            Span<byte> mask = compoundMask[..sampleCount];
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);

            // The wedge motion refinement reads a reference of another size through its resized copy. Reference:
            // the scaled_ref_frame of av1_compound_single_motion_search().
            Av1PlaneRegion<TSample> referencePlane = this.searchReferences.Span[(int)referenceFrame].CodedView.GetPlane(Av1Plane.Y);
            this.PrepareSingleInterPrediction(
                predictionMode,
                referenceFrame,
                blockSize,
                Av1Plane.Y,
                blockOrigin,
                0,
                0,
                vector,
                horizontalFilter,
                verticalFilter,
                blockSize,
                interPrediction,
                interResidual,
                interWorkspace.FilterRows);

            int shift = (this.bitDepth.GetBitCount() - 8) * 2;
            int acQuantizer = Av1QuantizationLookup.GetAcQuant(this.blockQIndex, this.quantization.DeltaQAc[0], this.bitDepth);
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

                Span<TSample> intraPrediction = this.PrepareInterIntraPlane(
                    transformWorkspace,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    Av1Plane.Y,
                    mode);

                interPrediction.CopyTo(blendedPrediction);
                Av1InterIntraMaskBuilder.FillInterIntraMask(mask, width, width, height, mode, invert: true);
                TOperator.BlendInterIntraPrediction(blendedPrediction, intraPrediction, mask, width, height);

                // The blend goes into pd->dst, which is the frame after the single prediction into tmp_buf.
                // Reference: av1_combine_interintra() in compute_best_interintra_mode(), after restore_dst_buf() in
                // av1_handle_inter_intra_mode().
                this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, blendedPrediction);
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

                // The model stores its luma error as the reference's prediction error, except when the cached mode
                // is reused without a model. Reference: model_rd_for_sb_with_curvfit() in
                // compute_best_interintra_mode(), which handle_smooth_inter_intra_mode() skips for a cached mode.
                if (!settings.ReuseInterIntraMode || cachedMode < 0)
                {
                    this.SetPredictionSse(referenceFrame, error);
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
            Span<TSample> selectedIntra = this.PrepareInterIntraPlane(
                transformWorkspace,
                in interWorkspace,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                blockSize,
                Av1Plane.Y,
                selectedMode);

            interPrediction.CopyTo(blendedPrediction);
            Av1InterIntraMaskBuilder.FillInterIntraMask(mask, width, width, height, selectedMode, invert: true);
            TOperator.BlendInterIntraPrediction(blendedPrediction, selectedIntra, mask, width, height);

            // Reference: the av1_combine_interintra() rebuild of the best mode in handle_smooth_inter_intra_mode().
            this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, blendedPrediction);
            int smoothRate = motionRate + modeCosts.GetInterIntraMode(blockSize, selectedMode) + modeCosts.GetWedgeInterIntra(blockSize, 0);
            long smoothBound = bestCost < 9 * (long.MaxValue / 16) ? (bestCost / 9) * 16 : long.MaxValue;
            smoothBound -= Av1RateDistortion.GetCost(this.rateMultiplier, smoothRate, 0);
            long smoothCost = this.EstimateInterPredictionResidual(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                in interWorkspace,
                in transformEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                blockSize,
                smoothRate,
                smoothBound,
                out _);

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
                Span<TSample> wedgeIntra = this.PrepareInterIntraPlane(
                    transformWorkspace,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    Av1Plane.Y,
                    mode);

                // The inter prediction minus the intra prediction, over the intra residual that is not
                // read again. Reference: the diff10 of pick_interintra_wedge().
                TOperator.SubtractPackedPrediction(interPrediction, wedgeIntra, intraResidual, width, height);
                long bestMaskCost = long.MaxValue;
                int bestMaskIndex = 0;
                for (int index = 0; index < 16; index++)
                {
                    Av1WedgeMask.Fill(mask, width, blockSize, index, wedgeSign: false, 0, 0, invert: false);
                    long squaredError = (long)Av1WedgeSearch.SumSquaredErrors(interResidual, intraResidual, mask);
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

            selectedIntra = this.PrepareInterIntraPlane(
                transformWorkspace,
                in interWorkspace,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                blockSize,
                Av1Plane.Y,
                wedgeMode);

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
                    sourcePlane.Samples[sourceOrigin..],
                    sourcePlane.Stride,
                    referencePlane.Samples,
                    referencePlane.Stride,
                    referenceOrigin,
                    size,
                    this.ApplySharpnessMargins(referenceVector.GetFullPixelSearchBounds(bounds), blockOrigin, size, 1),
                    referenceVector,
                    motionVectorCosts,
                    this.bitDepth,
                    Av1RateDistortion.GetMotionSearchSadPerBit(this.blockQIndex, this.bitDepth),
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
                        sourcePlane.Samples[sourceOrigin..],
                        sourcePlane.Stride,
                        referencePlane.Samples,
                        referencePlane.Stride,
                        referenceOrigin,
                        interWorkspace.BluePrediction,
                        size,
                        this.ApplySharpnessMargins(referenceVector.GetSubpixelSearchBounds(bounds), blockOrigin, size, Av1MotionVector.SubpixelScale),
                        referenceVector,
                        motionVectorCosts,
                        this.bitDepth,
                        this.rateMultiplier,
                        selectedIntra,
                        mask,
                        this.GetScaledSearchReference(referenceFrame, blockOrigin, interWorkspace.FilterRows));

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
                    if (this.IsScaledReference(referenceFrame))
                    {
                        this.PrepareScaledInterPrediction(
                            referenceFrame,
                            Av1Plane.Y,
                            blockOrigin,
                            0,
                            0,
                            trialVector,
                            horizontalFilter,
                            verticalFilter,
                            blockSize,
                            blendedPrediction,
                            intraResidual,
                            interWorkspace.FilterRows);
                    }
                    else
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
                            interWorkspace.FilterRows,
                            blockSize,
                            this.bitDepth);
                    }

                    TOperator.BlendInterIntraPrediction(blendedPrediction, selectedIntra, mask, width, height);

                    // The refined vector predicts luma into pd->dst, and the blend goes there too. Reference: av1_enc_build_inter_predictor()
                    // and av1_combine_interintra() after av1_compound_single_motion_search() in handle_wedge_inter_intra_mode().
                    this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, blendedPrediction);
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

                    // Reference: the model_rd_for_sb_with_curvfit() store to pred_sse after the refined vector of
                    // handle_wedge_inter_intra_mode().
                    this.SetPredictionSse(referenceFrame, error);
                    Av1RateDistortion.ModelPredictionError(
                        blockSize,
                        error,
                        visible.Width * visible.Height,
                        acQuantizer,
                        this.bitDepth,
                        this.rateMultiplier,
                        out int rate,
                        out long distortion);

                    int trialMotionRate = ((motionVectorCosts.GetCost(trialVector, referenceVector) * 108) + 64) >> 7;
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

                // Reference: the av1_combine_interintra() call for rd >= *best_rd in handle_wedge_inter_intra_mode().
                this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, blendedPrediction);
            }

            int selectedWedgeRate = wedgeMotionRate + wedgeSyntaxRate;
            long wedgeBound = smoothCost - Av1RateDistortion.GetCost(this.rateMultiplier, selectedWedgeRate, 0);
            long wedgeCost = this.EstimateInterPredictionResidual(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                in interWorkspace,
                in transformEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                blockSize,
                selectedWedgeRate,
                wedgeBound,
                out _);

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
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block, whose luma prediction is current.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="modeRate">The prediction syntax rate of the blend.</param>
        /// <param name="residualBound">The cost that the residual may still add, or a negative value when none is left.</param>
        /// <param name="residualStatistics">The rate and distortion of the estimated residual.</param>
        /// <returns>The cost of the blend, or <see cref="long.MaxValue"/> when the estimate stops.</returns>
        private long EstimateInterPredictionResidual(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int modeRate,
            long residualBound,
            out Av1RateDistortionStatistics residualStatistics)
        {
            residualStatistics = Av1RateDistortionStatistics.Invalid;
            if (residualBound < 0)
            {
                return long.MaxValue;
            }

            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            Av1PlaneRegion<TSample> lumaSource = this.source.GetPlane(Av1Plane.Y);
            TOperator.SubtractPrediction(
                Av1TransformBlockEncoder.GetPlaneSpan(lumaSource, blockOrigin),
                lumaSource.Stride,
                interWorkspace.LumaPrediction,
                interWorkspace.Residual,
                width,
                height);

            // A block crossing the frame edge is subtracted with the DCT_DCT border padding. Reference: the
            // av1_subtract_plane() call of estimate_yrd_for_sb().
            Av1TransformBlockEncoder.PadBorderResidual(
                this.blockWorkspace, Av1Plane.Y, blockOrigin, interWorkspace.Residual, width, width, height, Av1TransformType.DctDct);

            Av1TransformSize transformSize = blockSize.GetMaximumTransformSize();
            ReadOnlySpan<byte> above = lumaCoefficientEdges.Top.Slice(lumaCoefficientEdges.GetTopIndex(blockOrigin), blockSize.Get4x4WideCount());
            ReadOnlySpan<byte> left = lumaCoefficientEdges.Left.Slice(lumaCoefficientEdges.GetLeftIndex(blockOrigin), blockSize.Get4x4HighCount());

            // Mode evaluation that defers the transform size search codes the largest transform, which signals no
            // split flag. Reference: the tx_select test of av1_estimate_txfm_yrd().
            int transformRate = 0;
            if (this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select && blockSize > Av1BlockSize.Block4x4 &&
                (!this.picture.Parent.SpeedSettings.DeferTransformSizeSearch ||
                    this.blockWorkspace.EvaluationStage != Av1EncoderEvaluationStage.Candidate))
            {
                int context = Av1SymbolContextHelper.GetTransformPartitionContext(
                    transformEdges.Top[transformEdges.GetTopIndex(blockOrigin)],
                    transformEdges.Left[transformEdges.GetLeftIndex(blockOrigin)],
                    blockSize,
                    transformSize);

                transformRate = Av1SymbolEncoder.GetTransformPartitionCost(tables.ModeCosts, false, context);
            }

            int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            Av1ModeCosts modeCosts = tables.ModeCosts;
            int noSkipRate = Av1SymbolEncoder.GetSkipCost(modeCosts, false, skipContext);
            int skipRate = Av1SymbolEncoder.GetSkipCost(modeCosts, true, skipContext);
            long cost = Av1TransformBlockEncoder.EstimateInterTransform(
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                interWorkspace.Residual,
                width,
                interWorkspace.TransformCoefficients,
                writer,
                in tables,
                above,
                left,
                blockSize,
                GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0),
                transformSize,
                this.blockQIndex,
                this.quantization.DeltaQDc[0],
                this.bitDepth,
                this.blockWorkspace.EncoderOptions.Sharpness,
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
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="transformPrediction">The intra prediction destination.</param>
        /// <param name="interIntraAbove">The storage of the extended above edge.</param>
        /// <param name="interIntraLeft">The storage of the extended left edge.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="plane">The plane to predict.</param>
        /// <param name="interIntraMode">The intra mode of the blend.</param>
        /// <returns>The intra prediction of the plane.</returns>
        private Span<TSample> PrepareInterIntraPlane(
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1Plane plane,
            Av1InterIntraMode interIntraMode)
        {
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
                macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, 0).Block.PartitionType,
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
                macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, 0).Block.PartitionType,
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
                interIntraAbove,
                interIntraLeft);

            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            TOperator.PrepareIntra(
                transformWorkspace,
                Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, planeOrigin),
                sourcePlane.Stride,
                transformPrediction,
                width,
                interIntraAbove.Slice(1, width + height),
                interIntraLeft.Slice(1, width + height),
                hasLeft,
                hasAbove,
                intraMode,
                0,
                this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                this.UseSmoothIntraEdges(modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize, plane),
                interWorkspace.Residual,
                transformSize,
                this.bitDepth);

            return transformPrediction[..sampleCount];
        }

        /// <summary>
        /// Builds every active plane of the selected inter-intra prediction.
        /// </summary>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="transformPrediction">The intra prediction storage of the blend.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="compoundMask">The blend mask storage.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="hasChroma">Whether the block codes chroma.</param>
        /// <param name="mode">The single-reference mode.</param>
        /// <param name="referenceFrame">The reference frame.</param>
        /// <param name="vector">The motion vector.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="interIntraMode">The intra mode of the blend.</param>
        /// <param name="useWedge">Whether the blend uses a wedge mask.</param>
        /// <param name="wedgeIndex">The wedge mask index.</param>
        private void PrepareInterIntraPrediction(
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool hasChroma,
            Av1PredictionMode mode,
            Av1ReferenceFrameType referenceFrame,
            Av1MotionVector vector,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Av1InterIntraMode interIntraMode,
            bool useWedge,
            int wedgeIndex)
        {
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
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
                    Av1Plane.Y => interWorkspace.LumaPrediction[..sampleCount],
                    Av1Plane.U => interWorkspace.BluePrediction[..sampleCount],
                    _ => interWorkspace.RedPrediction[..sampleCount],
                };

                this.PrepareSingleInterPrediction(
                    mode,
                    referenceFrame,
                    blockSize,
                    plane,
                    planeOrigin,
                    subX,
                    subY,
                    vector,
                    horizontalFilter,
                    verticalFilter,
                    transformSize.ToBlockSize(),
                    interPrediction,
                    interWorkspace.Residual,
                    interWorkspace.FilterRows);

                Span<TSample> intraPrediction = this.PrepareInterIntraPlane(
                    transformWorkspace,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    plane,
                    interIntraMode);

                if (useWedge)
                {
                    Av1WedgeMask.Fill(compoundMask, width, blockSize, wedgeIndex, wedgeSign: false, subX, subY, invert: true);
                }
                else
                {
                    Av1InterIntraMaskBuilder.FillInterIntraMask(compoundMask, width, width, height, interIntraMode, invert: true);
                }

                TOperator.BlendInterIntraPrediction(interPrediction, intraPrediction[..sampleCount], compoundMask, width, height);

                // The blended luma goes into pd->dst, which is the frame. Reference: av1_build_interintra_predictor() in
                // av1_enc_build_inter_predictor().
                if (plane == Av1Plane.Y)
                {
                    this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, interPrediction);
                }
            }
        }

        /// <summary>
        /// Sets the local warped model that single-reference predictions use, or clears it when the block keeps
        /// another motion mode or its model is invalid. Reference: the WARPED_CAUSAL branch of motion_mode_rd(),
        /// with av1_findSamples(), av1_selectSamples() and av1_find_projection().
        /// </summary>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="partitionType">The partition that produced the block.</param>
        /// <param name="mode">The block decisions.</param>
        /// <param name="vector">The block motion vector.</param>
        /// <returns><see langword="true"/> when predictions use a valid warped model.</returns>
        private bool SetWarpedPrediction(
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1PartitionType partitionType,
            Av1EncoderBlockModeInfo mode,
            Av1MotionVector vector)
        {
            this.useWarpedPrediction = false;
            this.useObmcPrediction = false;
            if (mode.MotionMode == Av1MotionMode.Obmc)
            {
                this.obmcBlockOrigin = blockOrigin;
                this.obmcBlockSize = mode.BlockSize;
                this.obmcAboveAvailable = macroBlock.IsUpAvailable;
                this.obmcLeftAvailable = macroBlock.IsLeftAvailable;
                this.useObmcPrediction = true;
                return true;
            }

            if (mode.MotionMode != Av1MotionMode.Warped)
            {
                return false;
            }

            Av1EncoderBlockModeInfo sampleMode = mode;
            sampleMode.PartitionType = partitionType;
            Point position = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Span<Point> sourcePoints = stackalloc Point[Av1EncoderMotionVariation.MaximumSampleCount];
            Span<Point> referencePoints = stackalloc Point[Av1EncoderMotionVariation.MaximumSampleCount];
            int count = Av1EncoderMotionVariation.FindSamples(this.picture, macroBlock, position, sampleMode, sourcePoints, referencePoints);
            if (count == 0)
            {
                return false;
            }

            Av1GlobalMotionParameters model = Av1GlobalMotionParameters.DeriveLocalProjection(
                sourcePoints[..count], referencePoints[..count], mode.BlockSize, vector, position);

            if (model.IsInvalid)
            {
                return false;
            }

            this.warpedModel = model;
            this.useWarpedPrediction = true;
            return true;
        }

        /// <summary>
        /// Returns the reference types that a block reached by a rectangular or extended partition does not search:
        /// those that no square block of the superblock picked over the same area.
        /// Reference: the picked_ref_frames_mask setup of av1_rd_pick_inter_mode() with fetch_picked_ref_frames_mask().
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="partitionType">The partition that produced the block.</param>
        /// <returns>The skipped reference types, one bit per reference type.</returns>
        private int GetSkipReferenceFrameMask(Point blockOrigin, Av1BlockSize blockSize, Av1PartitionType partitionType)
        {
            int level = this.picture.Parent.SpeedSettings.GetRectangularPartitionReferencePruning(this.picture.Parent.FrameUpdateType);
            if (level == 0 || partitionType == Av1PartitionType.None ||
                (partitionType is Av1PartitionType.Horizontal or Av1PartitionType.Vertical && level < 2))
            {
                return 0;
            }

            int superblockMask = (1 << (this.picture.Sequence.SequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2)) - 1;
            int row = (blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2) & superblockMask;
            int column = (blockOrigin.X >> Av1Constants.ModeInfoSizeLog2) & superblockMask;
            int picked = 0;
            int[] masks = this.blockWorkspace.PickedReferenceFrameMasks;
            for (int i = row; i < row + blockSize.Get4x4HighCount(); i++)
            {
                for (int j = column; j < column + blockSize.Get4x4WideCount(); j++)
                {
                    picked |= masks[(i * 32) + j];
                }
            }

            return picked != 0 ? ~picked : 0;
        }

        /// <summary>
        /// Returns whether the selective reference search drops a reference or pair: from level two, LAST2 and LAST3
        /// when they precede GOLDEN, and from level three, ALTREF2 and BWDREF when they precede LAST. A reference whose
        /// predicted-vector SAD is the best of the past references is kept. Without temporal-dependency statistics
        /// no reference is kept by them. A compound pair that is neither kept nor made of the closest past and future
        /// references is dropped at pruning level three, and below it unless it holds the best predicted-vector SAD
        /// on each side.
        /// Reference: prune_ref_by_selective_ref_frame() with prune_ref(), has_closest_ref_frames() and
        /// has_best_pred_mv_sad().
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference, or none for a single reference.</param>
        /// <returns><see langword="true"/> when the reference or pair is dropped.</returns>
        private readonly bool PrunesReferenceBySelectiveReferenceFrame(Av1ReferenceFrameType first, Av1ReferenceFrameType second)
        {
            int level = this.picture.Parent.SpeedSettings.SelectiveReferenceFrameLevel;
            if (level == 0)
            {
                return false;
            }

            bool compound = second > Av1ReferenceFrameType.Intra;
            if (level >= 2 || (level == 1 && compound))
            {
                if (this.PrunesOlderReference(first, second, Av1ReferenceFrameType.Last3, Av1ReferenceFrameType.Golden) ||
                    this.PrunesOlderReference(first, second, Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Golden))
                {
                    return true;
                }
            }

            if (level >= 3 &&
                (this.PrunesOlderReference(first, second, Av1ReferenceFrameType.Alternate2, Av1ReferenceFrameType.Last) ||
                this.PrunesOlderReference(first, second, Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Last)))
            {
                return true;
            }

            Av1PictureParentControlSet parent = this.picture.Parent;
            int compoundPruning = parent.SpeedSettings.CompoundReferencePruningLevel;
            if (!compound || compoundPruning == 0)
            {
                return false;
            }

            bool closestPair = (first == parent.NearestPastReference || second == parent.NearestPastReference) &&
                (first == parent.NearestFutureReference || second == parent.NearestFutureReference);

            bool keptPair = (parent.KeepCompoundReferenceMask & (1 << (int)first)) != 0 &&
                (parent.KeepCompoundReferenceMask & (1 << (int)second)) != 0;

            if (keptPair || closestPair)
            {
                return false;
            }

            if (compoundPruning >= 3)
            {
                return true;
            }

            if (this.bestPastPredictionVectorSad == int.MaxValue || this.bestFuturePredictionVectorSad == int.MaxValue)
            {
                return true;
            }

            int firstSad = this.predictionVectorSads[(int)first];
            int secondSad = this.predictionVectorSads[(int)second];
            bool bestPast = firstSad == this.bestPastPredictionVectorSad || secondSad == this.bestPastPredictionVectorSad;
            bool bestFuture = firstSad == this.bestFuturePredictionVectorSad || secondSad == this.bestFuturePredictionVectorSad;
            return !(bestPast && bestFuture);
        }

        /// <summary>
        /// Prepares the pruning of the block's inter modes by the temporal dependency model: when the speed prunes by
        /// the model and the frame has ready statistics, sums the model prediction error of each reference over the
        /// block and keeps the best among the references that the model keeps or the selective pruning does not drop.
        /// Reference: the prune_inter_modes_based_on_tpl setup of av1_rd_pick_inter_mode() with
        /// get_block_level_tpl_stats(), and prune_modes_based_on_tpl of handle_inter_mode().
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        private void PrepareTplInterModePruning(Point blockOrigin, Av1BlockSize blockSize)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            if (parent.SpeedSettings.TplInterModePruningLevel == 0 || !parent.TplStatisticsReady ||
                parent.TplFrame is not { } tplFrame)
            {
                this.tplInterModePruning = false;
                return;
            }

            // A reference the model keeps, or one the selective pruning keeps on its own, may hold the best cost.
            Span<bool> validReferences = stackalloc bool[Av1TplModelConstants.InterReferenceCount];
            for (Av1ReferenceFrameType reference = Av1ReferenceFrameType.Last; reference <= Av1ReferenceFrameType.Alternate; reference++)
            {
                validReferences[(int)reference - (int)Av1ReferenceFrameType.Last] = this.tplKeepReferenceFrames[(int)reference] ||
                    !this.PrunesReferenceBySelectiveReferenceFrame(reference, Av1ReferenceFrameType.None);
            }

            this.tplInterModePruning = true;
            this.tplBestInterCost = Av1TplModePruning.GetBlockLevelStatistics(
                true,
                tplFrame,
                blockSize,
                blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2,
                blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                validReferences,
                this.tplReferenceInterCosts[..]);
        }

        /// <summary>
        /// Returns whether an inter block of the row above or the column to the left of the block uses one of the
        /// block's references, or that row or column is unavailable. The model then prunes none of the entries of the
        /// block's mode. Reference: find_ref_match_in_above_nbs() and find_ref_match_in_left_nbs() with
        /// ref_match_found_in_nb_blocks().
        /// </summary>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The block's neighbor availability.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference, or none for a single reference.</param>
        /// <returns><see langword="true"/> when a neighbor matches or is unavailable.</returns>
        private readonly bool HasNeighborReferenceMatch(
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType first,
            Av1ReferenceFrameType second)
        {
            if (!macroBlock.IsUpAvailable)
            {
                return true;
            }

            // Each step advances by the width of the above block found at the current column. A neighbor is its
            // allocation entry at the grid cell.
            int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            int endColumn = Math.Min(modeInfoColumn + blockSize.Get4x4WideCount(), this.picture.Parent.Common.ModeInfoColumnCount);
            int stride = this.picture.ModeInfoStride;
            for (int column = modeInfoColumn; column < endColumn;)
            {
                ref readonly Av1EncoderBlockModeInfo above = ref modeInfoAllocation[modeInfoGrid[((modeInfoRow - 1) * stride) + column]].Block;
                if (MatchesNeighborReference(above, first, second))
                {
                    return true;
                }

                column += above.BlockSize.Get4x4WideCount();
            }

            if (!macroBlock.IsLeftAvailable)
            {
                return true;
            }

            int endRow = Math.Min(modeInfoRow + blockSize.Get4x4HighCount(), this.picture.Parent.Common.ModeInfoRowCount);
            for (int row = modeInfoRow; row < endRow;)
            {
                ref readonly Av1EncoderBlockModeInfo left = ref modeInfoAllocation[modeInfoGrid[(row * stride) + modeInfoColumn - 1]].Block;
                if (MatchesNeighborReference(left, first, second))
                {
                    return true;
                }

                row += left.BlockSize.Get4x4HighCount();
            }

            return false;
        }

        /// <summary>
        /// Returns whether a neighbor is an inter block that uses the first reference, or the second of a compound
        /// pair. Reference: is_inter_block() and ref_match_found_in_nb_blocks().
        /// </summary>
        /// <param name="neighbor">The neighbor's mode.</param>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference, or none for a single reference.</param>
        /// <returns><see langword="true"/> when a reference matches.</returns>
        private static bool MatchesNeighborReference(
            Av1EncoderBlockModeInfo neighbor,
            Av1ReferenceFrameType first,
            Av1ReferenceFrameType second)
        {
            if (!neighbor.UseIntraBlockCopy && neighbor.ReferenceFrame <= Av1ReferenceFrameType.Intra)
            {
                return false;
            }

            return first == neighbor.ReferenceFrame || first == neighbor.SecondaryReferenceFrame ||
                (second > Av1ReferenceFrameType.Intra && (second == neighbor.ReferenceFrame || second == neighbor.SecondaryReferenceFrame));
        }

        /// <summary>
        /// Returns whether the model skips an entry of an inter mode: once the block has a best cost, when the
        /// references predicted much worse in the model than the best reference, by the pruning level of the speed.
        /// The caller has checked that pruning is on and that no neighbor shares a reference. Reference: the
        /// prune_modes_based_on_tpl test of the ref_mv_idx loop of handle_inter_mode(), with
        /// prune_modes_based_on_tpl_stats().
        /// </summary>
        /// <param name="bestCost">The best cost of the block so far. Reference: ref_best_rd.</param>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference, or none for a single reference.</param>
        /// <param name="referenceIndex">The dynamic reference list index of the entry. Reference: ref_mv_idx.</param>
        /// <param name="mode">The inter mode.</param>
        /// <returns><see langword="true"/> when the entry is skipped.</returns>
        private readonly bool PrunesInterModeByTpl(
            long bestCost,
            Av1ReferenceFrameType first,
            Av1ReferenceFrameType second,
            int referenceIndex,
            Av1PredictionMode mode)
            => bestCost != long.MaxValue && Av1TplModePruning.PruneInterMode(
                this.tplReferenceInterCosts[..],
                this.tplBestInterCost,
                (int)first,
                second > Av1ReferenceFrameType.Intra ? (int)second : 0,
                referenceIndex,
                mode,
                this.picture.Parent.SpeedSettings.TplInterModePruningLevel);

        /// <summary>
        /// Returns whether a reference or pair uses a candidate reference that precedes the anchor reference in
        /// display order, unless the temporal dependency model keeps the candidate or it has the best past
        /// predicted-vector SAD. Reference: prune_ref(), with the tpl_keep_ref_frame and pred_mv_sad tests of
        /// prune_ref_by_selective_ref_frame().
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference, or none.</param>
        /// <param name="candidate">The reference that may be dropped.</param>
        /// <param name="anchor">The reference it is compared with.</param>
        /// <returns><see langword="true"/> when the candidate is used and precedes the anchor.</returns>
        private readonly bool PrunesOlderReference(
            Av1ReferenceFrameType first,
            Av1ReferenceFrameType second,
            Av1ReferenceFrameType candidate,
            Av1ReferenceFrameType anchor)
        {
            if ((first != candidate && second != candidate) ||
                this.tplKeepReferenceFrames[(int)candidate] ||
                this.predictionVectorSads[(int)candidate] == this.bestPastPredictionVectorSad)
            {
                return false;
            }

            ReadOnlySpan<uint> slots = this.picture.Parent.FrameHeader.GetReferenceFrameIndices();
            Span<int> numbers = this.blockWorkspace.ReferenceFrameNumbers;
            return numbers[(int)slots[(int)candidate - (int)Av1ReferenceFrameType.Last]] <
                numbers[(int)slots[(int)anchor - (int)Av1ReferenceFrameType.Last]];
        }

        /// <summary>
        /// Returns how the decision cached for a sub-block of an asymmetric partition restricts an inter mode. A
        /// cached intra decision excludes every inter mode; a cached single-reference decision keeps only its own
        /// mode and reference; a cached compound decision keeps only its own mode and pair, and the single
        /// references whose new vectors its new components start from, without their motion modes.
        /// Reference: the mb_mode_cache test of inter_mode_search_order_independent_skip().
        /// </summary>
        /// <param name="mode">The inter prediction mode.</param>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference, or none for a single reference.</param>
        /// <returns>
        /// Zero without a cache, one to skip the mode, two to search it without motion modes, or three for the
        /// cached mode itself, whose references no reference pruning may remove.
        /// </returns>
        private readonly int GetInterModeCacheDecision(
            Av1PredictionMode mode,
            Av1ReferenceFrameType first,
            Av1ReferenceFrameType second)
        {
            Av1AsymmetricModeCacheEntry cache = this.activeModeCache;
            if (!cache.Active)
            {
                return 0;
            }

            if (cache.ReferenceFrame <= Av1ReferenceFrameType.Intra)
            {
                return 1;
            }

            if (cache.SecondaryReferenceFrame <= Av1ReferenceFrameType.Intra)
            {
                return mode == cache.Mode && first == cache.ReferenceFrame ? 3 : 1;
            }

            if (second <= Av1ReferenceFrameType.Intra)
            {
                // A single mode of a reference that a new component of the cached compound starts from is searched
                // for that vector only.
                bool startsCompound = cache.Mode switch
                {
                    Av1PredictionMode.NewNearMotionVector or Av1PredictionMode.NewNearestMotionVector
                        => first == cache.ReferenceFrame,
                    Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NearestNewMotionVector
                        => first == cache.SecondaryReferenceFrame,
                    Av1PredictionMode.NewNewMotionVector
                        => first == cache.ReferenceFrame || first == cache.SecondaryReferenceFrame,
                    _ => false
                };

                return startsCompound ? 2 : 1;
            }

            return mode == cache.Mode && first == cache.ReferenceFrame && second == cache.SecondaryReferenceFrame ? 3 : 1;
        }

        /// <summary>
        /// Returns whether the cached decision of an asymmetric sub-block is a compound prediction from this pair, which
        /// the partition's reference mask then does not remove. Reference: is_ref_frame_used_in_cache() for a compound
        /// reference type.
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference.</param>
        /// <returns><see langword="true"/> when the cache holds a compound decision of the pair.</returns>
        private readonly bool IsCachedCompoundPair(Av1ReferenceFrameType first, Av1ReferenceFrameType second)
            => this.activeModeCache.Active &&
                this.activeModeCache.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra &&
                this.activeModeCache.ReferenceFrame == first &&
                this.activeModeCache.SecondaryReferenceFrame == second;

        /// <summary>
        /// Returns whether the block searches no mode of a single reference: the rectangular pruning skips it, no
        /// compound pair that the pruning keeps uses it, and the cached decision of an asymmetric sub-block does not
        /// predict from it. Reference: is_ref_frame_used_by_compound_ref() and is_ref_frame_used_in_cache() with the
        /// skip_ref_frame_mask tests.
        /// </summary>
        /// <param name="reference">The reference type.</param>
        /// <returns><see langword="true"/> when the reference is skipped.</returns>
        private readonly bool IsSingleReferenceSkipped(int reference)
        {
            if ((this.skipReferenceFrameMask & (1 << reference)) == 0)
            {
                return false;
            }

            Av1AsymmetricModeCacheEntry cache = this.activeModeCache;
            if (cache.Active && ((int)cache.ReferenceFrame == reference || (int)cache.SecondaryReferenceFrame == reference))
            {
                return false;
            }

            for (int type = Av1Constants.ReferenceFrameCount; type < ModeContextReferenceFrameCount; type++)
            {
                if ((this.skipReferenceFrameMask & (1 << type)) == 0)
                {
                    (Av1ReferenceFrameType first, Av1ReferenceFrameType second) = GetReferenceFramePair(type);
                    if ((int)first == reference || (int)second == reference)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Records the reference type that the unsplit search of a square block picked over the block area.
        /// Reference: av1_update_picked_ref_frames_mask().
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The square block size.</param>
        /// <param name="mode">The picked block decisions.</param>
        private void UpdatePickedReferenceFrames(Point blockOrigin, Av1BlockSize blockSize, Av1EncoderBlockModeInfo mode)
        {
            int type = mode.ReferenceFrame <= Av1ReferenceFrameType.Intra
                ? 0
                : GetReferenceFrameType(mode.ReferenceFrame, mode.SecondaryReferenceFrame);

            int superblockMask = (1 << (this.picture.Sequence.SequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2)) - 1;
            int row = (blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2) & superblockMask;
            int column = (blockOrigin.X >> Av1Constants.ModeInfoSizeLog2) & superblockMask;
            int size = blockSize.Get4x4WideCount();
            int[] masks = this.blockWorkspace.PickedReferenceFrameMasks;
            for (int i = row; i < row + size; i++)
            {
                for (int j = column; j < column + size; j++)
                {
                    masks[(i * 32) + j] |= 1 << type;
                }
            }
        }

        /// <summary>
        /// Masks the modes of each reference whose predicted-vector SAD is poor. A reference far from the best SAD
        /// loses its fixed-vector modes, and with single-reference pruning a reference other than the closest ones
        /// loses every single-reference mode. Reference: av1_mv_pred() in setup_buffer_ref_mvs_inter(), then
        /// init_mode_skip_mask().
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="singleReferenceVectors">The reference vector stacks, indexed by reference type.</param>
        private void SetInterModeSkipMasks(
            Point blockOrigin,
            Av1BlockSize blockSize,
            ReadOnlySpan<Av1ReferenceMotionVectors> singleReferenceVectors)
        {
            const uint nearestNearZero =
                (1u << (int)Av1PredictionMode.NearestMotionVector) | (1u << (int)Av1PredictionMode.NearMotionVector) |
                (1u << (int)Av1PredictionMode.GlobalMotionVector) | (1u << (int)Av1PredictionMode.NearestNearestMotionVector) |
                (1u << (int)Av1PredictionMode.GlobalGlobalMotionVector) | (1u << (int)Av1PredictionMode.NearestNewMotionVector) |
                (1u << (int)Av1PredictionMode.NewNearestMotionVector) | (1u << (int)Av1PredictionMode.NewNearMotionVector) |
                (1u << (int)Av1PredictionMode.NearNewMotionVector) | (1u << (int)Av1PredictionMode.NearNearMotionVector);

            const uint singleAll =
                (1u << (int)Av1PredictionMode.NearestMotionVector) | (1u << (int)Av1PredictionMode.NearMotionVector) |
                (1u << (int)Av1PredictionMode.GlobalMotionVector) | (1u << (int)Av1PredictionMode.NewMotionVector);

            // Every single and compound inter mode. Reference: INTER_ALL.
            const uint interAll = ((1u << ((int)Av1PredictionMode.NewNewMotionVector + 1)) - 1) &
                ~((1u << (int)Av1PredictionMode.NearestMotionVector) - 1);

            this.interModeSkipMasks = default;
            Av1PictureParentControlSet parent = this.picture.Parent;
            ObuFrameHeader frameHeader = parent.FrameHeader;
            byte availableReferences = parent.AvailableReferenceMask;
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            Point position = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Size frameSize = new(
                parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> sourceBlock = Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin);

            Span<int> sads = stackalloc int[Av1Constants.ReferenceFrameCount + 1];
            sads.Fill(int.MaxValue);
            ReadOnlySpan<Av1EncoderFrame<TSample>> searchReferences = this.searchReferences.Span;
            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                // A reference that the block does not search measures no SAD. Reference: the skip_ref_frame_mask
                // test of set_params_rd_pick_inter_mode().
                if ((availableReferences & (1 << reference)) == 0 || this.IsSingleReferenceSkipped(reference))
                {
                    continue;
                }

                Av1MotionVector globalMotion = frameHeader.GetGlobalMotionParameters()[reference - (int)Av1ReferenceFrameType.Last]
                    .GetMotionVector(frameHeader.AllowHighPrecisionMotionVector, blockSize, position, frameHeader.ForceIntegerMotionVector);

                Av1MotionVector first = singleReferenceVectors[reference].GetStackVector(0, globalMotion);
                Av1MotionVector second = singleReferenceVectors[reference].GetStackVector(1, globalMotion);
                int count = first == second ? 1 : 2;

                // A reference of another size is measured through its copy resized to the frame size. Reference: the
                // scaled_ref_frame of setup_buffer_ref_mvs_inter().
                Av1PlaneRegion<TSample> referencePlane = searchReferences[reference].CodedView.GetPlane(Av1Plane.Y);
                ReadOnlySpan<TSample> referenceSamples = referencePlane.Samples;
                bool zeroSeen = false;
                int best = int.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    // Clamp the vector to the interpolation extension around the coded frame, then round it to whole
                    // samples. Reference: enc_clamp_mv().
                    Av1MotionVector vector = i == 0 ? first : second;
                    int column = Math.Clamp(vector.Column, -(blockOrigin.X + width + 4) * 8, (frameSize.Width - blockOrigin.X + 4) * 8);
                    int row = Math.Clamp(vector.Row, -(blockOrigin.Y + height + 4) * 8, (frameSize.Height - blockOrigin.Y + 4) * 8);
                    int fullRow = (row + 3 + (row >= 0 ? 1 : 0)) >> 3;
                    int fullColumn = (column + 3 + (column >= 0 ? 1 : 0)) >> 3;
                    if (fullRow == 0 && fullColumn == 0 && zeroSeen)
                    {
                        continue;
                    }

                    zeroSeen |= fullRow == 0 && fullColumn == 0;
                    int referenceOrigin = ((referencePlane.Bounds.Y + blockOrigin.Y + fullRow) * referencePlane.Stride) +
                        referencePlane.Bounds.X + blockOrigin.X + fullColumn;

                    int sad = TOperator.SumAbsoluteDifferences(
                        sourceBlock, sourcePlane.Stride, referenceSamples[referenceOrigin..], referencePlane.Stride, width, height, 1);

                    best = Math.Min(best, sad);
                }

                sads[reference] = best;
            }

            // The best past and future SADs are measured only when the alternate reference search or the single
            // reference pruning reads them; otherwise both stay at the maximum. Reference: the alt_ref_search_fp and
            // prune_single_ref test of set_params_rd_pick_inter_mode().
            int pruneLevel = parent.SpeedSettings.GetPruneSingleReferenceLevel(parent.FrameUpdateType);
            bool measuresBestSads = parent.SpeedSettings.AlternateReferenceSearchLevel != 0 || pruneLevel != 0;
            int minimum = int.MaxValue;
            InlineArray2<int> bestByDirectionStorage = default;
            Span<int> bestByDirection = bestByDirectionStorage;
            bestByDirection[0] = int.MaxValue;
            bestByDirection[1] = int.MaxValue;
            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                minimum = Math.Min(minimum, sads[reference]);
                if (measuresBestSads)
                {
                    int direction = parent.ReferenceDistances[reference] < 0 ? 0 : 1;
                    bestByDirection[direction] = Math.Min(bestByDirection[direction], sads[reference]);
                }
            }

            sads[..Av1Constants.ReferenceFrameCount].CopyTo(this.predictionVectorSads);
            this.bestPastPredictionVectorSad = bestByDirection[0];
            this.bestFuturePredictionVectorSad = bestByDirection[1];

            double pruneThreshold = pruneLevel <= 3 ? 1.20 : 1.05;
            Span<uint> masks = stackalloc uint[Av1Constants.ReferenceFrameCount];
            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                if ((availableReferences & (1 << reference)) != 0 && (sads[reference] >> 2) > minimum)
                {
                    masks[reference] = nearestNearZero;
                }
            }

            // A frame coded from the source of an alternate reference searches that reference alone. Without
            // temporal filtering it keeps the fixed-vector modes whose vector is the global one, and the faster
            // speeds search every mode of the reference. Reference: the is_src_frame_alt_ref branches of
            // init_mode_skip_mask(), with disable_inter_references_except_altref().
            int alternateSearchLevel = parent.SpeedSettings.AlternateReferenceSearchLevel;
            const int alternate = (int)Av1ReferenceFrameType.Alternate;
            if (parent.IsSourceAlternateReference)
            {
                if (!parent.EncoderOptions.EnableTemporalFilter)
                {
                    DisableReferencesExceptAlternate(masks);
                    masks[alternate] = ~nearestNearZero;
                    Av1MotionVector global = frameHeader.GetGlobalMotionParameters()[alternate - (int)Av1ReferenceFrameType.Last]
                        .GetMotionVector(frameHeader.AllowHighPrecisionMotionVector, blockSize, position, frameHeader.ForceIntegerMotionVector);

                    if (singleReferenceVectors[alternate].GetStackVector(1, global) != global)
                    {
                        masks[alternate] |= 1u << (int)Av1PredictionMode.NearMotionVector;
                    }

                    if (singleReferenceVectors[alternate].GetStackVector(0, global) != global)
                    {
                        masks[alternate] |= 1u << (int)Av1PredictionMode.NearestMotionVector;
                    }
                }

                if (alternateSearchLevel != 0 && (availableReferences & (1 << alternate)) != 0)
                {
                    masks[alternate] = 0;
                    DisableReferencesExceptAlternate(masks);
                }
            }

            // An unshown frame drops every mode of a later-type reference that lies in the past near LAST, when its
            // predicted-vector SAD is well above the best of the past. Reference: the alt_ref_search_fp branch of
            // init_mode_skip_mask().
            if (alternateSearchLevel != 0 && !frameHeader.ShowFrame && bestByDirection[0] < int.MaxValue)
            {
                int sadThreshold = bestByDirection[0] + (bestByDirection[0] >> 3);
                int start = alternateSearchLevel == 1 ? (int)Av1ReferenceFrameType.Alternate2 : (int)Av1ReferenceFrameType.Backward;
                for (int reference = start; reference <= alternate; reference++)
                {
                    int distance = parent.ReferenceDistances[reference];
                    if (distance < 0 &&
                        Math.Abs(distance - parent.ReferenceDistances[(int)Av1ReferenceFrameType.Last]) <= 4 &&
                        sads[reference] > sadThreshold)
                    {
                        masks[reference] |= interAll;
                    }
                }
            }

            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                uint mask = masks[reference];
                bool closest = (Av1ReferenceFrameType)reference == parent.NearestPastReference ||
                    (Av1ReferenceFrameType)reference == parent.NearestFutureReference;

                if (pruneLevel != 0 && (parent.KeepSingleReferenceMask & (1 << reference)) == 0 && !closest)
                {
                    int direction = parent.ReferenceDistances[reference] < 0 ? 0 : 1;
                    if (bestByDirection[direction] < int.MaxValue && sads[reference] > pruneThreshold * bestByDirection[direction])
                    {
                        mask |= singleAll;
                    }
                }

                this.interModeSkipMasks[reference] = mask;
            }
        }

        /// <summary>
        /// Drops every mode of every reference but ALTREF. A compound pair names ALTREF only second, so every pair
        /// goes too. Reference: disable_inter_references_except_altref().
        /// </summary>
        /// <param name="masks">The skipped modes of each reference, one bit per mode.</param>
        private static void DisableReferencesExceptAlternate(Span<uint> masks)
        {
            for (int reference = (int)Av1ReferenceFrameType.Last; reference < (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                masks[reference] = uint.MaxValue;
            }
        }

        /// <summary>
        /// Rejects near-motion modes when adjacent reference pairs do not meet the quantizer-dependent threshold.
        /// </summary>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The block's neighbor availability.</param>
        /// <param name="primary">The first reference of the mode.</param>
        /// <param name="secondary">The second reference of the mode, or none for a single reference.</param>
        /// <param name="bestCost">The cost of the best mode so far.</param>
        /// <returns><see langword="true"/> when the near-motion modes are rejected.</returns>
        private bool ShouldPruneNearMode(
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
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
            int threshold = thresholds[((level - 1) * 3) + (this.blockQIndex * 3 / 256)];
            Av1EncoderBlockModeInfo left = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -1).Block;
            Av1EncoderBlockModeInfo above = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -macroBlock.ModeInfoStride).Block;
            int matches = left.ReferenceFrame == primary && left.SecondaryReferenceFrame == secondary ? 1 : 0;
            matches += above.ReferenceFrame == primary && above.SecondaryReferenceFrame == secondary ? 1 : 0;
            return matches < threshold;
        }

        /// <summary>
        /// Returns whether a compound entry may be searched, as libaom decides it. build_cur_mv() tests each component
        /// that does not search a new vector with clamp_and_check_mv(), but each pass of its loop first assigns the
        /// get_this_mv() result to the same flag, so the test of the first component is overwritten and only the
        /// second component decides. A second component that searches a new vector leaves the entry valid. The
        /// encoder keeps that behavior, because the entry set changes the coded decisions. Reference:
        /// build_cur_mv().
        /// </summary>
        /// <param name="mode">The compound prediction mode.</param>
        /// <param name="secondary">The vector of the second reference.</param>
        /// <param name="secondaryModes">The single mode of the second component of each compound mode.</param>
        /// <param name="frameBounds">The full-pixel frame displacement region of the block.</param>
        /// <returns><see langword="true"/> when the entry may be searched.</returns>
        private static bool CompoundVectorsInFrameSearchBounds(
            Av1PredictionMode mode,
            Av1MotionVector secondary,
            ReadOnlySpan<Av1PredictionMode> secondaryModes,
            Rectangle frameBounds)
        {
            // The first component's test does not survive the second pass of the build_cur_mv() loop.
            int modeIndex = (int)mode - (int)Av1PredictionMode.CompoundInterModeStart;
            return secondaryModes[modeIndex] == Av1PredictionMode.NewMotionVector || secondary.IsInFrameSearchBounds(frameBounds);
        }

        /// <summary>
        /// Ranks compound references from complete translation costs followed by modeled filter costs.
        /// </summary>
        private InlineArray4<byte> GetCompoundReferenceMasks(uint searchedModes)
        {
            InlineArray4<byte> masksStorage = default;
            Span<byte> masks = masksStorage;
            int level = this.picture.Parent.SpeedSettings.CompoundSingleResultPruningLevel;
            if (level == 0)
            {
                masks[..].Fill(byte.MaxValue);
                return masksStorage;
            }

            ReadOnlySpan<long> simple = this.blockWorkspace.SingleReferenceSimpleCosts;
            ReadOnlySpan<long> modeled = this.blockWorkspace.SingleReferenceFilterCosts;
            InlineArray4<long> simpleCostsStorage = default;
            InlineArray4<long> modelCostsStorage = default;
            InlineArray4<byte> simpleReferencesStorage = default;
            InlineArray4<byte> modelReferencesStorage = default;
            InlineArray4<byte> orderedReferencesStorage = default;
            InlineArray4<bool> simpleValidStorage = default;
            InlineArray4<bool> modelValidStorage = default;
            Span<long> simpleCosts = simpleCostsStorage;
            Span<long> modelCosts = modelCostsStorage;
            Span<byte> simpleReferences = simpleReferencesStorage;
            Span<byte> modelReferences = modelReferencesStorage;
            Span<byte> orderedReferences = orderedReferencesStorage;
            Span<bool> simpleValid = simpleValidStorage;
            Span<bool> modelValid = modelValidStorage;
            for (int direction = 0; direction < 2; direction++)
            {
                int firstReference = direction == 0 ? (int)Av1ReferenceFrameType.Last : (int)Av1ReferenceFrameType.Golden + 1;
                int lastReference = direction == 0 ? (int)Av1ReferenceFrameType.Golden : (int)Av1ReferenceFrameType.Alternate;

                // The bound is the best GLOBALMV or NEWMV state of the direction, and only a searched reference has
                // a state; a mode skipped for a repeated vector keeps a copied modeled cost but no state. Reference:
                // the state[INTER_OFFSET(NEWMV)][0] and state[INTER_OFFSET(GLOBALMV)][0] bound of
                // analyze_single_states(), with collect_single_states().
                long bestSimple = long.MaxValue;
                long bestModel = long.MaxValue;
                for (int reference = firstReference; reference <= lastReference; reference++)
                {
                    for (int mode = (int)Av1PredictionMode.GlobalMotionVector; mode <= (int)Av1PredictionMode.NewMotionVector; mode++)
                    {
                        int modeOffset = mode - (int)Av1PredictionMode.InterModeStart;
                        if ((searchedModes & (1U << ((modeOffset * Av1Constants.ReferenceFrameCount) + reference))) == 0)
                        {
                            continue;
                        }

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

            return masksStorage;
        }

        /// <summary>
        /// Searches ordered compound modes using retained single-reference decisions.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="primaryReference">The first reference of the pair.</param>
        /// <param name="secondaryReference">The second reference of the pair.</param>
        /// <param name="nearestOnly">Whether only the nearest-nearest mode is searched.</param>
        /// <param name="topAverageCosts">The lowest average-blend costs of the block so far.</param>
        /// <param name="compoundMaskHistory">The wedge index, wedge sign and difference-weighted mask type that every pair of the block shares.</param>
        /// <param name="compoundTypeHistory">The last compound type of the pair.</param>
        /// <param name="outsideSingleReferenceCutoff">Whether both references cost more than the single-reference cutoff.</param>
        /// <param name="bestSingleModes">The best single-reference mode of each reference.</param>
        /// <param name="searchedSingleModes">The bit set of the single-reference modes and references whose filters were searched.</param>
        /// <param name="singleReferenceVectors">The reference vector lists of the single references.</param>
        /// <param name="newMotionVectors">The new vectors of each single reference, by list index.</param>
        /// <param name="newMotionVectorMasks">The list indices whose new vector is set, for each reference.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="selectedStatistics">The rate and distortion of the winner.</param>
        /// <param name="selectedVector">The motion vector of the winner.</param>
        /// <param name="selectedSecondaryVector">The second motion vector of a compound winner.</param>
        /// <param name="selectedStates">The transform states of the winner.</param>
        private void SelectCompoundBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<Av1EncoderReferenceContext> referenceContexts,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1ReferenceFrameType primaryReference,
            Av1ReferenceFrameType secondaryReference,
            bool nearestOnly,
            Span<long> topAverageCosts,
            Span<int> compoundMaskHistory,
            Span<int> compoundTypeHistory,
            bool outsideSingleReferenceCutoff,
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
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            if ((this.picture.Parent.AvailableReferenceMask & (1 << (int)primaryReference)) == 0 ||
                (this.picture.Parent.AvailableReferenceMask & (1 << (int)secondaryReference)) == 0 ||
                frameHeader.ReferenceMode == ObuReferenceMode.SingleReference ||
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) < 8 ||
                ((this.skipReferenceFrameMask & (1 << GetReferenceFrameType(primaryReference, secondaryReference))) != 0 &&
                    !this.IsCachedCompoundPair(primaryReference, secondaryReference)) ||
                this.PrunesCompoundReferencePair(primaryReference, secondaryReference) ||
                this.PrunesReferenceBySelectiveReferenceFrame(primaryReference, secondaryReference) ||
                outsideSingleReferenceCutoff)
            {
                return;
            }

            Point modeInfoPosition = new(
                blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

            ref Av1ReferenceMotionVectors referenceMotionVectors = ref this.blockWorkspace.ReferenceMotionVectors;
            referenceMotionVectors.Build(
                this.picture,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                macroBlock,
                modeInfoPosition,
                blockSize,
                modeInfo.Block.PartitionType,
                this.picture.Sequence.SequenceHeader,
                frameHeader,
                primaryReference,
                secondaryReference);

            // The model prunes no entry of the pair when a neighbor shares one of its references. Reference: the
            // ref_match_found_in_above_nb and ref_match_found_in_left_nb setup of handle_inter_mode().
            bool tplPruning = this.tplInterModePruning &&
                !this.HasNeighborReferenceMatch(modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize, primaryReference, secondaryReference);

            Span<byte> referenceCounts = stackalloc byte[Av1Constants.ReferenceFrameCount];
            Av1TileWriter.CollectNeighborReferenceCounts(modeInfoGrid, modeInfoAllocation, macroBlock, referenceCounts);
            int intraInterContext = Av1TileWriter.GetIntraInterContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int commonPredictionRate = Av1SymbolEncoder.GetIsInterCost(tables.ModeCosts, isInter: true, intraInterContext) +
                Av1SymbolEncoder.GetCompoundReferenceCost(
                    tables.ModeCosts,
                    primaryReference,
                    secondaryReference,
                    Av1SymbolContextHelper.GetReferenceModeContext(modeInfoGrid, modeInfoAllocation, macroBlock),
                    Av1SymbolContextHelper.GetCompoundReferenceTypeContext(modeInfoGrid, modeInfoAllocation, macroBlock),
                    referenceCounts);

            InlineArray16<Av1MotionVector> primaryVectorsStorage = default;
            InlineArray16<Av1MotionVector> secondaryVectorsStorage = default;
            InlineArray16<Av1PredictionMode> modesStorage = default;
            InlineArray16<byte> referenceIndicesStorage = default;
            Span<Av1MotionVector> primaryVectors = primaryVectorsStorage;
            Span<Av1MotionVector> secondaryVectors = secondaryVectorsStorage;
            Span<Av1PredictionMode> modes = modesStorage;
            Span<byte> referenceIndices = referenceIndicesStorage;
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

            // A list or global component that points beyond the frame displacement region removes its entry.
            // Reference: build_cur_mv() in handle_inter_mode() and ref_mv_idx_to_search().
            Av1PlaneRegion<TSample> boundsPlane = this.references.Span[(int)primaryReference].CodedView.GetPlane(Av1Plane.Y);
            Rectangle frameBounds = Av1MotionVector.GetFrameSearchBounds(
                new Rectangle(blockOrigin, new Size(blockSize.GetWidth(), blockSize.GetHeight())),
                new Size(
                    this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                    this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2),
                Math.Min(boundsPlane.Bounds.X, boundsPlane.Bounds.Y));

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
                    primaryVectors[candidateCount] = ClampToSubpixelRange(
                        primaryNewVectors[referenceIndex], referenceMotionVectors.GetCompoundNewReference(referenceIndex, 0), frameBounds);

                    secondaryVectors[candidateCount] = ClampToSubpixelRange(
                        secondaryNewVectors[referenceIndex], referenceMotionVectors.GetCompoundNewReference(referenceIndex, 1), frameBounds);

                    modes[candidateCount] = Av1PredictionMode.NewNewMotionVector;
                    referenceIndices[candidateCount++] = (byte)referenceIndex;
                }
            }

            if ((primaryNewVectorMask & 1) != 0)
            {
                primaryVectors[candidateCount] = ClampToSubpixelRange(
                    primaryNewVectors[0], referenceMotionVectors.GetCompoundNewReference(0, 0), frameBounds);

                secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(1);
                modes[candidateCount++] = Av1PredictionMode.NewNearestMotionVector;
            }

            if ((secondaryNewVectorMask & 1) != 0)
            {
                primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(0);
                secondaryVectors[candidateCount] = ClampToSubpixelRange(
                    secondaryNewVectors[0], referenceMotionVectors.GetCompoundNewReference(0, 1), frameBounds);

                modes[candidateCount++] = Av1PredictionMode.NearestNewMotionVector;
            }

            // Complete one syntax mode's DRL entries before moving to the next mode. The searched
            // vector cache uses the signaled index; only a NEAR predictor advances in the reference stack.
            for (int referenceIndex = 0; referenceIndex <= maximumNearIndex; referenceIndex++)
            {
                if ((primaryNewVectorMask & (1 << referenceIndex)) != 0)
                {
                    primaryVectors[candidateCount] = ClampToSubpixelRange(
                        primaryNewVectors[referenceIndex], referenceMotionVectors.GetCompoundNewReference(referenceIndex + 1, 0), frameBounds);

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
                    secondaryVectors[candidateCount] = ClampToSubpixelRange(
                        secondaryNewVectors[referenceIndex], referenceMotionVectors.GetCompoundNewReference(referenceIndex + 1, 1), frameBounds);

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

            Av1InterpolationFilter filter = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable
                ? Av1InterpolationFilter.Regular : frameHeader.InterpolationFilter;

            bool maskedCompoundEnabled = this.picture.Sequence.SequenceHeader.EnableMaskedCompound &&
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8;

            int compoundGroupContext = Av1TileWriter.GetCompoundGroupIndexContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int compoundIndexContext = Av1SymbolContextHelper.GetCompoundIndexContext(
                this.picture.Sequence.SequenceHeader.OrderHintInfo,
                frameHeader,
                primaryReference,
                secondaryReference,
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock);

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
            InlineArray4<byte> referenceMasksStorage = this.GetCompoundReferenceMasks(searchedSingleModes);
            Span<byte> referenceMasks = referenceMasksStorage;
            bool primaryNeighborMatch = false;
            bool secondaryNeighborMatch = false;
            for (int neighborIndex = 0; neighborIndex < 2; neighborIndex++)
            {
                if (neighborIndex == 0 ? !macroBlock.IsLeftAvailable : !macroBlock.IsUpAvailable)
                {
                    continue;
                }

                Av1EncoderBlockModeInfo neighbor = macroBlock.GetRelativeModeInfo(
                    modeInfoGrid,
                    modeInfoAllocation,
                    neighborIndex == 0 ? -1 : -macroBlock.ModeInfoStride).Block;

                if (neighbor.ReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    primaryNeighborMatch |= neighbor.ReferenceFrame == primaryReference ||
                        neighbor.SecondaryReferenceFrame == primaryReference;

                    secondaryNeighborMatch |= neighbor.ReferenceFrame == secondaryReference ||
                        neighbor.SecondaryReferenceFrame == secondaryReference;
                }
            }

            InlineArray2<Av1MotionVector> previousPrimaryStorage = default;
            InlineArray2<Av1MotionVector> previousSecondaryStorage = default;
            Span<Av1MotionVector> previousPrimary = previousPrimaryStorage;
            Span<Av1MotionVector> previousSecondary = previousSecondaryStorage;
            Av1PredictionMode previousMode = Av1PredictionMode.PredictionModeCount;
            byte previousVectorMask = 0;
            bool modeImproved = false;
            bool rejectMode = false;
            long modeBestCost = long.MaxValue;
            int candidateMask = 0;
            InlineArray3<long> translationCostsStorage = default;
            InlineArray4<Av1RateDistortionStatistics> planeStatisticsStorage = default;
            Span<long> translationCosts = translationCostsStorage;
            Span<Av1RateDistortionStatistics> planeStatistics = planeStatisticsStorage;
            Span<int> modeThresholdFactors = this.blockWorkspace.ModeThresholdFactors;
            Span<long> singleReferenceFilterCosts = this.blockWorkspace.SingleReferenceFilterCosts;
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
                    this.RecordMotionModeWinner(modeBestCost, true, default, default, default);
                    modeBestCost = long.MaxValue;
                    previousMode = mode;
                    previousVectorMask = 0;
                    modeImproved = false;
                    rejectMode = Av1ModeThresholds.ShouldSkip(
                        modeThresholdFactors,
                        this.ModeThresholdQuantizerFactor,
                        this.ModeThresholdSkipMultiplier,
                        blockSize,
                        mode,
                        primaryReference,
                        secondaryReference,
                        Math.Min(this.blockCostLimit, selectedStatistics.Cost),
                        selectedStatistics.AllTransformsEmpty);

                    rejectMode |= (this.interModeSkipMasks[(int)primaryReference] & (1u << (int)mode)) != 0;
                    rejectMode |= this.GetInterModeCacheDecision(mode, primaryReference, secondaryReference) == 1;
                    rejectMode |= mode == Av1PredictionMode.NearNearMotionVector &&
                        this.ShouldPruneNearMode(
                            modeInfoGrid,
                            modeInfoAllocation,
                            macroBlock,
                            primaryReference,
                            secondaryReference,
                            Math.Min(this.blockCostLimit, selectedStatistics.Cost));

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

                // The entries of one mode share a budget that their own results lower before the image tune biases
                // them. Reference: the ref_best_rd update in the ref_mv_idx loop of handle_inter_mode().
                long modeCostLimit = Math.Min(Math.Min(this.blockCostLimit, selectedStatistics.Cost), modeBestCost);
                if (startsMode && !rejectMode)
                {
                    bool nearMode = mode is Av1PredictionMode.NearNearMotionVector or
                        Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;

                    int referenceCount = nearMode ? maximumNearIndex + 1 :
                        mode == Av1PredictionMode.NewNewMotionVector ? maximumNewIndex + 1 : 1;

                    candidateMask = (1 << referenceCount) - 1;
                    if (referenceCount > 1)
                    {
                        // A scaled reference is not pruned. Reference: the av1_is_scaled() test of
                        // ref_mv_idx_to_search().
                        bool modelTranslation = nearMode && settings.PruneNearMotionByTranslation &&
                            blockSize.GetWidth() * blockSize.GetHeight() > 64 &&
                            !this.IsScaledReference(primaryReference) && !this.IsScaledReference(secondaryReference);

                        translationCosts[..].Fill(long.MaxValue);
                        long bestTranslation = long.MaxValue;
                        Av1EncoderBlockModeInfo prediction = modeInfo.Block;
                        prediction.Mode = mode;
                        prediction.ReferenceFrame = primaryReference;
                        prediction.SecondaryReferenceFrame = secondaryReference;
                        prediction.CompoundType = Av1CompoundType.Average;
                        prediction.CompoundGroupIndex = false;
                        prediction.CompoundIndex = true;
                        bool secondaryPastPair = primaryReference is Av1ReferenceFrameType.Last2 or Av1ReferenceFrameType.Last3 ||
                            secondaryReference is Av1ReferenceFrameType.Last2 or Av1ReferenceFrameType.Last3;

                        for (int index = candidateIndex; index < candidateCount && modes[index] == mode; index++)
                        {
                            // A later list entry of a pair that uses LAST2 or LAST3 is searched only when its stack
                            // weight marks a nearest neighbor. Reference: the reduce_inter_modes test of
                            // ref_mv_idx_early_breakout().
                            if (settings.ReduceInterReferenceIndices != 0 && referenceIndices[index] > 0 && secondaryPastPair &&
                                referenceMotionVectors.Weights[referenceIndices[index] + (nearMode ? 1 : 0)] <
                                Av1ReferenceMotionVectors.NearestCandidateWeight)
                            {
                                candidateMask &= ~(1 << referenceIndices[index]);
                                continue;
                            }

                            if (!CompoundVectorsInFrameSearchBounds(mode, secondaryVectors[index], secondaryModes, frameBounds))
                            {
                                candidateMask &= ~(1 << referenceIndices[index]);
                                continue;
                            }

                            // The translation model prices the mode without the differential vectors.
                            Av1MotionVector entryPrimary = primaryVectors[index];
                            Av1MotionVector entrySecondary = secondaryVectors[index];
                            int entryIndex = referenceIndices[index];
                            int translationModeRate = GetCompoundInterModeRate(
                                in tables, in motionVectorCosts, mode, entryPrimary, entrySecondary, entryIndex, referenceMotionVectors) -
                                GetCompoundMotionRate(in motionVectorCosts, mode, entryPrimary, entrySecondary, entryIndex, referenceMotionVectors);

                            int drlRate = translationModeRate -
                                Av1SymbolEncoder.GetInterCompoundModeCost(tables.ModeCosts, mode, referenceMotionVectors.ModeContext);

                            if (Av1RateDistortion.GetCost(this.rateMultiplier, commonPredictionRate + drlRate, 0) > modeCostLimit)
                            {
                                candidateMask &= ~(1 << referenceIndices[index]);
                                continue;
                            }

                            if (modelTranslation &&
                                Av1RateDistortion.GetCost(this.rateMultiplier, commonPredictionRate + translationModeRate, 0) <= modeCostLimit)
                            {
                                // The model predicts a new-vector component from the raw stack entry at the signaled
                                // index, not from a searched vector. Reference: the NEWMV branch of build_cur_mv(),
                                // which simple_translation_pred_rd() calls.
                                int componentModeIndex = (int)mode - (int)Av1PredictionMode.CompoundInterModeStart;
                                Av1MotionVector translationPrimary = primaryModes[componentModeIndex] == Av1PredictionMode.NewMotionVector
                                    ? referenceMotionVectors.GetCompoundNewReference(referenceIndices[index], 0)
                                    : primaryVectors[index];

                                Av1MotionVector translationSecondary = secondaryModes[componentModeIndex] == Av1PredictionMode.NewMotionVector
                                    ? referenceMotionVectors.GetCompoundNewReference(referenceIndices[index], 1)
                                    : secondaryVectors[index];

                                long translationLumaSquaredError = 0;
                                Av1RateDistortionStatistics estimate = this.GetInterFilterModelCost(
                                    in interWorkspace,
                                    firstIntermediate,
                                    secondIntermediate,
                                    compoundMask,
                                    modeInfoGrid,
                                    modeInfoAllocation,
                                    displacementVectors,
                                    translationPrimary,
                                    translationSecondary,
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
                                    interWorkspace.LumaPrediction,
                                    interWorkspace.BluePrediction,
                                    interWorkspace.RedPrediction,
                                    planeStatistics,
                                    ref translationLumaSquaredError);

                                // The estimate predicts luma into pd->dst, which is the frame. Reference: av1_enc_build_inter_predictor() in
                                // simple_translation_pred_rd().
                                this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, interWorkspace.LumaPrediction);

                                // Reference: the plane 0 pred_sse store of model_rd_for_sb_with_curvfit() in
                                // simple_translation_pred_rd().
                                this.SetPredictionSse(prediction.ReferenceFrame, translationLumaSquaredError);
                                translationCosts[referenceIndices[index]] = estimate.Cost;
                                bestTranslation = Math.Min(bestTranslation, estimate.Cost);
                            }
                        }

                        if (modelTranslation)
                        {
                            for (int index = 0; index < referenceCount; index++)
                            {
                                if (!((double)translationCosts[index] / bestTranslation < 1.05 &&
                                    (double)translationCosts[index] / modeCostLimit < 5))
                                {
                                    candidateMask &= ~(1 << index);
                                }
                            }
                        }
                    }
                }

                if (rejectMode || (candidateMask & (1 << referenceIndices[candidateIndex])) == 0 ||
                    !CompoundVectorsInFrameSearchBounds(mode, candidateSecondary, secondaryModes, frameBounds))
                {
                    continue;
                }

                if (tplPruning && this.PrunesInterModeByTpl(
                    modeCostLimit,
                    primaryReference,
                    secondaryReference,
                    referenceIndices[candidateIndex],
                    mode))
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

                int modeRate = commonPredictionRate + GetCompoundInterModeRate(
                    in tables, in motionVectorCosts, mode, candidatePrimary, candidateSecondary, referenceIndices[candidateIndex], referenceMotionVectors);

                if (mode != Av1PredictionMode.NearestNearestMotionVector &&
                    Av1RateDistortion.GetCost(this.rateMultiplier, modeRate, 0) > modeCostLimit)
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

                if (mode == Av1PredictionMode.GlobalGlobalMotionVector &&
                    this.PrunesZeroVectorWithSse(blockOrigin, blockSize, primaryReference, secondaryReference))
                {
                    continue;
                }

                if (!this.SelectCompoundBlend(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in interWorkspace,
                    in motionVectorCosts,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    modeCostLimit,
                    commonPredictionRate,
                    modes[candidateIndex],
                    primaryReference,
                    secondaryReference,
                    filter,
                    referenceIndices[candidateIndex],
                    referenceMotionVectors,
                    topAverageCosts,
                    compoundMaskHistory,
                    compoundTypeHistory,
                    ref candidatePrimary,
                    ref candidateSecondary,
                    out Av1CompoundType compoundType,
                    out int wedgeIndex,
                    out bool wedgeSign,
                    out Av1DifferenceWeightedMaskType maskType,
                    out int blendRate))
                {
                    continue;
                }

                // The blend search above chose how the two predictions combine. The two flags below
                // are how that choice reaches the bitstream: the group flag separates the masked
                // blends from the rest, and the index then separates a plain average from a
                // distance weighted one.
                // Each mode starts from simple translation, whatever an earlier trial left in the block.
                // Reference: init_mbmi().
                Av1EncoderBlockModeInfo candidateMode = modeInfo.Block;
                candidateMode.ReferenceFrame = primaryReference;
                candidateMode.SecondaryReferenceFrame = secondaryReference;
                candidateMode.Mode = modes[candidateIndex];
                candidateMode.MotionMode = Av1MotionMode.SimpleTranslation;
                candidateMode.SkipMode = false;
                candidateMode.CompoundType = compoundType;
                candidateMode.CompoundGroupIndex = compoundType is Av1CompoundType.Wedge or Av1CompoundType.DifferenceWeighted;
                candidateMode.CompoundIndex = compoundType != Av1CompoundType.DistanceWeighted;
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
                    singleReferenceFilterCosts[primaryCostIndex],
                    singleReferenceFilterCosts[secondaryCostIndex]);

                bool preparedPrediction = this.SelectInterFilters(
                    in tables,
                    in interWorkspace,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    block.HasChroma,
                    candidatePrimary,
                    candidateSecondary,
                    ref candidateMode,
                    modeCostLimit,
                    singleReferenceCost,
                    out int filterRate,
                    out long filterModelCost);

                // The filter search prices the prediction with a model rather than a transform, so
                // the thresholds below are deliberately loose. The shift by three removes the
                // fixed-point scale the model carries before the two ratios are applied: against
                // the block winner when the speed setting allows it, and always against the better
                // half of this compound pair.
                if (filterModelCost == long.MaxValue ||
                    (modeCostLimit != long.MaxValue &&
                        ((this.picture.Parent.SpeedSettings.ModelBasedInterpolationBreakout &&
                            (filterModelCost >> 3) * 3 > modeCostLimit) ||
                        (filterModelCost >> 3) * 6 > singleReferenceCost)))
                {
                    continue;
                }

                // A new vector equal to its reference codes no difference, so the mode is invalid.
                // Reference: av1_check_newmv_joint_nonzero() in motion_mode_rd().
                if (!HasCodedCompoundNewVectors(
                    modes[candidateIndex], candidatePrimary, candidateSecondary, referenceIndices[candidateIndex], referenceMotionVectors))
                {
                    continue;
                }

                Av1RateDistortionStatistics candidateStatistics = this.EvaluateInterCandidate(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    in motionVectorCosts,
                    transformPrediction,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    modeCostLimit,
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
                    referenceMotionVectors,
                    interWorkspace.LumaCandidateReconstruction,
                    interWorkspace.LumaCandidateCoefficients,
                    interWorkspace.BlueCandidateReconstruction,
                    interWorkspace.BlueCandidateCoefficients,
                    interWorkspace.RedCandidateReconstruction,
                    interWorkspace.RedCandidateCoefficients,
                    out bool candidateSkip,
                    out InlineArray64<Av1EncoderTransformBlockState> candidateLumaStates,
                    out InlineArray16<Av1TransformSize> candidateLumaSizes,
                    out InlineArray16<Av1EncoderTransformBlockState> candidateBlueState,
                    out InlineArray16<Av1EncoderTransformBlockState> candidateRedState);

                modeBestCost = Math.Min(modeBestCost, candidateStatistics.Cost);
                if (candidateStatistics.Cost >= Math.Min(this.blockCostLimit, selectedStatistics.Cost))
                {
                    continue;
                }

                modeImproved = true;
                selectedStatistics = candidateStatistics;
                selectedVector = candidatePrimary;
                selectedSecondaryVector = candidateSecondary;
                candidateLumaStates[..].CopyTo(selectedStates);

                // Setting the block transform size resets every transform size, so the searched tree is copied after it.
                modeInfo.Block.TransformSize = frameHeader.CodedLossless ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize();
                candidateLumaSizes[..].CopyTo(modeInfo.Block.InterTransformSizes);
                candidateBlueState[..].CopyTo(selectedStates[64..80]);
                candidateRedState[..].CopyTo(selectedStates[80..96]);
                modeInfo.Block.ReferenceFrame = primaryReference;
                modeInfo.Block.SecondaryReferenceFrame = secondaryReference;
                modeInfo.Block.Mode = modes[candidateIndex];

                // A compound winner codes simple translation outside skip mode, whatever an earlier single-reference
                // winner left in the block. Reference: init_mbmi().
                modeInfo.Block.MotionMode = candidateMode.MotionMode;
                modeInfo.Block.SkipMode = candidateMode.SkipMode;
                modeInfo.Block.Skip = candidateSkip;
                modeInfo.Block.CompoundGroupIndex = compoundType is Av1CompoundType.Wedge or Av1CompoundType.DifferenceWeighted;
                modeInfo.Block.CompoundIndex = compoundType != Av1CompoundType.DistanceWeighted;
                modeInfo.Block.CompoundType = compoundType;
                modeInfo.Block.CompoundWedgeIndex = (byte)wedgeIndex;
                modeInfo.Block.CompoundWedgeSign = wedgeSign;
                modeInfo.Block.DifferenceWeightedMaskType = maskType;
                modeInfo.Block.HorizontalInterpolationFilter = candidateMode.HorizontalInterpolationFilter;
                modeInfo.Block.VerticalInterpolationFilter = candidateMode.VerticalInterpolationFilter;
                block.ReferenceMotionVectorIndex = referenceIndices[candidateIndex];
            }

            this.RecordMotionModeWinner(modeBestCost, true, default, default, default);
        }

        /// <summary>
        /// Gets the sharpness 3 offset of an intra candidate's luma in an inter frame. Sharpness searches intra modes
        /// in an inter frame only for blocks of 16x16 or smaller, and such a block always lies inside the coded frame:
        /// the coded frame is a multiple of eight samples, and a block whose midpoint is inside it may only reach
        /// past it when the block is 32 samples or larger. So the intra search writes every sample that the measure
        /// reads. Reference: the sharpness return of search_intra_modes_in_interframe(), the has_rows and has_cols
        /// partition rules, and pd->dst in get_variance_stats().
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="samples">The candidate's luma samples at the block origin.</param>
        /// <param name="stride">The row stride of the samples.</param>
        /// <returns>The distortion offset.</returns>
        private readonly long GetIntraSmoothingOffset(Point blockOrigin, Av1BlockSize blockSize, ReadOnlySpan<TSample> samples, int stride)
        {
            Debug.Assert(
                this.blockWorkspace.GetVisibleSize(Av1Plane.Y, blockOrigin, blockSize.GetWidth(), blockSize.GetHeight()) ==
                    new Size(blockSize.GetWidth(), blockSize.GetHeight()),
                "An intra candidate charged at sharpness 3 lies inside the coded frame.");

            return this.GetSmoothingOffset(blockOrigin, blockSize, samples, stride);
        }

        /// <summary>
        /// Gets the distortion that sharpness 3 adds to a block whose luma samples are smoother than its source: the
        /// amount by which the source's departure from its own 3x3 smoothing exceeds the samples', or zero. A high bit
        /// depth scales both measures to eight bits first. The source past the coded frame repeats its edge, as the
        /// reference's extended source border does. Reference: get_variance_stats() and the var_offset of
        /// adjust_rdcost() and adjust_cost().
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="samples">The luma samples of the whole block that its destination holds, at the block origin.</param>
        /// <param name="stride">The row stride of the samples.</param>
        /// <returns>The distortion offset.</returns>
        private readonly long GetSmoothingOffset(Point blockOrigin, Av1BlockSize blockSize, ReadOnlySpan<TSample> samples, int stride)
        {
            this.GetVarianceStatistics(blockOrigin, blockSize, samples, stride, out long sourceVariance, out long sampleVariance);
            return Math.Max(sourceVariance - sampleVariance, 0);
        }

        /// <summary>
        /// Measures how much the block source and the block luma samples each differ from their own 3x3 smoothing.
        /// A high bit depth scales both measures to 8 bits.
        /// Past the coded frame, the source repeats its edge, as the extended source border of the reference does.
        /// Reference: av1_get_variance_stats().
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="samples">The luma samples of the whole block in its destination, at the block origin.</param>
        /// <param name="stride">The row stride of the samples.</param>
        /// <param name="sourceVariance">Gets the source measure.</param>
        /// <param name="sampleVariance">Gets the measure of the samples.</param>
        private readonly void GetVarianceStatistics(
            Point blockOrigin, Av1BlockSize blockSize, ReadOnlySpan<TSample> samples, int stride, out long sourceVariance, out long sampleVariance)
        {
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();

            // Sharpness 3 turns border padding off. So the visible size is the coded 8-sample boundary, and the source repeats its edge past it.
            // Each block origin is inside this boundary.
            Size visible = this.blockWorkspace.GetVisibleSize(Av1Plane.Y, blockOrigin, width, height);
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> sourceBlock = Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin);
            sourceVariance = TOperator.GetVarianceStatistic(sourceBlock, sourcePlane.Stride, width, height, visible.Width, visible.Height);
            sampleVariance = TOperator.GetVarianceStatistic(samples, stride, width, height, width, height);

            // Both measures round to the 8-bit scale. Reference: the ROUND_POWER_OF_TWO of get_variance_stats_hbd().
            int shift = (this.bitDepth.GetBitCount() - 8) * 2;
            if (shift != 0)
            {
                long half = 1L << (shift - 1);
                sourceVariance = (sourceVariance + half) >> shift;
                sampleVariance = (sampleVariance + half) >> shift;
            }
        }

        /// <summary>
        /// Gets the distortion that high bit depth sharpness 3 adds for the texture that a block loses.
        /// The offset is the amount by which the source measure is more than the sample measure.
        /// The offset is four times larger for a skipped block with low source detail, a smooth intra mode, an inter-intra blend or a compound.
        /// A single-reference prediction with low source detail also pays for the length of its motion vector.
        /// Reference: var_offset in the high bit depth branch of adjust_rdcost() and adjust_cost().
        /// </summary>
        /// <param name="sourceVariance">The source measure from <see cref="GetVarianceStatistics"/>.</param>
        /// <param name="sampleVariance">The sample measure from <see cref="GetVarianceStatistics"/>.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="skip">True when the block skips its transform.</param>
        /// <param name="quadruples">True for a smooth intra mode, an inter-intra blend or a compound.</param>
        /// <param name="singleReference">True for a single-reference inter prediction.</param>
        /// <param name="vector">The first motion vector of the block.</param>
        /// <returns>The distortion offset.</returns>
        private static long GetTextureLossOffset(
            long sourceVariance, long sampleVariance, Av1BlockSize blockSize, bool skip, bool quadruples, bool singleReference, Av1MotionVector vector)
        {
            long offset = sourceVariance > sampleVariance ? sourceVariance - sampleVariance : 0;
            long sourceVariancePerSample = sourceVariance / (blockSize.GetWidth() * blockSize.GetHeight());
            if (offset > 0 && ((skip && sourceVariancePerSample < 64) || quadruples))
            {
                offset *= 4;
            }

            if (singleReference)
            {
                int magnitude = Math.Abs(vector.Row) + Math.Abs(vector.Column);
                if (magnitude > 0 && sourceVariancePerSample < 64)
                {
                    offset += magnitude * (64 - sourceVariancePerSample);
                }
            }

            return offset;
        }

        /// <summary>
        /// Gets the energy difference between the most detailed and the least detailed 8x8 sub-blocks of the block source.
        /// The energy of a sub-block is the rounded natural logarithm of its per-sample variance plus one. A block narrower or shorter than 16 has none.
        /// The result is kept with its block size. A later block of the same size gets the same result at any position, as the cache of the reference does.
        /// Reference: get_sub_block_energy_diff().
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>The energy difference.</returns>
        private readonly int GetSubBlockEnergyDifference(Point blockOrigin, Av1BlockSize blockSize)
        {
            if (this.blockWorkspace.SubBlockEnergyBlockSize == blockSize)
            {
                return this.blockWorkspace.SubBlockEnergyDifference;
            }

            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int difference = 0;
            if (width >= 16 && height >= 16)
            {
                Span<TSample> zeros = stackalloc TSample[8];
                zeros.Clear();
                Av1PlaneRegion<TSample> luma = this.source.GetPlane(Av1Plane.Y);
                int shift = this.bitDepth.GetBitCount() - 8;
                uint minimum = uint.MaxValue;
                uint maximum = 0;
                for (int row = 0; row < height; row += 8)
                {
                    for (int column = 0; column < width; column += 8)
                    {
                        ReadOnlySpan<TSample> subBlock = Av1TransformBlockEncoder.GetPlaneSpan(luma, new Point(blockOrigin.X + column, blockOrigin.Y + row));
                        TOperator.GetMoments(subBlock, luma.Stride, zeros, 0, 8, 8, out int sum, out long squares);

                        // A high bit depth first rounds both moments to 8-bit precision. The variance is never less than zero.
                        // Reference: aom_highbd_10_variance8x8() and aom_highbd_12_variance8x8().
                        long normalizedSum = sum;
                        long normalizedSquares = squares;
                        if (shift > 0)
                        {
                            normalizedSum = (sum + (1 << (shift - 1))) >> shift;
                            normalizedSquares = (squares + (1L << ((2 * shift) - 1))) >> (2 * shift);
                        }

                        uint variance = (uint)Math.Max(normalizedSquares - ((normalizedSum * normalizedSum) / 64), 0) / 64;
                        minimum = Math.Min(minimum, variance);
                        maximum = Math.Max(maximum, variance);
                    }
                }

                int minimumEnergy = (int)Math.Round(Math.Log(minimum + 1.0), MidpointRounding.AwayFromZero);
                int maximumEnergy = (int)Math.Round(Math.Log(maximum + 1.0), MidpointRounding.AwayFromZero);
                difference = Math.Max(0, maximumEnergy - minimumEnergy);
            }

            this.blockWorkspace.SubBlockEnergyDifference = difference;
            this.blockWorkspace.SubBlockEnergyBlockSize = blockSize;
            return difference;
        }

        /// <summary>
        /// Increases the distortions that a skip decision compares, when high bit depth sharpness 3 adds a skip penalty.
        /// When the source measure is more than the prediction measure, the coded distortion gets the difference.
        /// The skip distortion also gets the difference, four times for a source with low detail.
        /// Reference: av1_get_tx_skip_dist().
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="prediction">The luma prediction of the block, which its destination holds.</param>
        /// <param name="codedDistortion">The distortion when the residual is coded.</param>
        /// <param name="skipDistortion">The distortion when the residual is skipped.</param>
        private readonly void PenalizeTransformSkip(
            Point blockOrigin, Av1BlockSize blockSize, ReadOnlySpan<TSample> prediction, ref long codedDistortion, ref long skipDistortion)
        {
            if (!this.PenalizesHighBitDepthSkip)
            {
                return;
            }

            int width = blockSize.GetWidth();
            this.GetVarianceStatistics(blockOrigin, blockSize, prediction, width, out long sourceVariance, out long sampleVariance);
            if (sourceVariance <= sampleVariance)
            {
                return;
            }

            long offset = sourceVariance - sampleVariance;
            codedDistortion += offset;
            skipDistortion += sourceVariance / (width * blockSize.GetHeight()) < 64 ? offset * 4 : offset;
        }

        /// <summary>
        /// Gets a value indicating whether the texture-loss charge treats a mode as smooth. The smooth modes are DC, the three smooth modes and Paeth.
        /// Reference: is_smooth_intra_mode in adjust_rdcost() and adjust_cost().
        /// </summary>
        /// <param name="mode">The prediction mode.</param>
        /// <returns>True when the mode is smooth.</returns>
        private static bool IsSmoothTextureMode(Av1PredictionMode mode)
            => mode is Av1PredictionMode.DC or Av1PredictionMode.Smooth or Av1PredictionMode.SmoothVertical or Av1PredictionMode.SmoothHorizontal or
                Av1PredictionMode.Paeth;

        /// <summary>
        /// Adds the texture-loss charge of high bit depth sharpness 3 to an inter trial, on a valid luma cost and on the trial statistics.
        /// Reference: the adjust_cost() of this_yrd and the adjust_rdcost() of rd_stats in motion_mode_rd().
        /// </summary>
        /// <param name="statistics">The statistics of the trial.</param>
        /// <param name="currentCost">The cost of the trial statistics before the charge.</param>
        /// <param name="sourceVariance">The source measure from <see cref="GetVarianceStatistics"/>.</param>
        /// <param name="sampleVariance">The prediction measure from <see cref="GetVarianceStatistics"/>.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="skip">True when the trial skips its transform.</param>
        /// <param name="isCompound">True when the trial is a compound.</param>
        /// <param name="isInterIntra">True when the trial is an inter-intra blend.</param>
        /// <param name="vector">The first motion vector of the trial.</param>
        private readonly void ChargeInterTextureLoss(
            ref Av1RateDistortionStatistics statistics,
            long currentCost,
            long sourceVariance,
            long sampleVariance,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool skip,
            bool isCompound,
            bool isInterIntra,
            Av1MotionVector vector)
        {
            long offset = GetTextureLossOffset(sourceVariance, sampleVariance, blockSize, skip, isCompound || isInterIntra, !isCompound, vector);
            if (statistics.LumaCost != long.MaxValue)
            {
                statistics.LumaCost = this.ChargeTextureLossCost(statistics.LumaCost, offset, blockOrigin, blockSize, true, isCompound, false);
            }

            this.ChargeTextureLoss(ref statistics, currentCost, offset, blockOrigin, blockSize, true, isCompound, false);
        }

        /// <summary>
        /// Adds the texture-loss charge of high bit depth sharpness 3 to the statistics of a block. First the distortion gets the offset.
        /// Then a block of 16x16 or larger with an energy difference gets an extra cost, from the cost before this step.
        /// A smooth intra mode pays the full difference, and another intra mode pays one quarter of it. A compound pays one quarter of its cost.
        /// A single reference pays nothing more. Reference: the high bit depth branch of adjust_rdcost().
        /// </summary>
        /// <param name="statistics">The statistics that get the charge.</param>
        /// <param name="currentCost">The cost of the statistics before the charge. The extra cost scales this cost.</param>
        /// <param name="offset">The texture-loss offset.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="isInter">True for an inter prediction.</param>
        /// <param name="isCompound">True for a compound inter prediction.</param>
        /// <param name="smoothIntra">True for the DC, smooth or Paeth intra modes.</param>
        private readonly void ChargeTextureLoss(
            ref Av1RateDistortionStatistics statistics,
            long currentCost,
            long offset,
            Point blockOrigin,
            Av1BlockSize blockSize,
            bool isInter,
            bool isCompound,
            bool smoothIntra)
        {
            if (offset > 0)
            {
                statistics.AddSmoothingOffset(this.rateMultiplier, offset);
                currentCost = statistics.Cost;
            }

            if (blockSize < Av1BlockSize.Block16x16)
            {
                return;
            }

            int difference = this.GetSubBlockEnergyDifference(blockOrigin, blockSize);
            if (difference <= 0)
            {
                return;
            }

            long extraCost = !isInter ? smoothIntra ? currentCost * difference : currentCost * difference / 4 : isCompound ? currentCost / 4 : 0;
            if (extraCost > 0)
            {
                statistics.AddEnergyCost(this.rateMultiplier, extraCost);
            }
        }

        /// <summary>
        /// Adds the texture-loss charge of high bit depth sharpness 3 to a luma cost. First the cost gets the price of the offset.
        /// Then a block of 16x16 or larger with an energy difference gets a multiple of the cost, by the same rules as <see cref="ChargeTextureLoss"/>.
        /// Reference: the high bit depth branch of adjust_cost().
        /// </summary>
        /// <param name="cost">The luma cost that gets the charge.</param>
        /// <param name="offset">The texture-loss offset.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="isInter">True for an inter prediction.</param>
        /// <param name="isCompound">True for a compound inter prediction.</param>
        /// <param name="smoothIntra">True for the DC, smooth or Paeth intra modes.</param>
        /// <returns>The cost with the charge.</returns>
        private readonly long ChargeTextureLossCost(
            long cost, long offset, Point blockOrigin, Av1BlockSize blockSize, bool isInter, bool isCompound, bool smoothIntra)
        {
            if (offset > 0)
            {
                cost += Av1RateDistortion.GetCost(this.rateMultiplier, 0, offset);
            }

            if (blockSize < Av1BlockSize.Block16x16)
            {
                return cost;
            }

            int difference = this.GetSubBlockEnergyDifference(blockOrigin, blockSize);
            if (difference <= 0)
            {
                return cost;
            }

            return !isInter ? smoothIntra ? cost + (cost * difference) : cost + (cost * difference / 4) : isCompound ? cost + (cost / 4) : cost;
        }

        /// <summary>
        /// Selects compound syntax using luma estimates before full residual coding.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="bestCost">The cost above which the search stops.</param>
        /// <param name="commonPredictionRate">The rate of the prediction syntax that every blend of the pair pays.</param>
        /// <param name="mode">The compound mode.</param>
        /// <param name="primaryReference">The first reference of the pair.</param>
        /// <param name="secondaryReference">The second reference of the pair.</param>
        /// <param name="filter">The interpolation filter of the estimates.</param>
        /// <param name="referenceIndex">The dynamic reference list index.</param>
        /// <param name="referenceMotionVectors">The reference vector list of the pair.</param>
        /// <param name="topAverageCosts">The lowest average-blend costs of the block so far.</param>
        /// <param name="maskHistory">The wedge index, wedge sign and difference-weighted mask type that every pair of the block shares.</param>
        /// <param name="typeHistory">The last compound type of the pair.</param>
        /// <param name="primary">The first vector, which a masked blend may refine.</param>
        /// <param name="secondary">The second vector, which a masked blend may refine.</param>
        /// <param name="selectedType">The selected compound type.</param>
        /// <param name="selectedWedgeIndex">The wedge mask index of a wedge blend.</param>
        /// <param name="selectedWedgeSign">The wedge sign of a wedge blend.</param>
        /// <param name="selectedMaskType">The mask type of a difference-weighted blend.</param>
        /// <param name="selectedTypeRate">The rate of the blend syntax.</param>
        /// <returns><see langword="true"/> when a blend is selected.</returns>
        private bool SelectCompoundBlend(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            long bestCost,
            int commonPredictionRate,
            Av1PredictionMode mode,
            Av1ReferenceFrameType primaryReference,
            Av1ReferenceFrameType secondaryReference,
            Av1InterpolationFilter filter,
            int referenceIndex,
            Av1ReferenceMotionVectors referenceMotionVectors,
            Span<long> topAverageCosts,
            Span<int> maskHistory,
            Span<int> typeHistory,
            ref Av1MotionVector primary,
            ref Av1MotionVector secondary,
            out Av1CompoundType selectedType,
            out int selectedWedgeIndex,
            out bool selectedWedgeSign,
            out Av1DifferenceWeightedMaskType selectedMaskType,
            out int selectedTypeRate)
        {
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int count = width * height;
            Span<TSample> firstPrediction = interWorkspace.LumaCandidateReconstruction[..count];
            Span<TSample> secondPrediction = interWorkspace.BlueCandidateReconstruction[..count];
            Span<byte> mask = compoundMask[..count];

            bool masked = this.picture.Sequence.SequenceHeader.EnableMaskedCompound && Math.Min(width, height) >= 8;
            bool jointCompound = this.picture.Sequence.SequenceHeader.OrderHintInfo.EnableJointCompound;
            bool supportsWedge = blockSize is Av1BlockSize.Block8x8 or Av1BlockSize.Block8x16 or Av1BlockSize.Block16x8 or
                Av1BlockSize.Block16x16 or Av1BlockSize.Block16x32 or Av1BlockSize.Block32x16 or Av1BlockSize.Block32x32 or
                Av1BlockSize.Block8x32 or Av1BlockSize.Block32x8;

            int groupContext = Av1TileWriter.GetCompoundGroupIndexContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int indexContext = Av1SymbolContextHelper.GetCompoundIndexContext(
                this.picture.Sequence.SequenceHeader.OrderHintInfo,
                frameHeader,
                primaryReference,
                secondaryReference,
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock);

            bool hasNew = mode is Av1PredictionMode.NewNewMotionVector or Av1PredictionMode.NewNearestMotionVector or
                Av1PredictionMode.NewNearMotionVector or Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NearNewMotionVector;

            Av1MotionVector initialPrimary = primary;
            Av1MotionVector initialSecondary = secondary;
            int initialMotionRate = GetCompoundMotionRate(in motionVectorCosts, mode, primary, secondary, referenceIndex, referenceMotionVectors);
            int totalModeRate = commonPredictionRate + GetCompoundInterModeRate(
                in tables, in motionVectorCosts, mode, primary, secondary, referenceIndex, referenceMotionVectors);

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
                int typeIndex = (int)previous.SelectedType;
                if (record.Rates[typeIndex] == int.MaxValue)
                {
                    // Without stored statistics the mode keeps the average blend at no type cost and no estimate,
                    // which only a block without a best cost survives. Reference: the early return of
                    // populate_reuse_comp_type_data() and the ref_best_rd test of process_compound_inter_mode().
                    selectedType = Av1CompoundType.Average;
                    selectedWedgeIndex = 0;
                    selectedWedgeSign = false;
                    selectedMaskType = Av1DifferenceWeightedMaskType.Type38;
                    selectedTypeRate = 0;
                    return bestCost == long.MaxValue;
                }

                selectedType = previous.SelectedType;
                selectedWedgeIndex = previous.WedgeIndex;
                selectedWedgeSign = previous.WedgeSign;
                selectedMaskType = previous.MaskType;
                selectedTypeRate = record.BlendRates[typeIndex];
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

            Av1ModeCosts modeCosts = tables.ModeCosts;
            ReadOnlySpan<Av1EncoderFrame<TSample>> references = this.references.Span;
            foreach (Av1CompoundType type in types)
            {
                bool isWedge = type == Av1CompoundType.Wedge;
                bool isMasked = isWedge || type == Av1CompoundType.DifferenceWeighted;
                if ((type == Av1CompoundType.DistanceWeighted && !jointCompound) ||
                    (isMasked && !masked) ||
                    (isWedge && (!supportsWedge || this.interSourceVariance <= settings.InterInterWedgeVarianceThreshold ||
                                typeHistory[0] == (int)Av1CompoundType.Average)))
                {
                    continue;
                }

                long currentBest = Math.Min(bestCost, bestEstimate);
                int baseBlendRate = Av1SymbolEncoder.GetCompoundBlendCost(tables.ModeCosts, blockSize, type, groupContext, indexContext, 0, masked);
                if (isWedge)
                {
                    baseBlendRate -= modeCosts.GetWedgeIndex(blockSize, 0) + 512;
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
                    // A GLOBAL_GLOBALMV block warps each single predictor with the model of its reference.
                    // Reference: av1_init_warp_params() in build_inter_predictors_single_buf().
                    Av1PredictionMode singleMode = mode == Av1PredictionMode.GlobalGlobalMotionVector
                        ? Av1PredictionMode.GlobalMotionVector
                        : Av1PredictionMode.NearestMotionVector;

                    this.PrepareInterPlanePrediction(
                        initialPrimary,
                        default,
                        Av1Plane.Y,
                        singleMode,
                        primaryReference,
                        Av1ReferenceFrameType.None,
                        false,
                        Av1CompoundType.Average,
                        0,
                        false,
                        default,
                        filter,
                        filter,
                        references[(int)primaryReference].CodedView.GetPlane(Av1Plane.Y),
                        default,
                        blockOrigin,
                        0,
                        0,
                        blockSize,
                        firstPrediction,
                        interWorkspace.Residual,
                        interWorkspace.FilterRows,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors);

                    this.PrepareInterPlanePrediction(
                        initialSecondary,
                        default,
                        Av1Plane.Y,
                        singleMode,
                        secondaryReference,
                        Av1ReferenceFrameType.None,
                        false,
                        Av1CompoundType.Average,
                        0,
                        false,
                        default,
                        filter,
                        filter,
                        references[(int)secondaryReference].CodedView.GetPlane(Av1Plane.Y),
                        default,
                        blockOrigin,
                        0,
                        0,
                        blockSize,
                        secondPrediction,
                        interWorkspace.Residual,
                        interWorkspace.FilterRows,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors);

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
                        in tables,
                        in interWorkspace,
                        compoundMask,
                        blockOrigin,
                        blockSize,
                        type,
                        firstPrediction,
                        secondPrediction,
                        out wedgeIndex,
                        out wedgeSign,
                        out maskType,
                        out long modelError);

                    int selectedBlendRate = Av1SymbolEncoder.GetCompoundBlendCost(
                        tables.ModeCosts, blockSize, type, groupContext, indexContext, wedgeIndex, masked);

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

                    int blendRate = Av1SymbolEncoder.GetCompoundBlendCost(tables.ModeCosts, blockSize, type, groupContext, indexContext, wedgeIndex, masked);

                    // The syntax of a wedge is priced before its prediction is formed. Half of the
                    // current winner is the allowance: a shape whose syntax alone already reaches
                    // it leaves no room for the distortion that will follow. A wedge kept from an
                    // earlier mode of the block is not pruned this way. Reference: the
                    // need_mask_search branches of av1_compound_type_rd().
                    if (isWedge && !modelMask && !reuseMask &&
                        Av1RateDistortion.GetCost(this.rateMultiplier, blendRate + totalModeRate, 0) >= currentBest / 2)
                    {
                        continue;
                    }

                    // A mask changes which samples each reference contributes, so the vectors that
                    // suited an average are no longer the best pair. Only a mode that codes its own
                    // vectors can be refined, and the speed settings decide whether the refinement
                    // is worth its cost for this blend. An average or distance blend refines unless the speed
                    // keeps refinement for NEW_NEWMV only. Reference: skip_mv_refinement_for_avg_distwtd in
                    // av1_compound_type_rd().
                    Av1MotionVector trialPrimary = initialPrimary;
                    Av1MotionVector trialSecondary = initialSecondary;
                    bool refinesUnmasked = settings.CompoundMotionSearchLevel < 2 || mode == Av1PredictionMode.NewNewMotionVector;
                    bool refine = hasNew && (isWedge ? settings.RefineWedgeMotion : isMasked ? !modelMask : refinesUnmasked);

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
                            in interWorkspace,
                            in motionVectorCosts,
                            blockOrigin,
                            blockSize,
                            primaryReference,
                            secondaryReference,
                            mode,
                            referenceIndex,
                            referenceMotionVectors,
                            motionMask,
                            isWedge,
                            ref trialPrimary,
                            ref trialSecondary);
                    }

                    // A wedge kept from an earlier mode of the block is predicted from the references, as a
                    // refined or difference-weighted blend is; a searched wedge blends the two single-reference
                    // predictions. Reference: av1_enc_build_inter_predictor() in the reuse branch of
                    // av1_compound_type_rd(), and av1_build_wedge_inter_predictor_from_buf() in its search loop.
                    int motionRate = GetCompoundMotionRate(in motionVectorCosts, mode, trialPrimary, trialSecondary, referenceIndex, referenceMotionVectors);
                    if (!isMasked || refine || (!isWedge && !modelMask) || (isWedge && reuseMask))
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
                            references[(int)primaryReference].CodedView.GetPlane(Av1Plane.Y),
                            references[(int)secondaryReference].CodedView.GetPlane(Av1Plane.Y),
                            blockOrigin,
                            0,
                            0,
                            blockSize,
                            interWorkspace.LumaPrediction,
                            interWorkspace.Residual,
                            interWorkspace.FilterRows,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors);
                    }
                    else
                    {
                        firstPrediction.CopyTo(interWorkspace.LumaPrediction);
                        TOperator.BlendInterIntraPrediction(interWorkspace.LumaPrediction, secondPrediction, mask, width, height);
                    }

                    int typeIndex = (int)type;
                    bool useCachedEstimate = record.Rates[typeIndex] != int.MaxValue &&
                        (modelMask || (!isMasked && settings.CompoundMotionSearchLevel == 2 && mode != Av1PredictionMode.NewNewMotionVector));

                    // Only the average blend predicts into the frame. libaom then moves the luma pd->dst to tmp_dst for the
                    // later types, and an average with stored statistics predicts nothing. Reference: the
                    // av1_enc_build_inter_predictor() calls of COMPOUND_AVERAGE and the restore_dst_buf() calls with tmp_dst in
                    // av1_compound_type_rd().
                    if (type == Av1CompoundType.Average && !useCachedEstimate)
                    {
                        this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, interWorkspace.LumaPrediction);
                    }

                    long model = this.GetCompoundPredictionModelCost(
                        in interWorkspace,
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
                        firstPrediction.CopyTo(interWorkspace.LumaPrediction);
                        TOperator.BlendInterIntraPrediction(interWorkspace.LumaPrediction, secondPrediction, mask, width, height);
                        model = maskModel + Av1RateDistortion.GetCost(this.rateMultiplier, blendRate + motionRate, 0);
                    }

                    // The gate prices the vectors the mode came with, even after a refinement moved them.
                    // Reference: the rs2 + *rate_mv of each prune_mode_by_skip_rd() call in av1_compound_type_rd().
                    if ((modelMask && !useCachedEstimate && settings.PruneCompoundTypeByModel && model > bestModel) ||
                        (!modelMask && !useCachedEstimate && !this.ShouldSearchCompoundTransforms(blendRate + initialMotionRate, predictionError << 4)))
                    {
                        continue;
                    }

                    // A kept wedge is estimated without a bound, and a searched wedge against the best of its type
                    // and the block. A difference blend searched without the model is bounded by the block's best.
                    // An average or distance blend is estimated without a bound when its vectors may be refined,
                    // whether or not the mode codes a new vector; otherwise its bound drops the whole mode syntax.
                    // A modeled masked blend's bound drops only its type and vector syntax. Reference: the
                    // estimate_yrd_for_sb() bounds of av1_compound_type_rd(), whose COMPOUND_DIFFWTD search branch
                    // passes ref_best_rd and whose skip_mv_refinement_for_avg_distwtd branch subtracts
                    // RDCOST(rs2 + rd_stats->rate), and of masked_compound_type_rd(), which subtracts
                    // RDCOST(*rs2 + *out_rate_mv).
                    long residualBound = isWedge && !modelMask && reuseMask ? long.MaxValue :
                        isWedge && !modelMask ? Math.Min(typeEstimate, currentBest) :
                        isMasked && !modelMask ? currentBest :
                        !isMasked && refinesUnmasked ? long.MaxValue :
                        !isMasked ? Math.Min(bestEstimate, threshold) - Av1RateDistortion.GetCost(this.rateMultiplier, blendRate + totalModeRate, 0) :
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
                            in tables,
                            transformCoefficients,
                            dequantizedCoefficients,
                            transformWorkspace,
                            in interWorkspace,
                            in transformEdges,
                            in lumaCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            macroBlock,
                            blockOrigin,
                            blockSize,
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
                        selectedTypeRate = 0;
                        return false;
                    }
                }
            }

            if (mode == Av1PredictionMode.NewNewMotionVector)
            {
                typeHistory[0] = (int)selectedType;
            }

            if (matchIndex < 0 && this.compoundSearchRecordCount < Av1CompoundSearchRecord.Capacity)
            {
                record.SelectedType = selectedType;
                record.WedgeIndex = (byte)selectedWedgeIndex;
                record.WedgeSign = selectedWedgeSign;
                record.MaskType = selectedMaskType;
                records[this.compoundSearchRecordCount++] = record;
            }

            // The mode carries the syntax cost of the best type, or none when no type produced an estimate; then
            // only a block without a best cost keeps it. Reference: best_compmode_interinter_cost of
            // av1_compound_type_rd() and the ref_best_rd test of process_compound_inter_mode().
            if (bestEstimate == long.MaxValue)
            {
                selectedTypeRate = 0;
                return bestCost == long.MaxValue;
            }

            selectedTypeRate = Av1SymbolEncoder.GetCompoundBlendCost(
                tables.ModeCosts, blockSize, selectedType, groupContext, indexContext, selectedWedgeIndex, masked);

            return bestCost == long.MaxValue || (bestEstimate >> 4) * 11 <= bestCost;
        }

        /// <summary>
        /// Gates luma-only compound transform estimates against the best completed inter prediction.
        /// </summary>
        private bool ShouldSearchCompoundTransforms(int syntaxRate, long predictionError)
        {
            int level = this.picture.Parent.SpeedSettings.GetInterTransformGateLevel(
                this.picture.Parent.FrameUpdateType,
                this.picture.Sequence.SequenceHeader.EnableMaskedCompound ? Av1TransformSearchCase.CompoundType : Av1TransformSearchCase.Default);

            if (level == 0 || this.bestInterLumaPredictionCost == long.MaxValue)
            {
                return true;
            }

            ReadOnlySpan<int> quantizerThresholds = [0, 0, 0, 80, 100, 140];
            ReadOnlySpan<int> scales = [int.MaxValue, 4, 3, 2, 2, 1];
            ReadOnlySpan<int> lumaMultipliers = [int.MaxValue, 32, 29, 17, 17, 17];
            int factor = 4;
            if (this.bestInterLumaPredictionCost > this.interSourceVarianceCost &&
                this.blockQIndex >= quantizerThresholds[level])
            {
                factor *= scales[level];
            }

            long threshold = (this.bestInterLumaPredictionCost * factor * lumaMultipliers[level]) >> 6;
            return Av1RateDistortion.GetCost(this.rateMultiplier, syntaxRate, predictionError) <= threshold;
        }

        /// <summary>
        /// Estimates the wedge sign of all masks from the errors of the two predictions over opposite quadrants.
        /// Reference: estimate_wedge_sign().
        /// </summary>
        /// <param name="sourcePlane">The luma source plane.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="firstPrediction">The first prediction, packed at the block width.</param>
        /// <param name="secondPrediction">The second prediction, packed at the block width.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <returns><see langword="true"/> when the first prediction fits the bottom-right side better.</returns>
        private readonly bool EstimateWedgeSign(
            Av1PlaneRegion<TSample> sourcePlane,
            Point blockOrigin,
            ReadOnlySpan<TSample> firstPrediction,
            ReadOnlySpan<TSample> secondPrediction,
            int width,
            int height)
        {
            // A wedge divides the block, so the sign says which reference fills which side. The squared
            // errors of the top-left and bottom-right quadrants come from the variance function of the
            // quadrant size, which rounds each one to eight-bit precision on its own.
            ReadOnlySpan<TSample> source = Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin);
            int stride = sourcePlane.Stride;
            int halfWidth = width >> 1;
            int halfHeight = height >> 1;
            int sourceOffset = (halfHeight * stride) + halfWidth;
            int predictionOffset = (halfHeight * width) + halfWidth;
            long firstTopLeft = this.GetQuadrantSquaredError(source, stride, firstPrediction, width, halfWidth, halfHeight);
            long firstBottomRight = this.GetQuadrantSquaredError(source[sourceOffset..], stride, firstPrediction[predictionOffset..], width, halfWidth, halfHeight);
            long secondTopLeft = this.GetQuadrantSquaredError(source, stride, secondPrediction, width, halfWidth, halfHeight);
            long secondBottomRight = this.GetQuadrantSquaredError(source[sourceOffset..], stride, secondPrediction[predictionOffset..], width, halfWidth, halfHeight);
            long topLeft = firstTopLeft - secondTopLeft;
            long bottomRight = secondBottomRight - firstBottomRight;
            return topLeft + bottomRight > 0;
        }

        /// <summary>
        /// Returns the squared error of one quadrant, rounded to eight-bit precision. Reference: the sse output
        /// of aom_variance and aom_highbd_{10,12}_variance.
        /// </summary>
        /// <param name="source">The source samples at the quadrant origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="prediction">The prediction samples at the quadrant origin.</param>
        /// <param name="predictionStride">The prediction row stride.</param>
        /// <param name="width">The quadrant width.</param>
        /// <param name="height">The quadrant height.</param>
        /// <returns>The rounded squared error.</returns>
        private readonly long GetQuadrantSquaredError(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            int width,
            int height)
        {
            TOperator.GetMoments(source, sourceStride, prediction, predictionStride, width, height, out _, out long squares);
            int shift = (this.bitDepth.GetBitCount() - 8) * 2;
            return shift == 0 ? squares : (squares + (1L << (shift - 1))) >> shift;
        }

        /// <summary>
        /// Selects a mask from rounded single predictors while retaining six-bit blend precision in its error estimate.
        /// </summary>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="compoundMask">The blend mask destination.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="compoundType">The masked compound type.</param>
        /// <param name="firstPrediction">The prediction of the first reference.</param>
        /// <param name="secondPrediction">The prediction of the second reference.</param>
        /// <param name="wedgeIndex">The selected wedge mask index.</param>
        /// <param name="wedgeSign">The selected wedge sign.</param>
        /// <param name="maskType">The selected difference-weighted mask type.</param>
        /// <param name="selectedError">The prediction error of the selected mask.</param>
        /// <returns>The modeled cost of the selected mask.</returns>
        private long SelectCompoundMask(
            in Av1CoefficientTables tables,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<byte> compoundMask,
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
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int count = width * height;
            Span<short> residualStorage = MemoryMarshal.Cast<int, short>(interWorkspace.BlueCandidateCoefficients);
            Span<short> firstResidual = residualStorage[..count];
            Span<short> secondResidual = residualStorage.Slice(count, count);
            Span<short> difference = interWorkspace.Residual[..count];
            Span<byte> mask = compoundMask[..count];
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> sourceBlock = Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin);
            TOperator.SubtractPrediction(sourceBlock, sourcePlane.Stride, firstPrediction, firstResidual, width, height);
            TOperator.SubtractPrediction(sourceBlock, sourcePlane.Stride, secondPrediction, secondResidual, width, height);

            // The second prediction minus the first. Reference: the aom_subtract_block() call that fills
            // diff10 in av1_compound_type_rd().
            TOperator.SubtractPackedPrediction(secondPrediction, firstPrediction, difference, width, height);

            bool fastSign = this.picture.Parent.SpeedSettings.FastWedgeSignEstimation;
            bool fixedSign = false;
            long signLimit = 0;
            int shift = (this.bitDepth.GetBitCount() - 8) * 2;
            if (compoundType == Av1CompoundType.Wedge && fastSign)
            {
                fixedSign = this.EstimateWedgeSign(sourcePlane, blockOrigin, firstPrediction, secondPrediction, width, height);
            }
            else if (compoundType == Av1CompoundType.Wedge)
            {
                // Each mask candidate is scored from how much better one reference predicts each
                // sample than the other. Holding that difference per sample lets every candidate be
                // scored by summing over the samples its mask selects, instead of forming a blended
                // prediction for each one. Reference: the sign_limit and av1_wedge_compute_delta_squares()
                // of pick_wedge(). The first residual buffer then holds the differences of squares.
                signLimit = (Av1ResidualBuilder.SumSquares(firstResidual) - Av1ResidualBuilder.SumSquares(secondResidual)) * 32;
                Av1WedgeSearch.ComputeDeltaSquares(firstResidual, firstResidual, secondResidual);
            }

            int acQuantizer = Av1QuantizationLookup.GetAcQuant(
                this.blockQIndex, this.quantization.DeltaQAc[0], this.bitDepth);

            long bestCost = long.MaxValue;
            selectedError = 0;
            wedgeIndex = 0;
            wedgeSign = false;
            maskType = Av1DifferenceWeightedMaskType.Type38;
            int candidates = compoundType == Av1CompoundType.Wedge ? 16 : 2;
            Av1ModeCosts modeCosts = tables.ModeCosts;
            for (int index = 0; index < candidates; index++)
            {
                bool sign = fixedSign;
                if (compoundType == Av1CompoundType.Wedge)
                {
                    Av1WedgeMask.Fill(mask, width, blockSize, index, false, 0, 0, false);
                    if (!fastSign)
                    {
                        sign = Av1WedgeSearch.GetSign(firstResidual, mask, signLimit);
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

                long error = (long)Av1WedgeSearch.SumSquaredErrors(secondResidual, difference, mask);
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

                int maskRate = compoundType == Av1CompoundType.Wedge ? modeCosts.GetWedgeIndex(blockSize, index) : 0;
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
                bestCost -= Av1RateDistortion.GetCost(this.rateMultiplier, modeCosts.GetWedgeIndex(blockSize, wedgeIndex), 0);
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
        /// <param name="interWorkspace">The inter prediction buffers of the block, whose luma prediction is current.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="syntaxRate">The prediction syntax rate of the candidate.</param>
        /// <param name="error">The squared prediction error.</param>
        /// <param name="rate">The modeled residual rate.</param>
        /// <param name="distortion">The modeled residual distortion.</param>
        /// <returns>The modeled cost of the candidate.</returns>
        private long GetCompoundPredictionModelCost(
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int syntaxRate,
            out long error,
            out int rate,
            out long distortion)
        {
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Size visible = this.GetPredictionModelSize(blockOrigin, blockSize, 0, 0);
            TOperator.GetMoments(
                Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin),
                sourcePlane.Stride,
                interWorkspace.LumaPrediction,
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
                this.blockQIndex, this.quantization.DeltaQAc[0], this.bitDepth);

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
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="primaryReference">The first reference.</param>
        /// <param name="secondaryReference">The second reference.</param>
        /// <param name="mode">The compound mode.</param>
        /// <param name="referenceIndex">The dynamic reference list index.</param>
        /// <param name="referenceMotionVectors">The reference vector list of the pair.</param>
        /// <param name="mask">The blend mask of the first reference.</param>
        /// <param name="wedge">Whether the mask is a wedge.</param>
        /// <param name="primary">The first vector, which the search may change.</param>
        /// <param name="secondary">The second vector, which the search may change.</param>
        private void RefineCompoundVectors(
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType primaryReference,
            Av1ReferenceFrameType secondaryReference,
            Av1PredictionMode mode,
            int referenceIndex,
            Av1ReferenceMotionVectors referenceMotionVectors,
            Span<byte> mask,
            bool wedge,
            ref Av1MotionVector primary,
            ref Av1MotionVector secondary)
        {
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            Av1MotionSearchSettings motionSettings = this.picture.Parent.MotionSearchSettings;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
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

            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> source = Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin);
            int stackIndex = mode is Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector
                ? referenceIndex + 1 : referenceIndex;

            ReadOnlySpan<Av1EncoderFrame<TSample>> references = this.references.Span;
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

                Av1PlaneRegion<TSample> movingPlane = references[(int)(component == 0 ? primaryReference : secondaryReference)].CodedView.GetPlane(Av1Plane.Y);
                Av1PlaneRegion<TSample> fixedPlane = references[(int)(component == 0 ? secondaryReference : primaryReference)].CodedView.GetPlane(Av1Plane.Y);
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
                    interWorkspace.BluePrediction,
                    interWorkspace.Residual,
                    interWorkspace.FilterRows,
                    blockSize,
                    this.bitDepth);

                // The mask describes the first reference. Reverse it only while the second reference
                // moves, then restore it before the caller builds the final compound prediction.
                if (component != 0)
                {
                    TensorPrimitives.Subtract((byte)64, mask, mask);
                }

                int referenceOrigin = ((movingPlane.Bounds.Y + blockOrigin.Y) * movingPlane.Stride) + movingPlane.Bounds.X + blockOrigin.X;
                Rectangle bounds = Av1MotionVector.GetFrameSearchBounds(
                    new Rectangle(blockOrigin, size), frameSize, Math.Min(movingPlane.Bounds.X, movingPlane.Bounds.Y));

                Av1MotionSearchBase.FullPixelSearch<TSample, TOperator> fullSearch = new(
                    source,
                    sourcePlane.Stride,
                    movingPlane.Samples,
                    movingPlane.Stride,
                    referenceOrigin,
                    size,
                    this.ApplySharpnessMargins(referenceVector.GetFullPixelSearchBounds(bounds), blockOrigin, size, 1),
                    referenceVector,
                    motionVectorCosts,
                    this.bitDepth,
                    Av1RateDistortion.GetMotionSearchSadPerBit(this.blockQIndex, this.bitDepth),
                    this.rateMultiplier,
                    interWorkspace.BluePrediction,
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
                        movingPlane.Samples,
                        movingPlane.Stride,
                        referenceOrigin,
                        interWorkspace.RedPrediction,
                        size,
                        this.ApplySharpnessMargins(referenceVector.GetSubpixelSearchBounds(bounds), blockOrigin, size, Av1MotionVector.SubpixelScale),
                        referenceVector,
                        motionVectorCosts,
                        this.bitDepth,
                        this.rateMultiplier,
                        interWorkspace.BluePrediction,
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
                        Rectangle fractionalBounds = this.ApplySharpnessMargins(
                            referenceVector.GetSubpixelSearchBounds(bounds), blockOrigin, size, Av1MotionVector.SubpixelScale);

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
                    TensorPrimitives.Subtract((byte)64, mask, mask);
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
        /// <param name="tables">The rate tables and level storage of the writer, which the caller read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="transformPrediction">The intra component storage of an inter-intra prediction.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound or inter-intra prediction.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="macroBlock">The block geometry and neighboring transform contexts.</param>
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
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            ReadOnlySpan<Av1EncoderTransformBlockState> states)
        {
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
            this.SetCodedBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, modeInfo.Block.BlockSize, modeInfo.Block.SegmentId);
            bool isInterIntra = modeInfo.Block.SecondaryReferenceFrame == Av1ReferenceFrameType.Intra;
            if (isInterIntra)
            {
                this.PrepareInterIntraPrediction(
                    transformWorkspace,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    modeInfo.Block.BlockSize,
                    block.HasChroma,
                    modeInfo.Block.Mode,
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

            this.SetWarpedPrediction(macroBlock, blockOrigin, modeInfo.Block.PartitionType, modeInfo.Block, vector);
            bool lossless = this.picture.Parent.FrameHeader.CodedLossless;
            int planeCount = block.HasChroma ? 3 : 1;
            bool coded = false;

            // The final chroma type comes from the final luma types, in which an empty luma block is DCT_DCT.
            // Reference: av1_get_tx_type() after encode_block() resets the type of a luma block without coefficients.
            Span<Rectangle> lumaBlocks = stackalloc Rectangle[64];
            Span<Av1TransformType> lumaTypes = stackalloc Av1TransformType[64];
            int lumaBlockCount = 0;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;

                // Every transform block of the plane writes into the same plane coefficients and states.
                Span<int> planeCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, plane);
                Span<Av1EncoderTransformBlockState> planeStates = this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane);
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
                    Av1Plane.Y => interWorkspace.LumaPrediction,
                    Av1Plane.U => interWorkspace.BluePrediction,
                    _ => interWorkspace.RedPrediction,
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
                    interWorkspace.Residual,
                    interWorkspace.FilterRows,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors);

                // The whole luma prediction goes into the frame before the transform blocks, also past the coded area.
                // Reference: av1_enc_build_inter_predictor() into pd->dst in encode_superblock(), before av1_encode_sb().
                if (plane == Av1Plane.Y)
                {
                    this.WriteInterLumaDestination(lumaFrame, blockOrigin, modeInfo.Block.BlockSize, prediction);
                }

                Av1NeighborEdges<byte> neighbors = plane switch
                {
                    Av1Plane.Y => lumaCoefficientEdges,
                    Av1Plane.U => blueCoefficientEdges,
                    _ => redCoefficientEdges,
                };

                int contextWidth = planeBlockSize.Get4x4WideCount();
                int contextHeight = planeBlockSize.Get4x4HighCount();
                Span<byte> transformContexts = interWorkspace.TransformContexts;
                Span<byte> topContexts = transformContexts[..contextWidth];
                Span<byte> leftContexts = transformContexts.Slice(contextWidth, contextHeight);
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
                    Av1EncoderTransformBlockState selectedState = lossless ? default : states[stateOffset + transformIndex];
                    if (planeIndex != 0 && !lossless)
                    {
                        Av1TransformType lumaType = Av1TransformType.DctDct;
                        for (int index = 0; index < lumaBlockCount; index++)
                        {
                            if (lumaBlocks[index].Contains(offset.X << subX, offset.Y << subY))
                            {
                                lumaType = lumaTypes[index];
                                break;
                            }
                        }

                        Av1TransformSetType chromaSet = Av1SymbolContextHelper.GetExtendedTransformSetType(
                            transformSize, isInter: true, this.picture.Parent.FrameHeader.UseReducedTransformSet);

                        selectedState.TransformType = lumaType.IsExtendedSetUsed(chromaSet) ? lumaType : Av1TransformType.DctDct;
                    }

                    this.ReconstructSelectedTransform(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        planeCoefficients,
                        planeStates,
                        context,
                        true,
                        blockOrigin,
                        modeInfo.Block.BlockSize,
                        planeOrigin + new Size(offset.X, offset.Y),
                        plane,
                        transformSize,
                        prediction[inputOffset..],
                        interWorkspace.Residual[inputOffset..],
                        width,
                        selectedState,
                        modeInfo.Block.Skip,
                        coefficientOffset);

                    Av1EncoderTransformBlockState state = planeStates[coefficientOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];

                    coded |= state.EndOfBlock != 0;
                    if (planeIndex == 0 && lumaBlockCount < lumaBlocks.Length)
                    {
                        lumaBlocks[lumaBlockCount] = new Rectangle(offset.X, offset.Y, transformWidth, transformHeight);
                        lumaTypes[lumaBlockCount++] = state.EndOfBlock == 0 ? Av1TransformType.DctDct : state.TransformType;
                    }

                    byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                        planeCoefficients[coefficientOffset..],
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

            this.useWarpedPrediction = false;
            this.useObmcPrediction = false;
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

            // The model measures only the visible samples: the frame edge with border padding, otherwise the coded
            // boundary. Reference: get_visible_dimensions() in model_rd_for_sb_with_curvfit().
            Size visible = this.picture.Parent.GetVisibleBoundary(0, 0);
            int right = blockOrigin.X + blockSize.GetWidth() - visible.Width;
            int bottom = blockOrigin.Y + blockSize.GetHeight() - visible.Height;
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
        /// Returns whether the modeled luma cost of a prepared residual exceeds the transform search bound by the
        /// level 1 margin. The model stores its luma prediction error as the reference's prediction error.
        /// Reference: model_based_tx_search_prune(), with model_rd_for_sb_with_curvfit() for the luma plane.
        /// </summary>
        /// <param name="blockOrigin">The luma coding-block origin.</param>
        /// <param name="blockSize">The coding-block size.</param>
        /// <param name="reference">The first reference of the candidate.</param>
        /// <param name="residual">The luma residual of the candidate's prediction.</param>
        /// <param name="costLimit">The bound of the transform search.</param>
        /// <returns><see langword="true"/> when the transform search can be skipped.</returns>
        private bool PrunesTransformSearchByModel(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType reference,
            ReadOnlySpan<short> residual,
            long costLimit)
        {
            Size visible = this.GetPredictionModelSize(blockOrigin, blockSize, 0, 0);
            long squaredError = Av1ResidualBuilder.SumSquares(residual, blockSize.GetWidth(), visible.Width, visible.Height);
            int normalizationShift = (this.bitDepth.GetBitCount() - 8) * 2;
            if (normalizationShift != 0)
            {
                squaredError = (squaredError + (1L << (normalizationShift - 1))) >> normalizationShift;
            }

            this.SetPredictionSse(reference, squaredError);
            int acQuantizer = Av1QuantizationLookup.GetAcQuant(this.blockQIndex, this.quantization.DeltaQAc[0], this.bitDepth);
            Av1RateDistortion.ModelPredictionError(
                blockSize,
                squaredError,
                visible.Width * visible.Height,
                acQuantizer,
                this.bitDepth,
                this.rateMultiplier,
                out int rate,
                out long distortion);

            // A model that predicts no coded coefficients keeps the search. Reference: the model_skip return.
            if (rate == 0)
            {
                return false;
            }

            // Level 1 compares three eighths of the modeled cost. Reference: prune_factor_by8.
            long modelCost = new Av1RateDistortionStatistics(this.rateMultiplier, rate, distortion).Cost;
            return ((modelCost * 3) >> 3) > costLimit;
        }

        /// <summary>
        /// Records the luma prediction error that a model or search leaves for a first reference.
        /// Reference: the (unsigned int)AOMMIN(sse, UINT_MAX) stores to x->pred_sse.
        /// </summary>
        private void SetPredictionSse(Av1ReferenceFrameType reference, long squaredError)
            => this.predictionSses[(int)reference] = (uint)Math.Min(squaredError, uint.MaxValue);

        /// <summary>
        /// Selects interpolation filters with per-block reuse and frame-history pruning.
        /// </summary>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="hasChroma">Whether the block codes chroma.</param>
        /// <param name="primary">The first motion vector.</param>
        /// <param name="secondary">The second motion vector of a compound prediction.</param>
        /// <param name="modeInfo">The block decisions, whose filters are set.</param>
        /// <param name="bestCandidateCost">The cost of the best candidate of the block so far.</param>
        /// <param name="singleReferenceCost">The best filter cost of the single references of a compound pair.</param>
        /// <param name="filterRate">The rate of the selected filter syntax.</param>
        /// <param name="modelCost">The modeled cost of the selected filters.</param>
        /// <returns>Whether all selected prediction planes are prepared in the shared workspace.</returns>
        private bool SelectInterFilters(
            in Av1CoefficientTables tables,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
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
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
            Av1ModeCosts modeCosts = tables.ModeCosts;
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            bool winnerSearch = settings.UseWinnerInterpolation && this.blockWorkspace.EvaluationStage == Av1EncoderEvaluationStage.Winner;
            bool compound = modeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra;
            bool dual = this.picture.Sequence.SequenceHeader.EnableDualFilter;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            bool writesFilters = Av1TileWriter.UsesSwitchableInterpolation(frameHeader, modeInfo);
            int verticalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo, modeInfoGrid, modeInfoAllocation, macroBlock, 0);
            int horizontalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo, modeInfoGrid, modeInfoAllocation, macroBlock, 1);
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
                // Reference: the pred_sse restore of a find_interp_filter_match() hit.
                this.predictionSses[(int)modeInfo.ReferenceFrame] = records[match].PredictionSse;
                modelCost = records[match].Cost;
                modeInfo.HorizontalInterpolationFilter = records[match].HorizontalFilter;
                modeInfo.VerticalInterpolationFilter = records[match].VerticalFilter;
                filterRate = Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(modeCosts, modeInfo.VerticalInterpolationFilter, verticalContext) +
                    (dual ? Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(modeCosts, modeInfo.HorizontalInterpolationFilter, horizontalContext) : 0);

                // Cached decisions retain syntax, not pixels. The caller rebuilds the current vectors'
                // prediction; approximate vector matches must never reuse another candidate's samples.
                return false;
            }

            // The adaptive search allows only the filters of interp_filter_search_mask, which a one-pass encode
            // never fills, so it tries no filter other than the regular one. Reference: the
            // adaptive_interp_filter_search tests of find_best_non_dual_interp_filter(), with
            // av1_setup_interp_filter_search_mask() called only in the stats-consuming pass of a two-pass encode.
            const int filterCount = Av1InterpolationProbabilities.FilterCount;
            int allowedMask = winnerSearch
                ? (1 << (int)Av1InterpolationFilter.Regular) |
                    (1 << (int)(settings.WinnerInterpolationUsesSharp ? Av1InterpolationFilter.Sharp : Av1InterpolationFilter.Smooth))
                : settings.InterpolationPruningLevel != 0 ? 0 : (1 << filterCount) - 1;

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

                    // Sharpness 3 never tries the smooth filter.
                    if (this.blockWorkspace.EncoderOptions.Sharpness == 3 && filter == (int)Av1InterpolationFilter.Smooth)
                    {
                        allowedMask &= ~(1 << filter);
                    }
                }
            }

            bool modelChroma = hasChroma && !winnerSearch && !settings.SkipInterpolationChromaModel;

            // The skip flags start from the frame's plane count and measure luma and the first chroma plane of every
            // block, whether or not the block codes chroma. Reference: set_default_interp_skip_flags() and the
            // plane loop of calc_interp_skip_pred_flag().
            bool chromaPlanes = !this.source.IsMonochrome;
            int defaultSkip = chromaPlanes ? 3 : 1;
            int horizontalSkip = defaultSkip;
            int verticalSkip = defaultSkip;
            const int interpolationExtension = 4;
            for (int referenceIndex = 0; referenceIndex < (compound ? 2 : 1); referenceIndex++)
            {
                // A scaled reference filters every position, so no axis skips its filter trials. Reference: the
                // av1_is_scaled() test of calc_interp_skip_pred_flag().
                if (this.IsScaledReference(referenceIndex == 0 ? modeInfo.ReferenceFrame : modeInfo.SecondaryReferenceFrame))
                {
                    horizontalSkip = 0;
                    verticalSkip = 0;
                    break;
                }

                Av1MotionVector vector = referenceIndex == 0 ? primary : secondary;
                for (int planeIndex = 0; planeIndex < (chromaPlanes ? 2 : 1); planeIndex++)
                {
                    int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                    int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;

                    // The plane extent is the subsampled block extent, at least four samples. Reference: the
                    // pd->width and pd->height that set_plane_n4() gives clamp_mv_to_umv_border_sb().
                    int planeWidth = Math.Max(blockSize.GetWidth() >> subX, 4);
                    int planeHeight = Math.Max(blockSize.GetHeight() >> subY, 4);
                    int horizontalBorder = (interpolationExtension + planeWidth) << 4;
                    int verticalBorder = (interpolationExtension + planeHeight) << 4;

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

            // A difference-weighted blend builds its mask during luma prediction, which chroma then reads, so luma is
            // predicted for the vertical decision when only the horizontal one predicts. Reference: the
            // COMPOUND_DIFFWTD test of calc_interp_skip_pred_flag().
            if (compound && modeInfo.CompoundIndex && modeInfo.CompoundType == Av1CompoundType.DifferenceWeighted &&
                horizontalSkip == 0 && verticalSkip == 1)
            {
                verticalSkip = 0;
            }

            // Chroma that the search does not model counts as skipped. Reference: the skip_model_rd_uv flags of
            // calc_interp_skip_pred_flag(); a winner search models luma alone.
            if (chromaPlanes && !modelChroma && (settings.SkipInterpolationChromaModel || winnerSearch))
            {
                horizontalSkip |= 2;
                verticalSkip |= 2;
            }

            bool horizontalPhase = horizontalSkip != defaultSkip;
            bool verticalPhase = verticalSkip != defaultSkip;
            int predictionSkip = horizontalSkip & verticalSkip;
            int predictedFilter = -1;
            if (!winnerSearch && !dual && settings.UseNeighborInterpolation && (horizontalPhase || verticalPhase) &&
                ((((blockOrigin.Y >> 2) + (blockOrigin.X >> 2)) >> (System.Numerics.BitOperations.Log2((uint)blockSize.GetWidth()) - 2)) +
                    this.blockWorkspace.FrameNumber & 1) != 0 &&
                macroBlock.IsUpAvailable && macroBlock.IsLeftAvailable)
            {
                Av1EncoderBlockModeInfo above = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -macroBlock.ModeInfoStride).Block;
                Av1EncoderBlockModeInfo left = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -1).Block;
                if (above.ReferenceFrame > Av1ReferenceFrameType.Intra && left.ReferenceFrame > Av1ReferenceFrameType.Intra &&
                    above.HorizontalInterpolationFilter == left.HorizontalInterpolationFilter &&
                    above.VerticalInterpolationFilter == left.VerticalInterpolationFilter)
                {
                    predictedFilter = (int)above.VerticalInterpolationFilter;
                }
            }

            int lumaCount = blockSize.GetWidth() * blockSize.GetHeight();
            Av1BlockSize chromaSize = blockSize.GetSubsampled(this.source.ChromaSubsamplingX != 0, this.source.ChromaSubsamplingY != 0);
            int chromaCount = hasChroma ? chromaSize.GetWidth() * chromaSize.GetHeight() : 0;
            Span<TSample> bestLuma = interWorkspace.LumaPrediction[..lumaCount];
            Span<TSample> trialLuma = interWorkspace.LumaCandidateReconstruction[..lumaCount];
            Span<TSample> bestBlue = interWorkspace.BluePrediction[..chromaCount];
            Span<TSample> trialBlue = interWorkspace.BlueCandidateReconstruction[..chromaCount];
            Span<TSample> bestRed = interWorkspace.RedPrediction[..chromaCount];
            Span<TSample> trialRed = interWorkspace.RedCandidateReconstruction[..chromaCount];
            Av1InterpolationFilter initialFilter = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable
                ? Av1InterpolationFilter.Regular : frameHeader.InterpolationFilter;

            // The default filter's model prices its switchable syntax even when the block codes none, because the
            // rate is taken before the search learns that no filter is needed. The coded rate stays zero then.
            // Reference: the *switchable_rate = get_switchable_rate() call ahead of the need_search return of
            // av1_interpolation_filter_search(), against the av1_is_interp_needed() rate of motion_mode_rd().
            int searchRate = Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(modeCosts, initialFilter, verticalContext) +
                (dual ? Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(modeCosts, initialFilter, horizontalContext) : 0);

            int regularRate = writesFilters ? searchRate : 0;

            InlineArray4<Av1RateDistortionStatistics> bestPlaneStatisticsStorage = default;
            InlineArray4<Av1RateDistortionStatistics> trialPlaneStatisticsStorage = default;
            Span<Av1RateDistortionStatistics> bestPlaneStatistics = bestPlaneStatisticsStorage;
            Span<Av1RateDistortionStatistics> trialPlaneStatistics = trialPlaneStatisticsStorage;
            long bestLumaSquaredError = 0;
            Av1RateDistortionStatistics regular = this.GetInterFilterModelCost(
                in interWorkspace,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                primary,
                secondary,
                modeInfo,
                blockOrigin,
                blockSize,
                modelChroma,
                initialFilter,
                initialFilter,
                searchRate,
                long.MaxValue,
                100,
                0,
                bestLuma,
                bestBlue,
                bestRed,
                bestPlaneStatistics,
                ref bestLumaSquaredError);

            long bestCost = regular.Cost;
            bool lumaUsesWorkspace = true;
            bool chromaUsesWorkspace = true;
            modeInfo.HorizontalInterpolationFilter = initialFilter;
            modeInfo.VerticalInterpolationFilter = initialFilter;
            filterRate = regularRate;

            // The default filter predicts into pd->dst, which is the frame here. The winner search keeps its frame
            // write to the end, where the frame holds its best prediction. Reference: the first interp_model_rd_eval() of
            // av1_interpolation_filter_search(), and fast_interp_search().
            if (!winnerSearch)
            {
                this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, bestLuma);
            }

            // These copy the libaom destination state through the trials. After the default filter, pd->dst moves to tmp_dst.
            // A winning trial that measured a plane swaps pd->dst between the frame and tmp_dst. A trial predicts luma into the
            // pd->dst of that time. A win that measured chroma only toggles the luma rebuild flag.
            // Reference: restore_dst_buf() with tmp_dst, swap_dst_buf() and recalc_luma_mc_data in av1_interpolation_filter_search()
            // and interpolation_filter_rd().
            bool destinationIsTemporary = true;
            bool rebuildLuma = false;

            // Reference: the pred_sse store after the default filter's model in av1_interpolation_filter_search().
            this.SetPredictionSse(modeInfo.ReferenceFrame, bestLumaSquaredError);
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
                int trialRate = Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(modeCosts, (Av1InterpolationFilter)vertical, verticalContext) +
                    (dual ? Av1SymbolEncoder.GetSwitchableInterpolationFilterCost(modeCosts, (Av1InterpolationFilter)horizontal, horizontalContext) : 0);

                if (Av1RateDistortion.GetCost(this.rateMultiplier, trialRate, 0) * scale / 100 > bestCost)
                {
                    continue;
                }

                // Keep unchanged plane estimates and samples in their existing views. Luma can
                // be integer-phase while subsampled chroma remains fractional, so reuse is per plane.
                int skipPlanes = winnerSearch ? 0 : !dual && sharp && sharpMatchesRegular ? defaultSkip : predictionSkip;
                bestPlaneStatistics[..].CopyTo(trialPlaneStatistics);
                long trialLumaSquaredError = bestLumaSquaredError;
                Av1RateDistortionStatistics trial = this.GetInterFilterModelCost(
                    in interWorkspace,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
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
                    trialPlaneStatistics,
                    ref trialLumaSquaredError);

                // The trial predicted luma into pd->dst. Reference: interp_model_rd_eval() in interpolation_filter_rd().
                if (!winnerSearch && (skipPlanes & 1) == 0 && !destinationIsTemporary)
                {
                    this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, trialLuma);
                }

                if (trial.Cost != long.MaxValue && trial.Cost * scale / 100 < bestCost)
                {
                    bestCost = trial.Cost;
                    modeInfo.HorizontalInterpolationFilter = (Av1InterpolationFilter)horizontal;
                    modeInfo.VerticalInterpolationFilter = (Av1InterpolationFilter)vertical;
                    filterRate = trialRate;

                    // Reference: the recalc_luma_mc_data updates and swap_dst_buf() of a win in interpolation_filter_rd().
                    if (!winnerSearch && skipPlanes != defaultSkip)
                    {
                        rebuildLuma = skipPlanes switch
                        {
                            0 => false,
                            1 => !rebuildLuma,
                            _ => rebuildLuma
                        };

                        destinationIsTemporary = !destinationIsTemporary;
                    }

                    // A win that skipped the chroma model keeps the earlier statistics, so the luma error that
                    // pred_sse reads stays too. Reference: the rd_stats_luma and rd_stats updates of
                    // interpolation_filter_rd(), made only for INTERP_EVAL_LUMA_EVAL_CHROMA and
                    // INTERP_SKIP_LUMA_EVAL_CHROMA.
                    if ((skipPlanes & 2) == 0)
                    {
                        bestLumaSquaredError = trialLumaSquaredError;
                        trialPlaneStatistics[..].CopyTo(bestPlaneStatistics);
                    }

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

            // The search ends with pd->dst on the buffer of the best prediction. When the luma of that buffer is not the best luma,
            // libaom predicts the best luma into it again. The winner search leaves its best prediction in the frame.
            // Reference: the swap_dst_buf() and the recalc_luma_mc_data rebuild at the end of av1_interpolation_filter_search(),
            // and the copy into orig_dst at the end of fast_interp_search().
            destinationIsTemporary = !destinationIsTemporary;
            if (winnerSearch || (rebuildLuma && !destinationIsTemporary))
            {
                this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, bestLuma);
            }

            if (!lumaUsesWorkspace)
            {
                bestLuma.CopyTo(interWorkspace.LumaPrediction);
            }

            if (modelChroma && !chromaUsesWorkspace)
            {
                bestBlue.CopyTo(interWorkspace.BluePrediction);
                bestRed.CopyTo(interWorkspace.RedPrediction);
            }

            if (hasChroma && !modelChroma)
            {
                // Difference-weighted chroma consumes the luma mask. Rebuild that mask for the winner
                // after filter trials, then prepare only the chroma planes omitted by the luma-only model.
                int firstPlane = compound && modeInfo.CompoundType == Av1CompoundType.DifferenceWeighted ? 0 : 1;
                ReadOnlySpan<Av1EncoderFrame<TSample>> references = this.references.Span;
                for (int planeIndex = firstPlane; planeIndex < 3; planeIndex++)
                {
                    Av1Plane plane = (Av1Plane)planeIndex;
                    int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                    int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                    Span<TSample> prediction = planeIndex == 0 ? interWorkspace.LumaPrediction :
                        planeIndex == 1 ? interWorkspace.BluePrediction : interWorkspace.RedPrediction;
                    Av1PlaneRegion<TSample> reference = references[(int)modeInfo.ReferenceFrame].CodedView.GetPlane(plane);
                    Av1PlaneRegion<TSample> secondaryReference = modeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                        ? references[(int)modeInfo.SecondaryReferenceFrame].CodedView.GetPlane(plane)
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
                        interWorkspace.Residual,
                        interWorkspace.FilterRows,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors);

                    // A difference-weighted blend predicts luma into pd->dst again. Reference: the av1_enc_build_inter_predictor()
                    // call after av1_interpolation_filter_search() in handle_inter_mode().
                    if (planeIndex == 0 && !destinationIsTemporary)
                    {
                        this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, prediction);
                    }
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
                    Cost = bestCost,
                    PredictionSse = (uint)Math.Min(bestLumaSquaredError, uint.MaxValue)
                };
            }

            // Reference: the pred_sse store of the selected filter's luma error at the end of
            // av1_interpolation_filter_search().
            this.SetPredictionSse(modeInfo.ReferenceFrame, bestLumaSquaredError);
            modelCost = bestCost;
            return true;
        }

        /// <summary>
        /// Ranks a filter pair from visible prediction error without transforming samples.
        /// </summary>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="vector">The motion vector.</param>
        /// <param name="secondaryVector">The second motion vector of a compound prediction.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="modelChroma">Whether the chroma planes are modeled too.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="filterRate">The rate of the filter syntax.</param>
        /// <param name="bestCost">The cost above which the model stops.</param>
        /// <param name="comparisonScale">The percentage of the partial cost that is compared with the best cost.</param>
        /// <param name="skipPlanes">Bit zero keeps the luma statistics and bit one the chroma statistics already in <paramref name="planeStatistics"/>.</param>
        /// <param name="lumaPrediction">The luma prediction destination.</param>
        /// <param name="bluePrediction">The blue prediction destination.</param>
        /// <param name="redPrediction">The red prediction destination.</param>
        /// <param name="planeStatistics">The modeled statistics of each plane.</param>
        /// <param name="lumaSquaredError">The squared luma prediction error.</param>
        /// <returns>The modeled rate and distortion, or an invalid result when the model stops.</returns>
        private Av1RateDistortionStatistics GetInterFilterModelCost(
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
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
            Span<Av1RateDistortionStatistics> planeStatistics,
            ref long lumaSquaredError)
        {
            int planeCount = modelChroma ? 3 : 1;
            bool simpleModel = this.picture.Parent.SpeedSettings.UseSimpleInterpolationModel;
            int rate = filterRate;
            long distortion = 0;
            ReadOnlySpan<Av1EncoderFrame<TSample>> references = this.references.Span;
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
                Span<short> residual = interWorkspace.Residual[..(width * height)];
                Av1PlaneRegion<TSample> primaryReferencePlane = references[(int)modeInfo.ReferenceFrame].CodedView.GetPlane(plane);
                Av1PlaneRegion<TSample> secondaryReferencePlane = modeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                    ? references[(int)modeInfo.SecondaryReferenceFrame].CodedView.GetPlane(plane)
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
                    residual,
                    interWorkspace.FilterRows,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors);

                Size visible = simpleModel ? new Size(width, height) : this.GetPredictionModelSize(blockOrigin, blockSize, subsamplingX, subsamplingY);
                int visibleWidth = visible.Width;
                int visibleHeight = visible.Height;

                // The source view includes samples extended to the coded dimensions; only the visible
                // rectangle counts.
                long squaredError = Av1ResidualBuilder.SumSquares(residual, width, visibleWidth, visibleHeight);

                int normalizationShift = (this.bitDepth.GetBitCount() - 8) * 2;
                if (normalizationShift != 0)
                {
                    squaredError = (squaredError + (1L << (normalizationShift - 1))) >> normalizationShift;
                }

                if (plane == Av1Plane.Y)
                {
                    lumaSquaredError = squaredError;
                }

                int acQuantizer = Av1QuantizationLookup.GetAcQuant(
                    this.blockQIndex,
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
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="bestCost">The cost above which the evaluation stops.</param>
        /// <param name="hasChroma">Whether the block codes chroma.</param>
        /// <param name="commonPredictionRate">The rate of the prediction syntax that the caller already priced.</param>
        /// <param name="referenceFrame">The reference frame, or intra for an intra block copy.</param>
        /// <param name="vector">The motion vector.</param>
        /// <param name="secondaryVector">The second motion vector of a compound prediction.</param>
        /// <param name="mode">The prediction mode.</param>
        /// <param name="secondaryReferenceFrame">The second reference frame of a compound prediction, or none.</param>
        /// <param name="usePreparedPrediction">Whether the prediction planes are already prepared in the workspace.</param>
        /// <param name="compoundType">The compound blend type.</param>
        /// <param name="compoundWedgeIndex">The wedge mask index of a wedge blend.</param>
        /// <param name="compoundWedgeSign">The wedge sign of a wedge blend.</param>
        /// <param name="differenceWeightedMaskType">The mask type of a difference-weighted blend.</param>
        /// <param name="compoundBlendRate">The rate of the compound blend syntax.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="referenceMotionVectorIndex">The dynamic reference list index.</param>
        /// <param name="isInterIntra">Whether the prediction is an inter-intra blend.</param>
        /// <param name="interIntraMode">The intra mode of an inter-intra blend.</param>
        /// <param name="useInterIntraWedge">Whether an inter-intra blend uses a wedge mask.</param>
        /// <param name="interIntraWedgeIndex">The wedge mask index of an inter-intra blend.</param>
        /// <param name="referenceMotionVectors">The reference vector list of the reference frame.</param>
        /// <param name="lumaReconstruction">The luma reconstruction storage of the candidate.</param>
        /// <param name="lumaCoefficients">The luma coefficient storage of the candidate.</param>
        /// <param name="blueReconstruction">The blue reconstruction storage of the candidate.</param>
        /// <param name="blueCoefficients">The blue coefficient storage of the candidate.</param>
        /// <param name="redReconstruction">The red reconstruction storage of the candidate.</param>
        /// <param name="redCoefficients">The red coefficient storage of the candidate.</param>
        /// <param name="skip">Whether the candidate codes no residual.</param>
        /// <param name="lumaStates">The luma transform states of the candidate.</param>
        /// <param name="lumaTransformSizes">The luma transform sizes of the candidate.</param>
        /// <param name="blueState">The blue transform states of the candidate.</param>
        /// <param name="redState">The red transform states of the candidate.</param>
        /// <returns>The rate and distortion of the candidate.</returns>
        private Av1RateDistortionStatistics EvaluateInterCandidate(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
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
            Av1ReferenceMotionVectors referenceMotionVectors,
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
            Span<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y).Samples;
            bool isCompound = secondaryReferenceFrame > Av1ReferenceFrameType.Intra;
            this.lumaSearchFailed = false;
            this.unbiasedInterTrialCost = long.MaxValue;
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
                    ? GetCompoundInterModeRate(
                        in tables,
                        in motionVectorCosts,
                        mode,
                        vector,
                        secondaryVector,
                        referenceMotionVectorIndex,
                        referenceMotionVectors)
                    : GetInterModeRate(in tables, in motionVectorCosts, mode, vector, referenceMotionVectorIndex, referenceMotionVectors));
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
                CompoundIndex = compoundType != Av1CompoundType.DistanceWeighted,
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
            else if (!isCompound && this.useWarpedPrediction)
            {
                predictionModeInfo.MotionMode = Av1MotionMode.Warped;
            }
            else if (!isCompound && this.useObmcPrediction)
            {
                predictionModeInfo.MotionMode = Av1MotionMode.Obmc;
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

            Av1EncoderFrame<TSample>.PlanarView primaryReference = referenceFrame == Av1ReferenceFrameType.Intra
                ? this.reconstruction
                : this.references.Span[(int)referenceFrame].CodedView;

            // The prediction error is only accumulated when something later reads it: either this
            // pass is estimating candidates rather than coding them, or a speed setting uses the
            // error to decide whether the transform search runs at all.
            bool measurePrediction = this.estimateInterCandidates ||
                (referenceFrame != Av1ReferenceFrameType.Intra && this.GetInterTransformGateLevel(blockSize) != 0);

            long predictionError = 0;
            long estimatedDistortion = 0;
            int estimatedRate = 0;
            int planeCount = hasChroma ? 3 : 1;
            Av1EncoderFrame<TSample>.PlanarView secondaryReference = isCompound
                ? this.references.Span[(int)secondaryReferenceFrame].CodedView
                : primaryReference;

            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Av1BlockSize planeSize = blockSize.GetSubsampled(subX != 0, subY != 0);
                Span<TSample> prediction = planeIndex == 0 ? interWorkspace.LumaPrediction :
                    planeIndex == 1 ? interWorkspace.BluePrediction : interWorkspace.RedPrediction;

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
                    secondaryReference.GetPlane(plane),
                    blockOrigin,
                    subX,
                    subY,
                    blockSize,
                    prediction,
                    interWorkspace.Residual,
                    interWorkspace.FilterRows,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors);

                if (measurePrediction)
                {
                    Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                    Point planeOrigin = planeIndex == 0 ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);

                    // A frame that pads its border measures the samples inside the frame only. Reference:
                    // get_visible_dimensions() in get_sse().
                    Size extent = this.picture.Parent.SpeedSettings.InterModeEstimation == 2 && this.estimateInterCandidates
                        ? this.GetPredictionModelSize(blockOrigin, blockSize, subX, subY)
                        : this.blockWorkspace.BorderPad
                            ? this.blockWorkspace.GetVisibleSize(plane, planeOrigin, planeSize.GetWidth(), planeSize.GetHeight())
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
                            this.blockQIndex, this.quantization.DeltaQAc[planeIndex], this.bitDepth);

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

            // A new prediction goes into pd->dst, which is the frame here. A prepared prediction is already in pd->dst.
            // Reference: av1_enc_build_inter_predictor() in handle_inter_mode() and rd_pick_intrabc_mode_sb().
            if (!usePreparedPrediction && !this.useObmcPrediction && !this.useWarpedPrediction)
            {
                this.WriteInterLumaDestination(lumaFrame, blockOrigin, blockSize, interWorkspace.LumaPrediction);
            }

            // Sharpness 3 measures the candidate's luma prediction, which the plane loop leaves in the current
            // destination, unless the image tune's bias replaces the charge. Reference: get_variance_stats() reading
            // pd->dst in adjust_rdcost() of motion_mode_rd().
            bool chargesPrediction = referenceFrame != Av1ReferenceFrameType.Intra && !this.BiasesInterCosts && this.ChargesSmoothing;
            long predictionSmoothingOffset = chargesPrediction
                ? this.GetSmoothingOffset(blockOrigin, blockSize, interWorkspace.LumaPrediction, blockSize.GetWidth())
                : 0;

            // High bit depth sharpness 3 measures the same prediction. It sets the charge when the skip is known.
            // Reference: av1_get_variance_stats() in the high bit depth branch of adjust_rdcost() of motion_mode_rd().
            bool chargesTextureLoss = referenceFrame != Av1ReferenceFrameType.Intra && !this.BiasesInterCosts && this.ChargesHighBitDepthTextureLoss;
            long predictionSourceVariance = 0;
            long predictionSampleVariance = 0;
            if (chargesTextureLoss)
            {
                this.GetVarianceStatistics(
                    blockOrigin, blockSize, interWorkspace.LumaPrediction, blockSize.GetWidth(), out predictionSourceVariance, out predictionSampleVariance);
            }

            if (this.estimateInterCandidates)
            {
                if (this.picture.Parent.SpeedSettings.InterModeEstimation == 1)
                {
                    this.blockWorkspace.InterModeModels[(int)blockSize].Estimate(
                        predictionError, out estimatedRate, out estimatedDistortion);
                }

                // An estimate clears the skip flag that each motion mode trial starts with.
                // So a winner that has only an estimate is not skippable for the mode thresholds.
                // Reference: the rd_stats->skip_txfm = 0 of the !do_tx_search branch of motion_mode_rd(), and the best_mode_skippable of update_search_state().
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

                // The bias follows the estimate's records. Reference: adjust_rdcost() after the !do_tx_search branch of
                // motion_mode_rd().
                if (referenceFrame != Av1ReferenceFrameType.Intra && this.BiasesInterCosts)
                {
                    estimate.AddInterPredictionBias(this.rateMultiplier);
                }
                else if (chargesPrediction)
                {
                    estimate.AddPredictionSmoothingOffset(this.rateMultiplier, predictionSmoothingOffset);
                }
                else if (chargesTextureLoss)
                {
                    // An estimate never skips, and its cost is the estimated cost.
                    // Reference: the rd_stats->rdcost = est_rd and rd_stats->skip_txfm = 0 of the !do_tx_search branch of motion_mode_rd().
                    this.ChargeInterTextureLoss(
                        ref estimate,
                        estimate.Cost,
                        predictionSourceVariance,
                        predictionSampleVariance,
                        blockOrigin,
                        blockSize,
                        false,
                        isCompound,
                        isInterIntra,
                        vector);
                }

                return estimate;
            }

            if (referenceFrame != Av1ReferenceFrameType.Intra && !this.ShouldSearchInterTransforms(candidate))
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            Av1RateDistortionStatistics statistics = this.EvaluatePreparedInterCandidate(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in interWorkspace,
                transformPrediction,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                macroBlock,
                blockOrigin,
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

            // A completed search takes the bias after the residual model records it, and later trials of the entry are
            // bounded by its cost before the bias. Reference: the ref_best_rd update before adjust_cost() and
            // adjust_rdcost() in motion_mode_rd().
            this.unbiasedInterTrialCost = statistics.Cost;
            if (referenceFrame != Av1ReferenceFrameType.Intra && statistics.Cost != long.MaxValue && this.BiasesInterCosts)
            {
                statistics.AddInterPredictionBias(this.rateMultiplier);
            }
            else if (chargesPrediction && statistics.Cost != long.MaxValue)
            {
                statistics.AddPredictionSmoothingOffset(this.rateMultiplier, predictionSmoothingOffset);
            }
            else if (chargesTextureLoss && statistics.Cost != long.MaxValue)
            {
                // The residual search starts its statistics at zero and never sets their cost.
                // So the extra cost scales zero, unless the offset sets the cost first.
                // Reference: the av1_init_rd_stats() of av1_txfm_search(), which sets rd_stats->rdcost to zero.
                this.ChargeInterTextureLoss(
                    ref statistics, 0, predictionSourceVariance, predictionSampleVariance, blockOrigin, blockSize, skip, isCompound, isInterIntra, vector);
            }

            return statistics;
        }

        /// <summary>
        /// Gets the gate level of the current inter search. The mode loop gates a block larger than 16x16 at the
        /// motion-mode level; the winner motion-mode search and the retained-candidate search use the default level.
        /// Reference: get_txfm_rd_gate_level() in motion_mode_rd() and tx_search_best_inter_candidates().
        /// </summary>
        private int GetInterTransformGateLevel(Av1BlockSize blockSize)
        {
            bool motionModeCase = !this.evaluatingMotionModeWinners && !this.searchingRetainedCandidates &&
                blockSize.GetWidth() * blockSize.GetHeight() > 256;

            Av1TransformSearchCase searchCase = motionModeCase ? Av1TransformSearchCase.MotionMode : Av1TransformSearchCase.Default;
            return this.picture.Parent.SpeedSettings.GetInterTransformGateLevel(this.picture.Parent.FrameUpdateType, searchCase);
        }

        /// <summary>
        /// Compares prediction-only error with the retained inter result before searching transforms.
        /// </summary>
        private bool ShouldSearchInterTransforms(Av1InterModeCandidate candidate)
        {
            int level = this.GetInterTransformGateLevel(candidate.ModeInfo.BlockSize);
            if (level == 0 || this.bestInterPredictionCost == long.MaxValue)
            {
                return true;
            }

            int quantizer = this.blockQIndex;
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
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="bestCost">The cost above which the evaluation stops.</param>
        /// <param name="hasChroma">Whether the block codes chroma.</param>
        /// <param name="candidate">The block decisions of the candidate.</param>
        /// <param name="lumaReconstruction">The luma reconstruction storage of the candidate.</param>
        /// <param name="lumaCoefficients">The luma coefficient storage of the candidate.</param>
        /// <param name="blueReconstruction">The blue reconstruction storage of the candidate.</param>
        /// <param name="blueCoefficients">The blue coefficient storage of the candidate.</param>
        /// <param name="redReconstruction">The red reconstruction storage of the candidate.</param>
        /// <param name="redCoefficients">The red coefficient storage of the candidate.</param>
        /// <param name="skip">Whether the candidate codes no residual.</param>
        /// <param name="lumaStates">The luma transform states of the candidate.</param>
        /// <param name="lumaTransformSizes">The luma transform sizes of the candidate.</param>
        /// <param name="blueState">The blue transform states of the candidate.</param>
        /// <param name="redState">The red transform states of the candidate.</param>
        /// <returns>The rate and distortion of the candidate.</returns>
        private Av1RateDistortionStatistics EvaluatePreparedInterCandidate(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> transformPrediction,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
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
            int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            Av1ModeCosts modeCosts = tables.ModeCosts;
            int noSkipCost = Av1SymbolEncoder.GetSkipCost(modeCosts, false, skipContext);
            int skipCost = Av1SymbolEncoder.GetSkipCost(modeCosts, true, skipContext);

            // The header alone above the budget and a failed luma search both leave the luma result invalid, unlike
            // a later bound on the combined cost or a failed chroma search. Reference: the rd_stats_y->rate ==
            // INT_MAX returns of av1_txfm_search().
            this.lumaSearchFailed = true;
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

            Av1EncoderBlockModeInfo lumaModeInfo = new()
            {
                BlockSize = blockSize,
                TransformSize = lumaTransformSize,
                Mode = mode,
                UseIntraBlockCopy = referenceFrame == Av1ReferenceFrameType.Intra
            };

            Av1RateDistortionStatistics lumaStatistics = this.EvaluateInterLumaTree(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in interWorkspace,
                transformPrediction,
                in transformEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                ref lumaModeInfo,
                lumaStates,
                transformCostLimit,
                out int lumaStateCount);

            if (lumaStatistics.Cost == long.MaxValue)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            this.lumaSearchFailed = false;
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
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
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
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in interWorkspace,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    macroBlock,
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

            int codedRate = predictionRate + noSkipCost + lumaRate + blueRate + redRate;
            long codedDistortion = lumaDistortion + blueDistortion + redDistortion;
            Av1RateDistortionStatistics selectedStatistics = new(this.rateMultiplier, codedRate, codedDistortion);
            long skipDistortion = lumaPredictionDistortion + bluePredictionDistortion + redPredictionDistortion;

            // All-empty residuals omit the transform tree. Nonempty residuals can also be discarded when
            // prediction alone costs no more; shared prediction syntax must not affect the rounded comparison.
            bool allEmpty = !lumaStatistics.HasCoefficients && !blueHasCoefficients && !redHasCoefficients;
            bool skippable = allEmpty;

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
                // and the choose_skip_txfm comparison of av1_txfm_search(). A mode search that defers the size
                // search runs the uniform search of TX_MODE_LARGEST, whose luma is skippable when it is empty.
                // Reference: the tx_mode_search_type set by set_mode_eval_params() for MODE_EVAL.
                bool recursiveLumaSearch = this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select &&
                    (!this.picture.Parent.SpeedSettings.DeferTransformSizeSearch ||
                        this.blockWorkspace.EvaluationStage != Av1EncoderEvaluationStage.Candidate);

                bool lumaSkippable = lumaStatistics.SkipPredicted || (recursiveLumaSearch
                    ? Av1RateDistortion.GetCost(this.rateMultiplier, skipCost, lumaPredictionDistortion) <=
                        Av1RateDistortion.GetCost(this.rateMultiplier, lumaRate + noSkipCost, lumaDistortion)
                    : !lumaStatistics.HasCoefficients);

                skippable = lumaSkippable && !blueHasCoefficients && !redHasCoefficients;

                // High bit depth sharpness 3 adds a skip penalty to the comparison only.
                // Reference: the av1_get_tx_skip_dist() call before the choose_skip_txfm comparison of av1_txfm_search().
                long comparedCodedDistortion = codedDistortion;
                long comparedSkipDistortion = skipDistortion;
                if (!skippable && this.UsesHighBitDepthSharpness)
                {
                    this.PenalizeTransformSkip(blockOrigin, blockSize, interWorkspace.LumaPrediction, ref comparedCodedDistortion, ref comparedSkipDistortion);
                }

                skip = skippable ||
                    Av1RateDistortion.GetCost(this.rateMultiplier, skipCost, comparedSkipDistortion) <=
                    Av1RateDistortion.GetCost(this.rateMultiplier, codedRate - predictionRate, comparedCodedDistortion);
            }

            if (skip)
            {
                selectedStatistics = new(
                    this.rateMultiplier,
                    predictionRate + skipCost,
                    skipDistortion);

                // A skippable residual whose skipped cost exceeds the budget fails the search. Reference: the
                // rd_stats->skip_txfm test at the end of av1_txfm_search().
                if (skippable && selectedStatistics.Cost > bestCost)
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                int lumaSampleCount = blockSize.GetWidth() * blockSize.GetHeight();
                interWorkspace.LumaPrediction[..lumaSampleCount].CopyTo(lumaReconstruction);
                lumaCoefficients[..lumaSampleCount].Clear();
                lumaStates = default;
                lumaTransformSizes[..].Fill(lumaTransformSize);
                if (hasChroma)
                {
                    Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                        this.source.ChromaSubsamplingX != 0, this.source.ChromaSubsamplingY != 0);

                    int chromaSampleCount = chromaBlockSize.GetWidth() * chromaBlockSize.GetHeight();
                    interWorkspace.BluePrediction[..chromaSampleCount].CopyTo(blueReconstruction);
                    interWorkspace.RedPrediction[..chromaSampleCount].CopyTo(redReconstruction);
                    blueCoefficients[..chromaSampleCount].Clear();
                    redCoefficients[..chromaSampleCount].Clear();
                    blueState = default;
                    redState = default;
                }
            }

            // The luma cost prices the skip flag that the complete residual search reports, and a block coded as
            // skip drops its luma rate and keeps the prediction error. Reference: this_yrd in motion_mode_rd().
            selectedStatistics.LumaCost = skip ? skippedLumaCost : codedLumaCost;
            selectedStatistics.HasCoefficients = !skip;

            // The residual search reports the skip that it chose, from an empty residual or from the comparison.
            // best_mode_skippable and the partition search read this result.
            // Reference: the rd_stats->skip_txfm = 1 and rd_stats->skip_txfm = 0 of the choose_skip_txfm branches of av1_txfm_search().
            selectedStatistics.AllTransformsEmpty = skip;
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
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="mode">The compound mode.</param>
        /// <param name="vector">The first vector.</param>
        /// <param name="secondaryVector">The second vector.</param>
        /// <param name="referenceMotionVectorIndex">The dynamic reference list index.</param>
        /// <param name="referenceMotionVectors">The reference vector list of the pair.</param>
        /// <returns>The syntax rate in 1/512-bit units.</returns>
        private static int GetCompoundInterModeRate(
            in Av1CoefficientTables tables,
            in Av1MotionVectorCosts motionVectorCosts,
            Av1PredictionMode mode,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            int referenceMotionVectorIndex,
            Av1ReferenceMotionVectors referenceMotionVectors)
        {
            int rate = Av1SymbolEncoder.GetInterCompoundModeCost(tables.ModeCosts, mode, referenceMotionVectors.ModeContext);
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
                    rate += Av1SymbolEncoder.GetDynamicReferenceListCost(tables.ModeCosts, advance, context);
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
                    rate += Av1SymbolEncoder.GetDynamicReferenceListCost(tables.ModeCosts, advance, context);
                    if (!advance)
                    {
                        break;
                    }
                }
            }

            return rate + GetCompoundMotionRate(in motionVectorCosts, mode, vector, secondaryVector, referenceMotionVectorIndex, referenceMotionVectors);
        }

        /// <summary>
        /// Returns whether every new vector of a compound mode differs from its reference vector.
        /// Reference: av1_check_newmv_joint_nonzero(), with the reference vectors of av1_get_ref_mv().
        /// </summary>
        /// <param name="mode">The compound mode.</param>
        /// <param name="vector">The first vector.</param>
        /// <param name="secondaryVector">The second vector.</param>
        /// <param name="referenceMotionVectorIndex">The dynamic reference list index.</param>
        /// <param name="referenceMotionVectors">The reference vector list of the pair.</param>
        /// <returns><see langword="false"/> when a new vector equals its reference vector.</returns>
        private static bool HasCodedCompoundNewVectors(
            Av1PredictionMode mode,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            int referenceMotionVectorIndex,
            Av1ReferenceMotionVectors referenceMotionVectors)
        {
            int newReferenceIndex = mode is Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector
                ? referenceMotionVectorIndex + 1
                : referenceMotionVectorIndex;

            if (mode is Av1PredictionMode.NewNewMotionVector or Av1PredictionMode.NewNearestMotionVector or Av1PredictionMode.NewNearMotionVector &&
                vector == referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 0))
            {
                return false;
            }

            return !(mode is Av1PredictionMode.NewNewMotionVector or Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NearNewMotionVector &&
                secondaryVector == referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 1));
        }

        /// <summary>
        /// Keeps a motion search range within eight samples of the visible frame when the sharpness is 3, and returns
        /// it unchanged otherwise. Reference: the sharpness margins of av1_make_default_fullpel_ms_params() and
        /// av1_make_default_subpel_ms_params().
        /// </summary>
        /// <param name="bounds">The search range, with exclusive right and bottom edges.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The luma block size.</param>
        /// <param name="scale">One for full-pixel ranges, eight for eighth-sample ranges.</param>
        /// <returns>The search range.</returns>
        private Rectangle ApplySharpnessMargins(Rectangle bounds, Point blockOrigin, Size blockSize, int scale)
        {
            if (this.blockWorkspace.EncoderOptions.Sharpness != 3)
            {
                return bounds;
            }

            ObuFrameSize frameSize = this.picture.Parent.FrameHeader.FrameSize;
            return Av1MotionVector.ClampToSharpnessMargins(
                bounds, blockOrigin, blockSize, new Size(frameSize.FrameWidth, frameSize.FrameHeight), scale);
        }

        /// <summary>
        /// Clamps a single-reference new vector that a compound mode reuses into the fractional search range around
        /// the compound reference vector. Reference: clamp_mv_in_range() in handle_newmv().
        /// </summary>
        /// <param name="vector">The reused single-reference vector.</param>
        /// <param name="reference">The compound reference vector of the component.</param>
        /// <param name="frameBounds">The full-pixel region of the block. Reference: x->mv_limits.</param>
        /// <returns>The clamped vector.</returns>
        private static Av1MotionVector ClampToSubpixelRange(Av1MotionVector vector, Av1MotionVector reference, Rectangle frameBounds)
        {
            Rectangle limits = reference.GetSubpixelSearchBounds(frameBounds);
            return new(
                Math.Clamp(vector.Row, limits.Top, limits.Bottom - 1),
                Math.Clamp(vector.Column, limits.Left, limits.Right - 1));
        }

        /// <summary>
        /// Measures differential motion syntax for the searched components of a compound mode.
        /// </summary>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="mode">The compound mode.</param>
        /// <param name="vector">The first vector.</param>
        /// <param name="secondaryVector">The second vector.</param>
        /// <param name="referenceMotionVectorIndex">The dynamic reference list index.</param>
        /// <param name="referenceMotionVectors">The reference vector list of the pair.</param>
        /// <returns>The differential vector rate in 1/512-bit units.</returns>
        private static int GetCompoundMotionRate(
            in Av1MotionVectorCosts motionVectorCosts,
            Av1PredictionMode mode,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            int referenceMotionVectorIndex,
            Av1ReferenceMotionVectors referenceMotionVectors)
        {
            int rate = 0;
            bool usesNear = mode is Av1PredictionMode.NearNearMotionVector or
                Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;

            int newReferenceIndex = usesNear ? referenceMotionVectorIndex + 1 : referenceMotionVectorIndex;
            if (mode is Av1PredictionMode.NewNearestMotionVector or
                Av1PredictionMode.NewNearMotionVector or
                Av1PredictionMode.NewNewMotionVector)
            {
                Av1MotionVector reference = referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 0);
                rate += ((motionVectorCosts.GetCost(vector, reference) * 108) + 64) >> 7;
            }

            if (mode is Av1PredictionMode.NearestNewMotionVector or
                Av1PredictionMode.NearNewMotionVector or
                Av1PredictionMode.NewNewMotionVector)
            {
                Av1MotionVector reference = referenceMotionVectors.GetCompoundNewReference(newReferenceIndex, 1);
                rate += ((motionVectorCosts.GetCost(secondaryVector, reference) * 108) + 64) >> 7;
            }

            return rate;
        }

        /// <summary>
        /// Measures the complete mode, dynamic-reference-list, and differential-vector syntax for one candidate.
        /// </summary>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="mode">The candidate single-reference inter mode.</param>
        /// <param name="vector">The candidate motion vector.</param>
        /// <param name="referenceMotionVectorIndex">The selected dynamic-reference-list entry.</param>
        /// <param name="referenceMotionVectors">The current spatial candidate stack.</param>
        /// <returns>The syntax rate in 1/512-bit units.</returns>
        private static int GetInterModeRate(
            in Av1CoefficientTables tables,
            in Av1MotionVectorCosts motionVectorCosts,
            Av1PredictionMode mode,
            Av1MotionVector vector,
            int referenceMotionVectorIndex,
            Av1ReferenceMotionVectors referenceMotionVectors)
        {
            int rate = Av1SymbolEncoder.GetInterModeCost(tables.ModeCosts, mode, referenceMotionVectors.ModeContext);
            if (mode == Av1PredictionMode.NearMotionVector)
            {
                for (int index = 1; index < 3 && referenceMotionVectors.Count > index + 1; index++)
                {
                    bool advance = referenceMotionVectorIndex >= index;
                    int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                    rate += Av1SymbolEncoder.GetDynamicReferenceListCost(tables.ModeCosts, advance, context);
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
                rate += Av1SymbolEncoder.GetDynamicReferenceListCost(tables.ModeCosts, advance, context);
                if (!advance)
                {
                    break;
                }
            }

            Av1MotionVector reference = referenceMotionVectors.GetNewReference(referenceMotionVectorIndex);

            // Mode selection discounts motion syntax to 108/128 of its estimated rate. Apply the rounded
            // weight to the vector alone; mode and dynamic-reference-list symbols retain their full rate.
            return rate + (((motionVectorCosts.GetCost(vector, reference) * 108) + 64) >> 7);
        }

        /// <summary>
        /// Evaluates chroma transforms using full-plane prediction and sequential coefficient contexts.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="costLimit">The cost above which the evaluation stops.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="plane">The chroma plane.</param>
        /// <param name="referenceFrame">The reference frame, or intra for an intra block copy.</param>
        /// <param name="secondaryReferenceFrame">The second reference frame of a compound prediction, or none.</param>
        /// <param name="vector">The motion vector.</param>
        /// <param name="secondaryVector">The second motion vector of a compound prediction.</param>
        /// <param name="predictionMode">The prediction mode.</param>
        /// <param name="usePreparedPrediction">Whether the prediction planes are already prepared in the workspace.</param>
        /// <param name="compoundType">The compound blend type.</param>
        /// <param name="compoundWedgeIndex">The wedge mask index of a wedge blend.</param>
        /// <param name="compoundWedgeSign">The wedge sign of a wedge blend.</param>
        /// <param name="differenceWeightedMaskType">The mask type of a difference-weighted blend.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="lumaTransformSizes">The luma transform sizes of the candidate.</param>
        /// <param name="lumaStates">The luma transform states of the candidate.</param>
        /// <param name="selectedReconstruction">The reconstruction storage of the plane.</param>
        /// <param name="selectedCoefficients">The coefficient storage of the plane.</param>
        /// <param name="states">The transform states of the plane.</param>
        /// <param name="rate">The coefficient rate of the plane.</param>
        /// <param name="distortion">The distortion of the plane.</param>
        /// <param name="predictionDistortion">The distortion of the plane without a residual.</param>
        /// <param name="hasCoefficients">Whether the plane codes a coefficient.</param>
        /// <returns><see langword="false"/> when the cost of the plane passes the limit.</returns>
        private bool EvaluateInterChromaPlane(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
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
            Span<TSample> prediction = plane == Av1Plane.U ? interWorkspace.BluePrediction : interWorkspace.RedPrediction;
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
                interWorkspace.Residual,
                interWorkspace.FilterRows,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors);

            // Coefficient coding reads the neighboring contexts of this plane, and the search
            // changes them as it prices each transform. Taking a copy leaves the tile contexts
            // untouched, so a candidate that loses leaves nothing behind.
            Av1NeighborEdges<byte> neighbors = plane == Av1Plane.U ? blueCoefficientEdges : redCoefficientEdges;

            int contextWidth = planeBlockSize.Get4x4WideCount();
            int contextHeight = planeBlockSize.Get4x4HighCount();
            Span<byte> transformContexts = interWorkspace.TransformContexts;
            Span<byte> topContexts = transformContexts[..contextWidth];
            Span<byte> leftContexts = transformContexts.Slice(contextWidth, contextHeight);
            neighbors.Top.Slice(neighbors.GetTopIndex(planeOrigin), contextWidth).CopyTo(topContexts);
            neighbors.Left.Slice(neighbors.GetLeftIndex(planeOrigin), contextHeight).CopyTo(leftContexts);
            Av1TransformSetType transformSet = Av1SymbolContextHelper.GetExtendedTransformSetType(
                transformSize, isInter: true, this.picture.Parent.FrameHeader.UseReducedTransformSet);

            bool recursiveLumaSearch = this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select &&
                (!this.picture.Parent.SpeedSettings.DeferTransformSizeSearch ||
                    this.blockWorkspace.EvaluationStage != Av1EncoderEvaluationStage.Candidate);

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

                    // The recursive luma search stores DCT_DCT for a luma block that quantized to nothing, and
                    // the uniform search of mode evaluation keeps the type it searched. Reference: the zero_blk_rd
                    // branch of try_tx_block_no_split() against update_txk_array() in search_tx_type().
                    bool emptyLumaIsDct = recursiveLumaSearch && lumaState.EndOfBlock == 0;
                    Av1TransformType transformType = emptyLumaIsDct || !lumaState.TransformType.IsExtendedSetUsed(transformSet)
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
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        plane,
                        predictionMode,
                        planeOrigin + new Size(x, y),
                        transformSize,
                        transformType,
                        context,
                        Av1TileWriter.GetTransformBlockContexts(Av1ComponentType.Chroma, in neighbors, planeOrigin, planeBlockSize, transformSize),
                        prediction[inputOffset..],
                        interWorkspace.Residual[inputOffset..],
                        width,
                        costLimit - currentCost,
                        interWorkspace.TransformReconstruction,
                        interWorkspace.TransformCoefficients,
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
        /// Blends the prediction of one plane with the predictions of the overlappable above and left neighbors over
        /// the neighbor's own vector, reference and filters, then rebuilds the residual. A plane block smaller than
        /// 8x8 blends with its left neighbors only.
        /// Reference: av1_build_prediction_by_above_preds() and av1_build_prediction_by_left_preds() with
        /// build_obmc_prediction(), then av1_build_obmc_inter_prediction(), foreach_overlappable_nb_above() and
        /// foreach_overlappable_nb_left().
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="subsamplingX">The horizontal subsampling shift.</param>
        /// <param name="subsamplingY">The vertical subsampling shift.</param>
        /// <param name="prediction">The contiguous plane prediction, blended in place.</param>
        /// <param name="residual">The contiguous residual, rebuilt from the blended prediction.</param>
        /// <param name="filterRows">The interpolation scratch.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        private void ApplyObmcPrediction(
            Av1Plane plane,
            int subsamplingX,
            int subsamplingY,
            Span<TSample> prediction,
            Span<short> residual,
            Span<short> filterRows,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors)
        {
            ReadOnlySpan<int> maximumNeighbors = [0, 1, 2, 3, 4, 4];
            Av1BlockSize blockSize = this.obmcBlockSize;
            Av1BlockSize planeSize = blockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
            int planeWidth = planeSize.GetWidth();
            int planeHeight = planeSize.GetHeight();
            Point position = new(this.obmcBlockOrigin.X >> Av1Constants.ModeInfoSizeLog2, this.obmcBlockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            int widthUnits = blockSize.Get4x4WideCount();
            int heightUnits = blockSize.Get4x4HighCount();
            const int largestStep = 16;
            Av1BitDepth bitDepth = this.picture.Sequence.SequenceHeader.ColorConfig.BitDepth;
            Span<TSample> neighborPrediction = stackalloc TSample[64 * 32];
            Span<byte> maskRows = stackalloc byte[64 * 32];

            // Reference: av1_skip_u4x4_pred_in_obmc().
            bool smallPlane = planeSize is Av1BlockSize.Block4x4 or Av1BlockSize.Block8x4 or Av1BlockSize.Block4x8;

            // A neighbor and its vector share the allocation index at the grid cell.
            int stride = this.picture.ModeInfoStride;
            if (this.obmcAboveAvailable && !smallPlane)
            {
                int endColumn = Math.Min(position.X + widthUnits, this.picture.Parent.FrameHeader.ModeInfoColumnCount);
                int limit = maximumNeighbors[System.Numerics.BitOperations.Log2((uint)widthUnits)];
                int aboveRow = (position.Y - 1) * stride;
                int count = 0;
                int step;
                for (int column = position.X; column < endColumn && count < limit; column += step)
                {
                    int neighborIndex = modeInfoGrid[aboveRow + column];
                    step = Math.Min(modeInfoAllocation[neighborIndex].Block.BlockSize.Get4x4WideCount(), largestStep);
                    if (step == 1)
                    {
                        // A four-sample neighbor is one half of a pair whose second block carries the chroma.
                        column &= ~1;
                        neighborIndex = modeInfoGrid[aboveRow + column + 1];
                        step = 2;
                    }

                    ref readonly Av1EncoderBlockModeInfo neighbor = ref modeInfoAllocation[neighborIndex].Block;
                    if (neighbor.ReferenceFrame <= Av1ReferenceFrameType.Intra && !neighbor.UseIntraBlockCopy)
                    {
                        continue;
                    }

                    count++;
                    int width = (Math.Min(widthUnits, step) << Av1Constants.ModeInfoSizeLog2) >> subsamplingX;
                    int predictionHeight = Math.Clamp(blockSize.GetHeight() >> (subsamplingY + 1), 4, 64 >> (subsamplingY + 1));
                    int overlap = (Math.Min(blockSize.GetHeight(), 64) >> 1) >> subsamplingY;
                    int planeColumn = ((column - position.X) << Av1Constants.ModeInfoSizeLog2) >> subsamplingX;
                    Av1EncoderDisplacementVector vector = displacementVectors[neighborIndex];
                    this.PredictObmcNeighbor(
                        plane,
                        subsamplingX,
                        subsamplingY,
                        new Av1MotionVector(vector.Row, vector.Column),
                        neighbor,
                        new Point((column << Av1Constants.ModeInfoSizeLog2) >> subsamplingX, (position.Y << Av1Constants.ModeInfoSizeLog2) >> subsamplingY),
                        width,
                        predictionHeight,
                        neighborPrediction,
                        filterRows,
                        bitDepth);

                    ReadOnlySpan<byte> verticalMask = Av1ObmcMask.Get(overlap);
                    for (int row = 0; row < overlap; row++)
                    {
                        maskRows.Slice(row * width, width).Fill(verticalMask[row]);
                    }

                    TOperator.BlendMask(prediction[planeColumn..], planeWidth, neighborPrediction, width, maskRows, width, width, overlap);
                }
            }

            if (this.obmcLeftAvailable)
            {
                int endRow = Math.Min(position.Y + heightUnits, this.picture.Parent.FrameHeader.ModeInfoRowCount);
                int limit = maximumNeighbors[System.Numerics.BitOperations.Log2((uint)heightUnits)];
                int count = 0;
                int step;
                for (int row = position.Y; row < endRow && count < limit; row += step)
                {
                    int neighborIndex = modeInfoGrid[(row * stride) + position.X - 1];
                    step = Math.Min(modeInfoAllocation[neighborIndex].Block.BlockSize.Get4x4HighCount(), largestStep);
                    if (step == 1)
                    {
                        row &= ~1;
                        neighborIndex = modeInfoGrid[((row + 1) * stride) + position.X - 1];
                        step = 2;
                    }

                    ref readonly Av1EncoderBlockModeInfo neighbor = ref modeInfoAllocation[neighborIndex].Block;
                    if (neighbor.ReferenceFrame <= Av1ReferenceFrameType.Intra && !neighbor.UseIntraBlockCopy)
                    {
                        continue;
                    }

                    count++;
                    int height = (Math.Min(heightUnits, step) << Av1Constants.ModeInfoSizeLog2) >> subsamplingY;
                    int predictionWidth = Math.Clamp(blockSize.GetWidth() >> (subsamplingX + 1), 4, 64 >> (subsamplingX + 1));
                    int overlap = (Math.Min(blockSize.GetWidth(), 64) >> 1) >> subsamplingX;
                    int planeRow = ((row - position.Y) << Av1Constants.ModeInfoSizeLog2) >> subsamplingY;
                    Av1EncoderDisplacementVector vector = displacementVectors[neighborIndex];
                    this.PredictObmcNeighbor(
                        plane,
                        subsamplingX,
                        subsamplingY,
                        new Av1MotionVector(vector.Row, vector.Column),
                        neighbor,
                        new Point((position.X << Av1Constants.ModeInfoSizeLog2) >> subsamplingX, (row << Av1Constants.ModeInfoSizeLog2) >> subsamplingY),
                        predictionWidth,
                        height,
                        neighborPrediction,
                        filterRows,
                        bitDepth);

                    TOperator.BlendMask(
                        prediction[(planeRow * planeWidth)..], planeWidth, neighborPrediction, predictionWidth, Av1ObmcMask.Get(overlap), 0, overlap, height);
                }
            }

            Point planeOrigin = new(this.obmcBlockOrigin.X >> subsamplingX, this.obmcBlockOrigin.Y >> subsamplingY);
            int sampleCount = planeWidth * planeHeight;
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            TOperator.SubtractPrediction(
                Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, planeOrigin),
                sourcePlane.Stride,
                prediction[..sampleCount],
                residual[..sampleCount],
                planeWidth,
                planeHeight);
        }

        /// <summary>
        /// Predicts one neighbor's rectangle of the OBMC overlap with the neighbor's first reference, vector and
        /// filters. Reference: build_obmc_prediction() with setup_address_for_obmc().
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="subsamplingX">The horizontal subsampling shift.</param>
        /// <param name="subsamplingY">The vertical subsampling shift.</param>
        /// <param name="vector">The motion vector of the neighbor.</param>
        /// <param name="neighbor">The neighbor decisions.</param>
        /// <param name="planeOrigin">The rectangle origin in plane samples.</param>
        /// <param name="width">The rectangle width.</param>
        /// <param name="height">The rectangle height.</param>
        /// <param name="destination">The contiguous destination.</param>
        /// <param name="filterRows">The interpolation scratch.</param>
        /// <param name="bitDepth">The coded bit depth.</param>
        private void PredictObmcNeighbor(
            Av1Plane plane,
            int subsamplingX,
            int subsamplingY,
            Av1MotionVector vector,
            Av1EncoderBlockModeInfo neighbor,
            Point planeOrigin,
            int width,
            int height,
            Span<TSample> destination,
            Span<short> filterRows,
            Av1BitDepth bitDepth)
        {
            // A neighbor predicts from its reference with that reference's scale factors. Reference:
            // av1_setup_build_prediction_by_above_pred() and av1_setup_build_prediction_by_left_pred().
            if (!neighbor.UseIntraBlockCopy && this.IsResizedReference(neighbor.ReferenceFrame))
            {
                this.PredictScaledInter(
                    neighbor.ReferenceFrame,
                    plane,
                    planeOrigin,
                    subsamplingX,
                    subsamplingY,
                    vector,
                    neighbor.HorizontalInterpolationFilter,
                    neighbor.VerticalInterpolationFilter,
                    destination,
                    width,
                    width,
                    height,
                    filterRows);

                return;
            }

            Av1PlaneRegion<TSample> reference = neighbor.UseIntraBlockCopy
                ? this.reconstruction.GetPlane(plane)
                : this.references.Span[(int)neighbor.ReferenceFrame].CodedView.GetPlane(plane);

            int columnQ4 = (planeOrigin.X << 4) + (vector.Column << (1 - subsamplingX));
            int rowQ4 = (planeOrigin.Y << 4) + (vector.Row << (1 - subsamplingY));
            TOperator.PredictTranslationalInter(
                reference,
                new Point(columnQ4 >> 4, rowQ4 >> 4),
                neighbor.HorizontalInterpolationFilter,
                neighbor.VerticalInterpolationFilter,
                columnQ4 & 15,
                rowQ4 & 15,
                destination,
                width,
                width,
                height,
                filterRows,
                bitDepth);
        }

        /// <summary>
        /// Builds the chroma prediction of a block four samples wide or high, whose subsampled plane block covers the
        /// neighboring luma blocks as well, from the vector, reference and filters of each covered block. The path
        /// applies only when every covered block predicts from a reference frame.
        /// Reference: is_sub8x8_inter() and build_inter_predictors_sub8x8().
        /// </summary>
        /// <param name="vector">The vector of the current block.</param>
        /// <param name="referenceFrame">The reference of the current block.</param>
        /// <param name="horizontalFilter">The horizontal filter of the current block.</param>
        /// <param name="verticalFilter">The vertical filter of the current block.</param>
        /// <param name="referencePlane">The reference plane of the current block.</param>
        /// <param name="plane">The chroma plane.</param>
        /// <param name="lumaOrigin">The plane block origin in luma samples.</param>
        /// <param name="subsamplingX">The horizontal subsampling shift.</param>
        /// <param name="subsamplingY">The vertical subsampling shift.</param>
        /// <param name="blockSize">The luma block size.</param>
        /// <param name="prediction">The contiguous prediction destination.</param>
        /// <param name="residual">The contiguous residual destination.</param>
        /// <param name="filterRows">The interpolation scratch.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <returns><see langword="true"/> when the prediction was built.</returns>
        private bool TryPrepareSubEightInterPrediction(
            Av1MotionVector vector,
            Av1ReferenceFrameType referenceFrame,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Av1PlaneRegion<TSample> referencePlane,
            Av1Plane plane,
            Point lumaOrigin,
            int subsamplingX,
            int subsamplingY,
            Av1BlockSize blockSize,
            Span<TSample> prediction,
            Span<short> residual,
            Span<short> filterRows,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors)
        {
            bool subFourX = blockSize.GetWidth() == 4 && subsamplingX != 0;
            bool subFourY = blockSize.GetHeight() == 4 && subsamplingY != 0;
            if (!subFourX && !subFourY)
            {
                return false;
            }

            // The plane block starts at the first covered luma block; the current block is the last one.
            int rowStart = subFourY ? -1 : 0;
            int columnStart = subFourX ? -1 : 0;
            Point current = new(
                (lumaOrigin.X >> Av1Constants.ModeInfoSizeLog2) - columnStart,
                (lumaOrigin.Y >> Av1Constants.ModeInfoSizeLog2) - rowStart);

            // A covered block and its vector share the allocation index at the grid cell.
            int stride = this.picture.ModeInfoStride;
            for (int row = rowStart; row <= 0; row++)
            {
                for (int column = columnStart; column <= 0; column++)
                {
                    if (row == 0 && column == 0)
                    {
                        continue;
                    }

                    int coveredIndex = modeInfoGrid[((current.Y + row) * stride) + current.X + column];
                    ref readonly Av1EncoderBlockModeInfo covered = ref modeInfoAllocation[coveredIndex].Block;

                    if (covered.ReferenceFrame <= Av1ReferenceFrameType.Intra || covered.UseIntraBlockCopy)
                    {
                        return false;
                    }
                }
            }

            Av1BlockSize planeSize = blockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
            int planeWidth = planeSize.GetWidth();
            int planeHeight = planeSize.GetHeight();
            int subWidth = blockSize.GetWidth() >> subsamplingX;
            int subHeight = blockSize.GetHeight() >> subsamplingY;
            Point planeOrigin = new(lumaOrigin.X >> subsamplingX, lumaOrigin.Y >> subsamplingY);
            int modeRow = rowStart;
            ReadOnlySpan<Av1EncoderFrame<TSample>> references = this.references.Span;
            for (int y = 0; y < planeHeight; y += subHeight, modeRow++)
            {
                int modeColumn = columnStart;
                for (int x = 0; x < planeWidth; x += subWidth, modeColumn++)
                {
                    Av1MotionVector subVector = vector;
                    Av1ReferenceFrameType subReferenceFrame = referenceFrame;
                    Av1PlaneRegion<TSample> subReference = referencePlane;
                    Av1InterpolationFilter subHorizontal = horizontalFilter;
                    Av1InterpolationFilter subVertical = verticalFilter;
                    if (modeRow != 0 || modeColumn != 0)
                    {
                        int coveredIndex = modeInfoGrid[((current.Y + modeRow) * stride) + current.X + modeColumn];
                        ref readonly Av1EncoderBlockModeInfo covered = ref modeInfoAllocation[coveredIndex].Block;
                        subVector = new Av1MotionVector(displacementVectors[coveredIndex].Row, displacementVectors[coveredIndex].Column);
                        subReferenceFrame = covered.ReferenceFrame;
                        subReference = references[(int)covered.ReferenceFrame].CodedView.GetPlane(plane);
                        subHorizontal = covered.HorizontalInterpolationFilter;
                        subVertical = covered.VerticalInterpolationFilter;
                    }

                    // Each covered block predicts from its reference frame with that reference's scale factors,
                    // even within the estimated real-time search. Reference: the ref_scale_factors of
                    // build_inter_predictors_sub8x8().
                    if (this.IsResizedReference(subReferenceFrame))
                    {
                        this.PredictScaledInter(
                            subReferenceFrame,
                            plane,
                            new Point(planeOrigin.X + x, planeOrigin.Y + y),
                            subsamplingX,
                            subsamplingY,
                            subVector,
                            subHorizontal,
                            subVertical,
                            prediction[((y * planeWidth) + x)..],
                            planeWidth,
                            subWidth,
                            subHeight,
                            filterRows);

                        continue;
                    }

                    int columnQ4 = ((planeOrigin.X + x) << 4) + (subVector.Column << (1 - subsamplingX));
                    int rowQ4 = ((planeOrigin.Y + y) << 4) + (subVector.Row << (1 - subsamplingY));
                    TOperator.PredictTranslationalInter(
                        subReference,
                        new Point(columnQ4 >> 4, rowQ4 >> 4),
                        subHorizontal,
                        subVertical,
                        columnQ4 & 15,
                        rowQ4 & 15,
                        prediction[((y * planeWidth) + x)..],
                        planeWidth,
                        subWidth,
                        subHeight,
                        filterRows,
                        this.picture.Sequence.SequenceHeader.ColorConfig.BitDepth);
                }
            }

            int sampleCount = planeWidth * planeHeight;
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            TOperator.SubtractPrediction(
                Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, planeOrigin),
                sourcePlane.Stride,
                prediction[..sampleCount],
                residual[..sampleCount],
                planeWidth,
                planeHeight);

            return true;
        }

        /// <summary>
        /// Returns the global motion model that a GLOBALMV block warps with: the rotation-zoom or affine model of its
        /// reference, for a block at least 8 samples in both directions, in a frame that allows fractional vectors.
        /// Reference: is_global_mv_block() with the global warp branch of av1_allow_warp().
        /// </summary>
        /// <param name="mode">The prediction mode.</param>
        /// <param name="reference">The reference of the prediction.</param>
        /// <param name="blockSize">The luma block size.</param>
        /// <param name="model">Receives the model.</param>
        /// <returns><see langword="true"/> when the block warps with the global model.</returns>
        private readonly bool TryGetGlobalWarpModel(
            Av1PredictionMode mode,
            Av1ReferenceFrameType reference,
            Av1BlockSize blockSize,
            out Av1GlobalMotionParameters model)
        {
            model = default;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            if (mode is not (Av1PredictionMode.GlobalMotionVector or Av1PredictionMode.GlobalGlobalMotionVector) ||
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) < 8 ||
                frameHeader.ForceIntegerMotionVector ||
                reference <= Av1ReferenceFrameType.Intra)
            {
                return false;
            }

            model = frameHeader.GetGlobalMotionParameters()[(int)reference - 1];
            return model.Type > Av1GlobalMotionType.Translation && !model.IsInvalid;
        }

        /// <summary>
        /// Warps one reference of a GLOBAL_GLOBALMV block into its compound intermediate when that reference has a
        /// rotation-zoom or affine global model and the plane block is at least 8x8. Each reference decides for
        /// itself, so one reference can warp while the other translates. Reference: av1_init_warp_params() for
        /// each reference in build_inter_predictors().
        /// </summary>
        /// <param name="mode">The prediction mode.</param>
        /// <param name="reference">The reference of this predictor.</param>
        /// <param name="blockSize">The luma block size.</param>
        /// <param name="referencePlane">The padded retained reference plane.</param>
        /// <param name="planeOrigin">The block origin in plane samples.</param>
        /// <param name="predictionSize">The plane block size.</param>
        /// <param name="subsamplingX">The horizontal subsampling of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling of the plane.</param>
        /// <param name="intermediate">The compound intermediate of this reference.</param>
        /// <param name="filterRows">The intermediate rows of the warp filter.</param>
        /// <returns><see langword="true"/> when the intermediate holds the warped predictor.</returns>
        private readonly bool TryPrepareGlobalCompoundIntermediate(
            Av1PredictionMode mode,
            Av1ReferenceFrameType reference,
            Av1BlockSize blockSize,
            Av1PlaneRegion<TSample> referencePlane,
            Point planeOrigin,
            Av1BlockSize predictionSize,
            int subsamplingX,
            int subsamplingY,
            Span<ushort> intermediate,
            Span<short> filterRows)
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            if (width < 8 || height < 8 || !this.TryGetGlobalWarpModel(mode, reference, blockSize, out Av1GlobalMotionParameters model))
            {
                return false;
            }

            Av1EncoderFrame<TSample> frame = this.references.Span[(int)reference];
            TOperator.PrepareWarpedCompoundIntermediate(
                referencePlane,
                Av1Math.DivideLog2Ceiling(frame.Width, subsamplingX),
                Av1Math.DivideLog2Ceiling(frame.Height, subsamplingY),
                planeOrigin,
                width,
                height,
                subsamplingX,
                subsamplingY,
                model,
                intermediate,
                filterRows,
                this.bitDepth);

            return true;
        }

        /// <summary>
        /// Builds the single-reference prediction of one plane and its residual. A GLOBALMV block of at least 8x8
        /// plane samples warps with the rotation-zoom or affine model of its reference; every other block
        /// translates. Reference: av1_init_warp_params() for the inter predictor that
        /// av1_build_interintra_predictor() blends.
        /// </summary>
        /// <param name="mode">The prediction mode.</param>
        /// <param name="referenceFrame">The reference of the prediction.</param>
        /// <param name="blockSize">The luma block size.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="planeOrigin">The block origin in plane samples.</param>
        /// <param name="subsamplingX">The horizontal subsampling of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling of the plane.</param>
        /// <param name="vector">The motion vector.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="predictionSize">The plane block size.</param>
        /// <param name="prediction">The contiguous prediction destination.</param>
        /// <param name="residual">The contiguous residual destination.</param>
        /// <param name="filterRows">The intermediate rows of the interpolation and warp filters.</param>
        private void PrepareSingleInterPrediction(
            Av1PredictionMode mode,
            Av1ReferenceFrameType referenceFrame,
            Av1BlockSize blockSize,
            Av1Plane plane,
            Point planeOrigin,
            int subsamplingX,
            int subsamplingY,
            Av1MotionVector vector,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Av1BlockSize predictionSize,
            Span<TSample> prediction,
            Span<short> residual,
            Span<short> filterRows)
        {
            Av1EncoderFrame<TSample> reference = this.references.Span[(int)referenceFrame];
            Av1PlaneRegion<TSample> referencePlane = reference.CodedView.GetPlane(plane);
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            if (width >= 8 && height >= 8 && this.TryGetGlobalWarpModel(mode, referenceFrame, blockSize, out Av1GlobalMotionParameters model))
            {
                int sampleCount = width * height;
                TOperator.PrepareWarpedInterPrediction(
                    referencePlane,
                    Av1Math.DivideLog2Ceiling(reference.Width, subsamplingX),
                    Av1Math.DivideLog2Ceiling(reference.Height, subsamplingY),
                    planeOrigin,
                    width,
                    height,
                    subsamplingX,
                    subsamplingY,
                    model,
                    prediction,
                    filterRows,
                    this.bitDepth);

                TOperator.SubtractPrediction(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, planeOrigin),
                    sourcePlane.Stride,
                    prediction[..sampleCount],
                    residual[..sampleCount],
                    width,
                    height);

                return;
            }

            if (this.IsScaledReference(referenceFrame))
            {
                this.PrepareScaledInterPrediction(
                    referenceFrame,
                    plane,
                    planeOrigin,
                    subsamplingX,
                    subsamplingY,
                    vector,
                    horizontalFilter,
                    verticalFilter,
                    predictionSize,
                    prediction,
                    residual,
                    filterRows);

                return;
            }

            int columnQ4 = (planeOrigin.X << 4) + (vector.Column << (1 - subsamplingX));
            int rowQ4 = (planeOrigin.Y << 4) + (vector.Row << (1 - subsamplingY));
            TOperator.PrepareTranslationalInterPrediction(
                sourcePlane,
                planeOrigin,
                referencePlane,
                new Point(columnQ4 >> 4, rowQ4 >> 4),
                horizontalFilter,
                verticalFilter,
                columnQ4 & 15,
                rowQ4 & 15,
                prediction,
                residual,
                filterRows,
                predictionSize,
                this.bitDepth);
        }

        /// <summary>
        /// Writes a luma inter prediction into the frame at the block, as av1_enc_build_inter_predictor() and av1_combine_interintra()
        /// write pd->dst when pd->dst is the frame. A later trial can read these samples from the frame, so the frame must hold them.
        /// The port does not write chroma inter predictions into the frame: no libaom code reads chroma samples that an earlier trial left
        /// in pd->dst. The port also does not write OBMC and warped predictions: libaom skips those modes at sharpness 3, and only
        /// sharpness 3 reads the luma samples that an earlier inter trial left.
        /// </summary>
        /// <param name="lumaFrame">The samples of the reconstructed luma plane, which the caller reads once outside its loops.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="prediction">The contiguous luma prediction of the block.</param>
        private readonly void WriteInterLumaDestination(Span<TSample> lumaFrame, Point blockOrigin, Av1BlockSize blockSize, ReadOnlySpan<TSample> prediction)
        {
            int width = blockSize.GetWidth();
            Av1TransformBlockEncoder.WriteFrameSamples(
                this.reconstruction.GetPlane(Av1Plane.Y), lumaFrame, blockOrigin, prediction, width, width, blockSize.GetHeight());
        }

        /// <summary>
        /// Builds the complete plane prediction and residual in contiguous block rows.
        /// </summary>
        /// <param name="vector">The motion vector.</param>
        /// <param name="secondaryVector">The second motion vector of a compound prediction.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="predictionMode">The prediction mode.</param>
        /// <param name="primaryReferenceFrame">The first reference frame.</param>
        /// <param name="secondaryReferenceFrame">The second reference frame of a compound prediction, or none.</param>
        /// <param name="usePreparedPrediction">Whether the prediction planes are already prepared in the workspace.</param>
        /// <param name="compoundType">The compound blend type.</param>
        /// <param name="compoundWedgeIndex">The wedge mask index of a wedge blend.</param>
        /// <param name="compoundWedgeSign">The wedge sign of a wedge blend.</param>
        /// <param name="differenceWeightedMaskType">The mask type of a difference-weighted blend.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="referencePlane">The plane of the first reference.</param>
        /// <param name="secondaryReferencePlane">The plane of the second reference.</param>
        /// <param name="lumaOrigin">The luma block origin.</param>
        /// <param name="subsamplingX">The horizontal subsampling of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling of the plane.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="prediction">The prediction destination.</param>
        /// <param name="residual">The residual destination.</param>
        /// <param name="filterRows">The intermediate rows of the prediction filters.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
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
            Av1PlaneRegion<TSample> referencePlane,
            Av1PlaneRegion<TSample> secondaryReferencePlane,
            Point lumaOrigin,
            int subsamplingX,
            int subsamplingY,
            Av1BlockSize blockSize,
            Span<TSample> prediction,
            Span<short> residual,
            Span<short> filterRows,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors)
        {
            // A chroma block of a block four samples wide or high starts at the first luma block it covers.
            // Reference: the odd mi_row and mi_col adjustment of setup_pred_plane().
            if (plane != Av1Plane.Y)
            {
                Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(lumaOrigin, subsamplingX, subsamplingY);
                lumaOrigin = new Point(chromaOrigin.X << subsamplingX, chromaOrigin.Y << subsamplingY);
            }

            Point planeOrigin = new(lumaOrigin.X >> subsamplingX, lumaOrigin.Y >> subsamplingY);
            int sourceColumnQ4 = (planeOrigin.X << 4) + (vector.Column << (1 - subsamplingX));
            int sourceRowQ4 = (planeOrigin.Y << 4) + (vector.Row << (1 - subsamplingY));
            Point predictionOrigin = new(sourceColumnQ4 >> 4, sourceRowQ4 >> 4);
            Av1BlockSize predictionSize = blockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
            int sampleCount = predictionSize.GetWidth() * predictionSize.GetHeight();
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            if (usePreparedPrediction)
            {
                TOperator.SubtractPrediction(
                    Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, planeOrigin),
                    sourcePlane.Stride,
                    prediction[..sampleCount],
                    residual[..sampleCount],
                    predictionSize.GetWidth(),
                    predictionSize.GetHeight());
            }
            else if (predictionMode >= Av1PredictionMode.CompoundInterModeStart)
            {
                // Compound prediction does not scale. Only layered images code frames of other sizes, and their layers
                // predict from LAST alone.
                Debug.Assert(
                    !this.IsResizedReference(primaryReferenceFrame) && !this.IsResizedReference(secondaryReferenceFrame),
                    "Compound prediction from a reference of another size is not supported.");

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
                bool primaryWarped = this.TryPrepareGlobalCompoundIntermediate(
                    predictionMode,
                    primaryReferenceFrame,
                    blockSize,
                    referencePlane,
                    planeOrigin,
                    predictionSize,
                    subsamplingX,
                    subsamplingY,
                    firstIntermediate,
                    filterRows);

                bool secondaryWarped = this.TryPrepareGlobalCompoundIntermediate(
                    predictionMode,
                    secondaryReferenceFrame,
                    blockSize,
                    secondaryReferencePlane,
                    planeOrigin,
                    predictionSize,
                    subsamplingX,
                    subsamplingY,
                    secondIntermediate,
                    filterRows);

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
                    compoundMask,
                    filterRows,
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
                    differenceWeightedMaskType,
                    primaryWarped,
                    secondaryWarped);
            }
            else if (predictionMode >= Av1PredictionMode.InterModeStart)
            {
                if (plane != Av1Plane.Y && this.TryPrepareSubEightInterPrediction(
                    vector,
                    primaryReferenceFrame,
                    horizontalFilter,
                    verticalFilter,
                    referencePlane,
                    plane,
                    lumaOrigin,
                    subsamplingX,
                    subsamplingY,
                    blockSize,
                    prediction,
                    residual,
                    filterRows,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors))
                {
                    return;
                }

                // A warped block predicts each plane of at least 8x8 samples with its local model, and a GLOBALMV
                // block with the rotation-zoom or affine model of its reference. A smaller plane keeps the
                // translational prediction, and so does a frame that forces integer vectors. Reference:
                // av1_init_warp_params() and av1_allow_warp(), with is_global_mv_block().
                bool warped = this.useWarpedPrediction;
                Av1GlobalMotionParameters warpModel = this.warpedModel;
                if (!warped && this.TryGetGlobalWarpModel(predictionMode, primaryReferenceFrame, blockSize, out Av1GlobalMotionParameters global))
                {
                    warped = true;
                    warpModel = global;
                }

                if (warped && predictionSize.GetWidth() >= 8 && predictionSize.GetHeight() >= 8)
                {
                    Av1EncoderFrame<TSample> reference = this.references.Span[(int)primaryReferenceFrame];
                    TOperator.PrepareWarpedInterPrediction(
                        referencePlane,
                        Av1Math.DivideLog2Ceiling(reference.Width, subsamplingX),
                        Av1Math.DivideLog2Ceiling(reference.Height, subsamplingY),
                        planeOrigin,
                        predictionSize.GetWidth(),
                        predictionSize.GetHeight(),
                        subsamplingX,
                        subsamplingY,
                        warpModel,
                        prediction,
                        filterRows,
                        this.bitDepth);

                    TOperator.SubtractPrediction(
                        Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, planeOrigin),
                        sourcePlane.Stride,
                        prediction[..sampleCount],
                        residual[..sampleCount],
                        predictionSize.GetWidth(),
                        predictionSize.GetHeight());

                    return;
                }

                // Reference-frame modes use the complete interpolation pipeline even when the current zero-phase
                // global vector reduces to a SIMD copy. Later fractional vectors therefore share decoder arithmetic.
                if (this.IsScaledReference(primaryReferenceFrame))
                {
                    this.PrepareScaledInterPrediction(
                        primaryReferenceFrame,
                        plane,
                        planeOrigin,
                        subsamplingX,
                        subsamplingY,
                        vector,
                        horizontalFilter,
                        verticalFilter,
                        predictionSize,
                        prediction,
                        residual,
                        filterRows);
                }
                else
                {
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
                        filterRows,
                        predictionSize,
                        this.picture.Sequence.SequenceHeader.ColorConfig.BitDepth);
                }

                if (this.useObmcPrediction)
                {
                    this.ApplyObmcPrediction(
                        plane,
                        subsamplingX,
                        subsamplingY,
                        prediction,
                        residual,
                        filterRows,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors);
                }
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
        /// <param name="writer">The tile symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the caller read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="plane">The plane of the transform block.</param>
        /// <param name="predictionMode">The prediction mode that selects the transform-type context.</param>
        /// <param name="planeOrigin">The transform block origin in plane samples.</param>
        /// <param name="transformSize">The transform size.</param>
        /// <param name="derivedTransformType">The type a chroma block derives.</param>
        /// <param name="blockContext">The coefficient contexts of the transform block.</param>
        /// <param name="originContext">The coefficient contexts at the origin of the coding block.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="residual">The residual samples.</param>
        /// <param name="inputStride">The number of prediction and residual samples between rows.</param>
        /// <param name="costLimit">The budget left for the transform block.</param>
        /// <param name="transformReconstruction">The reconstruction storage of the candidate.</param>
        /// <param name="trialCoefficients">The coefficient storage of the candidate.</param>
        /// <param name="selectedReconstruction">The reconstruction storage of the winner.</param>
        /// <param name="selectedCoefficients">The coefficient storage of the winner.</param>
        /// <param name="selectedState">The transform state of the winner.</param>
        /// <param name="selectedRate">The coefficient rate of the winner.</param>
        /// <param name="selectedDistortion">The distortion of the winner.</param>
        /// <param name="predictionDistortion">The distortion of the prediction alone.</param>
        private void EvaluateInterTransform(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Av1Plane plane,
            Av1PredictionMode predictionMode,
            Point planeOrigin,
            Av1TransformSize transformSize,
            Av1TransformType derivedTransformType,
            Av1TransformBlockContext blockContext,
            Av1TransformBlockContext originContext,
            ReadOnlySpan<TSample> prediction,
            Span<short> residual,
            int inputStride,
            long costLimit,
            Span<TSample> transformReconstruction,
            Span<int> trialCoefficients,
            Span<TSample> selectedReconstruction,
            Span<int> selectedCoefficients,
            out Av1EncoderTransformBlockState selectedState,
            out int selectedRate,
            out long selectedDistortion,
            out long predictionDistortion)
        {
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            int sampleCount = transformSize.GetSize2d();

            // The candidate uses the transform scratch and the winner the caller's selected storage. The search swaps
            // them on each improvement, so the winner is copied at most once, after the search.
            Span<TSample> candidateReconstruction = transformReconstruction[..sampleCount];
            Span<int> candidateCoefficients = trialCoefficients[..sampleCount];
            Span<TSample> bestReconstruction = selectedReconstruction[..sampleCount];
            Span<int> bestCoefficients = selectedCoefficients[..sampleCount];
            Span<int> candidateDequantized = dequantizedCoefficients;
            Span<int> bestDequantized = searchDequantizedCoefficients;
            TransformTypeSearchResult result = this.SearchTransformType(
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                plane,
                true,
                blockContext,
                originContext,
                Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, planeOrigin),
                sourcePlane.Stride,
                planeOrigin,
                transformSize,
                predictionMode,
                Av1FilterIntraMode.AllFilterIntraModes,
                derivedTransformType,
                false,
                costLimit,
                false,
                prediction,
                residual,
                inputStride,
                ref candidateReconstruction,
                ref bestReconstruction,
                ref candidateCoefficients,
                ref bestCoefficients,
                ref candidateDequantized,
                ref bestDequantized);

            selectedState = result.State;
            selectedRate = result.Rate;
            selectedDistortion = result.Distortion;

            // The winning type reports the residual energy that the block would leave unskipped. Reference: the sse
            // of search_tx_type(), which best_rd_stats keeps.
            predictionDistortion = result.Sse;

            // Callers retain the selected coefficients after this scratch workspace is reused by the next plane or motion vector. An inter
            // block is never reconstructed in the search, so there are no winner samples to keep. Reference: the is_inter test of recon_intra().
            if (bestCoefficients != selectedCoefficients[..sampleCount])
            {
                bestCoefficients.CopyTo(selectedCoefficients);
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    /// <summary>
    /// One nearest, three near, one global, and four searched new-motion references.
    /// The fourth NEWMV result is retained only for mixed compound modes whose near-index syntax addresses stack entry three.
    /// </summary>
    private const int MaximumInterModeCandidateCount = 9;

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
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
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize LumaTransformSize = Av1TransformSize.Size8x8;
            Buffer2DRegion<TSample> lumaSource = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<TSample> lumaReconstruction = this.reconstruction.GetPlane(Av1Plane.Y);
            Point modeInfoPosition = new(
                blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

            Span<Av1MotionVector> referenceCandidates = stackalloc Av1MotionVector[8];
            Span<int> referenceWeights = stackalloc int[8];
            Av1MotionVector reference = Av1IntraBlockCopy.FindReference(
                this.picture,
                macroBlock,
                modeInfoPosition,
                BlockSize,
                Av1PartitionType.None,
                referenceCandidates,
                referenceWeights);

            Span<Av1MotionVector> candidates = stackalloc Av1MotionVector[2];
            Av1IntraBlockCopySearchIndex search = this.picture.IntraBlockCopySearch;
            Av1MotionVectorCosts displacementCosts = this.blockWorkspace.GetDisplacementVectorCosts();
            int candidateCount = search.FindCandidates<TSample, TOperator>(
                lumaSource,
                lumaReconstruction,
                blockOrigin,
                macroBlock.Tile,
                this.picture.Sequence.SequenceHeader,
                displacementCosts,
                reference,
                this.quantization.QIndex[0],
                this.rateMultiplier,
                this.picture.Parent.MotionSearchSettings,
                candidates);

            if (candidateCount == 0)
            {
                return regularStatistics;
            }

            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            Av1RateDistortionStatistics bestStatistics = regularStatistics;
            bool hasSelectedCandidate = false;
            bool selectedSkip = false;
            Av1MotionVector selectedVector = default;
            Av1EncoderTransformBlockState selectedLumaState = default;
            Av1EncoderTransformBlockState selectedBlueState = default;
            Av1EncoderTransformBlockState selectedRedState = default;
            Av1EncoderInterPredictionWorkspace<TSample> workspace =
                this.blockWorkspace.GetInterPredictionWorkspace<TSample>();

            Av1TransformBlockContext lumaContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Luminance,
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                blockOrigin,
                BlockSize,
                LumaTransformSize);

            // A coded IBC residual uses the unsplit transform root at this fixed block size. A skipped block
            // omits both the transform-partition bit and coefficient syntax, so this rate is added only below.
            int transformPartitionRate = 0;
            if (this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select)
            {
                Av1NeighborArrayUnit<byte> transformContexts = this.picture.TransformFunctionContexts[tileIndex];
                int topIndex = transformContexts.GetTopIndex(blockOrigin);
                int leftIndex = transformContexts.GetLeftIndex(blockOrigin);
                int transformPartitionContext = Av1SymbolContextHelper.GetTransformPartitionContext(
                    transformContexts.Top[topIndex],
                    transformContexts.Left[leftIndex],
                    BlockSize,
                    LumaTransformSize);

                transformPartitionRate = writer.GetTransformPartitionCost(
                    false,
                    transformPartitionContext);
            }

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            Point chromaOrigin = new(blockOrigin.X >> subsamplingX, blockOrigin.Y >> subsamplingY);
            Av1TransformSize chromaTransformSize = BlockSize.GetMaxUvTransformSize(
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            Av1TransformBlockContext blueContext = default;
            Av1TransformBlockContext redContext = default;
            if (!this.source.IsMonochrome)
            {
                Av1BlockSize chromaBlockSize = BlockSize.GetSubsampled(
                    colorConfig.SubSamplingX,
                    colorConfig.SubSamplingY);

                blueContext = Av1TileWriter.GetTransformBlockContexts(
                    Av1ComponentType.Chroma,
                    this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                    chromaOrigin,
                    chromaBlockSize,
                    chromaTransformSize);

                redContext = Av1TileWriter.GetTransformBlockContexts(
                    Av1ComponentType.Chroma,
                    this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
                    chromaOrigin,
                    chromaBlockSize,
                    chromaTransformSize);
            }

            // Per-vector plane results reuse candidate scratch. Separate selected spans retain only a new
            // global winner, allowing the complete search to finish before committed reconstruction changes.
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1MotionVector candidate = candidates[candidateIndex];
                this.EvaluateInterPlane(
                    writer,
                    candidate,
                    default,
                    Av1Plane.Y,
                    Av1ComponentType.Luminance,
                    Av1PredictionMode.DC,
                    false,
                    Av1CompoundType.Average,
                    0,
                    false,
                    Av1DifferenceWeightedMaskType.Type38,
                    Av1InterpolationFilter.Bilinear,
                    Av1InterpolationFilter.Bilinear,
                    lumaReconstruction,
                    lumaReconstruction,
                    blockOrigin,
                    0,
                    0,
                    BlockSize,
                    LumaTransformSize,
                    Av1TransformType.AllTransformTypes,
                    lumaContext,
                    workspace.LumaPrediction,
                    workspace.Residual,
                    workspace.TransformReconstruction,
                    workspace.TransformCoefficients,
                    workspace.LumaCandidateReconstruction,
                    workspace.LumaCandidateCoefficients,
                    out Av1EncoderTransformBlockState lumaCandidateState,
                    out int lumaRate,
                    out long lumaDistortion,
                    out long lumaPredictionDistortion);

                int blueRate = 0;
                int redRate = 0;
                long blueDistortion = 0;
                long redDistortion = 0;
                long bluePredictionDistortion = 0;
                long redPredictionDistortion = 0;
                Av1EncoderTransformBlockState blueCandidateState = default;
                Av1EncoderTransformBlockState redCandidateState = default;
                if (!this.source.IsMonochrome)
                {
                    Av1TransformType chromaTransformType = lumaCandidateState.TransformType;
                    Av1TransformSetType chromaTransformSet = Av1SymbolContextHelper.GetExtendedTransformSetType(
                        chromaTransformSize,
                        isInter: true,
                        this.picture.Parent.FrameHeader.UseReducedTransformSet);

                    // Inter prediction does not signal an independent chroma transform type. Chroma reuses the
                    // selected luma type when that type belongs to its transform set and otherwise falls back to DCT.
                    if (!chromaTransformType.IsExtendedSetUsed(chromaTransformSet))
                    {
                        chromaTransformType = Av1TransformType.DctDct;
                    }

                    this.EvaluateInterPlane(
                        writer,
                        candidate,
                        default,
                        Av1Plane.U,
                        Av1ComponentType.Chroma,
                        Av1PredictionMode.DC,
                        false,
                        Av1CompoundType.Average,
                        0,
                        false,
                        Av1DifferenceWeightedMaskType.Type38,
                        Av1InterpolationFilter.Bilinear,
                        Av1InterpolationFilter.Bilinear,
                        this.reconstruction.GetPlane(Av1Plane.U),
                        this.reconstruction.GetPlane(Av1Plane.U),
                        blockOrigin,
                        subsamplingX,
                        subsamplingY,
                        BlockSize,
                        chromaTransformSize,
                        chromaTransformType,
                        blueContext,
                        workspace.BluePrediction,
                        workspace.Residual,
                        workspace.TransformReconstruction,
                        workspace.TransformCoefficients,
                        workspace.BlueCandidateReconstruction,
                        workspace.BlueCandidateCoefficients,
                        out blueCandidateState,
                        out blueRate,
                        out blueDistortion,
                        out bluePredictionDistortion);

                    this.EvaluateInterPlane(
                        writer,
                        candidate,
                        default,
                        Av1Plane.V,
                        Av1ComponentType.Chroma,
                        Av1PredictionMode.DC,
                        false,
                        Av1CompoundType.Average,
                        0,
                        false,
                        Av1DifferenceWeightedMaskType.Type38,
                        Av1InterpolationFilter.Bilinear,
                        Av1InterpolationFilter.Bilinear,
                        this.reconstruction.GetPlane(Av1Plane.V),
                        this.reconstruction.GetPlane(Av1Plane.V),
                        blockOrigin,
                        subsamplingX,
                        subsamplingY,
                        BlockSize,
                        chromaTransformSize,
                        chromaTransformType,
                        redContext,
                        workspace.RedPrediction,
                        workspace.Residual,
                        workspace.TransformReconstruction,
                        workspace.TransformCoefficients,
                        workspace.RedCandidateReconstruction,
                        workspace.RedCandidateCoefficients,
                        out redCandidateState,
                        out redRate,
                        out redDistortion,
                        out redPredictionDistortion);
                }

                int predictionRate = writer.GetUseIntraBlockCopyCost(true) +
                    displacementCosts.GetDisplacementVectorCost(candidate, reference);

                int residualRate = writer.GetSkipCost(false, skipContext) +
                    transformPartitionRate +
                    lumaRate +
                    blueRate +
                    redRate;

                long candidateDistortion = lumaDistortion + blueDistortion + redDistortion;
                int skipRate = writer.GetSkipCost(true, skipContext);
                long skipDistortion = lumaPredictionDistortion + bluePredictionDistortion + redPredictionDistortion;

                // Empty residuals omit the transform tree. Nonempty residuals may also be discarded when
                // prediction alone costs no more; exclude shared prediction syntax before rounding either rate.
                bool candidateSkip = (lumaCandidateState.EndOfBlock == 0 &&
                    blueCandidateState.EndOfBlock == 0 && redCandidateState.EndOfBlock == 0) ||
                    Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, skipDistortion) <=
                    Av1RateDistortion.GetCost(this.rateMultiplier, residualRate, candidateDistortion);

                Av1RateDistortionStatistics candidateStatistics = candidateSkip
                    ? new(this.rateMultiplier, predictionRate + skipRate, skipDistortion)
                    : new(this.rateMultiplier, predictionRate + residualRate, candidateDistortion);

                // Conventional intra and earlier IBC vectors retain strict search-order precedence on equal RD.
                if (candidateStatistics.Cost >= bestStatistics.Cost)
                {
                    continue;
                }

                bestStatistics = candidateStatistics;
                hasSelectedCandidate = true;
                selectedSkip = candidateSkip;
                selectedVector = candidate;
                if (candidateSkip)
                {
                    workspace.LumaPrediction.CopyTo(workspace.SelectedLumaReconstruction);
                    workspace.SelectedLumaCoefficients.Clear();
                    selectedLumaState = default;
                    if (!this.source.IsMonochrome)
                    {
                        int chromaSampleCount = chromaTransformSize.GetSize2d();
                        workspace.BluePrediction[..chromaSampleCount].CopyTo(workspace.SelectedBlueReconstruction);
                        workspace.RedPrediction[..chromaSampleCount].CopyTo(workspace.SelectedRedReconstruction);
                        workspace.SelectedBlueCoefficients[..chromaSampleCount].Clear();
                        workspace.SelectedRedCoefficients[..chromaSampleCount].Clear();
                        selectedBlueState = default;
                        selectedRedState = default;
                    }
                }
                else
                {
                    workspace.LumaCandidateReconstruction.CopyTo(workspace.SelectedLumaReconstruction);
                    workspace.LumaCandidateCoefficients.CopyTo(workspace.SelectedLumaCoefficients);
                    selectedLumaState = lumaCandidateState;
                    if (!this.source.IsMonochrome)
                    {
                        int chromaSampleCount = chromaTransformSize.GetSize2d();
                        workspace.BlueCandidateReconstruction[..chromaSampleCount]
                            .CopyTo(workspace.SelectedBlueReconstruction);

                        workspace.RedCandidateReconstruction[..chromaSampleCount]
                            .CopyTo(workspace.SelectedRedReconstruction);

                        workspace.BlueCandidateCoefficients[..chromaSampleCount]
                            .CopyTo(workspace.SelectedBlueCoefficients);

                        workspace.RedCandidateCoefficients[..chromaSampleCount]
                            .CopyTo(workspace.SelectedRedCoefficients);

                        selectedBlueState = blueCandidateState;
                        selectedRedState = redCandidateState;
                    }
                }
            }

            if (!hasSelectedCandidate)
            {
                return bestStatistics;
            }

            // Only the winning vector is now visible to later coding blocks. This single publication keeps
            // rejected motion vectors from contaminating intra references or entropy contexts.
            Span<int> retainedLumaCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.Y);
            Span<Av1EncoderTransformBlockState> retainedLumaTransformBlocks =
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.Y);

            int lumaTransformIndex = this.codedAreaLuma /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            ref Av1EncoderTransformBlockState retainedLumaState = ref retainedLumaTransformBlocks[lumaTransformIndex];
            CopyCandidate(
                workspace.SelectedLumaReconstruction,
                workspace.SelectedLumaCoefficients,
                lumaReconstruction,
                blockOrigin,
                retainedLumaCoefficients[this.codedAreaLuma..],
                LumaTransformSize,
                selectedLumaState,
                ref retainedLumaState);

            if (!this.source.IsMonochrome)
            {
                Span<int> retainedBlueCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.U);
                Span<int> retainedRedCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.V);
                Span<Av1EncoderTransformBlockState> retainedBlueTransformBlocks =
                    this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.U);

                Span<Av1EncoderTransformBlockState> retainedRedTransformBlocks =
                    this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.V);

                int chromaTransformIndex = this.codedAreaChroma /
                    Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

                ref Av1EncoderTransformBlockState retainedBlueState = ref retainedBlueTransformBlocks[chromaTransformIndex];
                ref Av1EncoderTransformBlockState retainedRedState = ref retainedRedTransformBlocks[chromaTransformIndex];
                CopyCandidate(
                    workspace.SelectedBlueReconstruction,
                    workspace.SelectedBlueCoefficients,
                    this.reconstruction.GetPlane(Av1Plane.U),
                    chromaOrigin,
                    retainedBlueCoefficients[this.codedAreaChroma..],
                    chromaTransformSize,
                    selectedBlueState,
                    ref retainedBlueState);

                CopyCandidate(
                    workspace.SelectedRedReconstruction,
                    workspace.SelectedRedCoefficients,
                    this.reconstruction.GetPlane(Av1Plane.V),
                    chromaOrigin,
                    retainedRedCoefficients[this.codedAreaChroma..],
                    chromaTransformSize,
                    selectedRedState,
                    ref retainedRedState);
            }

            modeInfo.Block.Mode = Av1PredictionMode.DC;
            modeInfo.Block.UvMode = Av1ChromaPredictionMode.DC;
            modeInfo.Block.TransformSize = LumaTransformSize;
            modeInfo.Block.Skip = selectedSkip;
            modeInfo.Block.UseIntraBlockCopy = true;
            block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 0;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 0;
            block.PredictionUnit.ChromaFromLumaIndex = 0;
            block.PredictionUnit.ChromaFromLumaSigns = 0;
            paletteInfo = default;
            this.picture.SetDisplacementVector(modeInfoPosition, selectedVector);
            return bestStatistics;
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
            out InlineArray18<Av1EncoderTransformBlockState> selectedStates)
        {
            Av1MacroBlockModeInfo initialModeInfo = modeInfo;
            Av1EncoderBlockStruct initialBlock = block;
            Av1RateDistortionStatistics selectedStatistics = this.SelectReferenceBlock(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                Av1ReferenceFrameType.Last,
                evaluateCompound: true,
                ref modeInfo,
                ref block,
                out selectedVector,
                out selectedSecondaryVector,
                out selectedStates,
                out InlineArray4<Av1MotionVector> lastNewVectors,
                out byte lastNewVectorMask);

            if (!this.hasDistinctGoldenReference)
            {
                this.RefineInterTransformSize(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    ref modeInfo,
                    block,
                    selectedVector,
                    selectedSecondaryVector,
                    ref selectedStatistics,
                    ref selectedStates);
                return selectedStatistics;
            }

            Av1MacroBlockModeInfo goldenModeInfo = initialModeInfo;
            Av1EncoderBlockStruct goldenBlock = initialBlock;
            Av1RateDistortionStatistics goldenStatistics = this.SelectReferenceBlock(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                Av1ReferenceFrameType.Golden,
                evaluateCompound: false,
                ref goldenModeInfo,
                ref goldenBlock,
                out Av1MotionVector goldenVector,
                out Av1MotionVector goldenSecondaryVector,
                out InlineArray18<Av1EncoderTransformBlockState> goldenStates,
                out InlineArray4<Av1MotionVector> goldenNewVectors,
                out byte goldenNewVectorMask);

            // LAST and its bounded compound candidate precede GOLDEN. A strict replacement preserves that
            // deterministic search order on equal RD while still exposing the independently retained reference.
            if (goldenStatistics.Cost < selectedStatistics.Cost)
            {
                modeInfo = goldenModeInfo;
                block = goldenBlock;
                selectedVector = goldenVector;
                selectedSecondaryVector = goldenSecondaryVector;
                selectedStates = goldenStates;
                selectedStatistics = goldenStatistics;
            }

            this.SelectNewCompoundBlock(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                in lastNewVectors,
                lastNewVectorMask,
                in goldenNewVectors,
                goldenNewVectorMask,
                ref modeInfo,
                ref block,
                ref selectedStatistics,
                ref selectedVector,
                ref selectedSecondaryVector,
                ref selectedStates);

            ObuSkipModeParameters skipModeParameters = this.picture.Parent.FrameHeader.SkipModeParameters;
            if (skipModeParameters.SkipModeFlag &&
                Math.Min(modeInfo.Block.BlockSize.GetWidth(), modeInfo.Block.BlockSize.GetHeight()) >= 8 &&
                skipModeParameters.FirstReferenceFrame == Av1ReferenceFrameType.Last &&
                skipModeParameters.SecondReferenceFrame == Av1ReferenceFrameType.Golden)
            {
                int skipModeContext = Av1TileWriter.GetSkipModeContext(macroBlock);
                selectedStatistics = new Av1RateDistortionStatistics(
                    this.rateMultiplier,
                    selectedStatistics.Rate + writer.GetSkipModeCost(false, skipModeContext),
                    selectedStatistics.Distortion);

                this.SelectSkipModeBlock(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    skipModeContext,
                    ref modeInfo,
                    ref block,
                    ref selectedStatistics,
                    ref selectedVector,
                    ref selectedSecondaryVector,
                    ref selectedStates);
            }

            this.RefineInterTransformSize(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                ref modeInfo,
                block,
                selectedVector,
                selectedSecondaryVector,
                ref selectedStatistics,
                ref selectedStates);

            return selectedStatistics;
        }

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
            ref InlineArray18<Av1EncoderTransformBlockState> selectedStates)
        {
            Av1TransformSize maximumTransformSize = modeInfo.Block.BlockSize.GetMaximumTransformSize();
            if (modeInfo.Block.Skip ||
                this.picture.Parent.FrameHeader.TransformMode != Av1TransformMode.Select ||
                maximumTransformSize == Av1TransformSize.Size4x4)
            {
                return;
            }

            this.PrepareSelectedInterLumaPrediction(
                macroBlock,
                blockOrigin,
                modeInfo,
                block,
                vector,
                secondaryVector);

            Av1RateDistortionStatistics maximumStatistics = this.EvaluatePreparedInterLumaGrid(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                modeInfo.Block,
                maximumTransformSize,
                Span<Av1EncoderTransformBlockState>.Empty);

            Av1RateDistortionStatistics bestStatistics = maximumStatistics;
            Av1TransformSize bestTransformSize = maximumTransformSize;
            InlineArray16<Av1EncoderTransformBlockState> bestStates = default;
            int bestStateCount = 0;
            Av1TransformSize candidateTransformSize = maximumTransformSize;
            Span<Av1EncoderTransformBlockState> candidateStates = stackalloc Av1EncoderTransformBlockState[16];
            for (int depth = 1; depth <= Av1Constants.MaxVarTransform; depth++)
            {
                candidateTransformSize = candidateTransformSize.GetSubSize();
                int transformCount =
                    (modeInfo.Block.BlockSize.GetWidth() / candidateTransformSize.GetWidth()) *
                    (modeInfo.Block.BlockSize.GetHeight() / candidateTransformSize.GetHeight());
                Av1RateDistortionStatistics candidateStatistics = this.EvaluatePreparedInterLumaGrid(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    modeInfo.Block,
                    candidateTransformSize,
                    candidateStates[..transformCount]);

                if (candidateStatistics.Cost < bestStatistics.Cost)
                {
                    bestStatistics = candidateStatistics;
                    bestTransformSize = candidateTransformSize;
                    candidateStates[..transformCount].CopyTo(bestStates);
                    bestStateCount = transformCount;
                }

                if (candidateTransformSize == Av1TransformSize.Size4x4)
                {
                    break;
                }
            }

            if (bestTransformSize == maximumTransformSize)
            {
                return;
            }

            selectedStatistics = new(
                this.rateMultiplier,
                selectedStatistics.Rate + bestStatistics.Rate - maximumStatistics.Rate,
                selectedStatistics.Distortion + bestStatistics.Distortion - maximumStatistics.Distortion);
            modeInfo.Block.TransformSize = bestTransformSize;
            bestStates[..bestStateCount].CopyTo(selectedStates);
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
                    hasChroma: false,
                    modeInfo.Block.ReferenceFrame,
                    vector,
                    modeInfo.Block.HorizontalInterpolationFilter,
                    modeInfo.Block.VerticalInterpolationFilter,
                    modeInfo.Block.InterIntraMode,
                    modeInfo.Block.UseInterIntraWedge,
                    modeInfo.Block.InterIntraWedgeIndex);
                return;
            }

            Av1TransformSize transformSize = modeInfo.Block.BlockSize.GetMaximumTransformSize();
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Span<TSample> prediction = workspace.LumaPrediction[..transformSize.GetSize2d()];
            Span<short> residual = workspace.Residual[..transformSize.GetSize2d()];
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
                    this.reference.GetPlane(Av1Plane.Y),
                    new Point(primaryColumnQ4 >> 4, primaryRowQ4 >> 4),
                    primaryColumnQ4 & 15,
                    primaryRowQ4 & 15,
                    this.goldenReference.GetPlane(Av1Plane.Y),
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
                    transformSize,
                    this.bitDepth,
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

            Av1EncoderFrame<TSample>.PlanarView reference = modeInfo.Block.ReferenceFrame == Av1ReferenceFrameType.Golden
                ? this.goldenReference
                : this.reference;
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
                transformSize,
                this.bitDepth);
        }

        private Av1RateDistortionStatistics EvaluatePreparedInterLumaGrid(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1EncoderBlockModeInfo modeInfo,
            Av1TransformSize transformSize,
            Span<Av1EncoderTransformBlockState> states)
        {
            Av1BlockSize blockSize = modeInfo.BlockSize;
            int blockWidth = blockSize.GetWidth();
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int transformSampleCount = transformSize.GetSize2d();
            Size codedExtent = GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0);
            int columnCount = codedExtent.Width / transformWidth;
            int rowCount = codedExtent.Height / transformHeight;
            Av1EncoderInterPredictionWorkspace<TSample> interWorkspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            this.blockWorkspace.GetInterIntraStorage<TSample>(out Span<TSample> tilePrediction, out _, out _);
            Span<short> residual = interWorkspace.Residual[..transformSampleCount];
            Span<int> candidateCoefficients = interWorkspace.TransformCoefficients[..transformSampleCount];
            Span<int> bestCoefficients = interWorkspace.LumaCandidateCoefficients[..transformSampleCount];
            Span<TSample> reconstruction = interWorkspace.TransformReconstruction[..transformSampleCount];
            Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            int contextWidth = blockSize.Get4x4WideCount();
            int contextHeight = blockSize.Get4x4HighCount();
            Span<byte> topContexts = modeWorkspace.TransformContexts[..contextWidth];
            Span<byte> leftContexts = modeWorkspace.TransformContexts.Slice(contextWidth, contextHeight);
            Av1NeighborArrayUnit<byte> coefficientNeighbors = this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex];
            coefficientNeighbors.Top.Slice(coefficientNeighbors.GetTopIndex(blockOrigin), contextWidth).CopyTo(topContexts);
            coefficientNeighbors.Left.Slice(coefficientNeighbors.GetLeftIndex(blockOrigin), contextHeight).CopyTo(leftContexts);
            int rate = this.GetUniformTransformPartitionRate(writer, macroBlock, blockOrigin, tileIndex, blockSize, transformSize);
            coefficientNeighbors.Top.Slice(coefficientNeighbors.GetTopIndex(blockOrigin), contextWidth).CopyTo(topContexts);
            coefficientNeighbors.Left.Slice(coefficientNeighbors.GetLeftIndex(blockOrigin), contextHeight).CopyTo(leftContexts);
            long distortion = 0;
            Av1TransformSetType transformSet = Av1SymbolContextHelper.GetExtendedTransformSetType(
                transformSize,
                isInter: true,
                this.picture.Parent.FrameHeader.UseReducedTransformSet);

            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0; column < columnCount; column++)
                {
                    int sourceOffset = (row * transformHeight * blockWidth) + (column * transformWidth);
                    for (int sampleRow = 0; sampleRow < transformHeight; sampleRow++)
                    {
                        interWorkspace.LumaPrediction.Slice(sourceOffset + (sampleRow * blockWidth), transformWidth)
                            .CopyTo(tilePrediction.Slice(sampleRow * transformWidth, transformWidth));
                    }

                    Point transformOrigin = blockOrigin + new Size(column * transformWidth, row * transformHeight);
                    TOperator.SubtractPrediction(
                        this.source.GetPlane(Av1Plane.Y),
                        transformOrigin,
                        tilePrediction,
                        residual,
                        transformSize);
                    int width4x4 = transformSize.Get4x4WideCount();
                    int height4x4 = transformSize.Get4x4HighCount();
                    Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                        Av1ComponentType.Luminance,
                        topContexts.Slice(column * width4x4, width4x4),
                        leftContexts.Slice(row * height4x4, height4x4),
                        blockSize,
                        transformSize);

                    long bestCost = long.MaxValue;
                    int bestRate = 0;
                    long bestDistortion = 0;
                    Av1EncoderTransformBlockState bestState = default;
                    for (Av1TransformType type = Av1TransformType.DctDct; type < Av1TransformType.AllTransformTypes; type++)
                    {
                        if (!type.IsExtendedSetUsed(transformSet))
                        {
                            continue;
                        }

                        Av1EncoderTransformBlockState state = default;
                        long candidateDistortion = TOperator.EncodePredictionCandidate(
                            this.blockWorkspace,
                            writer,
                            blockContext,
                            this.rateMultiplier,
                            true,
                            this.picture.Sequence.SequenceHeader.IsStillPicture,
                            this.source.GetPlane(Av1Plane.Y),
                            transformOrigin,
                            tilePrediction,
                            residual,
                            reconstruction,
                            transformWidth,
                            candidateCoefficients,
                            transformSize,
                            type,
                            Av1Plane.Y,
                            this.quantization.QIndex[0],
                            this.quantization.DeltaQDc[(int)Av1Plane.Y],
                            this.quantization.DeltaQAc[(int)Av1Plane.Y],
                            this.bitDepth,
                            ref state);
                        int candidateRate = writer.GetCoefficientCost(
                            transformSize,
                            type,
                            modeInfo.Mode,
                            candidateCoefficients,
                            Av1ComponentType.Luminance,
                            blockContext,
                            state.EndOfBlock,
                            this.picture.Parent.FrameHeader.UseReducedTransformSet,
                            Av1FilterIntraMode.AllFilterIntraModes,
                            usesInterTransformSet: true);
                        long cost = Av1RateDistortion.GetCost(this.rateMultiplier, candidateRate, candidateDistortion);
                        if (cost < bestCost)
                        {
                            candidateCoefficients.CopyTo(bestCoefficients);
                            bestCost = cost;
                            bestRate = candidateRate;
                            bestDistortion = candidateDistortion;
                            bestState = state;
                            bestState.EntropyContext = (byte)(blockContext.SkipContext | (blockContext.DcSignContext << 4));
                        }
                    }

                    int index = (row * columnCount) + column;
                    if (!states.IsEmpty)
                    {
                        states[index] = bestState;
                    }

                    byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                        bestCoefficients,
                        transformSize,
                        bestState.TransformType,
                        bestState.EndOfBlock);
                    topContexts.Slice(column * transformSize.Get4x4WideCount(), transformSize.Get4x4WideCount()).Fill(coefficientContext);
                    leftContexts.Slice(row * transformSize.Get4x4HighCount(), transformSize.Get4x4HighCount()).Fill(coefficientContext);
                    rate += bestRate;
                    distortion += bestDistortion;
                }
            }

            return new(this.rateMultiplier, rate, distortion);
        }

        private int GetUniformTransformPartitionRate(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1TransformSize selectedTransformSize)
        {
            Av1NeighborArrayUnit<byte> frameContexts = this.picture.TransformFunctionContexts[tileIndex];
            Av1EncoderModeDecisionWorkspace<TSample> workspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            int width = blockSize.Get4x4WideCount();
            int height = blockSize.Get4x4HighCount();
            Span<byte> above = workspace.TransformContexts[..width];
            Span<byte> left = workspace.TransformContexts.Slice(width, height);
            frameContexts.Top.Slice(frameContexts.GetTopIndex(blockOrigin), width).CopyTo(above);
            frameContexts.Left.Slice(frameContexts.GetLeftIndex(blockOrigin), height).CopyTo(left);
            int maximumBlocksWide = width + (Math.Min(0, macroBlock.ToRightEdge) >> 5);
            int maximumBlocksHigh = height + (Math.Min(0, macroBlock.ToBottomEdge) >> 5);
            return GetUniformTransformPartitionRate(
                writer,
                above,
                left,
                blockSize,
                blockSize.GetMaximumTransformSize(),
                selectedTransformSize,
                depth: 0,
                blockRow: 0,
                blockColumn: 0,
                maximumBlocksWide,
                maximumBlocksHigh);
        }

        private static int GetUniformTransformPartitionRate(
            Av1SymbolEncoder writer,
            Span<byte> aboveContexts,
            Span<byte> leftContexts,
            Av1BlockSize blockSize,
            Av1TransformSize transformSize,
            Av1TransformSize selectedTransformSize,
            int depth,
            int blockRow,
            int blockColumn,
            int maximumBlocksWide,
            int maximumBlocksHigh)
        {
            if (blockRow >= maximumBlocksHigh || blockColumn >= maximumBlocksWide)
            {
                return 0;
            }

            bool split = transformSize != selectedTransformSize &&
                transformSize > Av1TransformSize.Size4x4 &&
                depth < Av1Constants.MaxVarTransform;
            int rate = 0;
            if (transformSize > Av1TransformSize.Size4x4 && depth < Av1Constants.MaxVarTransform)
            {
                int maximumDimension = Math.Max(blockSize.GetWidth(), blockSize.GetHeight());
                Av1TransformSize maximumSquareTransform = maximumDimension switch
                {
                    >= 64 => Av1TransformSize.Size64x64,
                    >= 32 => Av1TransformSize.Size32x32,
                    >= 16 => Av1TransformSize.Size16x16,
                    _ => Av1TransformSize.Size8x8
                };
                int category = ((transformSize.GetSquareUpSize() != maximumSquareTransform && maximumSquareTransform > Av1TransformSize.Size8x8) ? 1 : 0) +
                    ((((int)Av1TransformSize.SquareSizes - 1) - (int)maximumSquareTransform) * 2);
                int above = aboveContexts[blockColumn] < transformSize.GetWidth() ? 1 : 0;
                int left = leftContexts[blockRow] < transformSize.GetHeight() ? 1 : 0;
                rate = writer.GetTransformPartitionCost(split, (category * 3) + above + left);
            }

            if (split)
            {
                Av1TransformSize subTransformSize = transformSize.GetSubSize();
                int subWidth = subTransformSize.Get4x4WideCount();
                int subHeight = subTransformSize.Get4x4HighCount();
                for (int row = 0; row < transformSize.Get4x4HighCount(); row += subHeight)
                {
                    for (int column = 0; column < transformSize.Get4x4WideCount(); column += subWidth)
                    {
                        rate += GetUniformTransformPartitionRate(
                            writer,
                            aboveContexts,
                            leftContexts,
                            blockSize,
                            subTransformSize,
                            selectedTransformSize,
                            depth + 1,
                            blockRow + row,
                            blockColumn + column,
                            maximumBlocksWide,
                            maximumBlocksHigh);
                    }
                }

                return rate;
            }

            int width = Math.Min(transformSize.Get4x4WideCount(), maximumBlocksWide - blockColumn);
            int height = Math.Min(transformSize.Get4x4HighCount(), maximumBlocksHigh - blockRow);
            aboveContexts.Slice(blockColumn, width).Fill((byte)transformSize.GetWidth());
            leftContexts.Slice(blockRow, height).Fill((byte)transformSize.GetHeight());
            return rate;
        }

        private void SelectSkipModeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            int skipModeContext,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1RateDistortionStatistics selectedStatistics,
            ref Av1MotionVector selectedVector,
            ref Av1MotionVector selectedSecondaryVector,
            ref InlineArray18<Av1EncoderTransformBlockState> selectedStates)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
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
                Av1ReferenceFrameType.Last,
                Av1ReferenceFrameType.Golden);

            Av1MotionVector primaryVector = referenceMotionVectors.GetCompoundNearestReference(0);
            Av1MotionVector secondaryVector = referenceMotionVectors.GetCompoundNearestReference(1);
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Av1RateDistortionStatistics candidateStatistics = this.EvaluateInterCandidate(
                writer,
                blockOrigin,
                blockSize,
                tileIndex,
                block.HasChroma,
                writer.GetSkipModeCost(true, skipModeContext),
                Av1TileWriter.GetSkipContext(macroBlock),
                0,
                Av1ReferenceFrameType.Last,
                primaryVector,
                secondaryVector,
                Av1PredictionMode.NearestNearestMotionVector,
                isCompound: true,
                usePreparedPrediction: false,
                forceSkip: true,
                Av1CompoundType.Average,
                0,
                false,
                Av1DifferenceWeightedMaskType.Type38,
                0,
                frameHeader.InterpolationFilter,
                frameHeader.InterpolationFilter,
                0,
                in referenceMotionVectors,
                workspace.LumaCandidateReconstruction,
                workspace.LumaCandidateCoefficients,
                workspace.BlueCandidateReconstruction,
                workspace.BlueCandidateCoefficients,
                workspace.RedCandidateReconstruction,
                workspace.RedCandidateCoefficients,
                out _,
                out Av1EncoderTransformBlockState lumaState,
                out Av1EncoderTransformBlockState blueState,
                out Av1EncoderTransformBlockState redState);

            if (candidateStatistics.Cost > selectedStatistics.Cost)
            {
                return;
            }

            selectedStatistics = candidateStatistics;
            selectedVector = primaryVector;
            selectedSecondaryVector = secondaryVector;
            selectedStates[0] = lumaState;
            selectedStates[16] = blueState;
            selectedStates[17] = redState;
            modeInfo.Block.ReferenceFrame = Av1ReferenceFrameType.Last;
            modeInfo.Block.SecondaryReferenceFrame = Av1ReferenceFrameType.Golden;
            modeInfo.Block.Mode = Av1PredictionMode.NearestNearestMotionVector;
            modeInfo.Block.Skip = true;
            modeInfo.Block.SkipMode = true;
            modeInfo.Block.CompoundGroupIndex = false;
            modeInfo.Block.CompoundIndex = true;
            modeInfo.Block.CompoundType = Av1CompoundType.Average;
            modeInfo.Block.HorizontalInterpolationFilter = frameHeader.InterpolationFilter;
            modeInfo.Block.VerticalInterpolationFilter = frameHeader.InterpolationFilter;
            block.ReferenceMotionVectorIndex = 0;
        }

        private Av1RateDistortionStatistics SelectReferenceBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1ReferenceFrameType referenceFrame,
            bool evaluateCompound,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            out Av1MotionVector selectedVector,
            out Av1MotionVector selectedSecondaryVector,
            out InlineArray18<Av1EncoderTransformBlockState> selectedStates,
            out InlineArray4<Av1MotionVector> searchedNewVectors,
            out byte searchedNewVectorMask)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            Av1TransformSize lumaTransformSize = blockSize.GetMaximumTransformSize();
            DebugGuard.IsTrue(
                blockSize.GetWidth() <= Av1Constants.MaxTransformSize && blockSize.GetHeight() <= Av1Constants.MaxTransformSize,
                "Single-transform inter prediction supports partition leaves no wider or taller than the maximum AV1 transform.");

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
            ref Av1ReferenceMotionVectors referenceMotionVectors = ref this.blockWorkspace.ReferenceMotionVectors;
            referenceMotionVectors.Build(
                this.picture,
                macroBlock,
                modeInfoPosition,
                blockSize,
                modeInfo.Block.PartitionType,
                this.picture.Sequence.SequenceHeader,
                frameHeader,
                referenceFrame,
                Av1ReferenceFrameType.None);

            Av1MotionVector globalMotion = frameHeader
                .GetGlobalMotionParameters()[(int)referenceFrame - (int)Av1ReferenceFrameType.Last]
                .GetMotionVector(
                    frameHeader.AllowHighPrecisionMotionVector,
                    blockSize,
                    modeInfoPosition,
                    frameHeader.ForceIntegerMotionVector);

            Span<Av1MotionVector> candidateVectors = stackalloc Av1MotionVector[MaximumInterModeCandidateCount];
            Span<Av1PredictionMode> candidateModes = stackalloc Av1PredictionMode[MaximumInterModeCandidateCount];
            Span<byte> candidateReferenceIndices = stackalloc byte[MaximumInterModeCandidateCount];
            int candidateCount = 0;

            // Keep distinct syntax choices even when their prediction vectors are equal.
            candidateVectors[candidateCount] = referenceMotionVectors.Nearest;
            candidateModes[candidateCount] = Av1PredictionMode.NearestMotionVector;
            candidateReferenceIndices[candidateCount++] = 0;

            int maximumNewIndex = Math.Min(3, Math.Max(0, referenceMotionVectors.Count - 1));
            for (int referenceIndex = 0; referenceIndex <= maximumNewIndex; referenceIndex++)
            {
                candidateVectors[candidateCount] = referenceMotionVectors.GetNewReference(referenceIndex);
                candidateModes[candidateCount] = Av1PredictionMode.NewMotionVector;
                candidateReferenceIndices[candidateCount++] = (byte)referenceIndex;
            }

            int maximumNearIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 2));
            for (int referenceIndex = 0; referenceIndex <= maximumNearIndex; referenceIndex++)
            {
                candidateVectors[candidateCount] = referenceMotionVectors.GetNearReference(referenceIndex);
                candidateModes[candidateCount] = Av1PredictionMode.NearMotionVector;
                candidateReferenceIndices[candidateCount++] = (byte)referenceIndex;
            }

            candidateVectors[candidateCount] = globalMotion;
            candidateModes[candidateCount] = Av1PredictionMode.GlobalMotionVector;
            candidateReferenceIndices[candidateCount++] = 0;
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
            int commonPredictionRate = writer.GetIsInterCost(
                isInter: true,
                Av1TileWriter.GetIntraInterContext(macroBlock)) +
                writer.GetSingleReferenceCost(referenceFrame, referenceCounts);

            int transformPartitionRate = 0;
            if (frameHeader.TransformMode == Av1TransformMode.Select)
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
            selectedSecondaryVector = default;
            selectedStates = default;
            searchedNewVectors = default;
            searchedNewVectorMask = 0;
            Av1PredictionMode selectedMode = default;
            int selectedReferenceIndex = 0;
            bool selectedSkip = false;
            bool selectedInterIntra = false;
            Av1InterIntraMode selectedInterIntraMode = default;
            bool selectedInterIntraWedge = false;
            int selectedInterIntraWedgeIndex = 0;
            Av1EncoderTransformBlockState selectedLumaState = default;
            Av1EncoderTransformBlockState selectedBlueState = default;
            Av1EncoderTransformBlockState selectedRedState = default;

            ObuSequenceHeader sequenceHeader = this.picture.Sequence.SequenceHeader;
            bool interIntraEligible = sequenceHeader.EnableInterIntraCompound &&
                blockSize is >= Av1BlockSize.Block8x8 and <= Av1BlockSize.Block32x32;
            bool isSwitchable = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable;
            bool isDualFilter = sequenceHeader.EnableDualFilter;
            Av1InterpolationFilter defaultFilter = isSwitchable ? Av1InterpolationFilter.Regular : frameHeader.InterpolationFilter;
            Av1InterpolationFilter selectedVerticalFilter = defaultFilter;
            Av1InterpolationFilter selectedHorizontalFilter = defaultFilter;
            const int FilterCount = Av1SymbolContextHelper.SwitchableInterpolationFilterCount;
            Span<int> verticalFilterRates = stackalloc int[FilterCount];
            Span<int> horizontalFilterRates = stackalloc int[FilterCount];
            int cheapestVerticalFilter = 0;
            int cheapestHorizontalFilter = 0;
            if (isSwitchable)
            {
                int verticalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo.Block, macroBlock, direction: 0);
                int horizontalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(modeInfo.Block, macroBlock, direction: 1);
                for (int filterIndex = 0; filterIndex < FilterCount; filterIndex++)
                {
                    Av1InterpolationFilter filter = (Av1InterpolationFilter)filterIndex;
                    verticalFilterRates[filterIndex] = writer.GetSwitchableInterpolationFilterCost(filter, verticalContext);
                    horizontalFilterRates[filterIndex] = isDualFilter ? writer.GetSwitchableInterpolationFilterCost(filter, horizontalContext) : 0;
                    if (verticalFilterRates[filterIndex] < verticalFilterRates[cheapestVerticalFilter])
                    {
                        cheapestVerticalFilter = filterIndex;
                    }

                    if (horizontalFilterRates[filterIndex] < horizontalFilterRates[cheapestHorizontalFilter])
                    {
                        cheapestHorizontalFilter = filterIndex;
                    }
                }
            }

            // An integer luma displacement can still land between chroma samples. Test the finest active plane's
            // phase before collapsing filter choices; otherwise odd translations would skip real chroma differences.
            int horizontalFractionMask = (Av1MotionVector.SubpixelScale << (block.HasChroma && sequenceHeader.ColorConfig.SubSamplingX ? 1 : 0)) - 1;
            int verticalFractionMask = (Av1MotionVector.SubpixelScale << (block.HasChroma && sequenceHeader.ColorConfig.SubSamplingY ? 1 : 0)) - 1;

            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<TSample> referencePlane = referenceFrame == Av1ReferenceFrameType.Golden
                ? this.goldenReference.GetPlane(Av1Plane.Y)
                : this.reference.GetPlane(Av1Plane.Y);
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

            // Rank interpolation families with prediction-error modeling before running a full transform search.
            // The selected inter reconstruction remains untouched while two existing prediction views alternate.
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                if (candidateModes[candidateIndex] == Av1PredictionMode.NewMotionVector)
                {
                    int referenceIndex = candidateReferenceIndices[candidateIndex];
                    Av1MotionVector referenceVector = candidateVectors[candidateIndex];
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
                    if (referenceIndex == 3)
                    {
                        // Single-reference NEWMV can signal only stack entries zero through two. Entry three is
                        // searched because mixed NEAR/NEW compound syntax addresses it as near DRL index two plus one.
                        continue;
                    }
                }

                modeInfo.Block.Mode = candidateModes[candidateIndex];
                bool writesFilters = Av1TileWriter.UsesSwitchableInterpolation(frameHeader, modeInfo.Block);
                Av1InterpolationFilter verticalFilter = defaultFilter;
                Av1InterpolationFilter horizontalFilter = defaultFilter;
                int filterRate = 0;
                if (writesFilters)
                {
                    bool hasHorizontalPhase = (candidateVectors[candidateIndex].Column & horizontalFractionMask) != 0;
                    bool hasVerticalPhase = (candidateVectors[candidateIndex].Row & verticalFractionMask) != 0;
                    int filterPairCount = isDualFilter ? FilterCount * FilterCount : FilterCount;
                    long bestModelCost = long.MaxValue;
                    bool bestPredictionUsesWorkspace = true;
                    Span<TSample> bestLumaPrediction = workspace.LumaPrediction;
                    Span<TSample> trialLumaPrediction = candidateLumaReconstruction;
                    Span<TSample> bestBluePrediction = workspace.BluePrediction;
                    Span<TSample> trialBluePrediction = candidateBlueReconstruction;
                    Span<TSample> bestRedPrediction = workspace.RedPrediction;
                    Span<TSample> trialRedPrediction = candidateRedReconstruction;
                    for (int filterPairIndex = 0; filterPairIndex < filterPairCount; filterPairIndex++)
                    {
                        int verticalFilterIndex = isDualFilter ? filterPairIndex / FilterCount : filterPairIndex;
                        int horizontalFilterIndex = isDualFilter ? filterPairIndex % FilterCount : filterPairIndex;

                        // At zero phase every filter produces identical samples. Retain only the cheapest signaled
                        // choice for that axis, or for the common filter when neither axis has a fractional phase.
                        bool redundantFilter = isDualFilter
                            ? (!hasVerticalPhase && verticalFilterIndex != cheapestVerticalFilter) ||
                              (!hasHorizontalPhase && horizontalFilterIndex != cheapestHorizontalFilter)
                            : !hasVerticalPhase && !hasHorizontalPhase && verticalFilterIndex != cheapestVerticalFilter;

                        if (redundantFilter)
                        {
                            continue;
                        }

                        Av1InterpolationFilter trialVerticalFilter = (Av1InterpolationFilter)verticalFilterIndex;
                        Av1InterpolationFilter trialHorizontalFilter = (Av1InterpolationFilter)horizontalFilterIndex;
                        int trialFilterRate = verticalFilterRates[verticalFilterIndex] + horizontalFilterRates[horizontalFilterIndex];
                        long modelCost = this.GetInterFilterModelCost(
                            candidateVectors[candidateIndex],
                            blockOrigin,
                            blockSize,
                            referenceFrame,
                            block.HasChroma,
                            trialHorizontalFilter,
                            trialVerticalFilter,
                            trialFilterRate,
                            trialLumaPrediction,
                            trialBluePrediction,
                            trialRedPrediction);

                        if (modelCost >= bestModelCost)
                        {
                            continue;
                        }

                        bestModelCost = modelCost;
                        verticalFilter = trialVerticalFilter;
                        horizontalFilter = trialHorizontalFilter;
                        filterRate = trialFilterRate;
                        Span<TSample> previousLumaPrediction = bestLumaPrediction;
                        bestLumaPrediction = trialLumaPrediction;
                        trialLumaPrediction = previousLumaPrediction;
                        Span<TSample> previousBluePrediction = bestBluePrediction;
                        bestBluePrediction = trialBluePrediction;
                        trialBluePrediction = previousBluePrediction;
                        Span<TSample> previousRedPrediction = bestRedPrediction;
                        bestRedPrediction = trialRedPrediction;
                        trialRedPrediction = previousRedPrediction;
                        bestPredictionUsesWorkspace = !bestPredictionUsesWorkspace;
                    }

                    // Keep the winner in the prediction views before transforms reuse candidate reconstruction.
                    // This requires at most one copy per plane and never rebuilds the chosen interpolation.
                    if (!bestPredictionUsesWorkspace)
                    {
                        bestLumaPrediction.CopyTo(workspace.LumaPrediction);
                        if (block.HasChroma)
                        {
                            bestBluePrediction.CopyTo(workspace.BluePrediction);
                            bestRedPrediction.CopyTo(workspace.RedPrediction);
                        }
                    }
                }

                Av1RateDistortionStatistics candidateStatistics = this.EvaluateInterCandidate(
                    writer,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    block.HasChroma,
                    commonPredictionRate + filterRate + (interIntraEligible ? writer.GetInterIntraCost(blockSize, false, default, false, 0) : 0),
                    skipContext,
                    transformPartitionRate,
                    referenceFrame,
                    candidateVectors[candidateIndex],
                    default,
                    candidateModes[candidateIndex],
                    isCompound: false,
                    usePreparedPrediction: writesFilters,
                    forceSkip: false,
                    Av1CompoundType.Average,
                    0,
                    false,
                    Av1DifferenceWeightedMaskType.Type38,
                    0,
                    horizontalFilter,
                    verticalFilter,
                    candidateReferenceIndices[candidateIndex],
                    in referenceMotionVectors,
                    candidateLumaReconstruction,
                    candidateLumaCoefficients,
                    candidateBlueReconstruction,
                    candidateBlueCoefficients,
                    candidateRedReconstruction,
                    candidateRedCoefficients,
                    out bool candidateSkip,
                    out Av1EncoderTransformBlockState candidateLumaState,
                    out Av1EncoderTransformBlockState candidateBlueState,
                    out Av1EncoderTransformBlockState candidateRedState);

                // Strict replacement preserves nearest, new, near, then global mode order on equal RD cost.
                if (candidateStatistics.Cost < selectedStatistics.Cost)
                {
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

                    selectedStatistics = candidateStatistics;
                    selectedVector = candidateVectors[candidateIndex];
                    selectedMode = candidateModes[candidateIndex];
                    selectedHorizontalFilter = horizontalFilter;
                    selectedVerticalFilter = verticalFilter;
                    selectedReferenceIndex = candidateReferenceIndices[candidateIndex];
                    selectedSkip = candidateSkip;
                    selectedInterIntra = false;
                    selectedLumaState = candidateLumaState;
                    selectedBlueState = candidateBlueState;
                    selectedRedState = candidateRedState;
                }

                if (!interIntraEligible)
                {
                    continue;
                }

                const int InterIntraModeCount = 4;
                const int WedgeIndexCount = 16;
                for (int interIntraCandidate = 0; interIntraCandidate < InterIntraModeCount + WedgeIndexCount; interIntraCandidate++)
                {
                    bool useWedge = interIntraCandidate >= InterIntraModeCount;
                    Av1InterIntraMode interIntraMode = useWedge
                        ? Av1InterIntraMode.DC
                        : (Av1InterIntraMode)interIntraCandidate;
                    int wedgeIndex = useWedge ? interIntraCandidate - InterIntraModeCount : 0;
                    this.PrepareInterIntraPrediction(
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        block.HasChroma,
                        referenceFrame,
                        candidateVectors[candidateIndex],
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
                        blockOrigin,
                        blockSize,
                        tileIndex,
                        block.HasChroma,
                        commonPredictionRate + filterRate + interIntraRate,
                        skipContext,
                        transformPartitionRate,
                        referenceFrame,
                        candidateVectors[candidateIndex],
                        default,
                        candidateModes[candidateIndex],
                        isCompound: false,
                        usePreparedPrediction: true,
                        forceSkip: false,
                        Av1CompoundType.Average,
                        0,
                        false,
                        Av1DifferenceWeightedMaskType.Type38,
                        0,
                        horizontalFilter,
                        verticalFilter,
                        candidateReferenceIndices[candidateIndex],
                        in referenceMotionVectors,
                        candidateLumaReconstruction,
                        candidateLumaCoefficients,
                        candidateBlueReconstruction,
                        candidateBlueCoefficients,
                        candidateRedReconstruction,
                        candidateRedCoefficients,
                        out bool interIntraSkip,
                        out Av1EncoderTransformBlockState interIntraLumaState,
                        out Av1EncoderTransformBlockState interIntraBlueState,
                        out Av1EncoderTransformBlockState interIntraRedState);

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
                    selectedVector = candidateVectors[candidateIndex];
                    selectedMode = candidateModes[candidateIndex];
                    selectedHorizontalFilter = horizontalFilter;
                    selectedVerticalFilter = verticalFilter;
                    selectedReferenceIndex = candidateReferenceIndices[candidateIndex];
                    selectedSkip = interIntraSkip;
                    selectedInterIntra = true;
                    selectedInterIntraMode = interIntraMode;
                    selectedInterIntraWedge = useWedge;
                    selectedInterIntraWedgeIndex = wedgeIndex;
                    selectedLumaState = interIntraLumaState;
                    selectedBlueState = interIntraBlueState;
                    selectedRedState = interIntraRedState;
                }
            }

            if (evaluateCompound &&
                this.hasDistinctGoldenReference &&
                frameHeader.ReferenceMode != ObuReferenceMode.SingleReference &&
                !isSwitchable)
            {
                referenceMotionVectors.Build(
                    this.picture,
                    macroBlock,
                    modeInfoPosition,
                    blockSize,
                    modeInfo.Block.PartitionType,
                    sequenceHeader,
                    frameHeader,
                    Av1ReferenceFrameType.Last,
                    Av1ReferenceFrameType.Golden);

                int compoundPredictionRate = writer.GetIsInterCost(
                    isInter: true,
                    Av1TileWriter.GetIntraInterContext(macroBlock)) +
                    writer.GetCompoundReferenceCost(
                        Av1ReferenceFrameType.Last,
                        Av1ReferenceFrameType.Golden,
                        Av1SymbolContextHelper.GetReferenceModeContext(macroBlock),
                        Av1SymbolContextHelper.GetCompoundReferenceTypeContext(macroBlock),
                        referenceCounts);

                Span<Av1MotionVector> primaryCompoundVectors = stackalloc Av1MotionVector[5];
                Span<Av1MotionVector> secondaryCompoundVectors = stackalloc Av1MotionVector[5];
                Span<Av1PredictionMode> compoundModes = stackalloc Av1PredictionMode[5];
                Span<byte> compoundReferenceIndices = stackalloc byte[5];
                int compoundCandidateCount = 0;

                primaryCompoundVectors[compoundCandidateCount] = referenceMotionVectors.GetCompoundNearestReference(0);
                secondaryCompoundVectors[compoundCandidateCount] = referenceMotionVectors.GetCompoundNearestReference(1);
                compoundModes[compoundCandidateCount++] = Av1PredictionMode.NearestNearestMotionVector;

                int maximumCompoundNearIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 2));
                for (int referenceIndex = 0; referenceIndex <= maximumCompoundNearIndex; referenceIndex++)
                {
                    primaryCompoundVectors[compoundCandidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 0);
                    secondaryCompoundVectors[compoundCandidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 1);
                    compoundModes[compoundCandidateCount] = Av1PredictionMode.NearNearMotionVector;
                    compoundReferenceIndices[compoundCandidateCount++] = (byte)referenceIndex;
                }

                primaryCompoundVectors[compoundCandidateCount] = frameHeader
                    .GetGlobalMotionParameters()[(int)Av1ReferenceFrameType.Last - 1]
                    .GetMotionVector(
                        frameHeader.AllowHighPrecisionMotionVector,
                        blockSize,
                        modeInfoPosition,
                        frameHeader.ForceIntegerMotionVector);

                secondaryCompoundVectors[compoundCandidateCount] = frameHeader
                    .GetGlobalMotionParameters()[(int)Av1ReferenceFrameType.Golden - 1]
                    .GetMotionVector(
                        frameHeader.AllowHighPrecisionMotionVector,
                        blockSize,
                        modeInfoPosition,
                        frameHeader.ForceIntegerMotionVector);

                compoundModes[compoundCandidateCount++] = Av1PredictionMode.GlobalGlobalMotionVector;

                bool maskedCompoundEnabled = sequenceHeader.EnableMaskedCompound &&
                    Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8;
                bool supportsWedge = blockSize is
                    Av1BlockSize.Block8x8 or
                    Av1BlockSize.Block8x16 or
                    Av1BlockSize.Block16x8 or
                    Av1BlockSize.Block16x16 or
                    Av1BlockSize.Block16x32 or
                    Av1BlockSize.Block32x16 or
                    Av1BlockSize.Block32x32 or
                    Av1BlockSize.Block8x32 or
                    Av1BlockSize.Block32x8;

                int compoundGroupContext = Av1TileWriter.GetCompoundGroupIndexContext(macroBlock);
                bool jointCompoundEnabled = sequenceHeader.OrderHintInfo.EnableJointCompound;
                int compoundIndexContext = jointCompoundEnabled
                    ? Av1SymbolContextHelper.GetCompoundIndexContext(
                        sequenceHeader.OrderHintInfo,
                        frameHeader,
                        Av1ReferenceFrameType.Last,
                        Av1ReferenceFrameType.Golden,
                        macroBlock)
                    : 0;

                for (int candidateIndex = 0; candidateIndex < compoundCandidateCount; candidateIndex++)
                {
                    int unmaskedCandidateCount = jointCompoundEnabled ? 2 : 1;
                    int blendCandidateCount = unmaskedCandidateCount + (maskedCompoundEnabled ? 2 + (supportsWedge ? 32 : 0) : 0);
                    for (int blendCandidateIndex = 0; blendCandidateIndex < blendCandidateCount; blendCandidateIndex++)
                    {
                        Av1CompoundType compoundType;
                        int wedgeIndex = 0;
                        bool wedgeSign = false;
                        Av1DifferenceWeightedMaskType maskType = Av1DifferenceWeightedMaskType.Type38;
                        if (blendCandidateIndex == 0)
                        {
                            compoundType = Av1CompoundType.Average;
                        }
                        else if (jointCompoundEnabled && blendCandidateIndex == 1)
                        {
                            compoundType = Av1CompoundType.DistanceWeighted;
                        }
                        else if (blendCandidateIndex < unmaskedCandidateCount + 2)
                        {
                            compoundType = Av1CompoundType.DifferenceWeighted;
                            maskType = (Av1DifferenceWeightedMaskType)(blendCandidateIndex - unmaskedCandidateCount);
                        }
                        else
                        {
                            compoundType = Av1CompoundType.Wedge;
                            int wedgeCandidate = blendCandidateIndex - unmaskedCandidateCount - 2;
                            wedgeIndex = wedgeCandidate >> 1;
                            wedgeSign = (wedgeCandidate & 1) != 0;
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
                            blockOrigin,
                            blockSize,
                            tileIndex,
                            block.HasChroma,
                            compoundPredictionRate,
                            skipContext,
                            transformPartitionRate,
                            Av1ReferenceFrameType.Last,
                            primaryCompoundVectors[candidateIndex],
                            secondaryCompoundVectors[candidateIndex],
                            compoundModes[candidateIndex],
                            isCompound: true,
                            usePreparedPrediction: false,
                            forceSkip: false,
                            compoundType,
                            wedgeIndex,
                            wedgeSign,
                            maskType,
                            blendRate,
                            defaultFilter,
                            defaultFilter,
                            compoundReferenceIndices[candidateIndex],
                            in referenceMotionVectors,
                            candidateLumaReconstruction,
                            candidateLumaCoefficients,
                            candidateBlueReconstruction,
                            candidateBlueCoefficients,
                            candidateRedReconstruction,
                            candidateRedCoefficients,
                            out bool candidateSkip,
                            out Av1EncoderTransformBlockState candidateLumaState,
                            out Av1EncoderTransformBlockState candidateBlueState,
                            out Av1EncoderTransformBlockState candidateRedState);

                        // libaom searches compound modes and blend syntax in stable order. Strict replacement
                        // retains the earlier motion mode, blend, and lower DRL entry on equal cost.
                        if (candidateStatistics.Cost >= selectedStatistics.Cost)
                        {
                            continue;
                        }

                        selectedStatistics = candidateStatistics;
                        selectedVector = primaryCompoundVectors[candidateIndex];
                        selectedSecondaryVector = secondaryCompoundVectors[candidateIndex];
                        selectedMode = compoundModes[candidateIndex];
                        selectedHorizontalFilter = defaultFilter;
                        selectedVerticalFilter = defaultFilter;
                        selectedReferenceIndex = compoundReferenceIndices[candidateIndex];
                        selectedSkip = candidateSkip;
                        selectedLumaState = candidateLumaState;
                        selectedBlueState = candidateBlueState;
                        selectedRedState = candidateRedState;
                        selectedInterIntra = false;
                        modeInfo.Block.SecondaryReferenceFrame = Av1ReferenceFrameType.Golden;
                        modeInfo.Block.CompoundGroupIndex = compoundType is Av1CompoundType.Wedge or Av1CompoundType.DifferenceWeighted;
                        modeInfo.Block.CompoundIndex = compoundType == Av1CompoundType.Average;
                        modeInfo.Block.CompoundType = compoundType;
                        modeInfo.Block.CompoundWedgeIndex = (byte)wedgeIndex;
                        modeInfo.Block.CompoundWedgeSign = wedgeSign;
                        modeInfo.Block.DifferenceWeightedMaskType = maskType;
                    }
                }
            }

            // Candidate pixels and coefficients remain scratch. Preserve the transform decisions so final
            // reconstruction can regenerate only the winner after other mode families reuse this storage.
            selectedStates[0] = selectedLumaState;
            selectedStates[16] = selectedBlueState;
            selectedStates[17] = selectedRedState;

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
            this.blockWorkspace.GetInterIntraStorage<TSample>(
                out Span<TSample> intraPrediction,
                out Span<TSample> aboveStorage,
                out Span<TSample> leftStorage);

            int planeCount = hasChroma ? 3 : 1;
            int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            Av1PredictionMode intraMode = interIntraMode switch
            {
                Av1InterIntraMode.Vertical => Av1PredictionMode.Vertical,
                Av1InterIntraMode.Horizontal => Av1PredictionMode.Horizontal,
                Av1InterIntraMode.Smooth => Av1PredictionMode.Smooth,
                _ => Av1PredictionMode.DC,
            };

            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
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
                bool bottomAvailable = modeInfoRow + (transformSize.Get4x4HighCount() << subY) < macroBlock.Tile.ModeInfoRowEnd;
                bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                    this.picture.Sequence.SequenceHeader.SuperblockSize,
                    blockSize,
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
                    blockSize,
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

                Span<TSample> interPrediction = plane switch
                {
                    Av1Plane.Y => workspace.LumaPrediction[..sampleCount],
                    Av1Plane.U => workspace.BluePrediction[..sampleCount],
                    _ => workspace.RedPrediction[..sampleCount],
                };
                int columnQ4 = (planeOrigin.X << 4) + (vector.Column << (1 - subX));
                int rowQ4 = (planeOrigin.Y << 4) + (vector.Row << (1 - subY));
                Av1EncoderFrame<TSample>.PlanarView reference = referenceFrame == Av1ReferenceFrameType.Golden
                    ? this.goldenReference
                    : this.reference;
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
                    transformSize,
                    this.bitDepth);

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

        private void SelectNewCompoundBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            in InlineArray4<Av1MotionVector> lastNewVectors,
            byte lastNewVectorMask,
            in InlineArray4<Av1MotionVector> goldenNewVectors,
            byte goldenNewVectorMask,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1RateDistortionStatistics selectedStatistics,
            ref Av1MotionVector selectedVector,
            ref Av1MotionVector selectedSecondaryVector,
            ref InlineArray18<Av1EncoderTransformBlockState> selectedStates)
        {
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            if (!this.hasDistinctGoldenReference ||
                frameHeader.ReferenceMode == ObuReferenceMode.SingleReference ||
                frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable ||
                (lastNewVectorMask | goldenNewVectorMask) == 0)
            {
                return;
            }

            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
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
                Av1ReferenceFrameType.Last,
                Av1ReferenceFrameType.Golden);

            Span<byte> referenceCounts = stackalloc byte[Av1Constants.ReferenceFrameCount];
            Av1TileWriter.CollectNeighborReferenceCounts(macroBlock, referenceCounts);
            int commonPredictionRate = writer.GetIsInterCost(
                isInter: true,
                Av1TileWriter.GetIntraInterContext(macroBlock)) +
                writer.GetCompoundReferenceCost(
                    Av1ReferenceFrameType.Last,
                    Av1ReferenceFrameType.Golden,
                    Av1SymbolContextHelper.GetReferenceModeContext(macroBlock),
                    Av1SymbolContextHelper.GetCompoundReferenceTypeContext(macroBlock),
                    referenceCounts);

            int transformPartitionRate = 0;
            Av1TransformSize lumaTransformSize = blockSize.GetMaximumTransformSize();
            if (frameHeader.TransformMode == Av1TransformMode.Select)
            {
                Av1NeighborArrayUnit<byte> transformContexts = this.picture.TransformFunctionContexts[tileIndex];
                int transformPartitionContext = Av1SymbolContextHelper.GetTransformPartitionContext(
                    transformContexts.Top[transformContexts.GetTopIndex(blockOrigin)],
                    transformContexts.Left[transformContexts.GetLeftIndex(blockOrigin)],
                    blockSize,
                    lumaTransformSize);

                transformPartitionRate = writer.GetTransformPartitionCost(false, transformPartitionContext);
            }

            Span<Av1MotionVector> primaryVectors = stackalloc Av1MotionVector[11];
            Span<Av1MotionVector> secondaryVectors = stackalloc Av1MotionVector[11];
            Span<Av1PredictionMode> modes = stackalloc Av1PredictionMode[11];
            Span<byte> referenceIndices = stackalloc byte[11];
            int candidateCount = 0;
            if ((goldenNewVectorMask & 1) != 0)
            {
                primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(0);
                secondaryVectors[candidateCount] = goldenNewVectors[0];
                modes[candidateCount++] = Av1PredictionMode.NearestNewMotionVector;
            }

            if ((lastNewVectorMask & 1) != 0)
            {
                primaryVectors[candidateCount] = lastNewVectors[0];
                secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearestReference(1);
                modes[candidateCount++] = Av1PredictionMode.NewNearestMotionVector;
            }

            int maximumNearIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 2));
            for (int referenceIndex = 0; referenceIndex <= maximumNearIndex; referenceIndex++)
            {
                int newReferenceIndex = referenceIndex + 1;
                if ((goldenNewVectorMask & (1 << newReferenceIndex)) != 0)
                {
                    primaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 0);
                    secondaryVectors[candidateCount] = goldenNewVectors[newReferenceIndex];
                    modes[candidateCount] = Av1PredictionMode.NearNewMotionVector;
                    referenceIndices[candidateCount++] = (byte)referenceIndex;
                }

                if ((lastNewVectorMask & (1 << newReferenceIndex)) != 0)
                {
                    primaryVectors[candidateCount] = lastNewVectors[newReferenceIndex];
                    secondaryVectors[candidateCount] = referenceMotionVectors.GetCompoundNearReference(referenceIndex, 1);
                    modes[candidateCount] = Av1PredictionMode.NewNearMotionVector;
                    referenceIndices[candidateCount++] = (byte)referenceIndex;
                }
            }

            int sharedNewMask = lastNewVectorMask & goldenNewVectorMask;
            int maximumNewIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 1));
            for (int referenceIndex = 0; referenceIndex <= maximumNewIndex; referenceIndex++)
            {
                if ((sharedNewMask & (1 << referenceIndex)) == 0)
                {
                    continue;
                }

                primaryVectors[candidateCount] = lastNewVectors[referenceIndex];
                secondaryVectors[candidateCount] = goldenNewVectors[referenceIndex];
                modes[candidateCount] = Av1PredictionMode.NewNewMotionVector;
                referenceIndices[candidateCount++] = (byte)referenceIndex;
            }

            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            Av1InterpolationFilter filter = frameHeader.InterpolationFilter;
            bool maskedCompoundEnabled = this.picture.Sequence.SequenceHeader.EnableMaskedCompound &&
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8;
            bool supportsWedge = blockSize is
                Av1BlockSize.Block8x8 or
                Av1BlockSize.Block8x16 or
                Av1BlockSize.Block16x8 or
                Av1BlockSize.Block16x16 or
                Av1BlockSize.Block16x32 or
                Av1BlockSize.Block32x16 or
                Av1BlockSize.Block32x32 or
                Av1BlockSize.Block8x32 or
                Av1BlockSize.Block32x8;

            int compoundGroupContext = Av1TileWriter.GetCompoundGroupIndexContext(macroBlock);
            bool jointCompoundEnabled = this.picture.Sequence.SequenceHeader.OrderHintInfo.EnableJointCompound;
            int compoundIndexContext = jointCompoundEnabled
                ? Av1SymbolContextHelper.GetCompoundIndexContext(
                    this.picture.Sequence.SequenceHeader.OrderHintInfo,
                    frameHeader,
                    Av1ReferenceFrameType.Last,
                    Av1ReferenceFrameType.Golden,
                    macroBlock)
                : 0;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                int unmaskedCandidateCount = jointCompoundEnabled ? 2 : 1;
                int blendCandidateCount = unmaskedCandidateCount + (maskedCompoundEnabled ? 2 + (supportsWedge ? 32 : 0) : 0);
                for (int blendCandidateIndex = 0; blendCandidateIndex < blendCandidateCount; blendCandidateIndex++)
                {
                    Av1CompoundType compoundType;
                    int wedgeIndex = 0;
                    bool wedgeSign = false;
                    Av1DifferenceWeightedMaskType maskType = Av1DifferenceWeightedMaskType.Type38;
                    if (blendCandidateIndex == 0)
                    {
                        compoundType = Av1CompoundType.Average;
                    }
                    else if (jointCompoundEnabled && blendCandidateIndex == 1)
                    {
                        compoundType = Av1CompoundType.DistanceWeighted;
                    }
                    else if (blendCandidateIndex < unmaskedCandidateCount + 2)
                    {
                        compoundType = Av1CompoundType.DifferenceWeighted;
                        maskType = (Av1DifferenceWeightedMaskType)(blendCandidateIndex - unmaskedCandidateCount);
                    }
                    else
                    {
                        compoundType = Av1CompoundType.Wedge;
                        int wedgeCandidate = blendCandidateIndex - unmaskedCandidateCount - 2;
                        wedgeIndex = wedgeCandidate >> 1;
                        wedgeSign = (wedgeCandidate & 1) != 0;
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
                        blockOrigin,
                        blockSize,
                        tileIndex,
                        block.HasChroma,
                        commonPredictionRate,
                        skipContext,
                        transformPartitionRate,
                        Av1ReferenceFrameType.Last,
                        primaryVectors[candidateIndex],
                        secondaryVectors[candidateIndex],
                        modes[candidateIndex],
                        isCompound: true,
                        usePreparedPrediction: false,
                        forceSkip: false,
                        compoundType,
                        wedgeIndex,
                        wedgeSign,
                        maskType,
                        blendRate,
                        filter,
                        filter,
                        referenceIndices[candidateIndex],
                        in referenceMotionVectors,
                        workspace.LumaCandidateReconstruction,
                        workspace.LumaCandidateCoefficients,
                        workspace.BlueCandidateReconstruction,
                        workspace.BlueCandidateCoefficients,
                        workspace.RedCandidateReconstruction,
                        workspace.RedCandidateCoefficients,
                        out bool candidateSkip,
                        out Av1EncoderTransformBlockState candidateLumaState,
                        out Av1EncoderTransformBlockState candidateBlueState,
                        out Av1EncoderTransformBlockState candidateRedState);

                    if (candidateStatistics.Cost >= selectedStatistics.Cost)
                    {
                        continue;
                    }

                    selectedStatistics = candidateStatistics;
                    selectedVector = primaryVectors[candidateIndex];
                    selectedSecondaryVector = secondaryVectors[candidateIndex];
                    selectedStates[0] = candidateLumaState;
                    selectedStates[16] = candidateBlueState;
                    selectedStates[17] = candidateRedState;
                    modeInfo.Block.ReferenceFrame = Av1ReferenceFrameType.Last;
                    modeInfo.Block.SecondaryReferenceFrame = Av1ReferenceFrameType.Golden;
                    modeInfo.Block.Mode = modes[candidateIndex];
                    modeInfo.Block.Skip = candidateSkip;
                    modeInfo.Block.CompoundGroupIndex = compoundType is Av1CompoundType.Wedge or Av1CompoundType.DifferenceWeighted;
                    modeInfo.Block.CompoundIndex = compoundType == Av1CompoundType.Average;
                    modeInfo.Block.CompoundType = compoundType;
                    modeInfo.Block.CompoundWedgeIndex = (byte)wedgeIndex;
                    modeInfo.Block.CompoundWedgeSign = wedgeSign;
                    modeInfo.Block.DifferenceWeightedMaskType = maskType;
                    modeInfo.Block.HorizontalInterpolationFilter = filter;
                    modeInfo.Block.VerticalInterpolationFilter = filter;
                    block.ReferenceMotionVectorIndex = referenceIndices[candidateIndex];
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
        private void ReconstructSelectedInterBlock(
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
            bool usesSplitLumaTransforms = modeInfo.Block.TransformSize != modeInfo.Block.BlockSize.GetMaximumTransformSize();
            if (usesSplitLumaTransforms)
            {
                this.PrepareSelectedInterLumaPrediction(
                    macroBlock,
                    blockOrigin,
                    modeInfo,
                    block,
                    vector,
                    secondaryVector);
            }
            else if (isInterIntra)
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

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int planeCount = block.HasChroma ? 3 : 1;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Point planeOrigin = new(blockOrigin.X >> subX, blockOrigin.Y >> subY);
                Av1TransformSize transformSize = planeIndex == 0
                    ? modeInfo.Block.TransformSize
                    : modeInfo.Block.BlockSize.GetMaxUvTransformSize(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

                if (planeIndex == 0 && usesSplitLumaTransforms)
                {
                    this.ReconstructSelectedInterLumaGrid(
                        writer,
                        macroBlock,
                        tileIndex,
                        blockOrigin,
                        modeInfo,
                        states);
                    continue;
                }

                int sampleCount = transformSize.GetSize2d();
                Span<TSample> prediction = workspace.LumaPrediction[..sampleCount];
                Span<short> residual = workspace.Residual[..sampleCount];

                // Convert eighth-luma-sample motion into the plane's sixteenth-sample interpolation
                // coordinates. The low four bits carry the phase; the remaining bits locate the reference.
                int columnQ4 = (planeOrigin.X << 4) + (vector.Column << (1 - subX));
                int rowQ4 = (planeOrigin.Y << 4) + (vector.Row << (1 - subY));
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

                    int secondaryColumnQ4 = (planeOrigin.X << 4) + (secondaryVector.Column << (1 - subX));
                    int secondaryRowQ4 = (planeOrigin.Y << 4) + (secondaryVector.Row << (1 - subY));
                    this.blockWorkspace.GetCompoundPredictionIntermediates(
                        out Span<ushort> firstIntermediate,
                        out Span<ushort> secondIntermediate);

                    TOperator.PrepareCompoundInterPrediction(
                        this.source.GetPlane(plane),
                        planeOrigin,
                        this.reference.GetPlane(plane),
                        new Point(columnQ4 >> 4, rowQ4 >> 4),
                        columnQ4 & 15,
                        rowQ4 & 15,
                        this.goldenReference.GetPlane(plane),
                        new Point(secondaryColumnQ4 >> 4, secondaryRowQ4 >> 4),
                        secondaryColumnQ4 & 15,
                        secondaryRowQ4 & 15,
                        modeInfo.Block.HorizontalInterpolationFilter,
                        modeInfo.Block.VerticalInterpolationFilter,
                        prediction,
                        residual,
                        firstIntermediate,
                        secondIntermediate,
                        this.blockWorkspace.GetCompoundPredictionMask(),
                        workspace.PredictionScratch,
                        transformSize,
                        this.bitDepth,
                        modeInfo.Block.BlockSize,
                        modeInfo.Block.CompoundType,
                        firstWeight,
                        secondWeight,
                        subX,
                        subY,
                        modeInfo.Block.CompoundWedgeIndex,
                        modeInfo.Block.CompoundWedgeSign,
                        modeInfo.Block.DifferenceWeightedMaskType);
                }
                else if (!isInterIntra)
                {
                    Buffer2DRegion<TSample> primaryReferencePlane = modeInfo.Block.ReferenceFrame == Av1ReferenceFrameType.Golden
                        ? this.goldenReference.GetPlane(plane)
                        : this.reference.GetPlane(plane);

                    TOperator.PrepareTranslationalInterPrediction(
                        this.source.GetPlane(plane),
                        planeOrigin,
                        primaryReferencePlane,
                        new Point(columnQ4 >> 4, rowQ4 >> 4),
                        modeInfo.Block.HorizontalInterpolationFilter,
                        modeInfo.Block.VerticalInterpolationFilter,
                        columnQ4 & 15,
                        rowQ4 & 15,
                        prediction,
                        residual,
                        workspace.PredictionScratch,
                        transformSize,
                        this.bitDepth);
                }

                if (isInterIntra)
                {
                    prediction = plane switch
                    {
                        Av1Plane.Y => workspace.LumaPrediction[..sampleCount],
                        Av1Plane.U => workspace.BluePrediction[..sampleCount],
                        _ => workspace.RedPrediction[..sampleCount],
                    };
                    TOperator.SubtractPrediction(
                        this.source.GetPlane(plane),
                        planeOrigin,
                        prediction,
                        residual,
                        transformSize);
                }

                int codedArea = planeIndex == 0 ? this.codedAreaLuma : this.codedAreaChroma;
                Av1NeighborArrayUnit<byte> neighbors = plane switch
                {
                    Av1Plane.Y => this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                    Av1Plane.U => this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                    _ => this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex]
                };

                Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                    planeIndex == 0 ? Av1ComponentType.Luminance : Av1ComponentType.Chroma,
                    neighbors.Top.Slice(neighbors.GetTopIndex(planeOrigin), transformSize.Get4x4WideCount()),
                    neighbors.Left.Slice(neighbors.GetLeftIndex(planeOrigin), transformSize.Get4x4HighCount()),
                    modeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0),
                    transformSize);

                this.ReconstructSelectedTransform(
                    writer,
                    blockContext,
                    true,
                    planeOrigin,
                    plane,
                    transformSize,
                    prediction,
                    residual,
                    states[planeIndex == 0 ? 0 : 15 + planeIndex],
                    modeInfo.Block.Skip,
                    codedArea);
            }
        }

        private void ReconstructSelectedInterLumaGrid(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            ushort tileIndex,
            Point blockOrigin,
            Av1MacroBlockModeInfo modeInfo,
            ReadOnlySpan<Av1EncoderTransformBlockState> states)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            Av1TransformSize transformSize = modeInfo.Block.TransformSize;
            int blockWidth = blockSize.GetWidth();
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int transformSampleCount = transformSize.GetSize2d();
            Size codedExtent = GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0);
            int columnCount = codedExtent.Width / transformWidth;
            int rowCount = codedExtent.Height / transformHeight;
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            this.blockWorkspace.GetInterIntraStorage<TSample>(out Span<TSample> tilePrediction, out _, out _);
            Span<short> residual = workspace.Residual[..transformSampleCount];
            Av1NeighborArrayUnit<byte> neighbors = this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex];
            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0; column < columnCount; column++)
                {
                    int sourceOffset = (row * transformHeight * blockWidth) + (column * transformWidth);
                    for (int sampleRow = 0; sampleRow < transformHeight; sampleRow++)
                    {
                        workspace.LumaPrediction.Slice(sourceOffset + (sampleRow * blockWidth), transformWidth)
                            .CopyTo(tilePrediction.Slice(sampleRow * transformWidth, transformWidth));
                    }

                    Point transformOrigin = blockOrigin + new Size(column * transformWidth, row * transformHeight);
                    TOperator.SubtractPrediction(
                        this.source.GetPlane(Av1Plane.Y),
                        transformOrigin,
                        tilePrediction,
                        residual,
                        transformSize);
                    Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
                        Av1ComponentType.Luminance,
                        neighbors.Top.Slice(neighbors.GetTopIndex(transformOrigin), transformSize.Get4x4WideCount()),
                        neighbors.Left.Slice(neighbors.GetLeftIndex(transformOrigin), transformSize.Get4x4HighCount()),
                        blockSize,
                        transformSize);
                    int transformIndex = (row * columnCount) + column;
                    this.ReconstructSelectedTransform(
                        writer,
                        context,
                        true,
                        transformOrigin,
                        Av1Plane.Y,
                        transformSize,
                        tilePrediction,
                        residual,
                        states[transformIndex],
                        modeInfo.Block.Skip,
                        this.codedAreaLuma + (transformIndex * transformSampleCount));
                }
            }
        }

        /// <summary>
        /// Ranks a filter pair from visible prediction error using the reference curve model, without transforming samples.
        /// </summary>
        private long GetInterFilterModelCost(
            Av1MotionVector vector,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType referenceFrame,
            bool hasChroma,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int filterRate,
            Span<TSample> lumaPrediction,
            Span<TSample> bluePrediction,
            Span<TSample> redPrediction)
        {
            const int InterpolationPrecisionBits = 4;
            const int InterpolationPhaseMask = (1 << InterpolationPrecisionBits) - 1;
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            int planeCount = hasChroma ? 3 : 1;
            int rate = filterRate;
            long distortion = 0;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subsamplingX = plane == Av1Plane.Y ? 0 : this.source.ChromaSubsamplingX;
                int subsamplingY = plane == Av1Plane.Y ? 0 : this.source.ChromaSubsamplingY;
                Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
                Av1TransformSize transformSize = plane == Av1Plane.Y
                    ? blockSize.GetMaximumTransformSize()
                    : blockSize.GetMaxUvTransformSize(subsamplingX != 0, subsamplingY != 0);
                int width = transformSize.GetWidth();
                int height = transformSize.GetHeight();
                Point planeOrigin = new(blockOrigin.X >> subsamplingX, blockOrigin.Y >> subsamplingY);
                int sourceColumn = (planeOrigin.X << InterpolationPrecisionBits) + (vector.Column << (1 - subsamplingX));
                int sourceRow = (planeOrigin.Y << InterpolationPrecisionBits) + (vector.Row << (1 - subsamplingY));
                Point predictionOrigin = new(sourceColumn >> InterpolationPrecisionBits, sourceRow >> InterpolationPrecisionBits);
                Span<TSample> prediction = plane == Av1Plane.Y ? lumaPrediction : plane == Av1Plane.U ? bluePrediction : redPrediction;
                Span<short> residual = workspace.Residual[..(width * height)];
                Buffer2DRegion<TSample> primaryReferencePlane = referenceFrame == Av1ReferenceFrameType.Golden
                    ? this.goldenReference.GetPlane(plane)
                    : this.reference.GetPlane(plane);

                TOperator.PrepareTranslationalInterPrediction(
                    this.source.GetPlane(plane),
                    planeOrigin,
                    primaryReferencePlane,
                    predictionOrigin,
                    horizontalFilter,
                    verticalFilter,
                    sourceColumn & InterpolationPhaseMask,
                    sourceRow & InterpolationPhaseMask,
                    prediction,
                    residual,
                    workspace.PredictionScratch,
                    transformSize,
                    this.picture.Sequence.SequenceHeader.ColorConfig.BitDepth);

                int visibleWidth = Math.Min(width, ((this.source.Width + subsamplingX) >> subsamplingX) - planeOrigin.X);
                int visibleHeight = Math.Min(height, ((this.source.Height + subsamplingY) >> subsamplingY) - planeOrigin.Y);
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

                Av1RateDistortion.ModelPredictionError(
                    planeBlockSize,
                    squaredError,
                    visibleWidth * visibleHeight,
                    acQuantizer,
                    this.bitDepth,
                    this.rateMultiplier,
                    out int planeRate,
                    out long planeDistortion);

                rate += planeRate;
                distortion += planeDistortion;
            }

            return Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
        }

        /// <summary>
        /// Evaluates one inter mode through prediction, transform, coefficient, skip, and distortion selection.
        /// </summary>
        private Av1RateDistortionStatistics EvaluateInterCandidate(
            Av1SymbolEncoder writer,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            bool hasChroma,
            int commonPredictionRate,
            int skipContext,
            int transformPartitionRate,
            Av1ReferenceFrameType referenceFrame,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1PredictionMode mode,
            bool isCompound,
            bool usePreparedPrediction,
            bool forceSkip,
            Av1CompoundType compoundType,
            int compoundWedgeIndex,
            bool compoundWedgeSign,
            Av1DifferenceWeightedMaskType differenceWeightedMaskType,
            int compoundBlendRate,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int referenceMotionVectorIndex,
            in Av1ReferenceMotionVectors referenceMotionVectors,
            Span<TSample> lumaReconstruction,
            Span<int> lumaCoefficients,
            Span<TSample> blueReconstruction,
            Span<int> blueCoefficients,
            Span<TSample> redReconstruction,
            Span<int> redCoefficients,
            out bool skip,
            out Av1EncoderTransformBlockState lumaState,
            out Av1EncoderTransformBlockState blueState,
            out Av1EncoderTransformBlockState redState)
        {
            Av1TransformSize lumaTransformSize = blockSize.GetMaximumTransformSize();
            Av1EncoderInterPredictionWorkspace<TSample> workspace =
                this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            Av1EncoderFrame<TSample>.PlanarView primaryReference = referenceFrame == Av1ReferenceFrameType.Golden
                ? this.goldenReference
                : this.reference;

            Av1TransformBlockContext lumaContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Luminance,
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                blockOrigin,
                blockSize,
                lumaTransformSize);

            this.EvaluateInterPlane(
                writer,
                vector,
                secondaryVector,
                Av1Plane.Y,
                Av1ComponentType.Luminance,
                mode,
                usePreparedPrediction,
                compoundType,
                compoundWedgeIndex,
                compoundWedgeSign,
                differenceWeightedMaskType,
                horizontalFilter,
                verticalFilter,
                primaryReference.GetPlane(Av1Plane.Y),
                this.goldenReference.GetPlane(Av1Plane.Y),
                blockOrigin,
                0,
                0,
                blockSize,
                lumaTransformSize,
                Av1TransformType.AllTransformTypes,
                lumaContext,
                workspace.LumaPrediction,
                workspace.Residual,
                workspace.TransformReconstruction,
                workspace.TransformCoefficients,
                lumaReconstruction,
                lumaCoefficients,
                out lumaState,
                out int lumaRate,
                out long lumaDistortion,
                out long lumaPredictionDistortion);

            // Empty luma transforms signal no transform type. Chroma inherits the decoder's inferred DCT
            // type, not the last searched luma type, so normalize before evaluating either chroma plane.
            if (lumaState.EndOfBlock == 0)
            {
                lumaState.TransformType = Av1TransformType.DctDct;
            }

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(
                blockOrigin,
                subsamplingX,
                subsamplingY);

            Av1TransformSize chromaTransformSize = blockSize.GetMaxUvTransformSize(
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            int blueRate = 0;
            int redRate = 0;
            long blueDistortion = 0;
            long redDistortion = 0;
            long bluePredictionDistortion = 0;
            long redPredictionDistortion = 0;
            blueState = default;
            redState = default;
            if (hasChroma)
            {
                Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                    colorConfig.SubSamplingX,
                    colorConfig.SubSamplingY);

                Av1TransformBlockContext blueContext = Av1TileWriter.GetTransformBlockContexts(
                    Av1ComponentType.Chroma,
                    this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                    chromaOrigin,
                    chromaBlockSize,
                    chromaTransformSize);

                Av1TransformBlockContext redContext = Av1TileWriter.GetTransformBlockContexts(
                    Av1ComponentType.Chroma,
                    this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
                    chromaOrigin,
                    chromaBlockSize,
                    chromaTransformSize);

                Av1TransformType chromaTransformType = lumaState.TransformType;
                Av1TransformSetType chromaTransformSet = Av1SymbolContextHelper.GetExtendedTransformSetType(
                    chromaTransformSize,
                    isInter: true,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet);

                if (!chromaTransformType.IsExtendedSetUsed(chromaTransformSet))
                {
                    chromaTransformType = Av1TransformType.DctDct;
                }

                this.EvaluateInterPlane(
                    writer,
                    vector,
                    secondaryVector,
                    Av1Plane.U,
                    Av1ComponentType.Chroma,
                    mode,
                    usePreparedPrediction,
                    compoundType,
                    compoundWedgeIndex,
                    compoundWedgeSign,
                    differenceWeightedMaskType,
                    horizontalFilter,
                    verticalFilter,
                    primaryReference.GetPlane(Av1Plane.U),
                    this.goldenReference.GetPlane(Av1Plane.U),
                    blockOrigin,
                    subsamplingX,
                    subsamplingY,
                    blockSize,
                    chromaTransformSize,
                    chromaTransformType,
                    blueContext,
                    workspace.BluePrediction,
                    workspace.Residual,
                    workspace.TransformReconstruction,
                    workspace.TransformCoefficients,
                    blueReconstruction,
                    blueCoefficients,
                    out blueState,
                    out blueRate,
                    out blueDistortion,
                    out bluePredictionDistortion);

                this.EvaluateInterPlane(
                    writer,
                    vector,
                    secondaryVector,
                    Av1Plane.V,
                    Av1ComponentType.Chroma,
                    mode,
                    usePreparedPrediction,
                    compoundType,
                    compoundWedgeIndex,
                    compoundWedgeSign,
                    differenceWeightedMaskType,
                    horizontalFilter,
                    verticalFilter,
                    primaryReference.GetPlane(Av1Plane.V),
                    this.goldenReference.GetPlane(Av1Plane.V),
                    blockOrigin,
                    subsamplingX,
                    subsamplingY,
                    blockSize,
                    chromaTransformSize,
                    chromaTransformType,
                    redContext,
                    workspace.RedPrediction,
                    workspace.Residual,
                    workspace.TransformReconstruction,
                    workspace.TransformCoefficients,
                    redReconstruction,
                    redCoefficients,
                    out redState,
                    out redRate,
                    out redDistortion,
                    out redPredictionDistortion);
            }

            int predictionRate = commonPredictionRate;
            if (!forceSkip)
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

            int codedRate = predictionRate +
                writer.GetSkipCost(false, skipContext) +
                transformPartitionRate +
                lumaRate +
                blueRate +
                redRate;

            long codedDistortion = lumaDistortion + blueDistortion + redDistortion;
            Av1RateDistortionStatistics selectedStatistics = new(this.rateMultiplier, codedRate, codedDistortion);
            int skipRate = writer.GetSkipCost(true, skipContext);
            long skipDistortion = lumaPredictionDistortion + bluePredictionDistortion + redPredictionDistortion;

            // All-empty residuals omit the transform tree. Nonempty residuals can also be discarded when
            // prediction alone costs no more; shared prediction syntax must not affect the rounded comparison.
            skip = forceSkip || (lumaState.EndOfBlock == 0 && blueState.EndOfBlock == 0 && redState.EndOfBlock == 0) ||
                Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, skipDistortion) <=
                Av1RateDistortion.GetCost(this.rateMultiplier, codedRate - predictionRate, codedDistortion);

            if (skip)
            {
                selectedStatistics = new(
                    this.rateMultiplier,
                    predictionRate + (forceSkip ? 0 : skipRate),
                    skipDistortion);
                workspace.LumaPrediction[..lumaTransformSize.GetSize2d()].CopyTo(lumaReconstruction);
                lumaCoefficients[..lumaTransformSize.GetSize2d()].Clear();
                lumaState = default;
                if (hasChroma)
                {
                    int chromaSampleCount = chromaTransformSize.GetSize2d();
                    workspace.BluePrediction[..chromaSampleCount].CopyTo(blueReconstruction);
                    workspace.RedPrediction[..chromaSampleCount].CopyTo(redReconstruction);
                    blueCoefficients[..chromaSampleCount].Clear();
                    redCoefficients[..chromaSampleCount].Clear();
                    blueState = default;
                    redState = default;
                }
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
        /// Builds one plane prediction and selects its transform without repeating interpolation for each transform type.
        /// </summary>
        private void EvaluateInterPlane(
            Av1SymbolEncoder writer,
            Av1MotionVector vector,
            Av1MotionVector secondaryVector,
            Av1Plane plane,
            Av1ComponentType componentType,
            Av1PredictionMode predictionMode,
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
            Av1TransformSize transformSize,
            Av1TransformType transformTypeSelection,
            Av1TransformBlockContext blockContext,
            Span<TSample> prediction,
            Span<short> residual,
            Span<TSample> transformReconstruction,
            Span<int> transformCoefficients,
            Span<TSample> selectedReconstruction,
            Span<int> selectedCoefficients,
            out Av1EncoderTransformBlockState selectedState,
            out int selectedRate,
            out long selectedDistortion,
            out long predictionDistortion)
        {
            Point planeOrigin = new(lumaOrigin.X >> subsamplingX, lumaOrigin.Y >> subsamplingY);
            int sourceColumnQ4 = (planeOrigin.X << 4) + (vector.Column << (1 - subsamplingX));
            int sourceRowQ4 = (planeOrigin.Y << 4) + (vector.Row << (1 - subsamplingY));
            Point predictionOrigin = new(sourceColumnQ4 >> 4, sourceRowQ4 >> 4);
            int sampleCount = transformSize.GetSize2d();
            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            if (usePreparedPrediction)
            {
                TOperator.SubtractPrediction(sourcePlane, planeOrigin, prediction[..sampleCount], residual[..sampleCount], transformSize);
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
                        Av1ReferenceFrameType.Last,
                        Av1ReferenceFrameType.Golden,
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
                    transformSize,
                    this.bitDepth,
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
                    transformSize,
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
                    transformSize);
            }

            // Prediction-only error remains available even when every transform quantizes to nonzero coefficients.
            // Normalize squared sample precision with rounding before adding four fractional distortion bits.
            int width = transformSize.GetWidth();
            int visibleWidth = Math.Min(width, sourcePlane.Width - planeOrigin.X);
            int visibleHeight = Math.Min(transformSize.GetHeight(), sourcePlane.Height - planeOrigin.Y);
            long predictionSquaredError = 0;
            if (visibleWidth == width)
            {
                predictionSquaredError = Av1ResidualBuilder.SumSquares(residual[..(width * visibleHeight)]);
            }
            else
            {
                for (int row = 0; row < visibleHeight; row++)
                {
                    predictionSquaredError += Av1ResidualBuilder.SumSquares(residual.Slice(row * width, visibleWidth));
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

            Av1TransformType firstTransformType = transformTypeSelection == Av1TransformType.AllTransformTypes
                ? Av1TransformType.DctDct
                : transformTypeSelection;
            Av1TransformType transformTypeLimit = transformTypeSelection == Av1TransformType.AllTransformTypes
                ? Av1TransformType.AllTransformTypes
                : (Av1TransformType)((int)transformTypeSelection + 1);

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
            for (Av1TransformType transformType = firstTransformType;
                transformType < transformTypeLimit;
                transformType++)
            {
                if (!transformType.IsExtendedSetUsed(transformSetType))
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
                    prediction[..sampleCount],
                    residual[..sampleCount],
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
                    ref candidateState);

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

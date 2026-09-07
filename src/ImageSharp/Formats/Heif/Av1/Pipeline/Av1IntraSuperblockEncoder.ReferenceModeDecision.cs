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
    /// The first effort tier that refines full-pixel motion to quarter-pixel precision.
    /// </summary>
    private const int MinimumSubpixelMotionSearchEffort = 7;

    /// <summary>
    /// The first effort tier that adds the final eighth-pixel refinement step.
    /// </summary>
    private const int MinimumHighPrecisionMotionSearchEffort = 8;

    /// <summary>
    /// One nearest, three near, one global, and three new-motion candidates.
    /// </summary>
    private const int MaximumInterModeCandidateCount = 8;

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

            Span<Av1MotionVector> candidates = stackalloc Av1MotionVector[4];
            Av1IntraBlockCopySearchIndex search = this.picture.IntraBlockCopySearch;
            int candidateCount = search.FindCandidates<TSample, TOperator>(
                lumaSource,
                lumaReconstruction,
                blockOrigin,
                macroBlock.Tile,
                this.picture.Sequence.SequenceHeader,
                writer,
                reference,
                this.rateMultiplier,
                candidates);

            candidateCount += search.FindPixelCandidates<TSample, TOperator>(
                lumaSource,
                lumaReconstruction,
                blockOrigin,
                macroBlock.Tile,
                this.picture.Sequence.SequenceHeader,
                writer,
                reference,
                this.quantization.QIndex[0],
                this.rateMultiplier,
                candidates[candidateCount..]);

            // Hash and full-pixel searches can converge on the same vector. Preserve the first search-order
            // occurrence so repeated vectors do not pay for duplicate transform searches or alter ties.
            int uniqueCandidateCount = 0;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1MotionVector candidate = candidates[candidateIndex];
                bool duplicate = false;
                for (int uniqueIndex = 0; uniqueIndex < uniqueCandidateCount; uniqueIndex++)
                {
                    if (candidate == candidates[uniqueIndex])
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    candidates[uniqueCandidateCount++] = candidate;
                }
            }

            if (uniqueCandidateCount == 0)
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
            for (int candidateIndex = 0; candidateIndex < uniqueCandidateCount; candidateIndex++)
            {
                Av1MotionVector candidate = candidates[candidateIndex];
                this.EvaluateInterPlane(
                    writer,
                    candidate,
                    Av1Plane.Y,
                    Av1ComponentType.Luminance,
                    Av1PredictionMode.DC,
                    usePreparedPrediction: false,
                    Av1InterpolationFilter.Bilinear,
                    Av1InterpolationFilter.Bilinear,
                    lumaReconstruction,
                    blockOrigin,
                    0,
                    0,
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
                        Av1Plane.U,
                        Av1ComponentType.Chroma,
                        Av1PredictionMode.DC,
                        usePreparedPrediction: false,
                        Av1InterpolationFilter.Bilinear,
                        Av1InterpolationFilter.Bilinear,
                        this.reconstruction.GetPlane(Av1Plane.U),
                        blockOrigin,
                        subsamplingX,
                        subsamplingY,
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
                        Av1Plane.V,
                        Av1ComponentType.Chroma,
                        Av1PredictionMode.DC,
                        usePreparedPrediction: false,
                        Av1InterpolationFilter.Bilinear,
                        Av1InterpolationFilter.Bilinear,
                        this.reconstruction.GetPlane(Av1Plane.V),
                        blockOrigin,
                        subsamplingX,
                        subsamplingY,
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
                    writer.GetDisplacementVectorCost(candidate, reference);

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
            out InlineArray3<Av1EncoderTransformBlockState> selectedStates)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize LumaTransformSize = Av1TransformSize.Size8x8;
            const int LastReferenceIndex = 0;
            DebugGuard.IsTrue(modeInfo.Block.BlockSize == BlockSize, "Inter prediction currently uses the prepared 8x8 partition tree.");

            modeInfo.Block.ReferenceFrame = Av1ReferenceFrameType.Last;
            modeInfo.Block.UvMode = Av1ChromaPredictionMode.DC;
            modeInfo.Block.TransformSize = LumaTransformSize;
            modeInfo.Block.UseIntraBlockCopy = false;
            block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 0;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 0;
            block.PredictionUnit.ChromaFromLumaIndex = 0;
            block.PredictionUnit.ChromaFromLumaSigns = 0;

            Av1EncoderInterPredictionWorkspace<TSample> workspace =
                this.blockWorkspace.GetInterPredictionWorkspace<TSample>();

            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
            ref Av1ReferenceMotionVectors referenceMotionVectors = ref this.blockWorkspace.ReferenceMotionVectors;
            referenceMotionVectors.Build(
                this.picture,
                macroBlock,
                modeInfoPosition,
                BlockSize,
                modeInfo.Block.PartitionType,
                this.picture.Sequence.SequenceHeader,
                frameHeader,
                Av1ReferenceFrameType.Last);

            Av1MotionVector globalMotion = frameHeader
                .GetGlobalMotionParameters()[LastReferenceIndex]
                .GetMotionVector(
                    frameHeader.AllowHighPrecisionMotionVector,
                    BlockSize,
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

            int maximumNewIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 1));
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
                writer.GetSingleReferenceCost(Av1ReferenceFrameType.Last, referenceCounts);

            int transformPartitionRate = 0;
            if (frameHeader.TransformMode == Av1TransformMode.Select)
            {
                Av1NeighborArrayUnit<byte> transformContexts = this.picture.TransformFunctionContexts[tileIndex];
                int topIndex = transformContexts.GetTopIndex(blockOrigin);
                int leftIndex = transformContexts.GetLeftIndex(blockOrigin);
                int transformPartitionContext = Av1SymbolContextHelper.GetTransformPartitionContext(
                    transformContexts.Top[topIndex],
                    transformContexts.Left[leftIndex],
                    BlockSize,
                    LumaTransformSize);

                transformPartitionRate = writer.GetTransformPartitionCost(false, transformPartitionContext);
            }

            Av1RateDistortionStatistics selectedStatistics = Av1RateDistortionStatistics.Invalid;
            selectedVector = default;
            selectedStates = default;
            Av1PredictionMode selectedMode = default;
            int selectedReferenceIndex = 0;
            bool selectedSkip = false;
            Av1EncoderTransformBlockState selectedLumaState = default;
            Av1EncoderTransformBlockState selectedBlueState = default;
            Av1EncoderTransformBlockState selectedRedState = default;

            ObuSequenceHeader sequenceHeader = this.picture.Sequence.SequenceHeader;
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
            Buffer2DRegion<TSample> referencePlane = this.reference.GetPlane(Av1Plane.Y);
            int sourceOrigin = ((sourcePlane.Bounds.Y + blockOrigin.Y) * sourcePlane.Stride) + sourcePlane.Bounds.X + blockOrigin.X;
            int referenceOrigin = ((referencePlane.Bounds.Y + blockOrigin.Y) * referencePlane.Stride) + referencePlane.Bounds.X + blockOrigin.X;
            Size frameSize = new(
                this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

            Rectangle frameBounds = Av1MotionVector.GetFrameSearchBounds(
                new Rectangle(blockOrigin, new Size(8)),
                frameSize,
                Math.Min(referencePlane.Bounds.X, referencePlane.Bounds.Y));

            Av1NeighborArrayUnit<byte> coefficientContexts = this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex];
            ReadOnlySpan<byte> aboveContexts = coefficientContexts.Top[coefficientContexts.GetTopIndex(blockOrigin)..];
            ReadOnlySpan<byte> leftContexts = coefficientContexts.Left[coefficientContexts.GetLeftIndex(blockOrigin)..];

            // Frame owners provide contiguous padded planes. Borrow those spans without copying source blocks
            // or reconstructing border samples, and keep the search scratch disjoint from retained inter winners.
            Av1MotionSearchBase.SingleReferenceSearch<TSample, TOperator> motionSearch = new(
                sourcePlane.Buffer.DangerousGetSingleSpan()[sourceOrigin..],
                sourcePlane.Stride,
                referencePlane.Buffer.DangerousGetSingleSpan(),
                referencePlane.Stride,
                referenceOrigin,
                BlockSize,
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
                int column = Math.Clamp(spatial.Column, -(blockOrigin.X + 8 + 4) * 8, (frameSize.Width - blockOrigin.X + 4) * 8);
                int row = Math.Clamp(spatial.Row, -(blockOrigin.Y + 8 + 4) * 8, (frameSize.Height - blockOrigin.Y + 4) * 8);
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
                    tileIndex,
                    block.HasChroma,
                    commonPredictionRate + filterRate,
                    skipContext,
                    transformPartitionRate,
                    candidateVectors[candidateIndex],
                    candidateModes[candidateIndex],
                    writesFilters,
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
                if (candidateStatistics.Cost >= selectedStatistics.Cost)
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

                selectedStatistics = candidateStatistics;
                selectedVector = candidateVectors[candidateIndex];
                selectedMode = candidateModes[candidateIndex];
                selectedHorizontalFilter = horizontalFilter;
                selectedVerticalFilter = verticalFilter;
                selectedReferenceIndex = candidateReferenceIndices[candidateIndex];
                selectedSkip = candidateSkip;
                selectedLumaState = candidateLumaState;
                selectedBlueState = candidateBlueState;
                selectedRedState = candidateRedState;
            }

            // Candidate pixels and coefficients remain scratch. Preserve the transform decisions so final
            // reconstruction can regenerate only the winner after other mode families reuse this storage.
            selectedStates[0] = selectedLumaState;
            selectedStates[1] = selectedBlueState;
            selectedStates[2] = selectedRedState;

            modeInfo.Block.Mode = selectedMode;
            modeInfo.Block.Skip = selectedSkip;
            modeInfo.Block.VerticalInterpolationFilter = selectedVerticalFilter;
            modeInfo.Block.HorizontalInterpolationFilter = selectedHorizontalFilter;
            block.ReferenceMotionVectorIndex = selectedReferenceIndex;
            return selectedStatistics;
        }

        /// <summary>
        /// Reconstructs the selected inter mode after intra trials have reused its arithmetic storage.
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The selected prediction and interpolation syntax.</param>
        /// <param name="block">The selected block parameters.</param>
        /// <param name="vector">The selected motion vector in eighth-luma-sample units.</param>
        /// <param name="states">The selected transform choices, indexed by plane.</param>
        private void ReconstructSelectedInterBlock(
            Point blockOrigin,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            ReadOnlySpan<Av1EncoderTransformBlockState> states)
        {
            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
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

                int sampleCount = transformSize.GetSize2d();
                Span<TSample> reconstruction = workspace.LumaCandidateReconstruction[..sampleCount];
                Span<int> coefficients = workspace.LumaCandidateCoefficients[..sampleCount];
                Span<TSample> prediction = workspace.LumaPrediction[..sampleCount];
                Span<short> residual = workspace.Residual[..sampleCount];

                // Convert eighth-luma-sample motion into the plane's sixteenth-sample interpolation
                // coordinates. The low four bits carry the phase; the remaining bits locate the reference.
                int columnQ4 = (planeOrigin.X << 4) + (vector.Column << (1 - subX));
                int rowQ4 = (planeOrigin.Y << 4) + (vector.Row << (1 - subY));
                TOperator.PrepareTranslationalInterPrediction(
                    this.source.GetPlane(plane),
                    planeOrigin,
                    this.reference.GetPlane(plane),
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

                Av1EncoderTransformBlockState state = default;
                if (modeInfo.Block.Skip || states[planeIndex].EndOfBlock == 0)
                {
                    // An empty transform retains prediction even when other planes have coded residuals.
                    // Re-quantizing its inferred DCT could otherwise introduce coefficients absent in the winner.
                    reconstruction = prediction;
                    coefficients.Clear();
                }
                else
                {
                    // Regenerate only the selected transform. Motion, transform choice, coefficient-rate
                    // measurement, and skip decisions are complete before this final reconstruction.
                    _ = TOperator.EncodePredictionCandidate(
                        this.blockWorkspace,
                        this.source.GetPlane(plane),
                        planeOrigin,
                        prediction,
                        residual,
                        reconstruction,
                        transformSize.GetWidth(),
                        coefficients,
                        transformSize,
                        states[planeIndex].TransformType,
                        plane,
                        this.quantization.QIndex[0],
                        this.quantization.DeltaQDc[planeIndex],
                        this.quantization.DeltaQAc[planeIndex],
                        this.bitDepth,
                        ref state);
                }

                int codedArea = planeIndex == 0 ? this.codedAreaLuma : this.codedAreaChroma;
                int transformIndex = codedArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                Span<int> retainedCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, plane);
                Span<Av1EncoderTransformBlockState> retainedStates = this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane);
                CopyCandidate(
                    reconstruction,
                    coefficients,
                    this.reconstruction.GetPlane(plane),
                    planeOrigin,
                    retainedCoefficients[codedArea..],
                    transformSize,
                    state,
                    ref retainedStates[transformIndex]);
            }
        }

        /// <summary>
        /// Ranks a filter pair from visible prediction error using the reference curve model, without transforming samples.
        /// </summary>
        private long GetInterFilterModelCost(
            Av1MotionVector vector,
            Point blockOrigin,
            bool hasChroma,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int filterRate,
            Span<TSample> lumaPrediction,
            Span<TSample> bluePrediction,
            Span<TSample> redPrediction)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
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
                Av1BlockSize planeBlockSize = BlockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
                Av1TransformSize transformSize = BlockSize.GetMaxUvTransformSize(subsamplingX != 0, subsamplingY != 0);
                int width = transformSize.GetWidth();
                int height = transformSize.GetHeight();
                Point planeOrigin = new(blockOrigin.X >> subsamplingX, blockOrigin.Y >> subsamplingY);
                int sourceColumn = (planeOrigin.X << InterpolationPrecisionBits) + (vector.Column << (1 - subsamplingX));
                int sourceRow = (planeOrigin.Y << InterpolationPrecisionBits) + (vector.Row << (1 - subsamplingY));
                Point predictionOrigin = new(sourceColumn >> InterpolationPrecisionBits, sourceRow >> InterpolationPrecisionBits);
                Span<TSample> prediction = plane == Av1Plane.Y ? lumaPrediction : plane == Av1Plane.U ? bluePrediction : redPrediction;
                Span<short> residual = workspace.Residual[..(width * height)];
                TOperator.PrepareTranslationalInterPrediction(
                    this.source.GetPlane(plane),
                    planeOrigin,
                    this.reference.GetPlane(plane),
                    predictionOrigin,
                    horizontalFilter,
                    verticalFilter,
                    sourceColumn & InterpolationPhaseMask,
                    sourceRow & InterpolationPhaseMask,
                    prediction,
                    residual,
                    workspace.PredictionScratch,
                    transformSize,
                    this.bitDepth);

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
            ushort tileIndex,
            bool hasChroma,
            int commonPredictionRate,
            int skipContext,
            int transformPartitionRate,
            Av1MotionVector vector,
            Av1PredictionMode mode,
            bool usePreparedPrediction,
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
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize LumaTransformSize = Av1TransformSize.Size8x8;
            Av1EncoderInterPredictionWorkspace<TSample> workspace =
                this.blockWorkspace.GetInterPredictionWorkspace<TSample>();

            Av1TransformBlockContext lumaContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Luminance,
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                blockOrigin,
                BlockSize,
                LumaTransformSize);

            this.EvaluateInterPlane(
                writer,
                vector,
                Av1Plane.Y,
                Av1ComponentType.Luminance,
                mode,
                usePreparedPrediction,
                horizontalFilter,
                verticalFilter,
                this.reference.GetPlane(Av1Plane.Y),
                blockOrigin,
                0,
                0,
                LumaTransformSize,
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

            Av1TransformSize chromaTransformSize = BlockSize.GetMaxUvTransformSize(
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
                Av1BlockSize chromaBlockSize = BlockSize.GetSubsampled(
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
                    Av1Plane.U,
                    Av1ComponentType.Chroma,
                    mode,
                    usePreparedPrediction,
                    horizontalFilter,
                    verticalFilter,
                    this.reference.GetPlane(Av1Plane.U),
                    blockOrigin,
                    subsamplingX,
                    subsamplingY,
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
                    Av1Plane.V,
                    Av1ComponentType.Chroma,
                    mode,
                    usePreparedPrediction,
                    horizontalFilter,
                    verticalFilter,
                    this.reference.GetPlane(Av1Plane.V),
                    blockOrigin,
                    subsamplingX,
                    subsamplingY,
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

            int predictionRate = commonPredictionRate +
                this.GetInterModeRate(
                    writer,
                    mode,
                    vector,
                    referenceMotionVectorIndex,
                    in referenceMotionVectors);

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
            skip = (lumaState.EndOfBlock == 0 && blueState.EndOfBlock == 0 && redState.EndOfBlock == 0) ||
                Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, skipDistortion) <=
                Av1RateDistortion.GetCost(this.rateMultiplier, codedRate - predictionRate, codedDistortion);

            if (skip)
            {
                selectedStatistics = new(this.rateMultiplier, predictionRate + skipRate, skipDistortion);
                workspace.LumaPrediction[..LumaTransformSize.GetSize2d()].CopyTo(lumaReconstruction);
                lumaCoefficients[..LumaTransformSize.GetSize2d()].Clear();
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
            Av1Plane plane,
            Av1ComponentType componentType,
            Av1PredictionMode predictionMode,
            bool usePreparedPrediction,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Buffer2DRegion<TSample> referencePlane,
            Point lumaOrigin,
            int subsamplingX,
            int subsamplingY,
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
                    prediction[..sampleCount],
                    residual[..sampleCount],
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
            long predictionSquaredError = Av1ResidualBuilder.SumSquares(residual[..sampleCount]);
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

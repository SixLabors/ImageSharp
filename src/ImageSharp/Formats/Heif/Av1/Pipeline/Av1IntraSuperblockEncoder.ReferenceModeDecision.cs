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
/// Provides full rate-distortion selection for reference-frame and intra-block-copy candidates.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// The first effort tier that searches a block-local motion vector.
    /// </summary>
    private const int MinimumInterMotionSearchEffort = 6;

    /// <summary>
    /// The smallest full-pixel radius used by block-local inter search.
    /// </summary>
    private const int MinimumInterMotionSearchRadius = 4;

    /// <summary>
    /// The first effort tier that refines full-pixel motion to quarter-pixel precision.
    /// </summary>
    private const int MinimumSubpixelMotionSearchEffort = 7;

    /// <summary>
    /// The first effort tier that adds the final eighth-pixel refinement step.
    /// </summary>
    private const int MinimumHighPrecisionMotionSearchEffort = 8;

    /// <summary>
    /// The physical border reserved on each side for fractional eight-tap filtering.
    /// </summary>
    private const int FractionalInterpolationBorder = 4;

    /// <summary>
    /// The number of cardinal and diagonal candidates examined at each search step.
    /// </summary>
    private const int InterMotionSearchDirectionCount = 8;

    /// <summary>
    /// One nearest, three near, one global, and three new-motion candidates.
    /// </summary>
    private const int MaximumInterModeCandidateCount = 8;

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        private long SelectIntraBlockCopy(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            long regularCost,
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
                return regularCost;
            }

            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            long bestCost = regularCost;
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
                    out bool hasEmptyLuma,
                    out Av1EncoderTransformBlockState emptyLumaState,
                    out long emptyLumaDistortion);

                int blueRate = 0;
                int redRate = 0;
                long blueDistortion = 0;
                long redDistortion = 0;
                long emptyBlueDistortion = 0;
                long emptyRedDistortion = 0;
                bool hasEmptyBlue = true;
                bool hasEmptyRed = true;
                Av1EncoderTransformBlockState blueCandidateState = default;
                Av1EncoderTransformBlockState redCandidateState = default;
                Av1EncoderTransformBlockState emptyBlueState = default;
                Av1EncoderTransformBlockState emptyRedState = default;
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
                        out hasEmptyBlue,
                        out emptyBlueState,
                        out emptyBlueDistortion);

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
                        out hasEmptyRed,
                        out emptyRedState,
                        out emptyRedDistortion);
                }

                int displacementRate = writer.GetDisplacementVectorCost(candidate, reference);
                int candidateRate = writer.GetUseIntraBlockCopyCost(true) +
                    displacementRate +
                    writer.GetSkipCost(false, skipContext) +
                    transformPartitionRate +
                    lumaRate +
                    blueRate +
                    redRate;

                long candidateDistortion = lumaDistortion + blueDistortion + redDistortion;
                long candidateCost = Av1RateDistortion.GetCost(this.rateMultiplier, candidateRate, candidateDistortion);
                bool candidateSkip = false;

                // The skip alternative is available only when every coded plane has an empty transform. Its
                // distortion comes from prediction alone and its rate excludes the transform tree and coefficients.
                if (hasEmptyLuma && hasEmptyBlue && hasEmptyRed)
                {
                    int skipRate = writer.GetUseIntraBlockCopyCost(true) +
                        displacementRate +
                        writer.GetSkipCost(true, skipContext);

                    long skipDistortion = emptyLumaDistortion + emptyBlueDistortion + emptyRedDistortion;
                    long skipCost = Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, skipDistortion);
                    if (skipCost < candidateCost)
                    {
                        candidateCost = skipCost;
                        candidateSkip = true;
                    }
                }

                // Conventional intra and earlier IBC vectors retain strict search-order precedence on equal RD.
                if (candidateCost >= bestCost)
                {
                    continue;
                }

                bestCost = candidateCost;
                hasSelectedCandidate = true;
                selectedSkip = candidateSkip;
                selectedVector = candidate;
                if (candidateSkip)
                {
                    workspace.LumaPrediction.CopyTo(workspace.SelectedLumaReconstruction);
                    workspace.SelectedLumaCoefficients.Clear();
                    selectedLumaState = emptyLumaState;
                    if (!this.source.IsMonochrome)
                    {
                        int chromaSampleCount = chromaTransformSize.GetSize2d();
                        workspace.BluePrediction[..chromaSampleCount].CopyTo(workspace.SelectedBlueReconstruction);
                        workspace.RedPrediction[..chromaSampleCount].CopyTo(workspace.SelectedRedReconstruction);
                        workspace.SelectedBlueCoefficients[..chromaSampleCount].Clear();
                        workspace.SelectedRedCoefficients[..chromaSampleCount].Clear();
                        selectedBlueState = emptyBlueState;
                        selectedRedState = emptyRedState;
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
                return bestCost;
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
            return bestCost;
        }

        /// <summary>
        /// Compares the retained intra result with an inter candidate without disturbing the intra result on loss.
        /// </summary>
        private long SelectInterPrediction(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            long regularCost,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            Av1MacroBlockModeInfo interModeInfo = modeInfo;
            Av1EncoderBlockStruct interBlock = block;
            Av1EncoderPaletteInfo interPaletteInfo = default;
            long selectedCost = this.SelectInterBlock(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                regularCost,
                ref interModeInfo,
                ref interBlock,
                ref interPaletteInfo);

            if (selectedCost < regularCost)
            {
                modeInfo = interModeInfo;
                block = interBlock;
                paletteInfo = interPaletteInfo;
            }

            return selectedCost;
        }

        /// <summary>
        /// Evaluates the supported LAST_FRAME modes and publishes only a strict improvement over the intra result.
        /// </summary>
        private long SelectInterBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            long regularCost,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
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
            paletteInfo = default;

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
            if (this.effort >= MinimumInterMotionSearchEffort)
            {
                // Predictor-stack modes precede global and new motion so strict ties retain the reference order.
                candidateVectors[candidateCount] = referenceMotionVectors.Nearest;
                candidateModes[candidateCount] = Av1PredictionMode.NearestMotionVector;
                candidateReferenceIndices[candidateCount++] = 0;

                int maximumNearIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 2));
                for (int referenceIndex = 0; referenceIndex <= maximumNearIndex; referenceIndex++)
                {
                    candidateVectors[candidateCount] = referenceMotionVectors.GetNearReference(referenceIndex);
                    candidateModes[candidateCount] = Av1PredictionMode.NearMotionVector;
                    candidateReferenceIndices[candidateCount++] = (byte)referenceIndex;
                }
            }

            candidateVectors[candidateCount] = globalMotion;
            candidateModes[candidateCount] = Av1PredictionMode.GlobalMotionVector;
            candidateReferenceIndices[candidateCount++] = 0;
            if (this.effort >= MinimumInterMotionSearchEffort)
            {
                int maximumNewIndex = Math.Min(2, Math.Max(0, referenceMotionVectors.Count - 1));
                for (int referenceIndex = 0; referenceIndex <= maximumNewIndex; referenceIndex++)
                {
                    Av1MotionVector newReference = referenceMotionVectors.GetNewReference(referenceIndex);
                    Av1MotionVector searched = this.FindInterMotionVector(
                        writer,
                        blockOrigin,
                        newReference,
                        referenceIndex);

                    // Equal prediction vectors can carry different DRL and mode costs. Preserve each syntax choice
                    // as an independent candidate instead of deduplicating solely by reconstructed pixels.
                    candidateVectors[candidateCount] = searched;
                    candidateModes[candidateCount] = Av1PredictionMode.NewMotionVector;
                    candidateReferenceIndices[candidateCount++] = (byte)referenceIndex;
                }
            }

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

            long selectedCost = regularCost;
            Av1MotionVector selectedVector = default;
            Av1PredictionMode selectedMode = default;
            int selectedReferenceIndex = 0;
            bool selectedSkip = false;
            Av1EncoderTransformBlockState selectedLumaState = default;
            Av1EncoderTransformBlockState selectedBlueState = default;
            Av1EncoderTransformBlockState selectedRedState = default;
            bool hasInterWinner = false;

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

            // Rank interpolation families with prediction-error modeling before running a full transform search.
            // The selected inter reconstruction remains untouched while two existing prediction views alternate.
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
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

                long candidateCost = this.EvaluateInterCandidate(
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

                // Strict replacement preserves predictor-stack, global, then new-motion order on equal RD cost.
                if (candidateCost >= selectedCost)
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

                selectedCost = candidateCost;
                selectedVector = candidateVectors[candidateIndex];
                selectedMode = candidateModes[candidateIndex];
                selectedHorizontalFilter = horizontalFilter;
                selectedVerticalFilter = verticalFilter;
                selectedReferenceIndex = candidateReferenceIndices[candidateIndex];
                selectedSkip = candidateSkip;
                selectedLumaState = candidateLumaState;
                selectedBlueState = candidateBlueState;
                selectedRedState = candidateRedState;
                hasInterWinner = true;
            }

            // Inter trials never overwrite retained picture state. The complete intra result remains authoritative
            // when no inter candidate strictly improves its rate-distortion cost.
            if (!hasInterWinner)
            {
                return regularCost;
            }

            Span<int> retainedLumaCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.Y);
            Span<Av1EncoderTransformBlockState> retainedLumaTransformBlocks =
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.Y);

            int lumaTransformIndex = this.codedAreaLuma /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            CopyCandidate(
                selectedLumaReconstruction,
                selectedLumaCoefficients,
                this.reconstruction.GetPlane(Av1Plane.Y),
                blockOrigin,
                retainedLumaCoefficients[this.codedAreaLuma..],
                LumaTransformSize,
                selectedLumaState,
                ref retainedLumaTransformBlocks[lumaTransformIndex]);

            if (block.HasChroma)
            {
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

                Span<int> retainedBlueCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.U);
                Span<int> retainedRedCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.V);
                Span<Av1EncoderTransformBlockState> retainedBlueTransformBlocks =
                    this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.U);

                Span<Av1EncoderTransformBlockState> retainedRedTransformBlocks =
                    this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.V);

                int chromaTransformIndex = this.codedAreaChroma /
                    Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

                CopyCandidate(
                    selectedBlueReconstruction,
                    selectedBlueCoefficients,
                    this.reconstruction.GetPlane(Av1Plane.U),
                    chromaOrigin,
                    retainedBlueCoefficients[this.codedAreaChroma..],
                    chromaTransformSize,
                    selectedBlueState,
                    ref retainedBlueTransformBlocks[chromaTransformIndex]);

                CopyCandidate(
                    selectedRedReconstruction,
                    selectedRedCoefficients,
                    this.reconstruction.GetPlane(Av1Plane.V),
                    chromaOrigin,
                    retainedRedCoefficients[this.codedAreaChroma..],
                    chromaTransformSize,
                    selectedRedState,
                    ref retainedRedTransformBlocks[chromaTransformIndex]);
            }

            modeInfo.Block.Mode = selectedMode;
            modeInfo.Block.Skip = selectedSkip;
            modeInfo.Block.VerticalInterpolationFilter = selectedVerticalFilter;
            modeInfo.Block.HorizontalInterpolationFilter = selectedHorizontalFilter;
            block.ReferenceMotionVectorIndex = selectedReferenceIndex;
            this.picture.SetDisplacementVector(modeInfoPosition, selectedVector);
            return selectedCost;
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

                // This view includes coded alignment samples, matching libaom when do_border_pad is false.
                // Its conditional border-padding policy is not implemented here; these are not visible-frame bounds.
                // Full blocks use one SIMD reduction; only a partial right edge needs row-sized reductions.
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
        private long EvaluateInterCandidate(
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
                out bool hasEmptyLuma,
                out Av1EncoderTransformBlockState emptyLumaState,
                out long emptyLumaDistortion);

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
            long emptyBlueDistortion = 0;
            long emptyRedDistortion = 0;
            bool hasEmptyBlue = true;
            bool hasEmptyRed = true;
            blueState = default;
            redState = default;
            Av1EncoderTransformBlockState emptyBlueState = default;
            Av1EncoderTransformBlockState emptyRedState = default;
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
                    out hasEmptyBlue,
                    out emptyBlueState,
                    out emptyBlueDistortion);

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
                    out hasEmptyRed,
                    out emptyRedState,
                    out emptyRedDistortion);
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
            long selectedCost = Av1RateDistortion.GetCost(this.rateMultiplier, codedRate, codedDistortion);
            skip = false;
            if (hasEmptyLuma && hasEmptyBlue && hasEmptyRed)
            {
                int skipRate = predictionRate + writer.GetSkipCost(true, skipContext);
                long skipDistortion = emptyLumaDistortion + emptyBlueDistortion + emptyRedDistortion;
                long skipCost = Av1RateDistortion.GetCost(this.rateMultiplier, skipRate, skipDistortion);
                if (skipCost < selectedCost)
                {
                    selectedCost = skipCost;
                    skip = true;
                    workspace.LumaPrediction[..LumaTransformSize.GetSize2d()].CopyTo(lumaReconstruction);
                    lumaCoefficients[..LumaTransformSize.GetSize2d()].Clear();
                    lumaState = emptyLumaState;
                    if (hasChroma)
                    {
                        int chromaSampleCount = chromaTransformSize.GetSize2d();
                        workspace.BluePrediction[..chromaSampleCount].CopyTo(blueReconstruction);
                        workspace.RedPrediction[..chromaSampleCount].CopyTo(redReconstruction);
                        blueCoefficients[..chromaSampleCount].Clear();
                        redCoefficients[..chromaSampleCount].Clear();
                        blueState = emptyBlueState;
                        redState = emptyRedState;
                    }
                }
            }

            return selectedCost;
        }

        /// <summary>
        /// Searches a bounded full-pixel neighborhood around the spatial reference vector.
        /// </summary>
        /// <param name="writer">The live tile entropy model used to measure vector syntax.</param>
        /// <param name="blockOrigin">The current 8x8 luma origin.</param>
        /// <param name="referenceVector">The differential reference from the spatial candidate stack.</param>
        /// <param name="referenceMotionVectorIndex">The selected dynamic-reference-list entry.</param>
        /// <returns>The lowest-cost full-pixel vector found by the effort-scaled search.</returns>
        private Av1MotionVector FindInterMotionVector(
            Av1SymbolEncoder writer,
            Point blockOrigin,
            Av1MotionVector referenceVector,
            int referenceMotionVectorIndex)
        {
            int effortShift = this.effort - MinimumInterMotionSearchEffort;
            int searchRadius = Math.Min(
                MinimumInterMotionSearchRadius << effortShift,
                Av1EncoderFrame<TSample>.LumaBorder);

            int referenceColumn = referenceVector.Column >> Av1MotionVector.SubpixelBits;
            int referenceRow = referenceVector.Row >> Av1MotionVector.SubpixelBits;
            int minimumColumn = Math.Max(-Av1EncoderFrame<TSample>.LumaBorder, referenceColumn - searchRadius);
            int maximumColumn = Math.Min(Av1EncoderFrame<TSample>.LumaBorder, referenceColumn + searchRadius);
            int minimumRow = Math.Max(-Av1EncoderFrame<TSample>.LumaBorder, referenceRow - searchRadius);
            int maximumRow = Math.Min(Av1EncoderFrame<TSample>.LumaBorder, referenceRow + searchRadius);
            Point best = new(
                Av1Math.Clamp(referenceColumn, minimumColumn, maximumColumn),
                Av1Math.Clamp(referenceRow, minimumRow, maximumRow));

            ref Av1ReferenceMotionVectors referenceMotionVectors = ref this.blockWorkspace.ReferenceMotionVectors;
            Av1MotionVector bestVector = new(
                best.Y * Av1MotionVector.SubpixelScale,
                best.X * Av1MotionVector.SubpixelScale);

            long bestCost = this.GetInterMotionCandidateCost(
                writer,
                blockOrigin,
                bestVector,
                Av1PredictionMode.NewMotionVector,
                referenceMotionVectorIndex,
                in referenceMotionVectors);

            for (int step = searchRadius; step > 0; step >>= 1)
            {
                Point stageBest = best;
                long stageBestCost = bestCost;
                for (int directionIndex = 0; directionIndex < InterMotionSearchDirectionCount; directionIndex++)
                {
                    Point direction = GetInterMotionSearchDirection(directionIndex);
                    Point candidate = new(
                        best.X + (direction.X * step),
                        best.Y + (direction.Y * step));

                    if (candidate.X < minimumColumn || candidate.X > maximumColumn ||
                        candidate.Y < minimumRow || candidate.Y > maximumRow)
                    {
                        continue;
                    }

                    Av1MotionVector candidateVector = new(
                        candidate.Y * Av1MotionVector.SubpixelScale,
                        candidate.X * Av1MotionVector.SubpixelScale);

                    long candidateCost = this.GetInterMotionCandidateCost(
                        writer,
                        blockOrigin,
                        candidateVector,
                        Av1PredictionMode.NewMotionVector,
                        referenceMotionVectorIndex,
                        in referenceMotionVectors);

                    // Strict replacement preserves the earlier reference-centered search position on ties.
                    if (candidateCost < stageBestCost)
                    {
                        stageBestCost = candidateCost;
                        stageBest = candidate;
                    }
                }

                best = stageBest;
                bestCost = stageBestCost;
            }

            bestVector = new(
                best.Y * Av1MotionVector.SubpixelScale,
                best.X * Av1MotionVector.SubpixelScale);

            if (this.effort < MinimumSubpixelMotionSearchEffort)
            {
                return bestVector;
            }

            int minimumSubpixel = (-Av1EncoderFrame<TSample>.LumaBorder + FractionalInterpolationBorder) *
                Av1MotionVector.SubpixelScale;

            int maximumSubpixel = (Av1EncoderFrame<TSample>.LumaBorder - FractionalInterpolationBorder) *
                Av1MotionVector.SubpixelScale;

            if (bestVector.Column < minimumSubpixel || bestVector.Column > maximumSubpixel ||
                bestVector.Row < minimumSubpixel || bestVector.Row > maximumSubpixel)
            {
                return bestVector;
            }

            int finalStep = this.effort >= MinimumHighPrecisionMotionSearchEffort ? 1 : 2;
            for (int step = Av1MotionVector.SubpixelScale >> 1; step >= finalStep; step >>= 1)
            {
                Av1MotionVector stageBest = bestVector;
                long stageBestCost = bestCost;
                for (int directionIndex = 0; directionIndex < InterMotionSearchDirectionCount; directionIndex++)
                {
                    Point direction = GetInterMotionSearchDirection(directionIndex);
                    Av1MotionVector candidate = new(
                        bestVector.Row + (direction.Y * step),
                        bestVector.Column + (direction.X * step));

                    if (candidate.Column < minimumSubpixel || candidate.Column > maximumSubpixel ||
                        candidate.Row < minimumSubpixel || candidate.Row > maximumSubpixel)
                    {
                        continue;
                    }

                    long candidateCost = this.GetInterMotionCandidateCost(
                        writer,
                        blockOrigin,
                        candidate,
                        Av1PredictionMode.NewMotionVector,
                        referenceMotionVectorIndex,
                        in referenceMotionVectors);

                    // Each precision stage remains centered on its incoming winner; strict replacement keeps
                    // the integer or coarser fractional vector when an interpolated candidate only ties it.
                    if (candidateCost < stageBestCost)
                    {
                        stageBestCost = candidateCost;
                        stageBest = candidate;
                    }
                }

                bestVector = stageBest;
                bestCost = stageBestCost;
            }

            return bestVector;
        }

        /// <summary>
        /// Combines normalized prediction error with the exact mode and vector syntax rate.
        /// </summary>
        /// <param name="writer">The live tile entropy model.</param>
        /// <param name="blockOrigin">The current 8x8 luma origin.</param>
        /// <param name="vector">The candidate motion vector.</param>
        /// <param name="mode">The candidate single-reference inter mode.</param>
        /// <param name="referenceMotionVectorIndex">The selected dynamic-reference-list entry.</param>
        /// <param name="referenceMotionVectors">The current spatial candidate stack.</param>
        /// <returns>The rate-distortion search cost.</returns>
        private long GetInterMotionCandidateCost(
            Av1SymbolEncoder writer,
            Point blockOrigin,
            Av1MotionVector vector,
            Av1PredictionMode mode,
            int referenceMotionVectorIndex,
            in Av1ReferenceMotionVectors referenceMotionVectors)
        {
            long predictionError;
            if (((vector.Row | vector.Column) & (Av1MotionVector.SubpixelScale - 1)) == 0)
            {
                Point predictionOrigin = new(
                    blockOrigin.X + (vector.Column >> Av1MotionVector.SubpixelBits),
                    blockOrigin.Y + (vector.Row >> Av1MotionVector.SubpixelBits));

                predictionError = TOperator.GetInterPredictionError(
                    this.source.GetPlane(Av1Plane.Y),
                    blockOrigin,
                    this.reference.GetPlane(Av1Plane.Y),
                    predictionOrigin,
                    this.bitDepth);
            }
            else
            {
                const Av1TransformSize SearchTransformSize = Av1TransformSize.Size8x8;
                Av1EncoderInterPredictionWorkspace<TSample> workspace =
                    this.blockWorkspace.GetInterPredictionWorkspace<TSample>();

                int sourceColumnQ4 = (blockOrigin.X << 4) + (vector.Column << 1);
                int sourceRowQ4 = (blockOrigin.Y << 4) + (vector.Row << 1);
                Point predictionOrigin = new(sourceColumnQ4 >> 4, sourceRowQ4 >> 4);
                ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;

                // Fractional candidates must pass through the same interpolation and residual kernels used by
                // final reconstruction; comparing only their integer origins would choose the wrong phase.
                TOperator.PrepareTranslationalInterPrediction(
                    this.source.GetPlane(Av1Plane.Y),
                    blockOrigin,
                    this.reference.GetPlane(Av1Plane.Y),
                    predictionOrigin,
                    frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable ? Av1InterpolationFilter.Regular : frameHeader.InterpolationFilter,
                    frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable ? Av1InterpolationFilter.Regular : frameHeader.InterpolationFilter,
                    sourceColumnQ4 & 15,
                    sourceRowQ4 & 15,
                    workspace.LumaPrediction,
                    workspace.Residual,
                    workspace.PredictionScratch,
                    SearchTransformSize,
                    this.bitDepth);

                predictionError = Av1ResidualBuilder.SumSquares(workspace.Residual);
                int normalizationShift = (this.bitDepth.GetBitCount() - 8) * 2;
                if (normalizationShift != 0)
                {
                    predictionError = (predictionError + (1L << (normalizationShift - 1))) >>
                        normalizationShift;
                }
            }

            int rate = this.GetInterModeRate(
                writer,
                mode,
                vector,
                referenceMotionVectorIndex,
                in referenceMotionVectors);

            return Av1RateDistortion.GetCost(this.rateMultiplier, rate, predictionError);
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
            return rate + writer.GetMotionVectorCost(
                vector,
                reference,
                this.picture.Parent.FrameHeader.MotionVectorPrecision);
        }

        /// <summary>
        /// Gets one cardinal or diagonal search direction in stable reference order.
        /// </summary>
        /// <param name="index">The zero-based direction index.</param>
        /// <returns>The unit full-pixel direction.</returns>
        private static Point GetInterMotionSearchDirection(int index)
            => index switch
            {
                0 => new Point(0, -1),
                1 => new Point(0, 1),
                2 => new Point(-1, 0),
                3 => new Point(1, 0),
                4 => new Point(-1, -1),
                5 => new Point(1, 1),
                6 => new Point(1, -1),
                _ => new Point(-1, 1)
            };

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
            out bool hasEmptyTransform,
            out Av1EncoderTransformBlockState emptyState,
            out long emptyDistortion)
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

            // Motion compensation and subtraction do not depend on transform type. Keep them outside the
            // transform loop so exhaustive luma search traverses the source and reference blocks only once.
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
            hasEmptyTransform = false;
            emptyState = default;
            emptyDistortion = 0;

            // The candidate and best spans alternate ownership whenever a transform improves the result.
            // This mirrors the reference's buffer-pointer swap and replaces a copy on every improvement
            // with at most one normalization copy after the transform search.
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

                if (candidateState.EndOfBlock == 0 &&
                    (!hasEmptyTransform || candidateDistortion < emptyDistortion))
                {
                    hasEmptyTransform = true;
                    emptyState = candidateState;
                    emptyDistortion = candidateDistortion;
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

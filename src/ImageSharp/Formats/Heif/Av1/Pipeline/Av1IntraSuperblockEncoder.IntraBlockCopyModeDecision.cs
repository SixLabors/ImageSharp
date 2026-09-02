// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Provides full rate-distortion selection for intra-block-copy candidates.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        private void SelectIntraBlockCopy(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            long regularModeCost,
            int regularEmptyTransformRate,
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
                return;
            }

            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            int regularRateAdjustment = writer.GetUseIntraBlockCopyCost(false) +
                writer.GetSkipCost(modeInfo.Block.Skip, skipContext);

            if (modeInfo.Block.Skip)
            {
                regularRateAdjustment -= regularEmptyTransformRate;
            }

            long bestCost = regularModeCost + Av1RateDistortion.GetCost(this.rateMultiplier, regularRateAdjustment, 0);
            bool hasSelectedCandidate = false;
            bool selectedSkip = false;
            Av1MotionVector selectedVector = default;
            Av1EncoderTransformBlockState selectedLumaState = default;
            Av1EncoderTransformBlockState selectedBlueState = default;
            Av1EncoderTransformBlockState selectedRedState = default;
            Av1EncoderIntraBlockCopyWorkspace<TSample> workspace =
                this.blockWorkspace.GetIntraBlockCopyWorkspace<TSample>();

            Av1TransformBlockContext lumaContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Luminance,
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                blockOrigin,
                BlockSize,
                LumaTransformSize);

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

            for (int candidateIndex = 0; candidateIndex < uniqueCandidateCount; candidateIndex++)
            {
                Av1MotionVector candidate = candidates[candidateIndex];
                this.EvaluateIntraBlockCopyPlane(
                    writer,
                    candidate,
                    Av1Plane.Y,
                    Av1ComponentType.Luminance,
                    blockOrigin,
                    0,
                    0,
                    LumaTransformSize,
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
                    this.EvaluateIntraBlockCopyPlane(
                        writer,
                        candidate,
                        Av1Plane.U,
                        Av1ComponentType.Chroma,
                        blockOrigin,
                        subsamplingX,
                        subsamplingY,
                        chromaTransformSize,
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

                    this.EvaluateIntraBlockCopyPlane(
                        writer,
                        candidate,
                        Av1Plane.V,
                        Av1ComponentType.Chroma,
                        blockOrigin,
                        subsamplingX,
                        subsamplingY,
                        chromaTransformSize,
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
                    lumaRate +
                    blueRate +
                    redRate;

                long candidateDistortion = lumaDistortion + blueDistortion + redDistortion;
                long candidateCost = Av1RateDistortion.GetCost(this.rateMultiplier, candidateRate, candidateDistortion);
                bool candidateSkip = false;
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
                return;
            }

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
            modeInfo.Block.Skip = selectedSkip;
            modeInfo.Block.UseIntraBlockCopy = true;
            block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 0;
            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 0;
            block.PredictionUnit.ChromaFromLumaIndex = 0;
            block.PredictionUnit.ChromaFromLumaSigns = 0;
            paletteInfo = default;
            this.picture.SetDisplacementVector(modeInfoPosition, selectedVector);
        }

        private void EvaluateIntraBlockCopyPlane(
            Av1SymbolEncoder writer,
            Av1MotionVector vector,
            Av1Plane plane,
            Av1ComponentType componentType,
            Point lumaOrigin,
            int subsamplingX,
            int subsamplingY,
            Av1TransformSize transformSize,
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
            Buffer2DRegion<TSample> reconstructionPlane = this.reconstruction.GetPlane(plane);
            TOperator.PrepareIntraBlockCopy(
                sourcePlane,
                planeOrigin,
                reconstructionPlane,
                predictionOrigin,
                (sourceColumnQ4 & 15) != 0,
                (sourceRowQ4 & 15) != 0,
                prediction[..sampleCount],
                residual[..sampleCount],
                transformSize);

            Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
                transformSize,
                isInter: true,
                this.picture.Parent.FrameHeader.UseReducedTransformSet);

            long bestCost = long.MaxValue;
            selectedState = default;
            selectedRate = 0;
            selectedDistortion = 0;
            hasEmptyTransform = false;
            emptyState = default;
            emptyDistortion = 0;
            for (Av1TransformType transformType = Av1TransformType.DctDct;
                transformType < Av1TransformType.AllTransformTypes;
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
                    transformReconstruction[..sampleCount],
                    transformCoefficients[..sampleCount],
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
                    Av1PredictionMode.DC,
                    transformCoefficients[..sampleCount],
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
                    transformReconstruction[..sampleCount].CopyTo(selectedReconstruction);
                    transformCoefficients[..sampleCount].CopyTo(selectedCoefficients);
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
        }
    }
}

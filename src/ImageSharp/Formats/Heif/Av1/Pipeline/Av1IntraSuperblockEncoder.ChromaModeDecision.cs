// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Provides live-probability chroma mode decisions for intra encoding.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// Gets the spatial chroma modes in the order used by the reference encoder.
    /// </summary>
    private static ReadOnlySpan<Av1ChromaPredictionMode> ChromaModeSearchOrder =>
    [
        Av1ChromaPredictionMode.DC,
        Av1ChromaPredictionMode.Horizontal,
        Av1ChromaPredictionMode.Vertical,
        Av1ChromaPredictionMode.Smooth,
        Av1ChromaPredictionMode.Paeth,
        Av1ChromaPredictionMode.SmoothVertical,
        Av1ChromaPredictionMode.SmoothHorizontal,
        Av1ChromaPredictionMode.Directional135Degrees,
        Av1ChromaPredictionMode.Directional203Degrees,
        Av1ChromaPredictionMode.Directional157Degrees,
        Av1ChromaPredictionMode.Directional67Degrees,
        Av1ChromaPredictionMode.Directional113Degrees,
        Av1ChromaPredictionMode.Directional45Degrees
    ];

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        private Av1ChromaPredictionMode SelectChromaMode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Span<int> retainedBlueCoefficients,
            Span<int> retainedRedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out long selectedCost)
        {
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int sampleCount = transformSize.GetSize2d();
            Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            if (chromaBlockSize.GetWidth() > width || chromaBlockSize.GetHeight() > height)
            {
                return this.SelectTiledChromaMode(
                    writer,
                    macroBlock,
                    modeInfo,
                    lumaOrigin,
                    chromaOrigin,
                    blockSize,
                    chromaBlockSize,
                    tileIndex,
                    lumaMode,
                    transformSize,
                    retainedBlueCoefficients,
                    retainedRedCoefficients,
                    retainedBlueStates,
                    retainedRedStates,
                    paletteInfo,
                    out selectedAngleDelta,
                    out selectedChromaFromLumaIndex,
                    out selectedChromaFromLumaSigns,
                    out selectedCost);
            }

            int modeInfoRow = lumaOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = lumaOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            bool hasLeft = macroBlock.IsLeftAvailable;
            bool hasAbove = macroBlock.IsUpAvailable;

            // Subsampled chroma belongs to the bottom-right luma unit in its shared 8x8 region. Its external
            // references therefore begin before that region, not immediately beside the owning 4x4 luma block.
            if (subsamplingX != 0 && blockSize.Get4x4WideCount() < Av1BlockSize.Block8x8.Get4x4WideCount())
            {
                hasLeft = modeInfoColumn - 1 > macroBlock.Tile.ModeInfoColumnStart;
            }

            if (subsamplingY != 0 && blockSize.Get4x4HighCount() < Av1BlockSize.Block8x8.Get4x4HighCount())
            {
                hasAbove = modeInfoRow - 1 > macroBlock.Tile.ModeInfoRowStart;
            }

            bool rightAvailable = modeInfoColumn + (transformSize.Get4x4WideCount() << subsamplingX) < macroBlock.Tile.ModeInfoColumnEnd;
            bool bottomAvailable = modeInfoRow + (transformSize.Get4x4HighCount() << subsamplingY) < macroBlock.Tile.ModeInfoRowEnd;
            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                blockSize,
                modeInfoRow,
                modeInfoColumn,
                hasAbove,
                rightAvailable,
                Av1PartitionType.None,
                transformSize,
                0,
                0,
                subsamplingX,
                subsamplingY);

            bool hasBottomLeft = Av1IntraReferenceAvailability.HasBottomLeft(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                blockSize,
                modeInfoRow,
                modeInfoColumn,
                bottomAvailable,
                hasLeft,
                Av1PartitionType.None,
                transformSize,
                0,
                0,
                subsamplingX,
                subsamplingY);

            Buffer2DRegion<TSample> blueSource = this.source.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> redSource = this.source.GetPlane(Av1Plane.V);
            Buffer2DRegion<TSample> blueReconstruction = this.reconstruction.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> redReconstruction = this.reconstruction.GetPlane(Av1Plane.V);
            Span<TSample> blueAboveStorage = workspace.GetReferenceSamples(0);
            Span<TSample> blueLeftStorage = workspace.GetReferenceSamples(1);
            Span<TSample> redAboveStorage = workspace.GetReferenceSamples(2);
            Span<TSample> redLeftStorage = workspace.GetReferenceSamples(3);
            this.PrepareReferenceSamples(
                blueReconstruction,
                chromaOrigin,
                width,
                height,
                hasLeft,
                hasAbove,
                hasTopRight,
                hasBottomLeft,
                blueAboveStorage,
                blueLeftStorage);

            this.PrepareReferenceSamples(
                redReconstruction,
                chromaOrigin,
                width,
                height,
                hasLeft,
                hasAbove,
                hasTopRight,
                hasBottomLeft,
                redAboveStorage,
                redLeftStorage);

            ReadOnlySpan<TSample> blueAbove = blueAboveStorage.Slice(1, width * 2);
            ReadOnlySpan<TSample> blueLeft = blueLeftStorage.Slice(1, height * 2);
            ReadOnlySpan<TSample> redAbove = redAboveStorage.Slice(1, width * 2);
            ReadOnlySpan<TSample> redLeft = redLeftStorage.Slice(1, height * 2);
            Av1TransformBlockContext blueContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Chroma,
                this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                chromaOrigin,
                chromaBlockSize,
                transformSize);

            Av1TransformBlockContext redContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Chroma,
                this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
                chromaOrigin,
                chromaBlockSize,
                transformSize);

            Span<TSample> candidateBlueReconstruction = workspace.GetCandidateReconstruction(0);
            Span<TSample> candidateRedReconstruction = workspace.GetCandidateReconstruction(1);
            Span<int> candidateBlueCoefficients = workspace.GetCandidateCoefficients(0);
            Span<int> candidateRedCoefficients = workspace.GetCandidateCoefficients(1);
            long bestCost = long.MaxValue;
            Av1ChromaPredictionMode bestMode = Av1ChromaPredictionMode.DC;
            selectedAngleDelta = 0;
            selectedChromaFromLumaIndex = 0;
            selectedChromaFromLumaSigns = 0;
            int baseModeCount = ChromaModeSearchOrder.Length;
            int deltaCount = AngleDeltaSearchOrder.Length;
            int directionalModeCount = (int)Av1ChromaPredictionMode.Directional67Degrees - (int)Av1ChromaPredictionMode.Vertical + 1;

            // Effort zero evaluates DC only, effort one adds every zero-angle mode, and higher levels add all directional adjustments.
            int candidateCount = this.effort switch
            {
                0 => 1,
                1 => baseModeCount,
                _ when blockSize >= Av1BlockSize.Block8x8 => baseModeCount + (directionalModeCount * deltaCount),
                _ => baseModeCount
            };

            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            int paletteDisabledCost = blockSize >= Av1BlockSize.Block8x8 &&
                this.picture.Parent.FrameHeader.AllowScreenContentTools
                ? writer.GetPaletteUvModeCost(false, hasLumaPalette)
                : 0;

            // Spatial base modes precede the six nonzero adjustments for each directional mode.
            // Chroma-from-luma remains a separate search because it consumes reconstructed luma AC state.
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1ChromaPredictionMode chromaMode;
                int angleDelta;
                if (candidateIndex < baseModeCount)
                {
                    chromaMode = ChromaModeSearchOrder[candidateIndex];
                    angleDelta = 0;
                }
                else
                {
                    int adjustedIndex = candidateIndex - baseModeCount;
                    chromaMode = (Av1ChromaPredictionMode)((int)Av1ChromaPredictionMode.Vertical + (adjustedIndex / deltaCount));
                    angleDelta = AngleDeltaSearchOrder[adjustedIndex % deltaCount];
                }

                Av1EncoderTransformBlockState candidateBlueState = default;
                Av1EncoderTransformBlockState candidateRedState = default;

                // A chroma mode and angle are shared by U and V, so neither plane can replace the
                // retained result independently. Their complete rate and distortion compete jointly.
                long candidateCost = this.GetChromaCandidateCost(
                    writer,
                    modeInfo,
                    lumaMode,
                    chromaMode,
                    angleDelta,
                    blockSize,
                    chromaOrigin,
                    transformSize,
                    blueSource,
                    redSource,
                    blueAbove,
                    blueLeft,
                    redAbove,
                    redLeft,
                    hasLeft,
                    hasAbove,
                    blueContext,
                    redContext,
                    paletteDisabledCost,
                    candidateBlueReconstruction[..sampleCount],
                    candidateRedReconstruction[..sampleCount],
                    candidateBlueCoefficients[..sampleCount],
                    candidateRedCoefficients[..sampleCount],
                    ref candidateBlueState,
                    ref candidateRedState);

                if (candidateCost < bestCost)
                {
                    CopyCandidate(
                        candidateBlueReconstruction,
                        candidateBlueCoefficients,
                        blueReconstruction,
                        chromaOrigin,
                        retainedBlueCoefficients,
                        transformSize,
                        candidateBlueState,
                        ref retainedBlueStates[0]);

                    CopyCandidate(
                        candidateRedReconstruction,
                        candidateRedCoefficients,
                        redReconstruction,
                        chromaOrigin,
                        retainedRedCoefficients,
                        transformSize,
                        candidateRedState,
                        ref retainedRedStates[0]);

                    bestCost = candidateCost;
                    bestMode = chromaMode;
                    selectedAngleDelta = angleDelta;
                }
            }

            bool chromaFromLumaAllowed = blockSize.AllowsChromaFromLuma(
                this.picture.Parent.FrameHeader.LosslessArray[modeInfo.Block.SegmentId],
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            if (this.effort >= 4 && chromaFromLumaAllowed)
            {
                Span<short> lumaQ3 = workspace.ChromaFromLumaSamples;
                Point chromaLumaOrigin = new(
                    chromaOrigin.X << subsamplingX,
                    chromaOrigin.Y << subsamplingY);

                // CfL consumes the complete luma region represented by this chroma block, which starts before
                // the bottom-right ownership point for subsampled 4x4 luma leaves.
                TOperator.PrepareChromaFromLuma(
                    this.reconstruction.GetPlane(Av1Plane.Y),
                    chromaLumaOrigin,
                    lumaQ3,
                    transformSize,
                    colorConfig.SubSamplingX,
                    colorConfig.SubSamplingY);

                // Every alpha candidate uses the same constant DC predictor, so compute each plane once and
                // refill the candidate block from its sample instead of rebuilding the identical edge average.
                TOperator.PrepareChromaFromLumaDc(
                    candidateBlueReconstruction[..sampleCount],
                    blueAbove,
                    blueLeft,
                    hasLeft,
                    hasAbove,
                    transformSize,
                    this.bitDepth);

                TSample blueDc = candidateBlueReconstruction[0];
                TOperator.PrepareChromaFromLumaDc(
                    candidateRedReconstruction[..sampleCount],
                    redAbove,
                    redLeft,
                    hasLeft,
                    hasAbove,
                    transformSize,
                    this.bitDepth);

                TSample redDc = candidateRedReconstruction[0];
                Span<int> blueRates = workspace.GetChromaFromLumaRates(0);
                Span<int> redRates = workspace.GetChromaFromLumaRates(1);
                Span<long> blueDistortions = workspace.GetChromaFromLumaDistortions(0);
                Span<long> redDistortions = workspace.GetChromaFromLumaDistortions(1);

                // Each plane has only 33 signed alpha values. Caching those complete transform results reduces
                // the joint search from 1089 transform pairs to 66 transforms plus inexpensive rate combinations.
                for (int alphaCandidateIndex = 0; alphaCandidateIndex < Av1ChromaFromLumaMath.AlphaCandidateCount; alphaCandidateIndex++)
                {
                    int alphaQ3 = Av1ChromaFromLumaMath.CandidateIndexToAlpha(alphaCandidateIndex);
                    Av1EncoderTransformBlockState candidateBlueState = default;
                    blueDistortions[alphaCandidateIndex] = this.GetChromaFromLumaPlaneCost(
                        writer,
                        lumaMode,
                        Av1Plane.U,
                        chromaOrigin,
                        transformSize,
                        blueSource,
                        blueDc,
                        blueContext,
                        lumaQ3,
                        alphaQ3,
                        candidateBlueReconstruction[..sampleCount],
                        candidateBlueCoefficients[..sampleCount],
                        ref candidateBlueState,
                        out blueRates[alphaCandidateIndex]);

                    Av1EncoderTransformBlockState candidateRedState = default;
                    redDistortions[alphaCandidateIndex] = this.GetChromaFromLumaPlaneCost(
                        writer,
                        lumaMode,
                        Av1Plane.V,
                        chromaOrigin,
                        transformSize,
                        redSource,
                        redDc,
                        redContext,
                        lumaQ3,
                        alphaQ3,
                        candidateRedReconstruction[..sampleCount],
                        candidateRedCoefficients[..sampleCount],
                        ref candidateRedState,
                        out redRates[alphaCandidateIndex]);
                }

                int chromaFromLumaModeRate = Av1TileWriter.GetChromaModeCost(
                    writer,
                    this.picture.Parent.FrameHeader,
                    colorConfig,
                    modeInfo,
                    blockSize,
                    lumaMode,
                    Av1ChromaPredictionMode.ChromaFromLuma,
                    0);

                bool chromaFromLumaSelected = false;
                int selectedBlueCandidateIndex = 0;
                int selectedRedCandidateIndex = 0;
                for (int blueCandidateIndex = 0; blueCandidateIndex < Av1ChromaFromLumaMath.AlphaCandidateCount; blueCandidateIndex++)
                {
                    int alphaU = Av1ChromaFromLumaMath.CandidateIndexToAlpha(blueCandidateIndex);
                    int signU = Av1ChromaFromLumaMath.AlphaToSign(alphaU);
                    int indexU = Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(alphaU);
                    for (int redCandidateIndex = 0; redCandidateIndex < Av1ChromaFromLumaMath.AlphaCandidateCount; redCandidateIndex++)
                    {
                        int alphaV = Av1ChromaFromLumaMath.CandidateIndexToAlpha(redCandidateIndex);
                        int signV = Av1ChromaFromLumaMath.AlphaToSign(alphaV);
                        if (signU == Av1ChromaFromLumaMath.SignZero && signV == Av1ChromaFromLumaMath.SignZero)
                        {
                            continue;
                        }

                        int indexV = Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(alphaV);
                        int jointSign = Av1ChromaFromLumaMath.JointSign(signU, signV);
                        int packedIndex = Av1ChromaFromLumaMath.PackIndices(indexU, indexV);
                        int rate = chromaFromLumaModeRate
                            + blueRates[blueCandidateIndex]
                            + redRates[redCandidateIndex]
                            + writer.GetChromaFromLumaCost(packedIndex, jointSign);

                        long distortion = blueDistortions[blueCandidateIndex] + redDistortions[redCandidateIndex];
                        long candidateCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
                        bool winsSearchOrderTie = candidateCost == bestCost
                            && !chromaFromLumaSelected
                            && bestMode != Av1ChromaPredictionMode.DC;

                        // CfL follows DC and precedes every other chroma mode in the reference search order.
                        if (candidateCost < bestCost || winsSearchOrderTie)
                        {
                            bestCost = candidateCost;
                            bestMode = Av1ChromaPredictionMode.ChromaFromLuma;
                            selectedAngleDelta = 0;
                            selectedBlueCandidateIndex = blueCandidateIndex;
                            selectedRedCandidateIndex = redCandidateIndex;
                            selectedChromaFromLumaIndex = (byte)packedIndex;
                            selectedChromaFromLumaSigns = (sbyte)jointSign;
                            chromaFromLumaSelected = true;
                        }
                    }
                }

                if (chromaFromLumaSelected)
                {
                    // The alpha tables retain only rate and distortion. Regenerate the two selected planes
                    // once here instead of copying reconstruction and coefficient blocks for all 66 trials.
                    Av1EncoderTransformBlockState candidateBlueState = default;
                    _ = this.GetChromaFromLumaPlaneCost(
                        writer,
                        lumaMode,
                        Av1Plane.U,
                        chromaOrigin,
                        transformSize,
                        blueSource,
                        blueDc,
                        blueContext,
                        lumaQ3,
                        Av1ChromaFromLumaMath.CandidateIndexToAlpha(selectedBlueCandidateIndex),
                        candidateBlueReconstruction[..sampleCount],
                        candidateBlueCoefficients[..sampleCount],
                        ref candidateBlueState,
                        out _);

                    CopyCandidate(
                        candidateBlueReconstruction,
                        candidateBlueCoefficients,
                        blueReconstruction,
                        chromaOrigin,
                        retainedBlueCoefficients,
                        transformSize,
                        candidateBlueState,
                        ref retainedBlueStates[0]);

                    Av1EncoderTransformBlockState candidateRedState = default;
                    _ = this.GetChromaFromLumaPlaneCost(
                        writer,
                        lumaMode,
                        Av1Plane.V,
                        chromaOrigin,
                        transformSize,
                        redSource,
                        redDc,
                        redContext,
                        lumaQ3,
                        Av1ChromaFromLumaMath.CandidateIndexToAlpha(selectedRedCandidateIndex),
                        candidateRedReconstruction[..sampleCount],
                        candidateRedCoefficients[..sampleCount],
                        ref candidateRedState,
                        out _);

                    CopyCandidate(
                        candidateRedReconstruction,
                        candidateRedCoefficients,
                        redReconstruction,
                        chromaOrigin,
                        retainedRedCoefficients,
                        transformSize,
                        candidateRedState,
                        ref retainedRedStates[0]);
                }
            }

            if (this.effort >= 5 &&
                blockSize == Av1BlockSize.Block8x8 &&
                this.picture.Parent.FrameHeader.AllowScreenContentTools &&
                this.SelectChromaPalette(
                    writer,
                    macroBlock,
                    modeInfo,
                    lumaOrigin,
                    chromaOrigin,
                    tileIndex,
                    lumaMode,
                    transformSize,
                    blueContext,
                    redContext,
                    candidateBlueReconstruction[..sampleCount],
                    candidateRedReconstruction[..sampleCount],
                    candidateBlueCoefficients[..sampleCount],
                    candidateRedCoefficients[..sampleCount],
                    retainedBlueCoefficients,
                    retainedRedCoefficients,
                    ref retainedBlueStates[0],
                    ref retainedRedStates[0],
                    ref bestCost,
                    ref paletteInfo))
            {
                bestMode = Av1ChromaPredictionMode.DC;
                selectedAngleDelta = 0;
                selectedChromaFromLumaIndex = 0;
                selectedChromaFromLumaSigns = 0;
            }

            selectedCost = bestCost;
            return bestMode;
        }

        private Av1ChromaPredictionMode SelectTiledChromaMode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
            Av1BlockSize chromaBlockSize,
            ushort tileIndex,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Span<int> retainedBlueCoefficients,
            Span<int> retainedRedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out long selectedCost)
        {
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            int blockWidth = chromaBlockSize.GetWidth();
            int blockHeight = chromaBlockSize.GetHeight();
            int blockSampleCount = blockWidth * blockHeight;
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int transformColumnCount = blockWidth / transformWidth;
            int transformRowCount = blockHeight / transformHeight;
            int transformBlockCount = transformColumnCount * transformRowCount;
            Span<TSample> candidateBlueReconstruction =
                workspace.GetCandidateReconstruction(0)[..blockSampleCount];

            Span<TSample> candidateRedReconstruction =
                workspace.GetCandidateReconstruction(1)[..blockSampleCount];

            Span<int> candidateBlueCoefficients =
                workspace.GetCandidateCoefficients(0)[..blockSampleCount];

            Span<int> candidateRedCoefficients =
                workspace.GetCandidateCoefficients(1)[..blockSampleCount];

            Span<Av1EncoderTransformBlockState> candidateStates = workspace.CandidateTransformBlocks;
            Span<Av1EncoderTransformBlockState> candidateBlueStates =
                candidateStates[..transformBlockCount];

            Span<Av1EncoderTransformBlockState> candidateRedStates =
                candidateStates.Slice(transformBlockCount, transformBlockCount);

            int contextWidth = chromaBlockSize.Get4x4WideCount();
            int contextHeight = chromaBlockSize.Get4x4HighCount();
            Span<byte> contexts = workspace.TransformContexts;
            Span<byte> blueTopContexts = contexts[..contextWidth];
            Span<byte> blueLeftContexts = contexts.Slice(contextWidth, contextHeight);
            Span<byte> redTopContexts = contexts.Slice(contextWidth + contextHeight, contextWidth);
            Span<byte> redLeftContexts = contexts.Slice(
                (2 * contextWidth) + contextHeight,
                contextHeight);

            Av1NeighborArrayUnit<byte> blueNeighbors =
                this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex];

            Av1NeighborArrayUnit<byte> redNeighbors =
                this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex];

            int blueTopIndex = blueNeighbors.GetTopIndex(chromaOrigin);
            int blueLeftIndex = blueNeighbors.GetLeftIndex(chromaOrigin);
            int redTopIndex = redNeighbors.GetTopIndex(chromaOrigin);
            int redLeftIndex = redNeighbors.GetLeftIndex(chromaOrigin);
            Buffer2DRegion<TSample> blueSource = this.source.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> redSource = this.source.GetPlane(Av1Plane.V);
            Buffer2DRegion<TSample> blueReconstruction = this.reconstruction.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> redReconstruction = this.reconstruction.GetPlane(Av1Plane.V);
            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            int paletteDisabledCost = this.picture.Parent.FrameHeader.AllowScreenContentTools
                ? writer.GetPaletteUvModeCost(false, hasLumaPalette)
                : 0;

            int baseModeCount = ChromaModeSearchOrder.Length;
            int deltaCount = AngleDeltaSearchOrder.Length;
            int directionalModeCount =
                (int)Av1ChromaPredictionMode.Directional67Degrees -
                (int)Av1ChromaPredictionMode.Vertical +
                1;

            int candidateCount = baseModeCount + (directionalModeCount * deltaCount);
            long bestCost = long.MaxValue;
            Av1ChromaPredictionMode bestMode = Av1ChromaPredictionMode.DC;
            selectedAngleDelta = 0;
            selectedChromaFromLumaIndex = 0;
            selectedChromaFromLumaSigns = 0;

            // A large chroma block is predicted and transformed in the same raster order used by the tile
            // writer. Each completed transform supplies both reconstructed edges and coefficient contexts.
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1ChromaPredictionMode chromaMode;
                int angleDelta;
                if (candidateIndex < baseModeCount)
                {
                    chromaMode = ChromaModeSearchOrder[candidateIndex];
                    angleDelta = 0;
                }
                else
                {
                    int adjustedIndex = candidateIndex - baseModeCount;
                    chromaMode = (Av1ChromaPredictionMode)(
                        (int)Av1ChromaPredictionMode.Vertical + (adjustedIndex / deltaCount));

                    angleDelta = AngleDeltaSearchOrder[adjustedIndex % deltaCount];
                }

                blueNeighbors.Top.Slice(blueTopIndex, contextWidth).CopyTo(blueTopContexts);
                blueNeighbors.Left.Slice(blueLeftIndex, contextHeight).CopyTo(blueLeftContexts);
                redNeighbors.Top.Slice(redTopIndex, contextWidth).CopyTo(redTopContexts);
                redNeighbors.Left.Slice(redLeftIndex, contextHeight).CopyTo(redLeftContexts);
                Av1PredictionMode predictionMode = chromaMode.ToLumaMode();
                long distortion = this.GetTiledChromaPlaneCost(
                    writer,
                    macroBlock,
                    lumaOrigin,
                    chromaOrigin,
                    blockSize,
                    chromaBlockSize,
                    transformSize,
                    subsamplingX,
                    subsamplingY,
                    lumaMode,
                    predictionMode,
                    angleDelta,
                    Av1Plane.U,
                    blueSource,
                    blueReconstruction,
                    candidateBlueReconstruction,
                    candidateBlueCoefficients,
                    candidateBlueStates,
                    blueTopContexts,
                    blueLeftContexts,
                    out int blueRate);

                distortion += this.GetTiledChromaPlaneCost(
                    writer,
                    macroBlock,
                    lumaOrigin,
                    chromaOrigin,
                    blockSize,
                    chromaBlockSize,
                    transformSize,
                    subsamplingX,
                    subsamplingY,
                    lumaMode,
                    predictionMode,
                    angleDelta,
                    Av1Plane.V,
                    redSource,
                    redReconstruction,
                    candidateRedReconstruction,
                    candidateRedCoefficients,
                    candidateRedStates,
                    redTopContexts,
                    redLeftContexts,
                    out int redRate);

                int rate = Av1TileWriter.GetChromaModeCost(
                    writer,
                    this.picture.Parent.FrameHeader,
                    colorConfig,
                    modeInfo,
                    blockSize,
                    lumaMode,
                    chromaMode,
                    angleDelta);

                rate += blueRate + redRate;
                if (chromaMode == Av1ChromaPredictionMode.DC)
                {
                    rate += paletteDisabledCost;
                }

                long candidateCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
                if (candidateCost < bestCost)
                {
                    CopyTiledCandidate(
                        candidateBlueReconstruction,
                        candidateBlueCoefficients,
                        candidateBlueStates,
                        blueReconstruction,
                        chromaOrigin,
                        blockWidth,
                        blockHeight,
                        transformSize,
                        retainedBlueCoefficients,
                        retainedBlueStates);

                    CopyTiledCandidate(
                        candidateRedReconstruction,
                        candidateRedCoefficients,
                        candidateRedStates,
                        redReconstruction,
                        chromaOrigin,
                        blockWidth,
                        blockHeight,
                        transformSize,
                        retainedRedCoefficients,
                        retainedRedStates);

                    bestCost = candidateCost;
                    bestMode = chromaMode;
                    selectedAngleDelta = angleDelta;
                }
            }

            selectedCost = bestCost;
            return bestMode;
        }

        private long GetTiledChromaPlaneCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1BlockSize blockSize,
            Av1BlockSize chromaBlockSize,
            Av1TransformSize transformSize,
            int subsamplingX,
            int subsamplingY,
            Av1PredictionMode lumaMode,
            Av1PredictionMode predictionMode,
            int angleDelta,
            Av1Plane plane,
            Buffer2DRegion<TSample> source,
            Buffer2DRegion<TSample> reconstruction,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            Span<Av1EncoderTransformBlockState> candidateStates,
            Span<byte> topContexts,
            Span<byte> leftContexts,
            out int rate)
        {
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            int blockWidth = chromaBlockSize.GetWidth();
            int blockHeight = chromaBlockSize.GetHeight();
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int transformSampleCount = transformSize.GetSize2d();
            int transformWidth4x4 = transformSize.Get4x4WideCount();
            int transformHeight4x4 = transformSize.Get4x4HighCount();
            Av1TransformType transformType = Av1SymbolContextHelper.GetDefaultIntraTransformType(
                predictionMode,
                transformSize,
                this.picture.Parent.FrameHeader.UseReducedTransformSet);

            Av1ComponentType componentType = Av1ComponentType.Chroma;
            Span<TSample> prediction = workspace.Prediction[..transformSampleCount];
            Span<short> residual = workspace.Residual[..transformSampleCount];
            Span<TSample> aboveStorage = workspace.GetReferenceSamples(0);
            Span<TSample> leftStorage = workspace.GetReferenceSamples(1);
            int coefficientOffset = 0;
            int transformIndex = 0;
            long distortion = 0;
            rate = 0;
            for (int transformRow = 0; transformRow < blockHeight / transformHeight; transformRow++)
            {
                int rowOffset = transformRow * transformHeight;
                for (int transformColumn = 0; transformColumn < blockWidth / transformWidth; transformColumn++)
                {
                    int columnOffset = transformColumn * transformWidth;
                    int reconstructionOffset = (rowOffset * blockWidth) + columnOffset;
                    Point transformOrigin = chromaOrigin + new Size(columnOffset, rowOffset);
                    this.PrepareTransformReferenceSamples(
                        reconstruction,
                        lumaOrigin,
                        chromaOrigin,
                        blockSize,
                        macroBlock,
                        transformRow,
                        transformColumn,
                        blockWidth,
                        transformSize,
                        subsamplingX,
                        subsamplingY,
                        candidateReconstruction,
                        aboveStorage,
                        leftStorage,
                        out bool hasLeft,
                        out bool hasAbove);

                    TOperator.PrepareIntra(
                        this.blockWorkspace,
                        source,
                        transformOrigin,
                        prediction,
                        aboveStorage.Slice(1, transformWidth * 2),
                        leftStorage.Slice(1, transformHeight * 2),
                        hasLeft,
                        hasAbove,
                        predictionMode,
                        angleDelta,
                        residual,
                        transformSize,
                        this.bitDepth);

                    Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                        componentType,
                        topContexts.Slice(transformColumn * transformWidth4x4, transformWidth4x4),
                        leftContexts.Slice(transformRow * transformHeight4x4, transformHeight4x4),
                        chromaBlockSize,
                        transformSize);

                    Span<int> transformCoefficients = candidateCoefficients.Slice(
                        coefficientOffset,
                        transformSampleCount);

                    ref Av1EncoderTransformBlockState state = ref candidateStates[transformIndex++];
                    distortion += TOperator.EncodePredictionCandidate(
                        this.blockWorkspace,
                        source,
                        transformOrigin,
                        prediction,
                        residual,
                        candidateReconstruction[reconstructionOffset..],
                        blockWidth,
                        transformCoefficients,
                        transformSize,
                        transformType,
                        plane,
                        this.quantization.QIndex[0],
                        this.quantization.DeltaQDc[(int)plane],
                        this.quantization.DeltaQAc[(int)plane],
                        this.bitDepth,
                        ref state);

                    rate += writer.GetCoefficientCost(
                        transformSize,
                        transformType,
                        lumaMode,
                        transformCoefficients,
                        componentType,
                        blockContext,
                        state.EndOfBlock,
                        this.picture.Parent.FrameHeader.UseReducedTransformSet,
                        Av1FilterIntraMode.AllFilterIntraModes,
                        usesInterTransformSet: false);

                    byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                        transformCoefficients,
                        transformSize,
                        transformType,
                        state.EndOfBlock);

                    topContexts
                        .Slice(transformColumn * transformWidth4x4, transformWidth4x4)
                        .Fill(coefficientContext);

                    leftContexts
                        .Slice(transformRow * transformHeight4x4, transformHeight4x4)
                        .Fill(coefficientContext);

                    coefficientOffset += transformSampleCount;
                }
            }

            return distortion;
        }

        private static void CopyTiledCandidate(
            ReadOnlySpan<TSample> candidateReconstruction,
            ReadOnlySpan<int> candidateCoefficients,
            ReadOnlySpan<Av1EncoderTransformBlockState> candidateStates,
            Buffer2DRegion<TSample> reconstruction,
            Point blockOrigin,
            int blockWidth,
            int blockHeight,
            Av1TransformSize transformSize,
            Span<int> retainedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedStates)
        {
            int blockSampleCount = blockWidth * blockHeight;
            int transformStateStride =
                transformSize.GetSize2d() /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            candidateCoefficients[..blockSampleCount].CopyTo(retainedCoefficients);
            for (int transformIndex = 0; transformIndex < candidateStates.Length; transformIndex++)
            {
                retainedStates[transformIndex * transformStateStride] = candidateStates[transformIndex];
            }

            for (int row = 0; row < blockHeight; row++)
            {
                candidateReconstruction.Slice(row * blockWidth, blockWidth)
                    .CopyTo(reconstruction.DangerousGetRowSpan(blockOrigin.Y + row).Slice(blockOrigin.X, blockWidth));
            }
        }

        private long GetChromaFromLumaPlaneCost(
            Av1SymbolEncoder writer,
            Av1PredictionMode lumaMode,
            Av1Plane plane,
            Point chromaOrigin,
            Av1TransformSize transformSize,
            Buffer2DRegion<TSample> source,
            TSample dc,
            Av1TransformBlockContext context,
            ReadOnlySpan<short> lumaQ3,
            int alphaQ3,
            Span<TSample> reconstruction,
            Span<int> coefficients,
            ref Av1EncoderTransformBlockState state,
            out int rate)
        {
            long distortion = TOperator.EncodeChromaFromLumaCandidate(
                this.blockWorkspace,
                source,
                chromaOrigin,
                reconstruction,
                dc,
                lumaQ3,
                alphaQ3,
                coefficients,
                transformSize,
                plane,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[(int)plane],
                this.quantization.DeltaQAc[(int)plane],
                this.bitDepth,
                ref state);

            rate = writer.GetCoefficientCost(
                transformSize,
                Av1TransformType.DctDct,
                lumaMode,
                coefficients,
                Av1ComponentType.Chroma,
                context,
                state.EndOfBlock,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                Av1FilterIntraMode.AllFilterIntraModes,
                usesInterTransformSet: false);

            return distortion;
        }

        private long GetChromaCandidateCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockModeInfo modeInfo,
            Av1PredictionMode lumaMode,
            Av1ChromaPredictionMode chromaMode,
            int angleDelta,
            Av1BlockSize blockSize,
            Point chromaOrigin,
            Av1TransformSize transformSize,
            Buffer2DRegion<TSample> blueSource,
            Buffer2DRegion<TSample> redSource,
            ReadOnlySpan<TSample> blueAbove,
            ReadOnlySpan<TSample> blueLeft,
            ReadOnlySpan<TSample> redAbove,
            ReadOnlySpan<TSample> redLeft,
            bool hasLeft,
            bool hasAbove,
            Av1TransformBlockContext blueContext,
            Av1TransformBlockContext redContext,
            int paletteDisabledCost,
            Span<TSample> candidateBlueReconstruction,
            Span<TSample> candidateRedReconstruction,
            Span<int> candidateBlueCoefficients,
            Span<int> candidateRedCoefficients,
            ref Av1EncoderTransformBlockState candidateBlueState,
            ref Av1EncoderTransformBlockState candidateRedState)
        {
            Av1PredictionMode predictionMode = chromaMode.ToLumaMode();

            // Intra chroma derives one transform type from the shared UV prediction mode. The type is not
            // signaled independently for either chroma plane, so U and V must use the same legal fallback.
            Av1TransformType transformType = Av1SymbolContextHelper.GetDefaultIntraTransformType(
                predictionMode,
                transformSize,
                this.picture.Parent.FrameHeader.UseReducedTransformSet);

            long distortion = TOperator.EncodeCandidate(
                this.blockWorkspace,
                blueSource,
                chromaOrigin,
                candidateBlueReconstruction,
                blueAbove,
                blueLeft,
                hasLeft,
                hasAbove,
                predictionMode,
                angleDelta,
                candidateBlueCoefficients,
                transformSize,
                transformType,
                Av1Plane.U,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[(int)Av1Plane.U],
                this.quantization.DeltaQAc[(int)Av1Plane.U],
                this.bitDepth,
                ref candidateBlueState);

            distortion += TOperator.EncodeCandidate(
                this.blockWorkspace,
                redSource,
                chromaOrigin,
                candidateRedReconstruction,
                redAbove,
                redLeft,
                hasLeft,
                hasAbove,
                predictionMode,
                angleDelta,
                candidateRedCoefficients,
                transformSize,
                transformType,
                Av1Plane.V,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[(int)Av1Plane.V],
                this.quantization.DeltaQAc[(int)Av1Plane.V],
                this.bitDepth,
                ref candidateRedState);

            // The mode and angle are written once for the UV pair; coefficient syntax remains independent
            // because each plane has its own EOB, scan values, and neighboring coefficient context.
            int rate = Av1TileWriter.GetChromaModeCost(
                writer,
                this.picture.Parent.FrameHeader,
                this.picture.Sequence.SequenceHeader.ColorConfig,
                modeInfo,
                blockSize,
                lumaMode,
                chromaMode,
                angleDelta);

            if (chromaMode == Av1ChromaPredictionMode.DC)
            {
                rate += paletteDisabledCost;
            }

            rate += writer.GetCoefficientCost(
                transformSize,
                transformType,
                lumaMode,
                candidateBlueCoefficients,
                Av1ComponentType.Chroma,
                blueContext,
                candidateBlueState.EndOfBlock,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                Av1FilterIntraMode.AllFilterIntraModes,
                usesInterTransformSet: false);

            rate += writer.GetCoefficientCost(
                transformSize,
                transformType,
                lumaMode,
                candidateRedCoefficients,
                Av1ComponentType.Chroma,
                redContext,
                candidateRedState.EndOfBlock,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                Av1FilterIntraMode.AllFilterIntraModes,
                usesInterTransformSet: false);

            return Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
        }

        private void PrepareReferenceSamples(
            Buffer2DRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            int width,
            int height,
            bool hasLeft,
            bool hasAbove,
            bool hasTopRight,
            bool hasBottomLeft,
            Span<TSample> aboveStorage,
            Span<TSample> leftStorage)
        {
            Span<TSample> above = aboveStorage.Slice(1, width * 2);
            Span<TSample> left = leftStorage.Slice(1, height * 2);
            if (hasAbove)
            {
                reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1).Slice(blockOrigin.X, width).CopyTo(above[..width]);
            }

            if (hasLeft)
            {
                for (int row = 0; row < height; row++)
                {
                    left[row] = reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + row)[blockOrigin.X - 1];
                }
            }

            int midpoint = 128 << (this.bitDepth.GetBitCount() - 8);
            if (!hasAbove)
            {
                above[..width].Fill(hasLeft ? left[0] : TOperator.CreateSample(midpoint - 1));
            }

            if (!hasLeft)
            {
                left[..height].Fill(hasAbove ? above[0] : TOperator.CreateSample(midpoint + 1));
            }

            if (hasTopRight)
            {
                reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1).Slice(blockOrigin.X + width, width).CopyTo(above[width..]);
            }
            else
            {
                above[width..].Fill(above[width - 1]);
            }

            if (hasBottomLeft)
            {
                for (int row = height; row < height * 2; row++)
                {
                    left[row] = reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + row)[blockOrigin.X - 1];
                }
            }
            else
            {
                left[height..].Fill(left[height - 1]);
            }

            // Zone-two projection and Paeth address the common corner immediately before both edges.
            // Missing edges derive it from the closest coded sample or the bit-depth midpoint.
            TSample corner = hasAbove && hasLeft
                ? reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1)[blockOrigin.X - 1]
                : hasAbove
                    ? above[0]
                    : hasLeft
                        ? left[0]
                        : TOperator.CreateSample(midpoint);

            aboveStorage[0] = corner;
            leftStorage[0] = corner;
        }
    }
}

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
            ushort tileIndex,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Span<int> retainedBlueCoefficients,
            Span<int> retainedRedCoefficients,
            ref Av1EncoderTransformBlockState retainedBlueState,
            ref Av1EncoderTransformBlockState retainedRedState,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out byte selectedChromaFromLumaIndex,
            out sbyte selectedChromaFromLumaSigns,
            out long selectedCost)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const int MaximumSampleCount = 8 * 8;
            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int sampleCount = transformSize.GetSize2d();
            int modeInfoRow = lumaOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = lumaOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            bool hasLeft = macroBlock.IsLeftAvailable;
            bool hasAbove = macroBlock.IsUpAvailable;
            bool rightAvailable = modeInfoColumn + (transformSize.Get4x4WideCount() << subsamplingX) < macroBlock.Tile.ModeInfoColumnEnd;
            bool bottomAvailable = modeInfoRow + (transformSize.Get4x4HighCount() << subsamplingY) < macroBlock.Tile.ModeInfoRowEnd;
            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                BlockSize,
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
                BlockSize,
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
            Span<TSample> blueAboveStorage = stackalloc TSample[17];
            Span<TSample> blueLeftStorage = stackalloc TSample[17];
            Span<TSample> redAboveStorage = stackalloc TSample[17];
            Span<TSample> redLeftStorage = stackalloc TSample[17];
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
            Av1BlockSize chromaBlockSize = BlockSize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);
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

            Span<TSample> candidateBlueReconstruction = stackalloc TSample[MaximumSampleCount];
            Span<TSample> candidateRedReconstruction = stackalloc TSample[MaximumSampleCount];
            Span<int> candidateBlueCoefficients = stackalloc int[MaximumSampleCount];
            Span<int> candidateRedCoefficients = stackalloc int[MaximumSampleCount];
            long bestCost = long.MaxValue;
            Av1ChromaPredictionMode bestMode = Av1ChromaPredictionMode.DC;
            selectedAngleDelta = 0;
            selectedChromaFromLumaIndex = 0;
            selectedChromaFromLumaSigns = 0;
            int baseModeCount = ChromaModeSearchOrder.Length;
            int deltaCount = AngleDeltaSearchOrder.Length;
            int directionalModeCount = (int)Av1ChromaPredictionMode.Directional67Degrees - (int)Av1ChromaPredictionMode.Vertical + 1;
            int candidateCount = baseModeCount + (directionalModeCount * deltaCount);
            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            int paletteDisabledCost = this.picture.Parent.FrameHeader.AllowScreenContentTools
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
                long candidateCost = this.GetChromaCandidateCost(
                    writer,
                    modeInfo,
                    lumaMode,
                    chromaMode,
                    angleDelta,
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
                        ref retainedBlueState);

                    CopyCandidate(
                        candidateRedReconstruction,
                        candidateRedCoefficients,
                        redReconstruction,
                        chromaOrigin,
                        retainedRedCoefficients,
                        transformSize,
                        candidateRedState,
                        ref retainedRedState);

                    bestCost = candidateCost;
                    bestMode = chromaMode;
                    selectedAngleDelta = angleDelta;
                }
            }

            bool chromaFromLumaAllowed = BlockSize.AllowsChromaFromLuma(
                this.picture.Parent.FrameHeader.LosslessArray[modeInfo.Block.SegmentId],
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            if (chromaFromLumaAllowed)
            {
                Span<short> lumaQ3 = stackalloc short[Av1ChromaFromLumaContext.BufferLine * 8];
                TOperator.PrepareChromaFromLuma(
                    this.reconstruction.GetPlane(Av1Plane.Y),
                    lumaOrigin,
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
                Span<int> blueRates = stackalloc int[Av1ChromaFromLumaMath.AlphaCandidateCount];
                Span<int> redRates = stackalloc int[Av1ChromaFromLumaMath.AlphaCandidateCount];
                Span<long> blueDistortions = stackalloc long[Av1ChromaFromLumaMath.AlphaCandidateCount];
                Span<long> redDistortions = stackalloc long[Av1ChromaFromLumaMath.AlphaCandidateCount];

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
                    BlockSize,
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
                        ref retainedBlueState);

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
                        ref retainedRedState);
                }
            }

            if (this.picture.Parent.FrameHeader.AllowScreenContentTools &&
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
                    ref retainedBlueState,
                    ref retainedRedState,
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
                Av1FilterIntraMode.AllFilterIntraModes);

            return distortion;
        }

        private long GetChromaCandidateCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockModeInfo modeInfo,
            Av1PredictionMode lumaMode,
            Av1ChromaPredictionMode chromaMode,
            int angleDelta,
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
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            Av1PredictionMode predictionMode = chromaMode.ToLumaMode();
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

            int rate = Av1TileWriter.GetChromaModeCost(
                writer,
                this.picture.Parent.FrameHeader,
                this.picture.Sequence.SequenceHeader.ColorConfig,
                modeInfo,
                BlockSize,
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
                Av1FilterIntraMode.AllFilterIntraModes);

            rate += writer.GetCoefficientCost(
                transformSize,
                transformType,
                lumaMode,
                candidateRedCoefficients,
                Av1ComponentType.Chroma,
                redContext,
                candidateRedState.EndOfBlock,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                Av1FilterIntraMode.AllFilterIntraModes);

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

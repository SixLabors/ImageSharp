// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Provides prediction-based intra mode estimation and selected-mode encoding.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    private static ReadOnlySpan<Av1PredictionMode> EstimatedIntraModes =>
    [
        Av1PredictionMode.DC,
        Av1PredictionMode.Vertical,
        Av1PredictionMode.Horizontal,
        Av1PredictionMode.Smooth
    ];

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        private int GetSourceVariance(Point blockOrigin, Av1BlockSize blockSize)
        {
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            Buffer2DRegion<TSample> source = this.source.GetPlane(Av1Plane.Y);
            Span<TSample> midpoint = this.blockWorkspace.GetModeDecisionWorkspace<TSample>().GetCandidateReconstruction(0)[..width];
            int sampleShift = this.bitDepth.GetBitCount() - 8;
            midpoint.Fill(TOperator.CreateSample(128 << sampleShift));
            TOperator.GetMoments(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                midpoint,
                0,
                width,
                height,
                out int sum,
                out long squares);

            // Normalize the two moments separately before subtracting the squared mean. At high bit
            // depths their independent rounding can make the centered result slightly negative.
            int squareShift = sampleShift * 2;
            sum = (sum + ((1 << sampleShift) >> 1)) >> sampleShift;
            squares = (squares + ((1L << squareShift) >> 1)) >> squareShift;
            int count = width * height;
            long variance = Math.Max(0, squares - (((long)sum * sum) / count));
            return (int)((variance + (count / 2)) / count);
        }

        private void EncodeEstimatedIntraBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            int sourceVariance,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            bool lossless = this.picture.Parent.FrameHeader.CodedLossless;
            Av1TransformSize transformSize = lossless
                ? Av1TransformSize.Size4x4
                : Math.Min(width, height) switch
                {
                    4 => Av1TransformSize.Size4x4,
                    8 => Av1TransformSize.Size8x8,
                    16 => Av1TransformSize.Size16x16,
                    32 => Av1TransformSize.Size32x32,
                    _ => Av1TransformSize.Size64x64
                };

            if (this.quantization.QIndex[0] > 150 && sourceVariance == 0 &&
                (blockOrigin.X == 0 || blockOrigin.Y == 0) && transformSize > Av1TransformSize.Size16x16)
            {
                transformSize = Av1TransformSize.Size16x16;
            }

            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            bool stillPicture = this.picture.Sequence.SequenceHeader.IsStillPicture;
            bool pruneModes = stillPicture && this.picture.Parent.EncodingSpeed == HeifEncodingSpeed.Level9;
            bool pruneSad = pruneModes && width == transformWidth && height == transformHeight;
            bool hasBothNeighbors = macroBlock.IsUpAvailable && macroBlock.IsLeftAvailable;
            Av1PredictionMode aboveMode = macroBlock.IsUpAvailable
                ? macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block.Mode
                : Av1PredictionMode.DC;

            Av1PredictionMode leftMode = macroBlock.IsLeftAvailable
                ? macroBlock.GetRelativeModeInfo(-1).Block.Mode
                : Av1PredictionMode.DC;

            Buffer2DRegion<TSample> source = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<TSample> destination = this.reconstruction.GetPlane(Av1Plane.Y);
            Span<TSample> predictedBlock = Av1TransformBlockEncoder.GetPlaneSpan(destination, blockOrigin);
            Av1EncoderModeDecisionWorkspace<TSample> workspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            Span<TSample> aboveStorage = workspace.GetReferenceSamples(0);
            Span<TSample> leftStorage = workspace.GetReferenceSamples(1);
            Span<short> residual = workspace.Residual[..transformSize.GetSize2d()];
            Size codedExtent = GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0);
            Av1TransformSize estimationSize = transformSize > Av1TransformSize.Size16x16 ? Av1TransformSize.Size16x16 : transformSize;
            Av1PredictionMode bestMode = Av1PredictionMode.DC;
            Av1RateDistortionStatistics bestStatistics = Av1RateDistortionStatistics.Invalid;
            uint bestSad = uint.MaxValue;
            int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
            Av1TileWriter.GetYModeContext(macroBlock, out byte aboveContext, out byte leftContext);

            foreach (Av1PredictionMode mode in EstimatedIntraModes)
            {
                if (sourceVariance == 0 && blockOrigin == Point.Empty &&
                    blockSize >= Av1BlockSize.Block32x32 && mode != Av1PredictionMode.DC)
                {
                    continue;
                }

                if (pruneModes && mode == Av1PredictionMode.Horizontal && bestMode == Av1PredictionMode.Vertical)
                {
                    continue;
                }

                if (pruneModes && hasBothNeighbors && mode != aboveMode && mode != leftMode &&
                    (((mode == Av1PredictionMode.Vertical || mode == Av1PredictionMode.Horizontal) && sourceVariance <= 50) ||
                        (mode == Av1PredictionMode.Smooth && bestMode == Av1PredictionMode.DC)))
                {
                    continue;
                }

                int rate = 0;
                long distortion = 0;
                bool skip = true;
                bool rejected = false;

                // Interior prediction edges deliberately contain prediction only. Residual reconstruction
                // belongs to the selected mode and must not alter the estimates of later transform units.
                for (int y = 0; y < codedExtent.Height; y += transformHeight)
                {
                    for (int x = 0; x < codedExtent.Width; x += transformWidth)
                    {
                        this.PrepareTransformReferenceSamples(
                            destination,
                            blockOrigin,
                            blockOrigin,
                            blockSize,
                            macroBlock,
                            y / transformHeight,
                            x / transformWidth,
                            destination.Stride,
                            transformSize,
                            0,
                            0,
                            predictedBlock,
                            aboveStorage,
                            leftStorage,
                            out bool hasLeft,
                            out bool hasAbove);

                        Point origin = blockOrigin + new Size(x, y);
                        TOperator.PrepareIntra(
                            this.blockWorkspace,
                            source,
                            origin,
                            predictedBlock[((y * destination.Stride) + x)..],
                            destination.Stride,
                            aboveStorage.Slice(1, transformWidth + transformHeight),
                            leftStorage.Slice(1, transformWidth + transformHeight),
                            hasLeft,
                            hasAbove,
                            mode,
                            0,
                            this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                            this.UseSmoothIntraEdges(macroBlock, blockOrigin, blockSize, Av1Plane.Y),
                            residual,
                            transformSize,
                            this.bitDepth);

                        if (pruneSad)
                        {
                            uint sad = (uint)TOperator.SumAbsoluteDifferences(
                                Av1TransformBlockEncoder.GetPlaneSpan(source, origin),
                                source.Stride,
                                predictedBlock,
                                destination.Stride,
                                width,
                                height,
                                1) >> (this.bitDepth.GetBitCount() - 8);

                            if (bestSad != uint.MaxValue && sad > bestSad + (bestSad >> 4))
                            {
                                rejected = true;
                                break;
                            }

                            bestSad = Math.Min(bestSad, sad);
                        }

                        int remainingWidth = transformWidth + (Math.Min(0, macroBlock.ToRightEdge) >> 3);
                        int remainingHeight = transformHeight + (Math.Min(0, macroBlock.ToBottomEdge) >> 3);
                        Size estimationExtent = new(
                            Math.Min(transformWidth, remainingWidth),
                            Math.Min(transformHeight, remainingHeight));

                        Av1IntraModeEstimator.Estimate(
                            this.blockWorkspace,
                            residual,
                            transformWidth,
                            estimationExtent,
                            estimationSize,
                            this.quantization.QIndex[0],
                            this.quantization.DeltaQDc[0],
                            this.quantization.DeltaQAc[0],
                            this.bitDepth,
                            false,
                            out int transformRate,
                            out long transformDistortion,
                            out bool transformSkip);

                        rate += transformRate;
                        distortion += transformDistortion;

                        // Each prediction unit replaces the estimate's skip decision along with its transform result.
                        skip = transformSkip;
                    }
                }

                if (rejected)
                {
                    continue;
                }

                rate = (skip ? 0 : rate) + writer.GetSkipCost(skip, skipContext);

                // Prediction estimates charge the mode symbol only. Angle syntax belongs to the
                // full transform search and would unfairly penalize horizontal and vertical estimates.
                rate += writer.GetLumaModeCost(mode, aboveContext, leftContext);
                Av1RateDistortionStatistics statistics = new(this.rateMultiplier, rate, distortion);
                Entropy.Av1SymbolWriter.DiagnosticSymbolTrace?.Add(
                    $"NRD {blockOrigin.X},{blockOrigin.Y} {blockSize} mode {(int)mode} rate {rate} dist {distortion} rd {statistics.Cost} skip {skip} var {sourceVariance} sad {bestSad}");
                if (statistics.Cost < bestStatistics.Cost)
                {
                    bestStatistics = statistics;
                    bestMode = mode;
                }
            }

            int lumaOffset = this.codedAreaLuma;
            Span<int> coefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.Y)[lumaOffset..];
            Span<Av1EncoderTransformBlockState> states = this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.Y)[
                (lumaOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount)..];

            // Palette thresholds use SAD per 4x4 unit, rather than per individual sample.
            uint normalizedSad = bestSad >> (BitOperations.Log2((uint)(width * height)) - 4);
            bool paletteSelected = false;
            bool prunePalette = stillPicture &&
                !((!pruneSad || normalizedSad > 20) && blockSize <= Av1BlockSize.Block16x16 && sourceVariance > 200);
            if (!prunePalette && Av1TileWriter.IsPaletteAllowed(this.picture.Parent.FrameHeader.AllowScreenContentTools, blockSize))
            {
                Av1RateDistortionStatistics paletteStatistics = bestStatistics;
                Av1TransformSize paletteTransformSize = transformSize;

                // The estimated search runs its palette through the complete search at the default stage,
                // with the transform size searched as the default stage sets it. Reference: the
                // set_mode_eval_params(DEFAULT_EVAL) that opens av1_nonrd_use_partition().
                Av1EncoderEvaluationStage previousStage = this.blockWorkspace.EvaluationStage;
                this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
                bool paletteImproved = this.SelectLumaPalette(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    coefficients,
                    states,
                    normalizedSad < 500 ? 32 : 64,
                    writer.GetInterFrameLumaModeCost(Av1PredictionMode.DC, blockSize),
                    ref paletteStatistics,
                    ref paletteInfo,
                    ref paletteTransformSize);
                this.blockWorkspace.EvaluationStage = previousStage;
                if (paletteImproved)
                {
                    // A skipped palette block omits its residual and mode rates. Apply skip syntax
                    // before comparing with the retained estimate, which already includes that syntax.
                    bool skip = !paletteStatistics.HasCoefficients;
                    int paletteRate = (skip ? 0 : paletteStatistics.Rate) + writer.GetSkipCost(skip, skipContext);
                    paletteStatistics = new(this.rateMultiplier, paletteRate, paletteStatistics.Distortion)
                    {
                        HasCoefficients = !skip,
                        AllTransformsEmpty = skip
                    };

                    paletteSelected = paletteStatistics.Cost < bestStatistics.Cost;
                    if (paletteSelected)
                    {
                        bestStatistics = paletteStatistics;
                        bestMode = Av1PredictionMode.DC;
                        transformSize = paletteTransformSize;
                    }
                    else
                    {
                        paletteInfo = default;
                    }
                }
            }

            bool copySelected = false;
            Av1MotionVector copyVector = default;
            Av1EncoderInterPredictionWorkspace<TSample> interWorkspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            if (!stillPicture && this.picture.Parent.IsScreenContent &&
                this.picture.Parent.FrameHeader.AllowIntraBlockCopy && blockSize <= Av1BlockSize.Block16x16 && paletteSelected)
            {
                Point position = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
                InlineArray8<Av1MotionVector> referenceCandidates = default;
                InlineArray8<int> referenceWeights = default;
                Av1MotionVector reference = Av1IntraBlockCopy.FindReference(
                    this.picture,
                    macroBlock,
                    position,
                    blockSize,
                    modeInfo.Block.PartitionType,
                    referenceCandidates,
                    referenceWeights);

                // The displacement search reads the source frame, not the reconstruction. Reference: the
                // xd->cur_buf, which is cpi->source, that av1_search_intrabc_nonrd() passes to
                // av1_setup_pred_block().
                if (this.picture.IntraBlockCopySearch.TryFindEstimatedCandidate<TSample, TOperator>(
                    source,
                    source,
                    blockOrigin,
                    blockSize,
                    macroBlock.Tile,
                    this.picture.Sequence.SequenceHeader,
                    this.blockWorkspace.GetDisplacementVectorCosts(),
                    reference,
                    this.quantization.QIndex[0],
                    this.rateMultiplier,
                    this.picture.Parent.MotionSearchSettings,
                    out copyVector))
                {
                    this.PrepareInterPlanePrediction(
                        copyVector,
                        default,
                        Av1Plane.Y,
                        Av1PredictionMode.DC,
                        Av1ReferenceFrameType.Intra,
                        Av1ReferenceFrameType.None,
                        false,
                        Av1CompoundType.Average,
                        0,
                        false,
                        Av1DifferenceWeightedMaskType.Type38,
                        Av1InterpolationFilter.Bilinear,
                        Av1InterpolationFilter.Bilinear,
                        destination,
                        destination,
                        blockOrigin,
                        0,
                        0,
                        blockSize,
                        interWorkspace.LumaPrediction,
                        interWorkspace.Residual);

                    Size extent = new(
                        width + (Math.Min(0, macroBlock.ToRightEdge) >> 3),
                        height + (Math.Min(0, macroBlock.ToBottomEdge) >> 3));

                    // This candidate uses the transform estimate directly. It does not add the
                    // full-search displacement and skip-symbol charges to the estimated residual.
                    Av1IntraModeEstimator.Estimate(
                        this.blockWorkspace,
                        interWorkspace.Residual,
                        width,
                        extent,
                        transformSize > Av1TransformSize.Size16x16 ? Av1TransformSize.Size16x16 : transformSize,
                        this.quantization.QIndex[0],
                        this.quantization.DeltaQDc[0],
                        this.quantization.DeltaQAc[0],
                        this.bitDepth,
                        false,
                        out int copyRate,
                        out long copyDistortion,
                        out _);

                    if (block.HasChroma)
                    {
                        int subX = this.source.ChromaSubsamplingX;
                        int subY = this.source.ChromaSubsamplingY;
                        Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
                        for (int planeIndex = 1; planeIndex <= 2; planeIndex++)
                        {
                            Av1Plane plane = (Av1Plane)planeIndex;
                            Buffer2DRegion<TSample> reconstructedPlane = this.reconstruction.GetPlane(plane);
                            Span<TSample> prediction = planeIndex == 1 ? interWorkspace.BluePrediction : interWorkspace.RedPrediction;
                            this.PrepareInterPlanePrediction(
                                copyVector,
                                default,
                                plane,
                                Av1PredictionMode.DC,
                                Av1ReferenceFrameType.Intra,
                                Av1ReferenceFrameType.None,
                                false,
                                Av1CompoundType.Average,
                                0,
                                false,
                                Av1DifferenceWeightedMaskType.Type38,
                                Av1InterpolationFilter.Bilinear,
                                Av1InterpolationFilter.Bilinear,
                                reconstructedPlane,
                                reconstructedPlane,
                                new Point(chromaOrigin.X << subX, chromaOrigin.Y << subY),
                                subX,
                                subY,
                                blockSize,
                                prediction,
                                interWorkspace.Residual);
                        }

                        Av1RateDistortionStatistics chroma = this.EstimateInterChroma(
                            blockOrigin, blockSize, interWorkspace.BluePrediction, interWorkspace.RedPrediction, true, true);
                        copyRate += chroma.Rate;
                        copyDistortion += chroma.Distortion;
                    }

                    Av1RateDistortionStatistics copyStatistics = new(this.rateMultiplier, copyRate, copyDistortion);
                    if (copyStatistics.Cost < bestStatistics.Cost)
                    {
                        copySelected = true;
                        bestStatistics = copyStatistics;
                        bestMode = Av1PredictionMode.DC;
                        paletteInfo = default;
                    }
                }
            }

            modeInfo.Block.Mode = bestMode;
            modeInfo.Block.UvMode = Av1ChromaPredictionMode.DC;
            modeInfo.Block.TransformSize = transformSize;
            block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            block.PredictionUnit.AngleDelta[0] = 0;
            block.PredictionUnit.AngleDelta[1] = 0;
            if (copySelected)
            {
                modeInfo.Block.ReferenceFrame = Av1ReferenceFrameType.Intra;
                modeInfo.Block.SecondaryReferenceFrame = Av1ReferenceFrameType.None;
                modeInfo.Block.UseIntraBlockCopy = true;
                modeInfo.Block.Skip = false;
                modeInfo.Block.HorizontalInterpolationFilter = Av1InterpolationFilter.Bilinear;
                modeInfo.Block.VerticalInterpolationFilter = Av1InterpolationFilter.Bilinear;
                this.EncodeEstimatedInterWinner(
                    writer,
                    macroBlock,
                    tileIndex,
                    blockOrigin,
                    ref modeInfo.Block,
                    block,
                    this.reconstruction,
                    this.reconstruction,
                    copyVector,
                    default,
                    interWorkspace.LumaPrediction,
                    Av1TransformType.DctDct);
                this.picture.SetDisplacementVector(
                    new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2), copyVector);
            }
            else
            {
                // The search leaves only a candidate reconstruction behind, so the selected block is
                // encoded again, palette included. Reference: the encode_b() that follows
                // av1_nonrd_pick_intra_mode(), which reaches encode_block_intra().
                this.EncodeSelectedIntraPlane(
                    writer,
                    tileIndex,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    Av1Plane.Y,
                    bestMode,
                    transformSize,
                    lumaOffset,
                    false,
                    paletteSelected ? paletteInfo.GetColors(Av1Plane.Y) : default);
            }

            codedExtent = GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0);
            this.codedAreaLuma += codedExtent.Width * codedExtent.Height;
            if (block.HasChroma)
            {
                int subX = this.source.ChromaSubsamplingX;
                int subY = this.source.ChromaSubsamplingY;
                Av1TransformSize chromaTransform = lossless
                    ? Av1TransformSize.Size4x4
                    : blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                if (!copySelected)
                {
                    this.EncodeSelectedIntraPlane(
                        writer,
                        tileIndex,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        Av1Plane.U,
                        Av1PredictionMode.DC,
                        chromaTransform,
                        this.codedAreaChroma,
                        false);
                    this.EncodeSelectedIntraPlane(
                        writer,
                        tileIndex,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        Av1Plane.V,
                        Av1PredictionMode.DC,
                        chromaTransform,
                        this.codedAreaChroma,
                        false);
                }

                Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(subX != 0, subY != 0);
                Size chromaExtent = GetCodedTransformExtent(macroBlock, chromaBlockSize, chromaTransform, subX, subY);
                this.codedAreaChroma += chromaExtent.Width * chromaExtent.Height;
            }

            this.SelectedBlockStatistics = bestStatistics;
        }

        private void EncodeSelectedIntraPlane(
            Av1SymbolEncoder writer,
            ushort tileIndex,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1Plane plane,
            Av1PredictionMode mode,
            Av1TransformSize transformSize,
            int coefficientOffset,
            bool skipResidual,
            ReadOnlySpan<ushort> paletteColors = default)
        {
            int planeIndex = (int)plane;
            int subX = plane == Av1Plane.Y ? 0 : this.source.ChromaSubsamplingX;
            int subY = plane == Av1Plane.Y ? 0 : this.source.ChromaSubsamplingY;
            Point planeOrigin = plane == Av1Plane.Y ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
            Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subX != 0, subY != 0);
            Size extent = GetCodedTransformExtent(macroBlock, planeBlockSize, transformSize, subX, subY);
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int sampleCount = transformSize.GetSize2d();
            Buffer2DRegion<TSample> source = this.source.GetPlane(plane);
            Buffer2DRegion<TSample> destination = this.reconstruction.GetPlane(plane);
            Span<TSample> reconstructedBlock = Av1TransformBlockEncoder.GetPlaneSpan(destination, planeOrigin);
            Av1EncoderModeDecisionWorkspace<TSample> workspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            Span<TSample> aboveStorage = workspace.GetReferenceSamples(0);
            Span<TSample> leftStorage = workspace.GetReferenceSamples(1);
            Span<short> residual = workspace.Residual[..sampleCount];
            Span<int> coefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, plane);
            Span<Av1EncoderTransformBlockState> states = this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane);

            int contextWidth = planeBlockSize.Get4x4WideCount();
            int contextHeight = planeBlockSize.Get4x4HighCount();
            Span<byte> topContexts = workspace.TransformContexts[..contextWidth];
            Span<byte> leftContexts = workspace.TransformContexts.Slice(contextWidth, contextHeight);
            Av1NeighborArrayUnit<byte> neighbors = plane switch
            {
                Av1Plane.Y => this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                Av1Plane.U => this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                _ => this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex]
            };

            neighbors.Top.Slice(neighbors.GetTopIndex(planeOrigin), contextWidth).CopyTo(topContexts);
            neighbors.Left.Slice(neighbors.GetLeftIndex(planeOrigin), contextHeight).CopyTo(leftContexts);
            Av1ComponentType component = plane == Av1Plane.Y ? Av1ComponentType.Luminance : Av1ComponentType.Chroma;
            Av1TransformType transformType = plane == Av1Plane.Y || this.picture.Parent.FrameHeader.CodedLossless
                ? Av1TransformType.DctDct
                : Av1SymbolContextHelper.GetDefaultIntraTransformType(mode, transformSize, this.picture.Parent.FrameHeader.UseReducedTransformSet);

            // A palette block predicts from its color index map. It keeps the transform types its search chose
            // only when the first transform block's type is not DCT_DCT; otherwise the context keeps the DCT_DCT
            // map it was allocated with, for every transform block. Reference: the tx_type_map that
            // av1_nonrd_pick_intra_mode() copies into the context when palette wins and xd->tx_type_map[0] is
            // not DCT_DCT, read back by encode_block_intra().
            Buffer2DRegion<byte> paletteMap = paletteColors.IsEmpty
                ? default
                : this.superblock.Workspace.GetPaletteMaps().GetMap(Av1PlaneType.Y, planeBlockSize.GetWidth(), planeBlockSize.GetHeight());
            bool keepsSearchedTypes = !paletteColors.IsEmpty &&
                states[coefficientOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount].TransformType != Av1TransformType.DctDct;

            // The selected predictor writes directly to the retained frame. Its inverse transform adds
            // residuals in place, so subsequent units consume reconstructed neighbors without a pixel copy.
            // An intra block codes its uniform transforms in raster order inside each 64x64 luma unit.
            // The packing pass reads coefficients in that order, so analysis must store them in it too.
            // Depth-first order belongs to inter transform trees only.
            Av1BlockSize maximumUnit = plane == Av1Plane.Y
                ? Av1BlockSize.Block64x64
                : Av1BlockSize.Block64x64.GetSubsampled(subX != 0, subY != 0);

            int unitWidth = Math.Min(maximumUnit.GetWidth(), extent.Width);
            int unitHeight = Math.Min(maximumUnit.GetHeight(), extent.Height);
            for (int unitY = 0; unitY < extent.Height; unitY += unitHeight)
            {
                for (int unitX = 0; unitX < extent.Width; unitX += unitWidth)
                {
                    for (int y = unitY; y < Math.Min(unitY + unitHeight, extent.Height); y += height)
                    {
                        for (int x = unitX; x < Math.Min(unitX + unitWidth, extent.Width); x += width)
                        {
                            this.PrepareTransformReferenceSamples(
                                destination,
                                blockOrigin,
                                planeOrigin,
                                blockSize,
                                macroBlock,
                                y / height,
                                x / width,
                                destination.Stride,
                                transformSize,
                                subX,
                                subY,
                                reconstructedBlock,
                                aboveStorage,
                                leftStorage,
                                out bool hasLeft,
                                out bool hasAbove);

                            Point origin = planeOrigin + new Size(x, y);
                            Span<TSample> transform = reconstructedBlock[((y * destination.Stride) + x)..];
                            ref Av1EncoderTransformBlockState state = ref states[
                                coefficientOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];

                            Av1TransformType blockTransformType = transformType;
                            if (!paletteColors.IsEmpty)
                            {
                                Span<TSample> prediction = workspace.Prediction[..sampleCount];
                                TOperator.PreparePalette(
                                    source,
                                    origin,
                                    paletteColors,
                                    paletteMap.GetSubRegion(x, y, width, height),
                                    prediction,
                                    residual,
                                    transformSize);

                                for (int row = 0; row < height; row++)
                                {
                                    prediction.Slice(row * width, width).CopyTo(transform.Slice(row * destination.Stride, width));
                                }

                                blockTransformType = keepsSearchedTypes ? state.TransformType : Av1TransformType.DctDct;
                            }
                            else
                            {
                                TOperator.PrepareIntra(
                                    this.blockWorkspace,
                                    source,
                                    origin,
                                    transform,
                                    destination.Stride,
                                    aboveStorage.Slice(1, width + height),
                                    leftStorage.Slice(1, width + height),
                                    hasLeft,
                                    hasAbove,
                                    mode,
                                    0,
                                    this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                                    this.UseSmoothIntraEdges(macroBlock, blockOrigin, blockSize, plane),
                                    residual,
                                    transformSize,
                                    this.bitDepth);
                            }

                            state = default;
                            Span<byte> transformTop = topContexts.Slice(x / 4, width / 4);
                            Span<byte> transformLeft = leftContexts.Slice(y / 4, height / 4);
                            Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
                                component,
                                transformTop,
                                transformLeft,
                                planeBlockSize,
                                transformSize);

                            state.EntropyContext = (byte)(context.SkipContext | (context.DcSignContext << 4));
                            if (skipResidual)
                            {
                                coefficients.Slice(coefficientOffset, sampleCount).Clear();
                            }
                            else
                            {
                                Av1TransformBlockEncoder.EncodeLossyCandidate(
                                    this.blockWorkspace,
                                    writer,
                                    context,
                                    residual,
                                    width,
                                    coefficients.Slice(coefficientOffset, sampleCount),
                                    transformSize,
                                    blockTransformType,
                                    this.quantization.QIndex[0],
                                    this.quantization.DeltaQDc[planeIndex],
                                    this.quantization.DeltaQAc[planeIndex],
                                    this.bitDepth,
                                    component,
                                    this.rateMultiplier,
                                    false,
                                    true,
                                    true,
                                    0,
                                    ref state);
                            }

                            // A luma transform block that quantized to nothing returns to DCT_DCT.
                            // Reference: the update_txk_array() call of encode_block_intra().
                            if (plane == Av1Plane.Y && state.EndOfBlock == 0)
                            {
                                state.TransformType = Av1TransformType.DctDct;
                            }

                            byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                                coefficients.Slice(coefficientOffset, sampleCount),
                                transformSize,
                                state.TransformType,
                                state.EndOfBlock);

                            transformTop.Fill(coefficientContext);
                            transformLeft.Fill(coefficientContext);

                            if (state.EndOfBlock > 0)
                            {
                                TOperator.AddSelectedResidual(
                                    this.blockWorkspace,
                                    transform,
                                    destination.Stride,
                                    transformSize,
                                    plane,
                                    this.bitDepth,
                                    this.quantization.QIndex[0] == 0,
                                    state);
                            }

                            coefficientOffset += sampleCount;
                        }
                    }
                }
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
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
            bool pruneModes = this.picture.Parent.EncodingSpeed == HeifEncodingSpeed.Level9;
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
                            out int transformRate,
                            out long transformDistortion,
                            out bool transformSkip);

                        rate += transformRate;
                        distortion += transformDistortion;
                        skip &= transformSkip;
                    }
                }

                if (rejected)
                {
                    continue;
                }

                rate = (skip ? 0 : rate) + writer.GetSkipCost(skip, skipContext);
                rate += Av1TileWriter.GetLumaModeCost(writer, macroBlock, blockSize, mode, 0, true);
                Av1RateDistortionStatistics statistics = new(this.rateMultiplier, rate, distortion);
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

            uint normalizedSad = bestSad >> BitOperations.Log2((uint)(width * height));
            bool paletteSelected = false;
            if ((!pruneSad || normalizedSad > 20) && blockSize <= Av1BlockSize.Block16x16 && sourceVariance > 200 &&
                Av1TileWriter.IsPaletteAllowed(this.picture.Parent.FrameHeader.AllowScreenContentTools, blockSize))
            {
                paletteSelected = this.SelectLumaPalette(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    coefficients,
                    states,
                    normalizedSad < 500 ? 32 : 64,
                    ref bestStatistics,
                    ref paletteInfo,
                    ref transformSize);

                if (paletteSelected)
                {
                    bestMode = Av1PredictionMode.DC;
                }
            }

            modeInfo.Block.Mode = bestMode;
            modeInfo.Block.UvMode = Av1ChromaPredictionMode.DC;
            modeInfo.Block.TransformSize = transformSize;
            block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            block.PredictionUnit.AngleDelta[0] = 0;
            block.PredictionUnit.AngleDelta[1] = 0;
            if (!paletteSelected)
            {
                this.EncodeSelectedIntraPlane(
                    writer,
                    tileIndex,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    Av1Plane.Y,
                    bestMode,
                    transformSize,
                    lumaOffset);
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

                this.EncodeSelectedIntraPlane(
                    writer,
                    tileIndex,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    Av1Plane.U,
                    Av1PredictionMode.DC,
                    chromaTransform,
                    this.codedAreaChroma);
                this.EncodeSelectedIntraPlane(
                    writer,
                    tileIndex,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    Av1Plane.V,
                    Av1PredictionMode.DC,
                    chromaTransform,
                    this.codedAreaChroma);

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
            int coefficientOffset)
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
            int dcDequantizer = Av1QuantizationLookup.GetDcQuant(
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[planeIndex],
                this.bitDepth);

            int acDequantizer = Av1QuantizationLookup.GetAcQuant(
                this.quantization.QIndex[0],
                this.quantization.DeltaQAc[planeIndex],
                this.bitDepth);

            // The selected predictor writes directly to the retained frame. Its inverse transform adds
            // residuals in place, so subsequent units consume reconstructed neighbors without a pixel copy.
            for (int y = 0; y < extent.Height; y += height)
            {
                for (int x = 0; x < extent.Width; x += width)
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

                    ref Av1EncoderTransformBlockState state = ref states[
                        coefficientOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];

                    state = default;
                    Av1TransformBlockEncoder.EncodeLossy(
                        this.blockWorkspace,
                        residual,
                        coefficients.Slice(coefficientOffset, sampleCount),
                        transformSize,
                        Av1TransformType.DctDct,
                        this.quantization.QIndex[0],
                        this.quantization.DeltaQDc[planeIndex],
                        this.quantization.DeltaQAc[planeIndex],
                        this.bitDepth,
                        ref state);

                    Span<byte> transformTop = topContexts.Slice(x / 4, width / 4);
                    Span<byte> transformLeft = leftContexts.Slice(y / 4, height / 4);
                    if (state.EndOfBlock > 0 && this.quantization.QIndex[0] != 0)
                    {
                        Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
                            component,
                            transformTop,
                            transformLeft,
                            planeBlockSize,
                            transformSize);

                        state.EndOfBlock = writer.OptimizeCoefficients(
                            this.blockWorkspace.TransformCoefficients,
                            coefficients.Slice(coefficientOffset, sampleCount),
                            this.blockWorkspace.DequantizedCoefficients,
                            transformSize,
                            Av1TransformType.DctDct,
                            component,
                            context,
                            dcDequantizer,
                            acDequantizer,
                            this.rateMultiplier,
                            this.bitDepth,
                            false,
                            true,
                            state.EndOfBlock);
                    }

                    byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                        coefficients.Slice(coefficientOffset, sampleCount),
                        transformSize,
                        Av1TransformType.DctDct,
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

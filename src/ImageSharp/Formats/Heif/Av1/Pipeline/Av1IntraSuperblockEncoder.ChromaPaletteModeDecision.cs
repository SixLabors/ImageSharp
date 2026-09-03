// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Provides paired chroma palette mode decisions for intra encoding.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        private bool SelectChromaPalette(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            ushort tileIndex,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Av1TransformBlockContext blueContext,
            Av1TransformBlockContext redContext,
            Span<TSample> candidateBlueReconstruction,
            Span<TSample> candidateRedReconstruction,
            Span<int> candidateBlueCoefficients,
            Span<int> candidateRedCoefficients,
            Span<int> retainedBlueCoefficients,
            Span<int> retainedRedCoefficients,
            ref Av1EncoderTransformBlockState retainedBlueState,
            ref Av1EncoderTransformBlockState retainedRedState,
            ref long bestCost,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const int LumaBlockLength = 8;
            Av1EncoderPaletteWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>().Palette;

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            ObuFrameSize frameSize = this.picture.Parent.FrameHeader.FrameSize;
            int rows = Math.Min(LumaBlockLength, frameSize.FrameHeight - lumaOrigin.Y) >> subsamplingY;
            int columns = Math.Min(LumaBlockLength, frameSize.FrameWidth - lumaOrigin.X) >> subsamplingX;
            int activeSampleCount = rows * columns;
            Span<short> blueSamples = workspace.GetSamples(0)[..activeSampleCount];
            Span<short> redSamples = workspace.GetSamples(1)[..activeSampleCount];
            Buffer2DRegion<TSample> blueSource = this.source.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> redSource = this.source.GetPlane(Av1Plane.V);
            TOperator.CopyPaletteSamples(blueSource, chromaOrigin, rows, columns, blueSamples);
            TOperator.CopyPaletteSamples(redSource, chromaOrigin, rows, columns, redSamples);

            Span<short> uniqueBlueColors = workspace.GetUniqueColors(0);
            Span<short> uniqueRedColors = workspace.GetUniqueColors(1);
            int uniqueBlueColorCount = 0;
            int uniqueRedColorCount = 0;
            short blueMinimum = blueSamples[0];
            short blueMaximum = blueSamples[0];
            short redMinimum = redSamples[0];
            short redMaximum = redSamples[0];
            for (int sampleIndex = 0; sampleIndex < activeSampleCount; sampleIndex++)
            {
                short blueSample = blueSamples[sampleIndex];
                short redSample = redSamples[sampleIndex];
                if (!uniqueBlueColors[..uniqueBlueColorCount].Contains(blueSample))
                {
                    uniqueBlueColors[uniqueBlueColorCount++] = blueSample;
                }

                if (!uniqueRedColors[..uniqueRedColorCount].Contains(redSample))
                {
                    uniqueRedColors[uniqueRedColorCount++] = redSample;
                }

                blueMinimum = Math.Min(blueMinimum, blueSample);
                blueMaximum = Math.Max(blueMaximum, blueSample);
                redMinimum = Math.Min(redMinimum, redSample);
                redMaximum = Math.Max(redMaximum, redSample);
            }

            int maximumColorCount = Math.Max(uniqueBlueColorCount, uniqueRedColorCount);
            if (maximumColorCount < 2)
            {
                return false;
            }

            int maximumPaletteSize = Math.Min(maximumColorCount, Av1Constants.PaletteMaxSize);
            Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContexts = this.picture.PaletteContexts[tileIndex];
            Span<ushort> colorCache = workspace.ColorCache;
            int colorCacheSize = Av1TileWriter.GetPaletteCache(
                paletteContexts,
                macroBlock,
                lumaOrigin,
                Av1Plane.U,
                colorCache);

            colorCache = colorCache[..colorCacheSize];
            int blockSizeContext = Av1TileWriter.GetPaletteBlockSizeContext(BlockSize);
            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            Buffer2DRegion<byte> colorIndexMap = this.superblock.Workspace
                .GetPaletteMaps()
                .GetMap(Av1PlaneType.Uv, width, height);

            Span<byte> retainedColorIndexMap = workspace.RetainedIndices;
            Span<short> blueCentroids = workspace.GetCentroids(0);
            Span<short> redCentroids = workspace.GetCentroids(1);
            Span<byte> colorIndices = workspace.Indices;
            Span<TSample> bluePrediction = workspace.GetPrediction(0);
            Span<TSample> redPrediction = workspace.GetPrediction(1);
            Span<short> blueResidual = workspace.GetResidual(0);
            Span<short> redResidual = workspace.GetResidual(1);
            Span<ushort> bluePaletteColorStorage = workspace.GetPaletteColors(0);
            Span<ushort> redPaletteColorStorage = workspace.GetPaletteColors(1);
            int sampleCount = transformSize.GetSize2d();
            int cacheThreshold = 4 << (this.bitDepth.GetBitCount() - 8);
            bool paletteSelected = false;

            // Chroma uses one paired K-means family; exhaustive size search avoids early header-cost pruning.
            for (int paletteSize = 2; paletteSize <= maximumPaletteSize; paletteSize++)
            {
                Span<short> candidateBlueCentroids = blueCentroids[..paletteSize];
                Span<short> candidateRedCentroids = redCentroids[..paletteSize];
                Av1PaletteKMeans2D.InitializeCentroids(
                    blueMinimum,
                    blueMaximum,
                    redMinimum,
                    redMaximum,
                    candidateBlueCentroids,
                    candidateRedCentroids);

                Av1PaletteKMeans2D.Cluster(
                    blueSamples,
                    redSamples,
                    candidateBlueCentroids,
                    candidateRedCentroids,
                    colorIndices[..activeSampleCount],
                    workspace.GetAlternateCentroids(0),
                    workspace.GetAlternateCentroids(1),
                    workspace.AlternateIndices);

                // Only U participates in the neighbor-color cache. Snap bounded U deltas before sorting,
                // while preserving each U/V centroid as one paired palette entry.
                for (int colorIndex = 0; colorIndex < paletteSize && !colorCache.IsEmpty; colorIndex++)
                {
                    int minimumDifference = Math.Abs(candidateBlueCentroids[colorIndex] - colorCache[0]);
                    int nearestCacheIndex = 0;
                    for (int cacheIndex = 1; cacheIndex < colorCache.Length; cacheIndex++)
                    {
                        int difference = Math.Abs(candidateBlueCentroids[colorIndex] - colorCache[cacheIndex]);
                        if (difference < minimumDifference)
                        {
                            minimumDifference = difference;
                            nearestCacheIndex = cacheIndex;
                        }
                    }

                    if (minimumDifference <= cacheThreshold)
                    {
                        candidateBlueCentroids[colorIndex] = (short)colorCache[nearestCacheIndex];
                    }
                }

                // U is the coded ordering key, so each swap carries its paired V color with it.
                for (int colorIndex = 0; colorIndex < paletteSize - 1; colorIndex++)
                {
                    int minimumIndex = colorIndex;
                    for (int candidateIndex = colorIndex + 1; candidateIndex < paletteSize; candidateIndex++)
                    {
                        if (candidateBlueCentroids[candidateIndex] < candidateBlueCentroids[minimumIndex])
                        {
                            minimumIndex = candidateIndex;
                        }
                    }

                    if (minimumIndex != colorIndex)
                    {
                        (candidateBlueCentroids[colorIndex], candidateBlueCentroids[minimumIndex]) =
                            (candidateBlueCentroids[minimumIndex], candidateBlueCentroids[colorIndex]);

                        (candidateRedCentroids[colorIndex], candidateRedCentroids[minimumIndex]) =
                            (candidateRedCentroids[minimumIndex], candidateRedCentroids[colorIndex]);
                    }
                }

                Av1PaletteKMeans2D.AssignIndices(
                    blueSamples,
                    redSamples,
                    candidateBlueCentroids,
                    candidateRedCentroids,
                    colorIndices);

                for (int row = 0; row < rows; row++)
                {
                    Span<byte> mapRow = colorIndexMap.DangerousGetRowSpan(row)[..width];
                    colorIndices.Slice(row * columns, columns).CopyTo(mapRow);
                    mapRow[columns..].Fill(mapRow[columns - 1]);
                }

                // The shared U/V map covers the complete declared chroma block even at visible frame edges.
                for (int row = rows; row < height; row++)
                {
                    colorIndexMap.DangerousGetRowSpan(rows - 1)[..width]
                        .CopyTo(colorIndexMap.DangerousGetRowSpan(row));
                }

                Span<ushort> bluePaletteColors = bluePaletteColorStorage[..paletteSize];
                Span<ushort> redPaletteColors = redPaletteColorStorage[..paletteSize];
                for (int colorIndex = 0; colorIndex < paletteSize; colorIndex++)
                {
                    bluePaletteColors[colorIndex] = (ushort)candidateBlueCentroids[colorIndex];
                    redPaletteColors[colorIndex] = (ushort)candidateRedCentroids[colorIndex];
                }

                // U and V share one color-index map but reconstruct through their own palette values and
                // residuals. Both preparations remain valid until the next palette-size candidate.
                TOperator.PreparePalette(
                    blueSource,
                    chromaOrigin,
                    bluePaletteColors,
                    colorIndexMap,
                    bluePrediction[..sampleCount],
                    blueResidual[..sampleCount],
                    transformSize);

                TOperator.PreparePalette(
                    redSource,
                    chromaOrigin,
                    redPaletteColors,
                    colorIndexMap,
                    redPrediction[..sampleCount],
                    redResidual[..sampleCount],
                    transformSize);

                // Intra chroma derives one transform type from the shared UV mode. Palette uses UV DC, so
                // both planes use DCT while retaining independent coefficient contexts and end positions.
                Av1EncoderTransformBlockState candidateBlueState = default;
                long distortion = TOperator.EncodePredictionCandidate(
                    this.blockWorkspace,
                    blueSource,
                    chromaOrigin,
                    bluePrediction,
                    blueResidual,
                    candidateBlueReconstruction,
                    transformSize.GetWidth(),
                    candidateBlueCoefficients,
                    transformSize,
                    Av1TransformType.DctDct,
                    Av1Plane.U,
                    this.quantization.QIndex[0],
                    this.quantization.DeltaQDc[(int)Av1Plane.U],
                    this.quantization.DeltaQAc[(int)Av1Plane.U],
                    this.bitDepth,
                    ref candidateBlueState);

                Av1EncoderTransformBlockState candidateRedState = default;
                distortion += TOperator.EncodePredictionCandidate(
                    this.blockWorkspace,
                    redSource,
                    chromaOrigin,
                    redPrediction,
                    redResidual,
                    candidateRedReconstruction,
                    transformSize.GetWidth(),
                    candidateRedCoefficients,
                    transformSize,
                    Av1TransformType.DctDct,
                    Av1Plane.V,
                    this.quantization.QIndex[0],
                    this.quantization.DeltaQDc[(int)Av1Plane.V],
                    this.quantization.DeltaQAc[(int)Av1Plane.V],
                    this.bitDepth,
                    ref candidateRedState);

                int rate = Av1TileWriter.GetChromaModeCost(
                    writer,
                    this.picture.Parent.FrameHeader,
                    colorConfig,
                    modeInfo,
                    BlockSize,
                    lumaMode,
                    Av1ChromaPredictionMode.DC,
                    0);

                rate += writer.GetPaletteUvModeCost(true, hasLumaPalette);
                rate += writer.GetPaletteSizeCost(paletteSize, blockSizeContext, Av1PlaneType.Uv);
                rate += Av1SymbolEncoder.GetPaletteUvColorCost(
                    colorCache,
                    bluePaletteColors,
                    redPaletteColors,
                    this.bitDepth.GetBitCount());

                rate += writer.GetPaletteColorMapCost(
                    paletteSize,
                    Av1PlaneType.Uv,
                    rows,
                    columns,
                    colorIndexMap);

                rate += writer.GetCoefficientCost(
                    transformSize,
                    Av1TransformType.DctDct,
                    lumaMode,
                    candidateBlueCoefficients,
                    Av1ComponentType.Chroma,
                    blueContext,
                    candidateBlueState.EndOfBlock,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet,
                    Av1FilterIntraMode.AllFilterIntraModes,
                    usesInterTransformSet: false);

                // Mode, palette, and color-map syntax is shared by the pair; coefficient syntax and
                // distortion remain per plane before the joint chroma rate-distortion comparison.
                rate += writer.GetCoefficientCost(
                    transformSize,
                    Av1TransformType.DctDct,
                    lumaMode,
                    candidateRedCoefficients,
                    Av1ComponentType.Chroma,
                    redContext,
                    candidateRedState.EndOfBlock,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet,
                    Av1FilterIntraMode.AllFilterIntraModes,
                    usesInterTransformSet: false);

                long candidateCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
                if (candidateCost < bestCost)
                {
                    // Every following palette size overwrites the shared maps and candidate spans, so a
                    // global improvement must retain reconstruction, coefficients, colors, and indices together.
                    Buffer2DRegion<TSample> blueReconstruction = this.reconstruction.GetPlane(Av1Plane.U);
                    Buffer2DRegion<TSample> redReconstruction = this.reconstruction.GetPlane(Av1Plane.V);
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

                    for (int row = 0; row < height; row++)
                    {
                        colorIndexMap.DangerousGetRowSpan(row)[..width]
                            .CopyTo(retainedColorIndexMap[(row * width)..]);
                    }

                    paletteInfo.PaletteSizes[1] = (byte)paletteSize;
                    paletteInfo.SetColors(Av1Plane.U, bluePaletteColors);
                    paletteInfo.SetColors(Av1Plane.V, redPaletteColors);
                    bestCost = candidateCost;
                    paletteSelected = true;
                }
            }

            if (paletteSelected)
            {
                for (int row = 0; row < height; row++)
                {
                    retainedColorIndexMap.Slice(row * width, width)
                        .CopyTo(colorIndexMap.DangerousGetRowSpan(row));
                }
            }

            return paletteSelected;
        }
    }
}

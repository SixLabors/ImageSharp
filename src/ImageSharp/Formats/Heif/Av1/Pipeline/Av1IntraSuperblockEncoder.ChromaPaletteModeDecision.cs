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
            Span<int> retainedBlueCoefficients,
            Span<int> retainedRedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            long workStart = Av1WorkCounters.Start();
            bool workResult = this.SelectChromaPaletteCore(writer, macroBlock, modeInfo, lumaOrigin, chromaOrigin, tileIndex, lumaMode, transformSize, retainedBlueCoefficients, retainedRedCoefficients, retainedBlueStates, retainedRedStates, ref bestStatistics, ref paletteInfo);
            Av1WorkCounters.Stop(Av1WorkCounters.ChromaPalette, workStart);
            return workResult;
        }

        private bool SelectChromaPaletteCore(
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
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            Av1EncoderPaletteWorkspace<TSample> workspace = modeWorkspace.Palette;

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);
            int width = chromaBlockSize.GetWidth();
            int height = chromaBlockSize.GetHeight();
            int sampleCount = width * height;
            int transformBlockCount = sampleCount / transformSize.GetSize2d();
            Span<TSample> candidateBlueReconstruction = modeWorkspace.GetCandidateReconstruction(0)[..sampleCount];
            Span<TSample> candidateRedReconstruction = modeWorkspace.GetCandidateReconstruction(1)[..sampleCount];
            Span<int> candidateBlueCoefficients = modeWorkspace.GetCandidateCoefficients(0)[..sampleCount];
            Span<int> candidateRedCoefficients = modeWorkspace.GetCandidateCoefficients(1)[..sampleCount];
            Span<Av1EncoderTransformBlockState> candidateBlueStates = modeWorkspace.CandidateTransformBlocks[..transformBlockCount];
            Span<Av1EncoderTransformBlockState> candidateRedStates =
                modeWorkspace.CandidateTransformBlocks.Slice(transformBlockCount, transformBlockCount);

            Av1BlockSize maximumUnitBlockSize =
                Av1BlockSize.Block64x64.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

            int contextWidth = chromaBlockSize.Get4x4WideCount();
            int contextHeight = chromaBlockSize.Get4x4HighCount();
            Span<byte> contexts = modeWorkspace.TransformContexts;
            Span<byte> blueTopContexts = contexts[..contextWidth];
            Span<byte> blueLeftContexts = contexts.Slice(contextWidth, contextHeight);
            Span<byte> redTopContexts = contexts.Slice(contextWidth + contextHeight, contextWidth);
            Span<byte> redLeftContexts = contexts.Slice((2 * contextWidth) + contextHeight, contextHeight);
            Av1NeighborArrayUnit<byte> blueNeighbors = this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex];
            Av1NeighborArrayUnit<byte> redNeighbors = this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex];
            int blueTopIndex = blueNeighbors.GetTopIndex(chromaOrigin);
            int blueLeftIndex = blueNeighbors.GetLeftIndex(chromaOrigin);
            int redTopIndex = redNeighbors.GetTopIndex(chromaOrigin);
            int redLeftIndex = redNeighbors.GetLeftIndex(chromaOrigin);
            Av1PlaneRegion<TSample> blueReconstruction = this.reconstruction.GetPlane(Av1Plane.U);
            Av1PlaneRegion<TSample> redReconstruction = this.reconstruction.GetPlane(Av1Plane.V);

            // Clip against the coded mode-info boundary before subsampling, as the decoder does. Visible odd
            // dimensions still have complete coded chroma samples; truncating them here can leave an empty palette input.
            // A chroma plane narrower or shorter than four samples belongs to a block that shares its chroma
            // with the neighbour it pairs with, so its samples cover the pair.
            // Reference: av1_get_block_dimensions().
            int chromaSub8Height = (blockSize.GetHeight() >> subsamplingY) < 4 ? 2 : 0;
            int chromaSub8Width = (blockSize.GetWidth() >> subsamplingX) < 4 ? 2 : 0;
            int rows = ((blockSize.GetHeight() + (Math.Min(0, macroBlock.ToBottomEdge) >> 3)) >> subsamplingY) + chromaSub8Height;
            int columns = ((blockSize.GetWidth() + (Math.Min(0, macroBlock.ToRightEdge) >> 3)) >> subsamplingX) + chromaSub8Width;
            int activeSampleCount = rows * columns;
            Span<short> blueSamples = workspace.GetSamples(0)[..activeSampleCount];
            Span<short> redSamples = workspace.GetSamples(1)[..activeSampleCount];
            Av1PlaneRegion<TSample> blueSource = this.source.GetPlane(Av1Plane.U);
            Av1PlaneRegion<TSample> redSource = this.source.GetPlane(Av1Plane.V);
            TOperator.CopyPaletteSamples(blueSource, chromaOrigin, rows, columns, blueSamples);
            TOperator.CopyPaletteSamples(redSource, chromaOrigin, rows, columns, redSamples);

            // Count native values for clustering, but count occupied 8-bit bins for deciding whether
            // palette is suitable. Binning controls the search only; centroids keep the full sample precision.
            Span<int> colorCounts = workspace.LumaColorCounts[..(1 << this.bitDepth.GetBitCount())];
            int uniqueBlueColorCount = this.CountPaletteColors(
                blueSamples, colorCounts, out int blueColorBins, out short blueMinimum, out short blueMaximum);

            int uniqueRedColorCount = this.CountPaletteColors(
                redSamples, colorCounts, out int redColorBins, out short redMinimum, out short redMaximum);

            int colorBins = Math.Max(blueColorBins, redColorBins);
            if (colorBins <= 1 || colorBins > 64)
            {
                return false;
            }

            int maximumColorCount = Math.Max(uniqueBlueColorCount, uniqueRedColorCount);
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
            int blockSizeContext = Av1TileWriter.GetPaletteBlockSizeContext(blockSize);
            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            Av1PlaneRegion<byte> colorIndexMap = this.superblock.Workspace
                .GetPaletteMaps()
                .GetMap(Av1PlaneType.Uv, width, height);

            Span<byte> retainedColorIndexMap = workspace.RetainedIndices;
            Span<short> blueCentroids = workspace.GetCentroids(0);
            Span<short> redCentroids = workspace.GetCentroids(1);
            Span<byte> colorIndices = workspace.Indices;
            Span<ushort> bluePaletteColorStorage = workspace.GetPaletteColors(0);
            Span<ushort> redPaletteColorStorage = workspace.GetPaletteColors(1);
            int cacheThreshold = 4 << (this.bitDepth.GetBitCount() - 8);
            bool paletteSelected = false;

            // A paired centroid assigns one index to both components. Larger palettes are considered only
            // while their shared syntax can still beat the current complete chroma decision.
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
                    Span<byte> mapRow = colorIndexMap.GetRowSpan(row)[..width];
                    colorIndices.Slice(row * columns, columns).CopyTo(mapRow);
                    mapRow[columns..].Fill(mapRow[columns - 1]);
                }

                // The shared U/V map covers the complete declared chroma block even at visible frame edges.
                for (int row = rows; row < height; row++)
                {
                    colorIndexMap.GetRowSpan(rows - 1)[..width]
                        .CopyTo(colorIndexMap.GetRowSpan(row));
                }

                Span<ushort> bluePaletteColors = bluePaletteColorStorage[..paletteSize];
                Span<ushort> redPaletteColors = redPaletteColorStorage[..paletteSize];
                for (int colorIndex = 0; colorIndex < paletteSize; colorIndex++)
                {
                    bluePaletteColors[colorIndex] = (ushort)candidateBlueCentroids[colorIndex];
                    redPaletteColors[colorIndex] = (ushort)candidateRedCentroids[colorIndex];
                }

                int rate = Av1TileWriter.GetChromaModeCost(
                    writer,
                    this.picture.Parent.FrameHeader,
                    colorConfig,
                    modeInfo,
                    blockSize,
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

                bool pruneByHeader = this.picture.Parent.SpeedSettings.EarlyTerminateChromaPaletteSearch;

                if (pruneByHeader && Av1RateDistortion.GetCost(this.rateMultiplier, rate, 0) >= bestStatistics.Cost)
                {
                    break;
                }

                // Every palette candidate starts from the same external coefficient contexts. Transform
                // traversal updates only these scratch edges, including all transforms of a lossless block.
                blueNeighbors.Top.Slice(blueTopIndex, contextWidth).CopyTo(blueTopContexts);
                blueNeighbors.Left.Slice(blueLeftIndex, contextHeight).CopyTo(blueLeftContexts);
                redNeighbors.Top.Slice(redTopIndex, contextWidth).CopyTo(redTopContexts);
                redNeighbors.Left.Slice(redLeftIndex, contextHeight).CopyTo(redLeftContexts);
                long distortion = this.GetTiledPlaneCost(
                    writer,
                    macroBlock,
                    lumaOrigin,
                    chromaOrigin,
                    blockSize,
                    chromaBlockSize,
                    transformSize,
                    maximumUnitBlockSize,
                    subsamplingX,
                    subsamplingY,
                    lumaMode,
                    Av1PredictionMode.DC,
                    0,
                    Av1Plane.U,
                    blueSource,
                    blueReconstruction,
                    bluePaletteColors,
                    colorIndexMap,
                    candidateBlueReconstruction,
                    candidateBlueCoefficients,
                    candidateBlueStates,
                    blueTopContexts,
                    blueLeftContexts,
                    bestStatistics.Cost,
                    out long bluePredictionDistortion,
                    out int blueRate);

                if (blueRate == int.MaxValue ||
                    Math.Min(
                        Av1RateDistortion.GetCost(this.rateMultiplier, blueRate, distortion),
                        Av1RateDistortion.GetCost(this.rateMultiplier, 0, bluePredictionDistortion)) > bestStatistics.Cost)
                {
                    continue;
                }

                long redDistortion = this.GetTiledPlaneCost(
                    writer,
                    macroBlock,
                    lumaOrigin,
                    chromaOrigin,
                    blockSize,
                    chromaBlockSize,
                    transformSize,
                    maximumUnitBlockSize,
                    subsamplingX,
                    subsamplingY,
                    lumaMode,
                    Av1PredictionMode.DC,
                    0,
                    Av1Plane.V,
                    redSource,
                    redReconstruction,
                    redPaletteColors,
                    colorIndexMap,
                    candidateRedReconstruction,
                    candidateRedCoefficients,
                    candidateRedStates,
                    redTopContexts,
                    redLeftContexts,
                    bestStatistics.Cost,
                    out long redPredictionDistortion,
                    out int redRate);

                if (redRate == int.MaxValue ||
                    Math.Min(
                        Av1RateDistortion.GetCost(this.rateMultiplier, blueRate + redRate, distortion + redDistortion),
                        Av1RateDistortion.GetCost(this.rateMultiplier, 0, bluePredictionDistortion + redPredictionDistortion)) > bestStatistics.Cost)
                {
                    continue;
                }

                distortion += redDistortion;

                rate += blueRate + redRate;
                Av1RateDistortionStatistics candidateStatistics = new(this.rateMultiplier, rate, distortion)
                {
                    ResidualRate = blueRate + redRate
                };

                if (candidateStatistics.Cost < bestStatistics.Cost)
                {
                    // Every following palette size overwrites the shared maps and candidate spans, so a
                    // global improvement must retain reconstruction, coefficients, colors, and indices together.
                    CopyTiledCandidate(
                        candidateBlueReconstruction,
                        candidateBlueCoefficients,
                        candidateBlueStates,
                        blueReconstruction,
                        chromaOrigin,
                        width,
                        GetCodedTransformExtent(macroBlock, chromaBlockSize, transformSize, subsamplingX, subsamplingY),
                        transformSize,
                        retainedBlueCoefficients,
                        retainedBlueStates);

                    CopyTiledCandidate(
                        candidateRedReconstruction,
                        candidateRedCoefficients,
                        candidateRedStates,
                        redReconstruction,
                        chromaOrigin,
                        width,
                        GetCodedTransformExtent(macroBlock, chromaBlockSize, transformSize, subsamplingX, subsamplingY),
                        transformSize,
                        retainedRedCoefficients,
                        retainedRedStates);

                    for (int row = 0; row < height; row++)
                    {
                        colorIndexMap.GetRowSpan(row)[..width]
                            .CopyTo(retainedColorIndexMap[(row * width)..]);
                    }

                    paletteInfo.PaletteSizes[1] = (byte)paletteSize;
                    paletteInfo.SetColors(Av1Plane.U, bluePaletteColors);
                    paletteInfo.SetColors(Av1Plane.V, redPaletteColors);
                    bestStatistics = candidateStatistics;
                    paletteSelected = true;
                }
            }

            if (paletteSelected)
            {
                for (int row = 0; row < height; row++)
                {
                    retainedColorIndexMap.Slice(row * width, width)
                        .CopyTo(colorIndexMap.GetRowSpan(row));
                }
            }

            return paletteSelected;
        }

        internal static bool ShouldPruneChromaPaletteByHeader(ObuFrameType frameType)
            => frameType is ObuFrameType.KeyFrame or ObuFrameType.IntraOnlyFrame;

        private int CountPaletteColors(
            ReadOnlySpan<short> samples,
            Span<int> counts,
            out int occupiedBins,
            out short minimum,
            out short maximum)
        {
            counts.Clear();
            foreach (short sample in samples)
            {
                counts[sample]++;
            }

            // Scanning in sample-value order counts occupied bins without a second histogram. Frequencies
            // remain available for luma's dominant-color seeds; no input sample is rounded or overwritten.
            int colorCount = 0;
            int previousBin = -1;
            int binShift = this.bitDepth.GetBitCount() - 8;
            occupiedBins = 0;
            minimum = short.MaxValue;
            maximum = 0;
            for (int color = 0; color < counts.Length; color++)
            {
                if (counts[color] != 0)
                {
                    colorCount++;
                    minimum = Math.Min(minimum, (short)color);
                    maximum = (short)color;
                    int bin = color >> binShift;
                    if (bin != previousBin)
                    {
                        occupiedBins++;
                        previousBin = bin;
                    }
                }
            }

            return colorCount;
        }
    }
}

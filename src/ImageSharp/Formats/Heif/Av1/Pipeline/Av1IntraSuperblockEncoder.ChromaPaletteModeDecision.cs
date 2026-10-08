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
        /// <summary>
        /// Searches the paired chroma palettes of an intra block and keeps one that costs less than the best result.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="sourcePlanes">The samples of the source frame planes, read once per frame pass.</param>
        /// <param name="reconstructionPlanes">The samples of the reconstructed frame planes, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="modeInfo">The block decisions, with the selected luma mode.</param>
        /// <param name="lumaOrigin">The block origin in luma samples.</param>
        /// <param name="chromaOrigin">The block origin in chroma samples.</param>
        /// <param name="lumaMode">The selected luma mode.</param>
        /// <param name="transformSize">The chroma transform size.</param>
        /// <param name="retainedBlueStates">The transform states of the blue winner.</param>
        /// <param name="retainedRedStates">The transform states of the red winner.</param>
        /// <param name="bestStatistics">The rate and distortion of the best chroma result.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        /// <returns><see langword="true"/> when a palette replaced the best result.</returns>
        private bool SelectChromaPalette(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            in Av1EncoderFrame<TSample>.PlanarSamples sourcePlanes,
            in Av1EncoderFrame<TSample>.PlanarSamples reconstructionPlanes,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            return this.SelectChromaPaletteCore(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                searchCoefficients,
                searchReconstructions,
                in paletteEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                in sourcePlanes,
                in reconstructionPlanes,
                macroBlock,
                modeInfo,
                lumaOrigin,
                chromaOrigin,
                lumaMode,
                transformSize,
                retainedBlueStates,
                retainedRedStates,
                ref bestStatistics,
                ref paletteInfo);
        }

        /// <summary>
        /// Clusters the paired chroma colors of an intra block into palettes of each size, searches them and keeps a
        /// palette that costs less than the best result.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="sourcePlanes">The samples of the source frame planes, read once per frame pass.</param>
        /// <param name="reconstructionPlanes">The samples of the reconstructed frame planes, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="modeInfo">The block decisions, with the selected luma mode.</param>
        /// <param name="lumaOrigin">The block origin in luma samples.</param>
        /// <param name="chromaOrigin">The block origin in chroma samples.</param>
        /// <param name="lumaMode">The selected luma mode.</param>
        /// <param name="transformSize">The chroma transform size.</param>
        /// <param name="retainedBlueStates">The transform states of the blue winner.</param>
        /// <param name="retainedRedStates">The transform states of the red winner.</param>
        /// <param name="bestStatistics">The rate and distortion of the best chroma result.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        /// <returns><see langword="true"/> when a palette replaced the best result.</returns>
        private bool SelectChromaPaletteCore(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            in Av1EncoderFrame<TSample>.PlanarSamples sourcePlanes,
            in Av1EncoderFrame<TSample>.PlanarSamples reconstructionPlanes,
            Av1MacroBlockD macroBlock,
            Av1MacroBlockModeInfo modeInfo,
            Point lumaOrigin,
            Point chromaOrigin,
            Av1PredictionMode lumaMode,
            Av1TransformSize transformSize,
            Span<Av1EncoderTransformBlockState> retainedBlueStates,
            Span<Av1EncoderTransformBlockState> retainedRedStates,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
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

            // Every palette candidate starts from the same tile edges.
            int blueTopIndex = blueCoefficientEdges.GetTopIndex(chromaOrigin);
            int blueLeftIndex = blueCoefficientEdges.GetLeftIndex(chromaOrigin);
            int redTopIndex = redCoefficientEdges.GetTopIndex(chromaOrigin);
            int redLeftIndex = redCoefficientEdges.GetLeftIndex(chromaOrigin);
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

            // Every palette size reads the same source and frame planes, so they are read once.
            ReadOnlySpan<TSample> blueSourceSamples = sourcePlanes.GetPlane(Av1Plane.U);
            ReadOnlySpan<TSample> redSourceSamples = sourcePlanes.GetPlane(Av1Plane.V);
            Span<TSample> blueReconstructionSamples = reconstructionPlanes.GetPlane(Av1Plane.U);
            Span<TSample> redReconstructionSamples = reconstructionPlanes.GetPlane(Av1Plane.V);
            TOperator.CopyPaletteSamples(
                blueSourceSamples[blueSource.GetOffset(chromaOrigin.X, chromaOrigin.Y)..], blueSource.Stride, rows, columns, blueSamples);

            TOperator.CopyPaletteSamples(
                redSourceSamples[redSource.GetOffset(chromaOrigin.X, chromaOrigin.Y)..], redSource.Stride, rows, columns, redSamples);

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
            Span<ushort> colorCache = workspace.ColorCache;
            int colorCacheSize = Av1TileWriter.GetPaletteCache(in paletteEdges, macroBlock, lumaOrigin, Av1Plane.U, colorCache);

            colorCache = colorCache[..colorCacheSize];
            int blockSizeContext = Av1TileWriter.GetPaletteBlockSizeContext(blockSize);
            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            Av1PlaneRegion<byte> colorIndexMap = this.superblock.Workspace
                .GetPaletteMaps()
                .GetMap(Av1PlaneType.Uv, width, height);

            // The map is read once; map row r starts one stride per row after the map origin.
            Span<byte> mapSamples = colorIndexMap.Samples;
            int mapOrigin = colorIndexMap.Origin;
            int mapStride = colorIndexMap.Stride;
            Span<byte> retainedColorIndexMap = workspace.RetainedIndices;
            Span<short> blueCentroids = workspace.GetCentroids(0);
            Span<short> redCentroids = workspace.GetCentroids(1);
            Span<byte> colorIndices = workspace.Indices;
            Span<ushort> bluePaletteColorStorage = workspace.GetPaletteColors(0);
            Span<ushort> redPaletteColorStorage = workspace.GetPaletteColors(1);
            int cacheThreshold = 4 << (this.bitDepth.GetBitCount() - 8);
            bool paletteSelected = false;

            // Every palette size of the block reads the same mode rates.
            Av1ModeCosts modeCosts = tables.ModeCosts;

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
                    Span<byte> mapRow = mapSamples.Slice(mapOrigin + (row * mapStride), width);
                    colorIndices.Slice(row * columns, columns).CopyTo(mapRow);
                    mapRow[columns..].Fill(mapRow[columns - 1]);
                }

                // The shared U/V map covers the complete declared chroma block even at visible frame edges.
                Span<byte> lastMapRow = mapSamples.Slice(mapOrigin + ((rows - 1) * mapStride), width);
                for (int row = rows; row < height; row++)
                {
                    lastMapRow.CopyTo(mapSamples.Slice(mapOrigin + (row * mapStride), width));
                }

                Span<ushort> bluePaletteColors = bluePaletteColorStorage[..paletteSize];
                Span<ushort> redPaletteColors = redPaletteColorStorage[..paletteSize];
                for (int colorIndex = 0; colorIndex < paletteSize; colorIndex++)
                {
                    bluePaletteColors[colorIndex] = (ushort)candidateBlueCentroids[colorIndex];
                    redPaletteColors[colorIndex] = (ushort)candidateRedCentroids[colorIndex];
                }

                int rate = Av1TileWriter.GetChromaModeCost(
                    modeCosts,
                    this.picture.Parent.FrameHeader,
                    colorConfig,
                    modeInfo,
                    blockSize,
                    lumaMode,
                    Av1ChromaPredictionMode.DC,
                    0);

                rate += Av1SymbolEncoder.GetPaletteUvModeCost(modeCosts, true, hasLumaPalette);
                rate += Av1SymbolEncoder.GetPaletteSizeCost(modeCosts, paletteSize, blockSizeContext, Av1PlaneType.Uv);
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
                blueCoefficientEdges.Top.Slice(blueTopIndex, contextWidth).CopyTo(blueTopContexts);
                blueCoefficientEdges.Left.Slice(blueLeftIndex, contextHeight).CopyTo(blueLeftContexts);
                redCoefficientEdges.Top.Slice(redTopIndex, contextWidth).CopyTo(redTopContexts);
                redCoefficientEdges.Left.Slice(redLeftIndex, contextHeight).CopyTo(redLeftContexts);
                long distortion = this.GetTiledPlaneCost(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    searchCoefficients,
                    searchReconstructions,
                    modeInfoGrid,
                    modeInfoAllocation,
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
                    blueSourceSamples,
                    blueReconstruction,
                    blueReconstructionSamples,
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
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    searchCoefficients,
                    searchReconstructions,
                    modeInfoGrid,
                    modeInfoAllocation,
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
                    redSourceSamples,
                    redReconstruction,
                    redReconstructionSamples,
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
                    // Every later palette size overwrites the shared maps and candidate spans, so a new best keeps its
                    // transform states, colors and indices together. The frame keeps what the last trial wrote.
                    Size codedExtent = GetCodedTransformExtent(macroBlock, chromaBlockSize, transformSize, subsamplingX, subsamplingY);
                    CopyTiledCandidate(candidateBlueStates, codedExtent, transformSize, retainedBlueStates);
                    CopyTiledCandidate(candidateRedStates, codedExtent, transformSize, retainedRedStates);

                    for (int row = 0; row < height; row++)
                    {
                        mapSamples.Slice(mapOrigin + (row * mapStride), width).CopyTo(retainedColorIndexMap[(row * width)..]);
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
                    retainedColorIndexMap.Slice(row * width, width).CopyTo(mapSamples.Slice(mapOrigin + (row * mapStride), width));
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

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
/// Provides luma palette mode decisions for intra encoding.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Searches the luma palettes of an intra block and keeps one that costs less than the best result.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="retainedStates">The transform states of the luma winner.</param>
        /// <param name="colorThreshold">The largest number of distinct colors that a palette search accepts.</param>
        /// <param name="dcModeCost">The rate of the DC luma mode, which a palette block signals.</param>
        /// <param name="bestStatistics">The rate and distortion of the best result.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        /// <param name="selectedTransformSize">The transform size of the best result.</param>
        /// <returns><see langword="true"/> when a palette replaced the best result.</returns>
        private bool SelectLumaPalette(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Span<Av1EncoderTransformBlockState> retainedStates,
            int colorThreshold,
            int dcModeCost,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref Av1TransformSize selectedTransformSize)
        {
            return this.SelectLumaPaletteCore(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                blockSize,
                retainedStates,
                colorThreshold,
                dcModeCost,
                ref bestStatistics,
                ref paletteInfo,
                ref selectedTransformSize);
        }

        /// <summary>
        /// Clusters the luma colors of an intra block into palettes of each size, searches their transform sizes and
        /// keeps a palette that costs less than the best result.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="retainedStates">The transform states of the luma winner.</param>
        /// <param name="colorThreshold">The largest number of distinct colors that a palette search accepts.</param>
        /// <param name="dcModeCost">The rate of the DC luma mode, which a palette block signals.</param>
        /// <param name="bestStatistics">The rate and distortion of the best result.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        /// <param name="selectedTransformSize">The transform size of the best result.</param>
        /// <returns><see langword="true"/> when a palette replaced the best result.</returns>
        private bool SelectLumaPaletteCore(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Span<Av1EncoderTransformBlockState> retainedStates,
            int colorThreshold,
            int dcModeCost,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref Av1TransformSize selectedTransformSize)
        {
            Av1EncoderPaletteWorkspace<TSample> workspace = modeWorkspace.Palette;
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            int blockWidth = blockSize.GetWidth();
            int blockHeight = blockSize.GetHeight();

            // Signed frame edges exclude samples beyond the coded mode-info boundary. Palette indices
            // repeat the final active sample when prediction later covers the whole coding block.
            int rows = blockHeight + (Math.Min(0, macroBlock.ToBottomEdge) >> 3);
            int columns = blockWidth + (Math.Min(0, macroBlock.ToRightEdge) >> 3);
            ReadOnlySpan<TSample> block = sourceLuma[sourcePlane.GetOffset(blockOrigin.X, blockOrigin.Y)..];
            Span<int> counts = workspace.LumaColorCounts[..(1 << this.bitDepth.GetBitCount())];
            int colorCount = this.CountPaletteColors(
                block, sourcePlane.Stride, rows, columns, counts, out int occupiedBins, out short minimum, out short maximum);

            if (occupiedBins <= 1 || occupiedBins > colorThreshold)
            {
                return false;
            }

            // Only a block that passes the color gate copies its samples for clustering.
            Span<short> samples = workspace.GetSamples(0)[..(rows * columns)];
            TOperator.CopyPaletteSamples(block, sourcePlane.Stride, rows, columns, samples);

            int maximumPaletteSize = Math.Min(colorCount, Av1Constants.PaletteMaxSize);
            InlineArray8<short> dominantColorStorage = default;
            Span<short> dominantColors = dominantColorStorage;
            int dominantCount = 0;

            // At most eight seeds survive. The loop inserts them by decreasing frequency. Because it visits values in ascending order, a
            // tie keeps the lower color, and the full histogram needs no sort.
            for (int color = 0; color < counts.Length; color++)
            {
                if (counts[color] == 0)
                {
                    continue;
                }

                int index = Math.Min(dominantCount, maximumPaletteSize - 1);
                if (dominantCount == maximumPaletteSize && counts[color] <= counts[dominantColors[index]])
                {
                    continue;
                }

                while (index > 0 && counts[color] > counts[dominantColors[index - 1]])
                {
                    dominantColors[index] = dominantColors[index - 1];
                    index--;
                }

                dominantColors[index] = (short)color;
                dominantCount = Math.Min(dominantCount + 1, maximumPaletteSize);
            }

            int blockSizeContext = Av1TileWriter.GetPaletteBlockSizeContext(blockSize);
            int neighborContext = Av1TileWriter.GetPaletteYModeContext(in paletteEdges, macroBlock, blockOrigin);
            Span<ushort> colorCache = workspace.ColorCache;
            int colorCacheSize = Av1TileWriter.GetPaletteCache(in paletteEdges, macroBlock, blockOrigin, Av1Plane.Y, colorCache);
            Av1PlaneRegion<byte> colorIndexMap = this.superblock.Workspace.GetPaletteMaps().GetMap(Av1PlaneType.Y, blockWidth, blockHeight);
            int transformSizeContext = Av1TileWriter.GetTransformSizeContext(
                in transformEdges,
                modeInfoGrid,
                modeInfoAllocation,
                macroBlock,
                blockOrigin,
                blockSize);

            Av1EncoderSpeedSettings speedSettings = this.picture.Parent.SpeedSettings;
            int speed = (int)speedSettings.Speed;
            int searchLevel = speedSettings.PaletteSearchLevel;
            int headerPruneLevel = speedSettings.LumaPaletteHeaderPruneLevel;

            int sourceVariance = this.GetSourceVariance(
                sourceLuma, sourceBlue, sourceRed, modeWorkspace.GetCandidateReconstruction(0), blockOrigin, blockSize);

            bool selected = false;

            // Every palette size of every family uses the same block, neighbors and workspace buffers, so the loops
            // below read them once here.
            Span<short> centroidStorage = workspace.GetCentroids(0);
            Span<short> alternateCentroids = workspace.GetAlternateCentroids(0);
            Span<byte> clusterIndices = workspace.Indices[..samples.Length];
            Span<byte> alternateIndices = workspace.AlternateIndices;
            LumaPaletteSearch search = new()
            {
                Writer = writer,
                MacroBlock = macroBlock,
                BlockOrigin = blockOrigin,
                BlockSize = blockSize,
                BlockState = this.GetLumaBlockState(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize),
                TransformSizeContext = transformSizeContext,
                SourceVariance = sourceVariance,
                Samples = samples,
                Rows = rows,
                Columns = columns,
                ColorCache = colorCache[..colorCacheSize],
                BlockSizeContext = blockSizeContext,
                NeighborContext = neighborContext,
                ColorIndexMap = colorIndexMap,
                RetainedStates = retainedStates,
                DcModeCost = dcModeCost
            };

            // Frequency seeds precede range-seeded clustering. Each family finishes its coarse/fine
            // or ascending/descending search before the next family reuses the sample workspace.
            for (int family = 0; family < 2; family++)
            {
                bool twoColorClustering = family == 1 && colorCount == 2;
                bool coarseSearch = searchLevel == 1 && colorCount > 2;
                int start = coarseSearch ? maximumPaletteSize is 5 or 8 ? 2 : 3 : 2;
                int end = maximumPaletteSize + 1;
                int step = coarseSearch ? 3 : 1;
                int winner = maximumPaletteSize + 1;
                int lastSearched = start;
                for (int stage = 0; stage < 2; stage++)
                {
                    bool gateHeader = stage == 0 && !twoColorClustering && headerPruneLevel != 0;
                    for (int paletteSize = start; step > 0 ? paletteSize < end : paletteSize > end; paletteSize += step)
                    {
                        Span<short> centroids = centroidStorage[..paletteSize];
                        if (family == 0)
                        {
                            ((ReadOnlySpan<short>)dominantColors)[..paletteSize].CopyTo(centroids);
                        }
                        else if (twoColorClustering)
                        {
                            centroids[0] = minimum;
                            centroids[1] = maximum;
                        }
                        else
                        {
                            Av1PaletteKMeans.InitializeCentroids(minimum, maximum, centroids);
                            Av1PaletteKMeans.Cluster(samples, centroids, clusterIndices, alternateCentroids, alternateIndices);
                        }

                        int candidatePruneLevel = gateHeader ? headerPruneLevel : 0;
                        bool improved = this.EvaluateLumaPaletteCandidate(
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            in lumaCoefficientEdges,
                            in search,
                            centroids,
                            candidatePruneLevel,
                            ref bestStatistics,
                            ref paletteInfo,
                            ref selectedTransformSize,
                            out bool headerBreakout);

                        selected |= improved;
                        lastSearched = paletteSize;
                        if (improved)
                        {
                            winner = paletteSize;
                        }

                        if (headerBreakout)
                        {
                            lastSearched = end;
                            break;
                        }

                        if (searchLevel == 2 && !improved)
                        {
                            break;
                        }
                    }

                    if (stage != 0 || twoColorClustering)
                    {
                        break;
                    }

                    if (coarseSearch)
                    {
                        if (winner > maximumPaletteSize)
                        {
                            break;
                        }

                        start = winner == 2 ? 3 : Math.Max(winner - 1, 2);
                        int last = winner == maximumPaletteSize ? winner - 1 : Math.Min(winner + 1, Av1Constants.PaletteMaxSize);
                        step = Math.Max(1, last - start);
                        end = last + 1;
                    }
                    else
                    {
                        if (lastSearched >= maximumPaletteSize)
                        {
                            break;
                        }

                        start = maximumPaletteSize;
                        end = lastSearched;
                        step = -1;
                    }
                }
            }

            if (selected)
            {
                Span<byte> retainedIndices = workspace.RetainedIndices;
                Span<byte> mapSamples = colorIndexMap.Samples;
                for (int row = 0; row < blockHeight; row++)
                {
                    retainedIndices.Slice(row * blockWidth, blockWidth).CopyTo(mapSamples.Slice(colorIndexMap.GetOffset(0, row), blockWidth));
                }
            }

            return selected;
        }

        /// <summary>
        /// Codes one luma palette candidate: snaps its colors to the neighbor cache, builds the color map, prices the
        /// palette syntax and searches its transform sizes. The candidate replaces the best result if it costs less.
        /// </summary>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the caller read once.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block, which hold the palette buffers.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the type estimates.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="search">The block values that every candidate of the palette search shares.</param>
        /// <param name="centroids">The palette colors of the candidate, which this method sorts and compacts.</param>
        /// <param name="headerPruneLevel">How strongly the palette syntax cost alone can reject the candidate.</param>
        /// <param name="bestStatistics">The statistics of the best candidate so far.</param>
        /// <param name="paletteInfo">The palette of the best candidate so far.</param>
        /// <param name="selectedTransformSize">The transform size of the best candidate so far.</param>
        /// <param name="headerBreakout">Whether the palette syntax cost alone rejected the candidate.</param>
        /// <returns><see langword="true"/> if the candidate became the best result.</returns>
        private bool EvaluateLumaPaletteCandidate(
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in LumaPaletteSearch search,
            Span<short> centroids,
            int headerPruneLevel,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref Av1TransformSize selectedTransformSize,
            out bool headerBreakout)
        {
            return this.EvaluateLumaPaletteCandidateCore(
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in lumaCoefficientEdges,
                in search,
                centroids,
                headerPruneLevel,
                ref bestStatistics,
                ref paletteInfo,
                ref selectedTransformSize,
                out headerBreakout);
        }

        /// <inheritdoc cref="EvaluateLumaPaletteCandidate"/>
        private bool EvaluateLumaPaletteCandidateCore(
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in LumaPaletteSearch search,
            Span<short> centroids,
            int headerPruneLevel,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref Av1TransformSize selectedTransformSize,
            out bool headerBreakout)
        {
            headerBreakout = false;

            // The block and its neighbor values.
            Av1SymbolEncoder writer = search.Writer;
            Av1MacroBlockD macroBlock = search.MacroBlock;
            Point blockOrigin = search.BlockOrigin;
            Av1BlockSize blockSize = search.BlockSize;
            LumaBlockState blockState = search.BlockState;
            int sourceVariance = search.SourceVariance;

            // The visible source samples and the color map.
            ReadOnlySpan<short> samples = search.Samples;
            int rows = search.Rows;
            int columns = search.Columns;
            Av1PlaneRegion<byte> colorIndexMap = search.ColorIndexMap;

            // The palette syntax contexts and rates.
            ReadOnlySpan<ushort> colorCache = search.ColorCache;
            int blockSizeContext = search.BlockSizeContext;
            int neighborContext = search.NeighborContext;
            int transformSizeContext = search.TransformSizeContext;
            int dcModeCost = search.DcModeCost;

            // The transform block states of the best candidate.
            Span<Av1EncoderTransformBlockState> retainedStates = search.RetainedStates;
            int blockWidth = blockSize.GetWidth();
            int blockHeight = blockSize.GetHeight();
            Av1EncoderPaletteWorkspace<TSample> workspace = modeWorkspace.Palette;
            int bitDepth = this.bitDepth.GetBitCount();
            int cacheThreshold = 4 << (bitDepth - 8);

            // Nearby colors snap to a coded-neighbor cache entry when the quantization error is bounded.
            // Snapping can merge centroids, so sorting and compaction below establish the final coded palette.
            for (int colorIndex = 0; colorIndex < centroids.Length && !colorCache.IsEmpty; colorIndex++)
            {
                int minimumDifference = Math.Abs(centroids[colorIndex] - colorCache[0]);
                int nearestCacheIndex = 0;
                for (int cacheIndex = 1; cacheIndex < colorCache.Length; cacheIndex++)
                {
                    int difference = Math.Abs(centroids[colorIndex] - colorCache[cacheIndex]);
                    if (difference < minimumDifference)
                    {
                        minimumDifference = difference;
                        nearestCacheIndex = cacheIndex;
                    }
                }

                if (minimumDifference <= cacheThreshold)
                {
                    centroids[colorIndex] = (short)colorCache[nearestCacheIndex];
                }
            }

            centroids.Sort();
            int paletteSize = 1;
            for (int colorIndex = 1; colorIndex < centroids.Length; colorIndex++)
            {
                if (centroids[colorIndex] != centroids[colorIndex - 1])
                {
                    centroids[paletteSize++] = centroids[colorIndex];
                }
            }

            if (paletteSize < 2)
            {
                return false;
            }

            ReadOnlySpan<short> paletteCentroids = centroids[..paletteSize];
            Span<ushort> paletteColors = workspace.GetPaletteColors(0)[..paletteSize];
            for (int colorIndex = 0; colorIndex < paletteSize; colorIndex++)
            {
                paletteColors[colorIndex] = (ushort)paletteCentroids[colorIndex];
            }

            // The map is contiguous with a stride of the block width, so the indices of the visible samples go straight
            // into it, and a block at a frame edge then spreads them to the full block size in place.
            Span<byte> mapSamples = colorIndexMap.Samples;
            int mapOrigin = colorIndexMap.Origin;
            int mapStride = colorIndexMap.Stride;
            Av1PaletteKMeans.AssignIndices(samples, paletteCentroids, mapSamples.Slice(mapOrigin, rows * columns));
            ExtendPaletteColorMap(mapSamples[mapOrigin..], columns, rows, blockWidth, blockHeight);

            // In an inter frame, the cost of the intra reference joins the total only after the search. Thus the header gate and the
            // candidate comparison leave it out.
            Av1ModeCosts modeCosts = tables.ModeCosts;
            int rate = dcModeCost;
            rate += Av1SymbolEncoder.GetPaletteYModeCost(modeCosts, true, blockSizeContext, neighborContext);
            rate += Av1SymbolEncoder.GetPaletteSizeCost(modeCosts, paletteSize, blockSizeContext, Av1PlaneType.Y);
            rate += Av1SymbolEncoder.GetPaletteYColorCost(colorCache, paletteColors, bitDepth);

            // When the speed settings discount the color map, the search prices only the first map index. The rest of the map costs nothing.
            rate += this.picture.Parent.SpeedSettings.DiscountPaletteColorCost
                ? Av1SymbolEncoder.GetUniformCost(paletteSize, mapSamples[mapOrigin])
                : writer.GetPaletteColorMapCost(paletteSize, Av1PlaneType.Y, rows, columns, colorIndexMap);

            if (headerPruneLevel != 0)
            {
                long headerCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, 0);
                if ((headerCost >> (headerPruneLevel == 1 ? 1 : 0)) > Math.Min(this.blockCostLimit, bestStatistics.Cost))
                {
                    headerBreakout = true;
                    return false;
                }
            }

            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Av1PlaneRegion<TSample> reconstructionPlane = this.reconstruction.GetPlane(Av1Plane.Y);
            bool lossless = this.BlockLossless;
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            int maximumDepth = lossless || this.picture.Parent.FrameHeader.TransformMode != Av1TransformMode.Select ||
                (settings.DeferTransformSizeSearch && this.blockWorkspace.EvaluationStage == Av1EncoderEvaluationStage.Candidate)
                    ? 0
                    : blockWidth == blockHeight ? settings.IntraSquareTransformSearchDepth : settings.IntraRectangularTransformSearchDepth;

            // The palette search bounds every depth by the best candidate so far. A depth is kept when its cost with the palette syntax
            // is less than the cost of that candidate.
            long candidateLimit = Math.Min(this.blockCostLimit, bestStatistics.Cost);
            Av1RateDistortionStatistics candidateStatistics = this.ChooseUniformTransformSize(
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in lumaCoefficientEdges,
                macroBlock,
                sourcePlane,
                reconstructionPlane,
                blockOrigin,
                blockSize,
                in blockState,
                lossless ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize(),
                maximumDepth,
                sourceVariance,
                Av1PredictionMode.DC,
                0,
                Av1FilterIntraMode.AllFilterIntraModes,
                paletteSize,
                paletteColors,
                rate,
                0,
                transformSizeContext,
                candidateLimit,
                candidateLimit,
                modeWorkspace.CandidateTransformBlocks,
                retainedStates,
                out Av1TransformSize transformSize);

            long candidateCost = candidateStatistics.Cost;
            bool selected = candidateCost < candidateLimit;
            if (selected)
            {
                Span<byte> retainedIndices = workspace.RetainedIndices;
                for (int row = 0; row < blockHeight; row++)
                {
                    mapSamples.Slice(mapOrigin + (row * mapStride), blockWidth).CopyTo(retainedIndices[(row * blockWidth)..]);
                }

                paletteInfo.PaletteSizes[0] = (byte)paletteSize;
                paletteInfo.SetColors(Av1Plane.Y, paletteColors);
                selectedTransformSize = transformSize;
                bestStatistics = candidateStatistics;
            }

            Av1EncoderPaletteInfo candidatePalette = default;
            candidatePalette.PaletteSizes[0] = (byte)paletteSize;
            candidatePalette.SetColors(Av1Plane.Y, paletteColors);
            this.RetainLumaCandidate(
                in modeWorkspace,
                new LumaCandidate
                {
                    Mode = Av1PredictionMode.DC,
                    FilterMode = Av1FilterIntraMode.AllFilterIntraModes,
                    Palette = candidatePalette,
                    PaletteHeaderRate = rate,
                    Cost = candidateCost
                },
                blockSize);

            return selected;
        }

        /// <summary>
        /// Spreads the indices of the visible samples of a block, packed with a stride of the visible width, to the full
        /// block size, repeating the last visible column to the right and the last visible row below.
        /// </summary>
        /// <remarks>
        /// The rows move from the last to the first, so that each packed row is read before a wider row overwrites it.
        /// </remarks>
        /// <param name="map">The contiguous map with a stride of <paramref name="width"/>.</param>
        /// <param name="columns">The number of visible columns.</param>
        /// <param name="rows">The number of visible rows.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        private static void ExtendPaletteColorMap(Span<byte> map, int columns, int rows, int width, int height)
        {
            if (columns < width)
            {
                for (int row = rows - 1; row >= 0; row--)
                {
                    Span<byte> mapRow = map.Slice(row * width, width);
                    map.Slice(row * columns, columns).CopyTo(mapRow);
                    mapRow[columns..].Fill(mapRow[columns - 1]);
                }
            }

            Span<byte> lastRow = map.Slice((rows - 1) * width, width);
            for (int row = rows; row < height; row++)
            {
                lastRow.CopyTo(map.Slice(row * width, width));
            }
        }

        /// <summary>
        /// Holds the block values that every candidate of one luma palette search shares. The search builds it once
        /// before it tries its palette sizes.
        /// </summary>
        private readonly ref struct LumaPaletteSearch
        {
            /// <summary>
            /// Gets the tile symbol encoder.
            /// </summary>
            public Av1SymbolEncoder Writer { get; init; }

            /// <summary>
            /// Gets the block and its neighbor availability.
            /// </summary>
            public Av1MacroBlockD MacroBlock { get; init; }

            /// <summary>
            /// Gets the luma block origin.
            /// </summary>
            public Point BlockOrigin { get; init; }

            /// <summary>
            /// Gets the block size.
            /// </summary>
            public Av1BlockSize BlockSize { get; init; }

            /// <summary>
            /// Gets the values of the block that depend only on the block and its neighbors.
            /// </summary>
            public LumaBlockState BlockState { get; init; }

            /// <summary>
            /// Gets the transform size context.
            /// </summary>
            public int TransformSizeContext { get; init; }

            /// <summary>
            /// Gets the source variance of the block.
            /// </summary>
            public int SourceVariance { get; init; }

            /// <summary>
            /// Gets the visible source samples of the block, row after row.
            /// </summary>
            public ReadOnlySpan<short> Samples { get; init; }

            /// <summary>
            /// Gets the number of visible rows.
            /// </summary>
            public int Rows { get; init; }

            /// <summary>
            /// Gets the number of visible columns.
            /// </summary>
            public int Columns { get; init; }

            /// <summary>
            /// Gets the palette colors of the neighbors.
            /// </summary>
            public ReadOnlySpan<ushort> ColorCache { get; init; }

            /// <summary>
            /// Gets the palette block size context.
            /// </summary>
            public int BlockSizeContext { get; init; }

            /// <summary>
            /// Gets the palette context from the neighbors.
            /// </summary>
            public int NeighborContext { get; init; }

            /// <summary>
            /// Gets the color map of the block.
            /// </summary>
            public Av1PlaneRegion<byte> ColorIndexMap { get; init; }

            /// <summary>
            /// Gets the transform block states of the best candidate.
            /// </summary>
            public Span<Av1EncoderTransformBlockState> RetainedStates { get; init; }

            /// <summary>
            /// Gets the rate of the DC mode that a palette block codes.
            /// </summary>
            public int DcModeCost { get; init; }
        }
    }
}

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
        private bool SelectLumaPalette(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Span<int> retainedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedStates,
            int colorThreshold,
            int dcModeCost,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref Av1TransformSize selectedTransformSize)
        {
            long workStart = Av1WorkCounters.Start();
            bool workResult = this.SelectLumaPaletteCore(writer, macroBlock, blockOrigin, blockSize, tileIndex, retainedCoefficients, retainedStates, colorThreshold, dcModeCost, ref bestStatistics, ref paletteInfo, ref selectedTransformSize);
            Av1WorkCounters.Stop(Av1WorkCounters.PaletteYSearch, workStart);
            return workResult;
        }

        private bool SelectLumaPaletteCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Span<int> retainedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedStates,
            int colorThreshold,
            int dcModeCost,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref Av1TransformSize selectedTransformSize)
        {
            Av1WorkCounters.Count(Av1WorkCounters.PaletteYSearch);
            Av1EncoderPaletteWorkspace<TSample> workspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>().Palette;
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            int blockWidth = blockSize.GetWidth();
            int blockHeight = blockSize.GetHeight();

            // Signed frame edges exclude samples beyond the coded mode-info boundary. Palette indices
            // repeat the final active sample when prediction later covers the whole coding block.
            int rows = blockHeight + (Math.Min(0, macroBlock.ToBottomEdge) >> 3);
            int columns = blockWidth + (Math.Min(0, macroBlock.ToRightEdge) >> 3);
            Span<short> samples = workspace.GetSamples(0)[..(rows * columns)];
            TOperator.CopyPaletteSamples(sourcePlane, blockOrigin, rows, columns, samples);
            Span<int> counts = workspace.LumaColorCounts[..(1 << this.bitDepth.GetBitCount())];
            int colorCount = this.CountPaletteColors(samples, counts, out int occupiedBins, out short minimum, out short maximum);
            if (occupiedBins <= 1 || occupiedBins > colorThreshold)
            {
                return false;
            }

            Entropy.Av1SymbolWriter.DiagnosticSymbolTrace?.Add(
                $"PAL {blockOrigin.X},{blockOrigin.Y} {blockSize} colors {colorCount} bins {occupiedBins} best {bestStatistics.Cost} limit {this.blockCostLimit}");

            int maximumPaletteSize = Math.Min(colorCount, Av1Constants.PaletteMaxSize);
            InlineArray8<short> dominantColors = default;
            int dominantCount = 0;

            // Only eight seeds survive. Insert by decreasing frequency; visiting values in ascending
            // order preserves the lower-color tie break without sorting the complete histogram.
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

            Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContexts = this.picture.PaletteContexts[tileIndex];
            int blockSizeContext = Av1TileWriter.GetPaletteBlockSizeContext(blockSize);
            int neighborContext = Av1TileWriter.GetPaletteYModeContext(paletteContexts, macroBlock, blockOrigin);
            Span<ushort> colorCache = workspace.ColorCache;
            int colorCacheSize = Av1TileWriter.GetPaletteCache(paletteContexts, macroBlock, blockOrigin, Av1Plane.Y, colorCache);
            Av1PlaneRegion<byte> colorIndexMap = this.superblock.Workspace.GetPaletteMaps().GetMap(Av1PlaneType.Y, blockWidth, blockHeight);
            int transformSizeContext = Av1TileWriter.GetTransformSizeContext(
                this.picture.TransformFunctionContexts[tileIndex],
                macroBlock,
                blockOrigin,
                blockSize);

            Av1EncoderSpeedSettings speedSettings = this.picture.Parent.SpeedSettings;
            int speed = (int)speedSettings.Speed;
            int searchLevel = speedSettings.PaletteSearchLevel;
            int headerPruneLevel = speedSettings.LumaPaletteHeaderPruneLevel;

            int sourceVariance = this.GetSourceVariance(blockOrigin, blockSize);
            bool selected = false;

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
                        Span<short> centroids = workspace.GetCentroids(0)[..paletteSize];
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
                            Av1PaletteKMeans.Cluster(
                                samples,
                                centroids,
                                workspace.Indices[..samples.Length],
                                workspace.GetAlternateCentroids(0),
                                workspace.AlternateIndices);
                        }

                        bool improved = this.EvaluateLumaPaletteCandidate(
                            writer,
                            macroBlock,
                            blockOrigin,
                            blockSize,
                            tileIndex,
                            transformSizeContext,
                            sourceVariance,
                            samples,
                            rows,
                            columns,
                            colorCache[..colorCacheSize],
                            blockSizeContext,
                            neighborContext,
                            centroids,
                            colorIndexMap,
                            retainedCoefficients,
                            retainedStates,
                            gateHeader ? headerPruneLevel : 0,
                            dcModeCost,
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
                for (int row = 0; row < blockHeight; row++)
                {
                    workspace.RetainedIndices.Slice(row * blockWidth, blockWidth).CopyTo(colorIndexMap.GetRowSpan(row));
                }
            }

            return selected;
        }

        private bool EvaluateLumaPaletteCandidate(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            int transformSizeContext,
            int sourceVariance,
            ReadOnlySpan<short> samples,
            int rows,
            int columns,
            ReadOnlySpan<ushort> colorCache,
            int blockSizeContext,
            int neighborContext,
            Span<short> centroids,
            Av1PlaneRegion<byte> colorIndexMap,
            Span<int> retainedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedStates,
            int headerPruneLevel,
            int dcModeCost,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref Av1TransformSize selectedTransformSize,
            out bool headerBreakout)
        {
            long workStart = Av1WorkCounters.Start();
            bool workResult = this.EvaluateLumaPaletteCandidateCore(writer, macroBlock, blockOrigin, blockSize, tileIndex, transformSizeContext, sourceVariance, samples, rows, columns, colorCache, blockSizeContext, neighborContext, centroids, colorIndexMap, retainedCoefficients, retainedStates, headerPruneLevel, dcModeCost, ref bestStatistics, ref paletteInfo, ref selectedTransformSize, out headerBreakout);
            Av1WorkCounters.Stop(Av1WorkCounters.PaletteCandidate, workStart);
            return workResult;
        }

        private bool EvaluateLumaPaletteCandidateCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            int transformSizeContext,
            int sourceVariance,
            ReadOnlySpan<short> samples,
            int rows,
            int columns,
            ReadOnlySpan<ushort> colorCache,
            int blockSizeContext,
            int neighborContext,
            Span<short> centroids,
            Av1PlaneRegion<byte> colorIndexMap,
            Span<int> retainedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedStates,
            int headerPruneLevel,
            int dcModeCost,
            ref Av1RateDistortionStatistics bestStatistics,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref Av1TransformSize selectedTransformSize,
            out bool headerBreakout)
        {
            Av1WorkCounters.Count(Av1WorkCounters.PaletteYRd);
            headerBreakout = false;
            int blockWidth = blockSize.GetWidth();
            int blockHeight = blockSize.GetHeight();
            Av1EncoderModeDecisionWorkspace<TSample> modeDecisionWorkspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            Av1EncoderPaletteWorkspace<TSample> workspace = modeDecisionWorkspace.Palette;
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

            Span<byte> colorIndices = workspace.Indices;
            Av1PaletteKMeans.AssignIndices(samples, paletteCentroids, colorIndices);
            for (int row = 0; row < rows; row++)
            {
                Span<byte> mapRow = colorIndexMap.GetRowSpan(row)[..blockWidth];
                colorIndices.Slice(row * columns, columns).CopyTo(mapRow);
                mapRow[columns..].Fill(mapRow[columns - 1]);
            }

            // Padding repeats the last active edge so transform prediction matches coded-frame edge extension.
            for (int row = rows; row < blockHeight; row++)
            {
                colorIndexMap.GetRowSpan(rows - 1)[..blockWidth]
                    .CopyTo(colorIndexMap.GetRowSpan(row));
            }

            // The intra reference cost of an inter frame joins the total only after the search, so the header gate
            // and the candidate comparison leave it out. Reference: intra_mode_info_cost_y() in palette_rd_y(), and
            // the ref_frame_cost that av1_search_palette_mode() adds to rate2.
            int rate = dcModeCost;
            rate += writer.GetPaletteYModeCost(true, blockSizeContext, neighborContext);
            rate += writer.GetPaletteSizeCost(paletteSize, blockSizeContext, Av1PlaneType.Y);
            rate += Av1SymbolEncoder.GetPaletteYColorCost(colorCache, paletteColors, bitDepth);
            rate += writer.GetPaletteColorMapCost(paletteSize, Av1PlaneType.Y, rows, columns, colorIndexMap);
            if (headerPruneLevel != 0)
            {
                long headerCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, 0);
                if ((headerCost >> (headerPruneLevel == 1 ? 1 : 0)) > Math.Min(this.blockCostLimit, bestStatistics.Cost))
                {
                    Entropy.Av1SymbolWriter.DiagnosticSymbolTrace?.Add(
                        $"PALGATE {blockOrigin.X},{blockOrigin.Y} header {rate} headerCost {headerCost} best {bestStatistics.Cost} limit {this.blockCostLimit}");

                    headerBreakout = true;
                    return false;
                }
            }

            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Av1PlaneRegion<TSample> reconstructionPlane = this.reconstruction.GetPlane(Av1Plane.Y);
            int sampleCount = blockWidth * blockHeight;
            Span<TSample> candidateReconstruction = modeDecisionWorkspace.GetCandidateReconstruction(0)[..sampleCount];
            Span<int> candidateCoefficients = modeDecisionWorkspace.GetCandidateCoefficients(0)[..sampleCount];
            bool lossless = this.picture.Parent.FrameHeader.CodedLossless;
            Av1TransformSize transformSize = lossless ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize();
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            int maximumDepth = lossless || this.picture.Parent.FrameHeader.TransformMode != Av1TransformMode.Select ||
                (settings.DeferTransformSizeSearch && this.blockWorkspace.EvaluationStage == Av1EncoderEvaluationStage.Candidate)
                    ? 0
                    : blockWidth == blockHeight ? settings.IntraSquareTransformSearchDepth : settings.IntraRectangularTransformSearchDepth;

            // Every depth opens with the best candidate cost so far. With the breakout, a later depth also
            // stops at the best depth this candidate already measured, taken without its palette syntax,
            // because the running cost inside a depth carries only the non-skip flag, the size syntax and
            // the coefficients.
            // Reference: the rd_thresh of choose_tx_size_type_from_rd(), which palette_rd_y() reaches
            // through av1_pick_uniform_tx_size_type_yrd() with *best_rd.
            long costLimit = Math.Min(this.blockCostLimit, bestStatistics.Cost);
            long bestDepthCost = long.MaxValue;
            long previousCost = long.MaxValue;
            bool selected = false;
            long candidateCost = long.MaxValue;

            // Each size covers the complete coding block. Preserve only improving mosaics before the
            // next size reuses candidate storage; coefficient states retain their transform-unit spacing.
            for (int depth = 0; depth <= maximumDepth; depth++, transformSize = transformSize.GetSubSize())
            {
                int transformCount = sampleCount / transformSize.GetSize2d();
                Span<Av1EncoderTransformBlockState> candidateStates = modeDecisionWorkspace.CandidateTransformBlocks[..transformCount];
                Av1RateDistortionStatistics candidateStatistics = this.GetUniformLumaCandidateCost(
                    writer,
                    macroBlock,
                    sourcePlane,
                    reconstructionPlane,
                    blockOrigin,
                    blockSize,
                    transformSize,
                    tileIndex,
                    sourceVariance,
                    Av1PredictionMode.DC,
                    0,
                    Av1FilterIntraMode.AllFilterIntraModes,
                    paletteSize,
                    paletteColors,
                    rate,
                    0,
                    transformSizeContext,
                    costLimit,
                    candidateReconstruction,
                    candidateCoefficients,
                    candidateStates,
                    out bool skipSmallerTransforms);

                candidateCost = Math.Min(candidateCost, candidateStatistics.Cost);
                if (settings.UseIntraTransformRdBreakout && candidateStatistics.Cost < bestDepthCost)
                {
                    bestDepthCost = candidateStatistics.Cost;
                    costLimit = Math.Min(costLimit, candidateStatistics.TransformCost);
                }

                if (candidateStatistics.Cost < Math.Min(this.blockCostLimit, bestStatistics.Cost))
                {
                    CopyTiledCandidate(
                        candidateReconstruction,
                        candidateCoefficients,
                        candidateStates,
                        reconstructionPlane,
                        blockOrigin,
                        blockWidth,
                        GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0),
                        transformSize,
                        retainedCoefficients,
                        retainedStates);

                    for (int row = 0; row < blockHeight; row++)
                    {
                        colorIndexMap.GetRowSpan(row)[..blockWidth].CopyTo(workspace.RetainedIndices[(row * blockWidth)..]);
                    }

                    paletteInfo.PaletteSizes[0] = (byte)paletteSize;
                    paletteInfo.SetColors(Av1Plane.Y, paletteColors);
                    selectedTransformSize = transformSize;
                    bestStatistics = candidateStatistics;
                    selected = true;
                }

                if (skipSmallerTransforms || transformSize == Av1TransformSize.Size4x4 ||
                    (depth > 0 && depth < maximumDepth && sourceVariance < 256 &&
                        previousCost != long.MaxValue && candidateStatistics.Cost > previousCost))
                {
                    break;
                }

                previousCost = candidateStatistics.Cost;
            }

            Entropy.Av1SymbolWriter.DiagnosticSymbolTrace?.Add(
                $"PALCAND {blockOrigin.X},{blockOrigin.Y} n {paletteSize} header {rate} cost {candidateCost} best {bestStatistics.Cost} limit {this.blockCostLimit} selected {selected}");

            Av1EncoderPaletteInfo candidatePalette = default;
            candidatePalette.PaletteSizes[0] = (byte)paletteSize;
            candidatePalette.SetColors(Av1Plane.Y, paletteColors);
            this.RetainLumaCandidate(
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
    }
}

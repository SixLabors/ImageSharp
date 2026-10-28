// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
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
        where TOperator : struct, IBlockEncodingOperator<TSample, TOperator>
    {
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
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
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
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
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
            SearchControl control = TOperator.CreateSearchControl(ref this);

            return control.SelectChromaPalette(
                writer,
                in tables,
                modeWorkspace.Cast<byte>(),
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
                MemoryMarshal.AsBytes(sourceLuma),
                MemoryMarshal.AsBytes(sourceBlue),
                MemoryMarshal.AsBytes(sourceRed),
                MemoryMarshal.AsBytes(reconstructionLuma),
                MemoryMarshal.AsBytes(reconstructionBlue),
                MemoryMarshal.AsBytes(reconstructionRed),
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
        /// Counts the distinct values of a source block, straight from the source plane, before any copy of the samples.
        /// </summary>
        /// <param name="block">The source block, from its top-left sample.</param>
        /// <param name="stride">The number of samples between rows of <paramref name="block"/>.</param>
        /// <param name="rows">The active row count.</param>
        /// <param name="columns">The active column count.</param>
        /// <param name="counts">The histogram with one bin per sample value, which this method fills.</param>
        /// <param name="occupiedBins">The number of occupied eight-bit bins, which gates the palette search.</param>
        /// <param name="minimum">The smallest sample value.</param>
        /// <param name="maximum">The largest sample value.</param>
        /// <returns>The number of distinct sample values.</returns>
        private int CountPaletteColors(
            ReadOnlySpan<TSample> block,
            int stride,
            int rows,
            int columns,
            Span<int> counts,
            out int occupiedBins,
            out short minimum,
            out short maximum)
        {
            // The block is read in place, so a block that the color gate rejects is never copied.
            counts.Clear();
            for (int row = 0; row < rows; row++)
            {
                foreach (TSample sample in block.Slice(row * stride, columns))
                {
                    counts[TOperator.GetSampleValue(sample)]++;
                }
            }

            // A scan in sample-value order counts occupied bins without a second histogram. The frequencies stay available for the
            // dominant-color seeds of luma. No input sample is rounded or overwritten.
            int colorCount = 0;
            int previousBin = -1;
            int binShift = this.State.BitDepth.GetBitCount() - 8;
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

        /// <summary>Counts native colors and occupied eight-bit bins for one palette source plane.</summary>
        /// <param name="plane">The chroma plane.</param>
        /// <param name="samples">The complete source plane storage.</param>
        /// <param name="origin">The chroma block origin.</param>
        /// <param name="rows">The visible row count.</param>
        /// <param name="columns">The visible column count.</param>
        /// <param name="counts">The reusable native color counters.</param>
        /// <param name="bins">The occupied eight-bit bin count used to prune palette search.</param>
        /// <param name="minimum">The smallest native sample.</param>
        /// <param name="maximum">The largest native sample.</param>
        /// <returns>The number of distinct native colors.</returns>
        public int CountPlanePaletteColors(
            Av1Plane plane,
            ReadOnlySpan<TSample> samples,
            Point origin,
            int rows,
            int columns,
            Span<int> counts,
            out int bins,
            out short minimum,
            out short maximum)
        {
            Av1PlaneRegion<TSample> source = this.source.GetPlane(plane);
            return this.CountPaletteColors(
                Av1TransformBlockEncoder.GetPlaneSpan(samples, source, origin),
                source.Stride,
                rows,
                columns,
                counts,
                out bins,
                out minimum,
                out maximum);
        }

        /// <summary>Copies an admitted palette block into the existing signed-sample clustering workspace.</summary>
        /// <param name="plane">The chroma plane.</param>
        /// <param name="samples">The complete source plane storage.</param>
        /// <param name="origin">The chroma block origin.</param>
        /// <param name="rows">The visible row count.</param>
        /// <param name="columns">The visible column count.</param>
        /// <param name="destination">The existing clustering storage.</param>
        public readonly void CopyPlanePaletteSamples(
            Av1Plane plane,
            ReadOnlySpan<TSample> samples,
            Point origin,
            int rows,
            int columns,
            Span<short> destination)
        {
            Av1PlaneRegion<TSample> source = this.source.GetPlane(plane);
            TOperator.CopyPaletteSamples(Av1TransformBlockEncoder.GetPlaneSpan(samples, source, origin), source.Stride, rows, columns, destination);
        }
    }

    /// <summary>Shares search decisions while typed workers process the native samples.</summary>
    internal readonly ref partial struct SearchControl
    {
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
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
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
        public bool SelectChromaPalette(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<byte> modeWorkspace,
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
            ReadOnlySpan<byte> sourceLuma,
            ReadOnlySpan<byte> sourceBlue,
            ReadOnlySpan<byte> sourceRed,
            Span<byte> reconstructionLuma,
            Span<byte> reconstructionBlue,
            Span<byte> reconstructionRed,
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
            Av1EncoderPaletteWorkspace<byte> workspace = modeWorkspace.Palette;

            ObuColorConfig colorConfig = this.state.Picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);
            int width = chromaBlockSize.GetWidth();
            int height = chromaBlockSize.GetHeight();
            int sampleCount = width * height;
            int transformBlockCount = sampleCount / transformSize.GetSize2d();
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

            // The block clips against the coded mode-info boundary before subsampling, as the decoder does. Visible odd dimensions still
            // have complete coded chroma samples. A clip after subsampling can truncate them and leave an empty palette input. A chroma
            // plane narrower or shorter than four samples belongs to a block that shares its chroma with the neighbor it pairs with, so
            // its samples cover the pair.
            int chromaSub8Height = (blockSize.GetHeight() >> subsamplingY) < 4 ? 2 : 0;
            int chromaSub8Width = (blockSize.GetWidth() >> subsamplingX) < 4 ? 2 : 0;
            int rows = ((blockSize.GetHeight() + (Math.Min(0, macroBlock.ToBottomEdge) >> 3)) >> subsamplingY) + chromaSub8Height;
            int columns = ((blockSize.GetWidth() + (Math.Min(0, macroBlock.ToRightEdge) >> 3)) >> subsamplingX) + chromaSub8Width;
            int activeSampleCount = rows * columns;
            Span<short> blueSamples = workspace.GetSamples(0)[..activeSampleCount];
            Span<short> redSamples = workspace.GetSamples(1)[..activeSampleCount];

            // Every palette size reads the same source and frame planes, so they are read once.
            ReadOnlySpan<byte> blueSourceSamples = sourceBlue;
            ReadOnlySpan<byte> redSourceSamples = sourceRed;
            Span<byte> blueReconstructionSamples = reconstructionBlue;
            Span<byte> redReconstructionSamples = reconstructionRed;

            // The clustering counts native values, but the palette decision counts occupied 8-bit bins. The bins control the search only.
            // The centroids keep the full sample precision.
            Span<int> colorCounts = workspace.LumaColorCounts[..(1 << this.state.BitDepth.GetBitCount())];
            int uniqueBlueColorCount = this.CountPlanePaletteColors(
                Av1Plane.U, sourceBlue, chromaOrigin, rows, columns, colorCounts, out int blueColorBins, out short blueMinimum, out short blueMaximum);

            int uniqueRedColorCount = this.CountPlanePaletteColors(
                Av1Plane.V, sourceRed, chromaOrigin, rows, columns, colorCounts, out int redColorBins, out short redMinimum, out short redMaximum);

            int colorBins = Math.Max(blueColorBins, redColorBins);
            if (colorBins <= 1 || colorBins > 64)
            {
                return false;
            }

            // Only a block that passes the color gate copies its samples for clustering.
            this.CopyPlanePaletteSamples(Av1Plane.U, sourceBlue, chromaOrigin, rows, columns, blueSamples);
            this.CopyPlanePaletteSamples(Av1Plane.V, sourceRed, chromaOrigin, rows, columns, redSamples);

            int maximumColorCount = Math.Max(uniqueBlueColorCount, uniqueRedColorCount);
            int maximumPaletteSize = Math.Min(maximumColorCount, Av1Constants.PaletteMaxSize);
            Span<ushort> colorCache = workspace.ColorCache;
            int colorCacheSize = Av1TileWriter.GetPaletteCache(in paletteEdges, macroBlock, lumaOrigin, Av1Plane.U, colorCache);

            colorCache = colorCache[..colorCacheSize];
            int blockSizeContext = Av1TileWriter.GetPaletteBlockSizeContext(blockSize);
            bool hasLumaPalette = paletteInfo.PaletteSizes[0] != 0;
            Av1PlaneRegion<byte> colorIndexMap = this.state.Superblock.Workspace
                .GetPaletteMaps()
                .GetMap(Av1PlaneType.Uv, width, height);

            // The map is read once. Map row r starts r strides after the map origin.
            Span<byte> mapSamples = colorIndexMap.Samples;
            int mapOrigin = colorIndexMap.Origin;
            int mapStride = colorIndexMap.Stride;
            Span<byte> retainedColorIndexMap = workspace.RetainedIndices;
            Span<short> blueCentroids = workspace.GetCentroids(0);
            Span<short> redCentroids = workspace.GetCentroids(1);
            Span<byte> colorIndices = workspace.Indices;
            Span<ushort> bluePaletteColorStorage = workspace.GetPaletteColors(0);
            Span<ushort> redPaletteColorStorage = workspace.GetPaletteColors(1);
            int cacheThreshold = 4 << (this.state.BitDepth.GetBitCount() - 8);
            bool paletteSelected = false;

            // Every palette size of the block reads the same mode rates, and signals the same DC chroma mode.
            Av1ModeCosts modeCosts = tables.ModeCosts;
            int dcModeRate = Av1TileWriter.GetChromaModeCost(
                modeCosts,
                this.state.Picture.Parent.FrameHeader,
                colorConfig,
                modeInfo,
                blockSize,
                lumaMode,
                Av1ChromaPredictionMode.DC,
                0);

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

                // The map is contiguous with a stride of the block width, so the indices of the visible samples go
                // straight into it. The shared U/V map covers the complete declared chroma block even at visible frame
                // edges, so a block at an edge then spreads them to the full block size in place.
                Av1PaletteKMeans2D.AssignIndices(
                    blueSamples,
                    redSamples,
                    candidateBlueCentroids,
                    candidateRedCentroids,
                    mapSamples.Slice(mapOrigin, rows * columns));

                ExtendPaletteColorMap(mapSamples[mapOrigin..], columns, rows, width, height);

                Span<ushort> bluePaletteColors = bluePaletteColorStorage[..paletteSize];
                Span<ushort> redPaletteColors = redPaletteColorStorage[..paletteSize];
                for (int colorIndex = 0; colorIndex < paletteSize; colorIndex++)
                {
                    bluePaletteColors[colorIndex] = (ushort)candidateBlueCentroids[colorIndex];
                    redPaletteColors[colorIndex] = (ushort)candidateRedCentroids[colorIndex];
                }

                int rate = dcModeRate;
                rate += Av1SymbolEncoder.GetPaletteUvModeCost(modeCosts, true, hasLumaPalette);
                rate += Av1SymbolEncoder.GetPaletteSizeCost(modeCosts, paletteSize, blockSizeContext, Av1PlaneType.Uv);
                rate += Av1SymbolEncoder.GetPaletteUvColorCost(
                    colorCache,
                    bluePaletteColors,
                    redPaletteColors,
                    this.state.BitDepth.GetBitCount());

                rate += writer.GetPaletteColorMapCost(
                    paletteSize,
                    Av1PlaneType.Uv,
                    rows,
                    columns,
                    colorIndexMap);

                bool pruneByHeader = this.state.Picture.Parent.SpeedSettings.EarlyTerminateChromaPaletteSearch;

                if (pruneByHeader && Av1RateDistortion.GetCost(this.state.RateMultiplier, rate, 0) >= bestStatistics.Cost)
                {
                    break;
                }

                // Every palette candidate starts from the same external coefficient contexts. Transform
                // traversal updates only these copied context edges, including all transforms of a lossless block.
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
                        blueSourceSamples,
                        blueReconstructionSamples,
                        bluePaletteColors,
                        colorIndexMap,
                        candidateBlueCoefficients,
                        candidateBlueStates,
                        blueTopContexts,
                        blueLeftContexts,
                        bestStatistics.Cost,
                        out long bluePredictionDistortion,
                        out int blueRate);

                if (blueRate == int.MaxValue ||
                    Math.Min(
                        Av1RateDistortion.GetCost(this.state.RateMultiplier, blueRate, distortion),
                        Av1RateDistortion.GetCost(this.state.RateMultiplier, 0, bluePredictionDistortion)) > bestStatistics.Cost)
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
                        redSourceSamples,
                        redReconstructionSamples,
                        redPaletteColors,
                        colorIndexMap,
                        candidateRedCoefficients,
                        candidateRedStates,
                        redTopContexts,
                        redLeftContexts,
                        bestStatistics.Cost,
                        out long redPredictionDistortion,
                        out int redRate);

                if (redRate == int.MaxValue ||
                    Math.Min(
                        Av1RateDistortion.GetCost(this.state.RateMultiplier, blueRate + redRate, distortion + redDistortion),
                        Av1RateDistortion.GetCost(this.state.RateMultiplier, 0, bluePredictionDistortion + redPredictionDistortion)) > bestStatistics.Cost)
                {
                    continue;
                }

                distortion += redDistortion;

                rate += blueRate + redRate;
                Av1RateDistortionStatistics candidateStatistics = new(this.state.RateMultiplier, rate, distortion)
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
    }
}

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
            Buffer2DRegion<TSample> sourcePlane,
            Buffer2DRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            ushort tileIndex,
            Av1TransformSetType transformSetType,
            Av1TransformBlockContext blockContext,
            int transformSizeRate,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            Span<int> retainedCoefficients,
            ref Av1EncoderTransformBlockState retainedState,
            ref long bestCost,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const int BlockLength = 8;
            Av1EncoderPaletteWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>().Palette;

            ObuFrameSize frameSize = this.picture.Parent.FrameHeader.FrameSize;
            int rows = Math.Min(BlockLength, frameSize.FrameHeight - blockOrigin.Y);
            int columns = Math.Min(BlockLength, frameSize.FrameWidth - blockOrigin.X);
            int sampleCount = rows * columns;
            Span<short> samples = workspace.GetSamples(0)[..sampleCount];
            TOperator.CopyPaletteSamples(sourcePlane, blockOrigin, rows, columns, samples);

            Span<short> uniqueColors = workspace.GetUniqueColors(0);
            Span<int> colorCounts = workspace.LumaColorCounts;
            int uniqueColorCount = 0;
            short minimum = samples[0];
            short maximum = samples[0];
            foreach (short sample in samples)
            {
                int colorIndex = uniqueColors[..uniqueColorCount].IndexOf(sample);
                if (colorIndex >= 0)
                {
                    colorCounts[colorIndex]++;
                }
                else
                {
                    uniqueColors[uniqueColorCount] = sample;
                    colorCounts[uniqueColorCount] = 1;
                    uniqueColorCount++;
                }

                minimum = Math.Min(minimum, sample);
                maximum = Math.Max(maximum, sample);
            }

            if (uniqueColorCount < 2)
            {
                return false;
            }

            int maximumPaletteSize = Math.Min(uniqueColorCount, Av1Constants.PaletteMaxSize);
            Span<byte> dominantOrder = workspace.LumaDominantOrder;
            for (int index = 0; index < uniqueColorCount; index++)
            {
                dominantOrder[index] = (byte)index;
            }

            // Count order chooses the colors that explain most samples first; sample value resolves equal counts.
            for (int index = 1; index < uniqueColorCount; index++)
            {
                byte current = dominantOrder[index];
                int destination = index;
                while (destination > 0)
                {
                    byte preceding = dominantOrder[destination - 1];
                    bool precedes = colorCounts[current] > colorCounts[preceding] ||
                        (colorCounts[current] == colorCounts[preceding] && uniqueColors[current] < uniqueColors[preceding]);

                    if (!precedes)
                    {
                        break;
                    }

                    dominantOrder[destination] = preceding;
                    destination--;
                }

                dominantOrder[destination] = current;
            }

            Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContexts = this.picture.PaletteContexts[tileIndex];
            int blockSizeContext = Av1TileWriter.GetPaletteBlockSizeContext(BlockSize);
            int neighborContext = Av1TileWriter.GetPaletteYModeContext(paletteContexts, macroBlock, blockOrigin);
            Span<ushort> colorCache = workspace.ColorCache;
            int colorCacheSize = Av1TileWriter.GetPaletteCache(
                paletteContexts,
                macroBlock,
                blockOrigin,
                Av1Plane.Y,
                colorCache);

            Buffer2DRegion<byte> colorIndexMap = this.superblock.Workspace
                .GetPaletteMaps()
                .GetMap(Av1PlaneType.Y, BlockLength, BlockLength);

            Span<byte> retainedColorIndexMap = workspace.RetainedIndices;
            Span<short> centroids = workspace.GetCentroids(0);
            bool paletteSelected = false;

            // Exhaustive ascending size search avoids the reference encoder's speed-dependent pruning.
            for (int paletteSize = 2; paletteSize <= maximumPaletteSize; paletteSize++)
            {
                for (int index = 0; index < paletteSize; index++)
                {
                    centroids[index] = uniqueColors[dominantOrder[index]];
                }

                this.EvaluateLumaPaletteCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    transformSetType,
                    blockContext,
                    transformSizeRate,
                    samples,
                    rows,
                    columns,
                    colorCache[..colorCacheSize],
                    blockSizeContext,
                    neighborContext,
                    centroids[..paletteSize],
                    colorIndexMap,
                    candidateReconstruction,
                    candidateCoefficients,
                    retainedCoefficients,
                    retainedColorIndexMap,
                    reconstructionPlane,
                    ref retainedState,
                    ref bestCost,
                    ref paletteInfo,
                    ref paletteSelected);
            }

            if (uniqueColorCount == 2)
            {
                centroids[0] = minimum;
                centroids[1] = maximum;
                this.EvaluateLumaPaletteCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    transformSetType,
                    blockContext,
                    transformSizeRate,
                    samples,
                    rows,
                    columns,
                    colorCache[..colorCacheSize],
                    blockSizeContext,
                    neighborContext,
                    centroids[..2],
                    colorIndexMap,
                    candidateReconstruction,
                    candidateCoefficients,
                    retainedCoefficients,
                    retainedColorIndexMap,
                    reconstructionPlane,
                    ref retainedState,
                    ref bestCost,
                    ref paletteInfo,
                    ref paletteSelected);
            }
            else
            {
                Span<byte> clusterIndices = workspace.Indices[..sampleCount];
                for (int paletteSize = 2; paletteSize <= maximumPaletteSize; paletteSize++)
                {
                    Span<short> candidateCentroids = centroids[..paletteSize];
                    Av1PaletteKMeans.InitializeCentroids(minimum, maximum, candidateCentroids);
                    Av1PaletteKMeans.Cluster(
                        samples,
                        candidateCentroids,
                        clusterIndices,
                        workspace.GetAlternateCentroids(0),
                        workspace.AlternateIndices);

                    this.EvaluateLumaPaletteCandidate(
                        writer,
                        macroBlock,
                        blockOrigin,
                        transformSetType,
                        blockContext,
                        transformSizeRate,
                        samples,
                        rows,
                        columns,
                        colorCache[..colorCacheSize],
                        blockSizeContext,
                        neighborContext,
                        candidateCentroids,
                        colorIndexMap,
                        candidateReconstruction,
                        candidateCoefficients,
                        retainedCoefficients,
                        retainedColorIndexMap,
                        reconstructionPlane,
                        ref retainedState,
                        ref bestCost,
                        ref paletteInfo,
                        ref paletteSelected);
                }
            }

            if (paletteSelected)
            {
                for (int row = 0; row < BlockLength; row++)
                {
                    retainedColorIndexMap.Slice(row * BlockLength, BlockLength)
                        .CopyTo(colorIndexMap.DangerousGetRowSpan(row));
                }
            }

            return paletteSelected;
        }

        private void EvaluateLumaPaletteCandidate(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1TransformSetType transformSetType,
            Av1TransformBlockContext blockContext,
            int transformSizeRate,
            ReadOnlySpan<short> samples,
            int rows,
            int columns,
            ReadOnlySpan<ushort> colorCache,
            int blockSizeContext,
            int neighborContext,
            Span<short> centroids,
            Buffer2DRegion<byte> colorIndexMap,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            Span<int> retainedCoefficients,
            Span<byte> retainedColorIndexMap,
            Buffer2DRegion<TSample> reconstructionPlane,
            ref Av1EncoderTransformBlockState retainedState,
            ref long bestCost,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref bool paletteSelected)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;
            const int BlockLength = 8;
            Av1EncoderPaletteWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>().Palette;

            int bitDepth = this.bitDepth.GetBitCount();
            int cacheThreshold = 4 << (bitDepth - 8);
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
                return;
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
                Span<byte> mapRow = colorIndexMap.DangerousGetRowSpan(row)[..BlockLength];
                colorIndices.Slice(row * columns, columns).CopyTo(mapRow);
                mapRow[columns..].Fill(mapRow[columns - 1]);
            }

            // Padding repeats the last active edge so transform prediction matches coded-frame edge extension.
            for (int row = rows; row < BlockLength; row++)
            {
                colorIndexMap.DangerousGetRowSpan(rows - 1)[..BlockLength]
                    .CopyTo(colorIndexMap.DangerousGetRowSpan(row));
            }

            Span<TSample> prediction = workspace.GetPrediction(0);
            Span<short> residual = workspace.GetResidual(0);
            TOperator.PreparePalette(
                this.source.GetPlane(Av1Plane.Y),
                blockOrigin,
                paletteColors,
                colorIndexMap,
                prediction,
                residual,
                TransformSize);

            int rate = Av1TileWriter.GetLumaModeCost(
                writer,
                macroBlock,
                BlockSize,
                Av1PredictionMode.DC,
                0);

            rate += transformSizeRate;
            rate += writer.GetPaletteYModeCost(true, blockSizeContext, neighborContext);
            rate += writer.GetPaletteSizeCost(paletteSize, blockSizeContext, Av1PlaneType.Y);
            rate += Av1SymbolEncoder.GetPaletteYColorCost(colorCache, paletteColors, bitDepth);
            rate += writer.GetPaletteColorMapCost(
                paletteSize,
                Av1PlaneType.Y,
                rows,
                columns,
                colorIndexMap);

            for (Av1TransformType transformType = Av1TransformType.DctDct;
                transformType < Av1TransformType.AllTransformTypes;
                transformType++)
            {
                if (!transformType.IsExtendedSetUsed(transformSetType))
                {
                    continue;
                }

                Av1EncoderTransformBlockState candidateState = default;
                long distortion = TOperator.EncodePredictionCandidate(
                    this.blockWorkspace,
                    this.source.GetPlane(Av1Plane.Y),
                    blockOrigin,
                    prediction,
                    residual,
                    candidateReconstruction,
                    TransformSize.GetWidth(),
                    candidateCoefficients,
                    TransformSize,
                    transformType,
                    Av1Plane.Y,
                    this.quantization.QIndex[0],
                    this.quantization.DeltaQDc[(int)Av1Plane.Y],
                    this.quantization.DeltaQAc[(int)Av1Plane.Y],
                    this.bitDepth,
                    ref candidateState);

                int candidateRate = rate + writer.GetCoefficientCost(
                    TransformSize,
                    transformType,
                    Av1PredictionMode.DC,
                    candidateCoefficients,
                    Av1ComponentType.Luminance,
                    blockContext,
                    candidateState.EndOfBlock,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet,
                    Av1FilterIntraMode.AllFilterIntraModes);

                long candidateCost = Av1RateDistortion.GetCost(this.rateMultiplier, candidateRate, distortion);
                if (candidateCost < bestCost)
                {
                    CopyCandidate(
                        candidateReconstruction,
                        candidateCoefficients,
                        reconstructionPlane,
                        blockOrigin,
                        retainedCoefficients,
                        TransformSize,
                        candidateState,
                        ref retainedState);

                    for (int row = 0; row < BlockLength; row++)
                    {
                        colorIndexMap.DangerousGetRowSpan(row)[..BlockLength]
                            .CopyTo(retainedColorIndexMap[(row * BlockLength)..]);
                    }

                    paletteInfo.PaletteSizes[0] = (byte)paletteSize;
                    paletteInfo.SetColors(Av1Plane.Y, paletteColors);
                    bestCost = candidateCost;
                    paletteSelected = true;
                }
            }
        }
    }
}

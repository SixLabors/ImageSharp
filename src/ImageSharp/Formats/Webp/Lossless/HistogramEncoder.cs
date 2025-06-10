// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

internal static class HistogramEncoder
{
    /// <summary>
    /// Number of partitions for the three dominant (literal, red and blue) symbol costs.
    /// </summary>
    private const int NumPartitions = 4;

    /// <summary>
    /// The size of the bin-hash corresponding to the three dominant costs.
    /// </summary>
    private const int BinSize = NumPartitions * NumPartitions * NumPartitions;

    /// <summary>
    /// Maximum number of histograms allowed in greedy combining algorithm.
    /// </summary>
    private const int MaxHistoGreedy = 100;

    private const uint NonTrivialSym = 0xffffffff;

    private const ushort InvalidHistogramSymbol = ushort.MaxValue;

    public static void GetHistoImageSymbols(
        MemoryAllocator memoryAllocator,
        int xSize,
        int ySize,
        Vp8LBackwardRefs refs,
        uint quality,
        int histoBits,
        int cacheBits,
        Vp8LHistogramSet imageHisto,
        Vp8LHistogram tmpHisto,
        Span<ushort> histogramSymbols)
    {
        int histoXSize = histoBits > 0 ? LosslessUtils.SubSampleSize(xSize, histoBits) : 1;
        int histoYSize = histoBits > 0 ? LosslessUtils.SubSampleSize(ySize, histoBits) : 1;
        int imageHistoRawSize = histoXSize * histoYSize;
        const int entropyCombineNumBins = BinSize;

        using IMemoryOwner<ushort> tmp = memoryAllocator.Allocate<ushort>(imageHistoRawSize * 2, AllocationOptions.Clean);
        Span<ushort> mapTmp = tmp.Slice(0, imageHistoRawSize);
        Span<ushort> clusterMappings = tmp.Slice(imageHistoRawSize, imageHistoRawSize);

        using Vp8LHistogramSet origHisto = new(memoryAllocator, imageHistoRawSize, cacheBits);

        // Construct the histograms from the backward references.
        HistogramBuild(xSize, histoBits, refs, origHisto);

        // Copies the histograms and computes its bitCost. histogramSymbols is optimized.
        int numUsed = HistogramCopyAndAnalyze(origHisto, imageHisto, histogramSymbols);

        bool entropyCombine = numUsed > entropyCombineNumBins * 2 && quality < 100;
        if (entropyCombine)
        {
            int numClusters = numUsed;
            double combineCostFactor = GetCombineCostFactor(imageHistoRawSize, quality);
            HistogramAnalyzeEntropyBin(imageHisto, mapTmp);

            // Collapse histograms with similar entropy.
            HistogramCombineEntropyBin(imageHisto, histogramSymbols, clusterMappings, tmpHisto, mapTmp, entropyCombineNumBins, combineCostFactor);

            OptimizeHistogramSymbols(clusterMappings, numClusters, mapTmp, histogramSymbols);
        }

        float x = quality / 100F;

        // Cubic ramp between 1 and MaxHistoGreedy:
        int thresholdSize = (int)(1 + (x * x * x * (MaxHistoGreedy - 1)));
        bool doGreedy = HistogramCombineStochastic(imageHisto, thresholdSize);
        if (doGreedy)
        {
            RemoveEmptyHistograms(imageHisto);
            HistogramCombineGreedy(imageHisto);
        }

        // Find the optimal map from original histograms to the final ones.
        RemoveEmptyHistograms(imageHisto);
        HistogramRemap(origHisto, imageHisto, histogramSymbols);
    }

    private static void RemoveEmptyHistograms(Vp8LHistogramSet histograms)
    {
        for (int i = histograms.Count - 1; i >= 0; i--)
        {
            if (histograms[i] == null)
            {
                histograms.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Construct the histograms from the backward references.
    /// </summary>
    private static void HistogramBuild(
        int xSize,
        int histoBits,
        Vp8LBackwardRefs backwardRefs,
        Vp8LHistogramSet histograms)
    {
        int x = 0, y = 0;
        int histoXSize = LosslessUtils.SubSampleSize(xSize, histoBits);

        foreach (PixOrCopy v in backwardRefs)
        {
            int ix = ((y >> histoBits) * histoXSize) + (x >> histoBits);
            histograms[ix].AddSinglePixOrCopy(in v, false);
            x += v.Len;
            while (x >= xSize)
            {
                x -= xSize;
                y++;
            }
        }
    }

    /// <summary>
    /// Partition histograms to different entropy bins for three dominant (literal,
    /// red and blue) symbol costs and compute the histogram aggregate bitCost.
    /// </summary>
    private static void HistogramAnalyzeEntropyBin(Vp8LHistogramSet histograms, Span<ushort> binMap)
    {
        int histoSize = histograms.Count;
        DominantCostRange costRange = new();

        // Analyze the dominant (literal, red and blue) entropy costs.
        for (int i = 0; i < histoSize; i++)
        {
            if (histograms[i] == null)
            {
                continue;
            }

            costRange.UpdateDominantCostRange(histograms[i]);
        }

        // bin-hash histograms on three of the dominant (literal, red and blue)
        // symbol costs and store the resulting bin_id for each histogram.
        for (int i = 0; i < histoSize; i++)
        {
            if (histograms[i] == null)
            {
                continue;
            }

            binMap[i] = (ushort)costRange.GetHistoBinIndex(histograms[i], NumPartitions);
        }
    }

    private static int HistogramCopyAndAnalyze(
        Vp8LHistogramSet origHistograms,
        Vp8LHistogramSet histograms,
        Span<ushort> histogramSymbols)
    {
        Vp8LStreaks stats = new();
        Vp8LBitEntropy bitsEntropy = new();
        for (int clusterId = 0, i = 0; i < origHistograms.Count; i++)
        {
            Vp8LHistogram origHistogram = origHistograms[i];
            origHistogram.UpdateHistogramCost(stats, bitsEntropy);

            // Skip the histogram if it is completely empty, which can happen for tiles with no information (when they are skipped because of LZ77).
            if (!origHistogram.IsUsed(0) && !origHistogram.IsUsed(1) && !origHistogram.IsUsed(2) && !origHistogram.IsUsed(3) && !origHistogram.IsUsed(4))
            {
                origHistograms[i] = null;
                histograms[i] = null;
                histogramSymbols[i] = InvalidHistogramSymbol;
            }
            else
            {
                origHistogram.CopyTo(histograms[i]);
                histogramSymbols[i] = (ushort)clusterId++;
            }
        }

        int numUsed = 0;
        foreach (ushort h in histogramSymbols)
        {
            if (h != InvalidHistogramSymbol)
            {
                numUsed++;
            }
        }

        return numUsed;
    }

    private static void HistogramCombineEntropyBin(
        Vp8LHistogramSet histograms,
        Span<ushort> clusters,
        Span<ushort> clusterMappings,
        Vp8LHistogram curCombo,
        ReadOnlySpan<ushort> binMap,
        int numBins,
        double combineCostFactor)
    {
        Span<HistogramBinInfo> binInfo = stackalloc HistogramBinInfo[BinSize];
        for (int idx = 0; idx < numBins; idx++)
        {
            binInfo[idx].First = -1;
            binInfo[idx].NumCombineFailures = 0;
        }

        // By default, a cluster matches itself.
        for (int idx = 0; idx < histograms.Count; idx++)
        {
            clusterMappings[idx] = (ushort)idx;
        }

        List<int> indicesToRemove = [];
        Vp8LStreaks stats = new();
        Vp8LBitEntropy bitsEntropy = new();
        for (int idx = 0; idx < histograms.Count; idx++)
        {
            if (histograms[idx] == null)
            {
                continue;
            }

            int binId = binMap[idx];
            int first = binInfo[binId].First;
            if (first == -1)
            {
                binInfo[binId].First = (short)idx;
            }
            else
            {
                // Try to merge #idx into #first (both share the same binId)
                double bitCost = histograms[idx].BitCost;
                double bitCostThresh = -bitCost * combineCostFactor;
                double currCostDiff = histograms[first].AddEval(histograms[idx], stats, bitsEntropy, bitCostThresh, curCombo);

                if (currCostDiff < bitCostThresh)
                {
                    // Try to merge two histograms only if the combo is a trivial one or
                    // the two candidate histograms are already non-trivial.
                    // For some images, 'tryCombine' turns out to be false for a lot of
                    // histogram pairs. In that case, we fallback to combining
                    // histograms as usual to avoid increasing the header size.
                    bool tryCombine = curCombo.TrivialSymbol != NonTrivialSym || (histograms[idx].TrivialSymbol == NonTrivialSym && histograms[first].TrivialSymbol == NonTrivialSym);
                    const int maxCombineFailures = 32;
                    if (tryCombine || binInfo[binId].NumCombineFailures >= maxCombineFailures)
                    {
                        // Move the (better) merged histogram to its final slot.
                        (histograms[first], curCombo) = (curCombo, histograms[first]);

                        histograms[idx] = null;
                        indicesToRemove.Add(idx);
                        clusterMappings[clusters[idx]] = clusters[first];
                    }
                    else
                    {
                        binInfo[binId].NumCombineFailures++;
                    }
                }
            }
        }

        for (int i = indicesToRemove.Count - 1; i >= 0; i--)
        {
            histograms.RemoveAt(indicesToRemove[i]);
        }
    }

    /// <summary>
    /// Given a Histogram set, the mapping of clusters 'clusterMapping' and the
    /// current assignment of the cells in 'symbols', merge the clusters and assign the smallest possible clusters values.
    /// </summary>
    private static void OptimizeHistogramSymbols(Span<ushort> clusterMappings, int numClusters, Span<ushort> clusterMappingsTmp, Span<ushort> symbols)
    {
        bool doContinue = true;

        // First, assign the lowest cluster to each pixel.
        while (doContinue)
        {
            doContinue = false;
            for (int i = 0; i < numClusters; i++)
            {
                int k = clusterMappings[i];
                while (k != clusterMappings[k])
                {
                    clusterMappings[k] = clusterMappings[clusterMappings[k]];
                    k = clusterMappings[k];
                }

                if (k != clusterMappings[i])
                {
                    doContinue = true;
                    clusterMappings[i] = (ushort)k;
                }
            }
        }

        // Create a mapping from a cluster id to its minimal version.
        int clusterMax = 0;
        clusterMappingsTmp.Clear();

        // Re-map the ids.
        for (int i = 0; i < symbols.Length; i++)
        {
            if (symbols[i] == InvalidHistogramSymbol)
            {
                continue;
            }

            int cluster = clusterMappings[symbols[i]];
            if (cluster > 0 && clusterMappingsTmp[cluster] == 0)
            {
                clusterMax++;
                clusterMappingsTmp[cluster] = (ushort)clusterMax;
            }

            symbols[i] = clusterMappingsTmp[cluster];
        }
    }

    /// <summary>
    /// Perform histogram aggregation using a stochastic approach.
    /// </summary>
    /// <returns>true if a greedy approach needs to be performed afterwards, false otherwise.</returns>
    private static bool HistogramCombineStochastic(Vp8LHistogramSet histograms, int minClusterSize)
    {
        uint seed = 1;
        int triesWithNoSuccess = 0;
        int numUsed = histograms.Count(h => h != null);
        int outerIters = numUsed;
        int numTriesNoSuccess = (int)((uint)outerIters / 2);
        Vp8LStreaks stats = new();
        Vp8LBitEntropy bitsEntropy = new();

        if (numUsed < minClusterSize)
        {
            return true;
        }

        // Priority list of histogram pairs. Its size impacts the quality of the compression and the speed:
        // the smaller the faster but the worse for the compression.
        List<HistogramPair> histoPriorityList = [];
        const int maxSize = 9;

        // Fill the initial mapping.
        Span<int> mappings = histograms.Count <= 64 ? stackalloc int[histograms.Count] : new int[histograms.Count];
        for (int j = 0, i = 0; i < histograms.Count; i++)
        {
            if (histograms[i] == null)
            {
                continue;
            }

            mappings[j++] = i;
        }

        // Collapse similar histograms.
        for (int i = 0; i < outerIters && numUsed >= minClusterSize && ++triesWithNoSuccess < numTriesNoSuccess; i++)
        {
            double bestCost = histoPriorityList.Count == 0 ? 0D : histoPriorityList[0].CostDiff;
            int numTries = (int)((uint)numUsed / 2);
            uint randRange = (uint)((numUsed - 1) * numUsed);

            // Pick random samples.
            for (int j = 0; numUsed >= 2 && j < numTries; j++)
            {
                // Choose two different histograms at random and try to combine them.
                uint tmp = MyRand(ref seed) % randRange;
                int idx1 = (int)(tmp / (numUsed - 1));
                int idx2 = (int)(tmp % (numUsed - 1));
                if (idx2 >= idx1)
                {
                    idx2++;
                }

                idx1 = mappings[idx1];
                idx2 = mappings[idx2];

                // Calculate cost reduction on combination.
                double currCost = HistoPriorityListPush(histoPriorityList, maxSize, histograms, idx1, idx2, bestCost, stats, bitsEntropy);

                // Found a better pair?
                if (currCost < 0)
                {
                    bestCost = currCost;

                    if (histoPriorityList.Count == maxSize)
                    {
                        break;
                    }
                }
            }

            if (histoPriorityList.Count == 0)
            {
                continue;
            }

            // Get the best histograms.
            int bestIdx1 = histoPriorityList[0].Idx1;
            int bestIdx2 = histoPriorityList[0].Idx2;

            int mappingIndex = mappings.IndexOf(bestIdx2);
            Span<int> src = mappings.Slice(mappingIndex + 1, numUsed - mappingIndex - 1);
            Span<int> dst = mappings[mappingIndex..];
            src.CopyTo(dst);

            // Merge the histograms and remove bestIdx2 from the list.
            HistogramAdd(histograms[bestIdx2], histograms[bestIdx1], histograms[bestIdx1]);
            histograms[bestIdx1].BitCost = histoPriorityList[0].CostCombo;
            histograms[bestIdx2] = null;
            numUsed--;

            for (int j = 0; j < histoPriorityList.Count;)
            {
                HistogramPair p = histoPriorityList[j];
                bool isIdx1Best = p.Idx1 == bestIdx1 || p.Idx1 == bestIdx2;
                bool isIdx2Best = p.Idx2 == bestIdx1 || p.Idx2 == bestIdx2;
                bool doEval = false;

                // The front pair could have been duplicated by a random pick so
                // check for it all the time nevertheless.
                if (isIdx1Best && isIdx2Best)
                {
                    histoPriorityList[j] = histoPriorityList[^1];
                    histoPriorityList.RemoveAt(histoPriorityList.Count - 1);
                    continue;
                }

                // Any pair containing one of the two best indices should only refer to
                // bestIdx1. Its cost should also be updated.
                if (isIdx1Best)
                {
                    p.Idx1 = bestIdx1;
                    doEval = true;
                }
                else if (isIdx2Best)
                {
                    p.Idx2 = bestIdx1;
                    doEval = true;
                }

                // Make sure the index order is respected.
                if (p.Idx1 > p.Idx2)
                {
                    (p.Idx1, p.Idx2) = (p.Idx2, p.Idx1);
                }

                if (doEval)
                {
                    // Re-evaluate the cost of an updated pair.
                    HistoListUpdatePair(histograms[p.Idx1], histograms[p.Idx2], stats, bitsEntropy, 0D, p);

                    if (p.CostDiff >= 0D)
                    {
                        histoPriorityList[j] = histoPriorityList[^1];
                        histoPriorityList.RemoveAt(histoPriorityList.Count - 1);
                        continue;
                    }
                }

                HistoListUpdateHead(histoPriorityList, p);
                j++;
            }

            triesWithNoSuccess = 0;
        }

        return numUsed <= minClusterSize;
    }

    private static void HistogramCombineGreedy(Vp8LHistogramSet histograms)
    {
        int histoSize = histograms.Count(h => h != null);

        // Priority list of histogram pairs.
        List<HistogramPair> histoPriorityList = [];
        int maxSize = histoSize * histoSize;
        Vp8LStreaks stats = new();
        Vp8LBitEntropy bitsEntropy = new();

        for (int i = 0; i < histoSize; i++)
        {
            if (histograms[i] == null)
            {
                continue;
            }

            for (int j = i + 1; j < histoSize; j++)
            {
                if (histograms[j] == null)
                {
                    continue;
                }

                HistoPriorityListPush(histoPriorityList, maxSize, histograms, i, j, 0.0d, stats, bitsEntropy);
            }
        }

        while (histoPriorityList.Count > 0)
        {
            int idx1 = histoPriorityList[0].Idx1;
            int idx2 = histoPriorityList[0].Idx2;
            HistogramAdd(histograms[idx2], histograms[idx1], histograms[idx1]);
            histograms[idx1].BitCost = histoPriorityList[0].CostCombo;

            // Remove merged histogram.
            histograms[idx2] = null;

            // Remove pairs intersecting the just combined best pair.
            for (int i = 0; i < histoPriorityList.Count;)
            {
                HistogramPair p = histoPriorityList[i];
                if (p.Idx1 == idx1 || p.Idx2 == idx1 || p.Idx1 == idx2 || p.Idx2 == idx2)
                {
                    // Replace item at pos i with the last one and shrinking the list.
                    histoPriorityList[i] = histoPriorityList[^1];
                    histoPriorityList.RemoveAt(histoPriorityList.Count - 1);
                }
                else
                {
                    HistoListUpdateHead(histoPriorityList, p);
                    i++;
                }
            }

            // Push new pairs formed with combined histogram to the list.
            for (int i = 0; i < histoSize; i++)
            {
                if (i == idx1 || histograms[i] == null)
                {
                    continue;
                }

                HistoPriorityListPush(histoPriorityList, maxSize, histograms, idx1, i, 0.0d, stats, bitsEntropy);
            }
        }
    }

    private static void HistogramRemap(
        Vp8LHistogramSet input,
        Vp8LHistogramSet output,
        Span<ushort> symbols)
    {
        int inSize = input.Count;
        int outSize = output.Count;
        Vp8LStreaks stats = new();
        Vp8LBitEntropy bitsEntropy = new();
        if (outSize > 1)
        {
            for (int i = 0; i < inSize; i++)
            {
                if (input[i] == null)
                {
                    // Arbitrarily set to the previous value if unused to help future LZ77.
                    symbols[i] = symbols[i - 1];
                    continue;
                }

                int bestOut = 0;
                double bestBits = double.MaxValue;
                for (int k = 0; k < outSize; k++)
                {
                    double curBits = output[k].AddThresh(input[i], stats, bitsEntropy, bestBits);
                    if (k == 0 || curBits < bestBits)
                    {
                        bestBits = curBits;
                        bestOut = k;
                    }
                }

                symbols[i] = (ushort)bestOut;
            }
        }
        else
        {
            for (int i = 0; i < inSize; i++)
            {
                symbols[i] = 0;
            }
        }

        // Recompute each output.
        int paletteCodeBits = output[0].PaletteCodeBits;
        for (int i = 0; i < outSize; i++)
        {
            output[i].Clear();
            output[i].PaletteCodeBits = paletteCodeBits;
        }

        for (int i = 0; i < inSize; i++)
        {
            if (input[i] == null)
            {
                continue;
            }

            int idx = symbols[i];
            input[i].Add(output[idx], output[idx]);
        }
    }

    /// <summary>
    /// Create a pair from indices "idx1" and "idx2" provided its cost is inferior to "threshold", a negative entropy.
    /// </summary>
    /// <returns>The cost of the pair, or 0 if it superior to threshold.</returns>
    private static double HistoPriorityListPush(
        List<HistogramPair> histoList,
        int maxSize,
        Vp8LHistogramSet histograms,
        int idx1,
        int idx2,
        double threshold,
        Vp8LStreaks stats,
        Vp8LBitEntropy bitsEntropy)
    {
        HistogramPair pair = new();

        if (histoList.Count == maxSize)
        {
            return 0D;
        }

        if (idx1 > idx2)
        {
            (idx1, idx2) = (idx2, idx1);
        }

        pair.Idx1 = idx1;
        pair.Idx2 = idx2;
        Vp8LHistogram h1 = histograms[idx1];
        Vp8LHistogram h2 = histograms[idx2];

        HistoListUpdatePair(h1, h2, stats, bitsEntropy, threshold, pair);

        // Do not even consider the pair if it does not improve the entropy.
        if (pair.CostDiff >= threshold)
        {
            return 0.0d;
        }

        histoList.Add(pair);

        HistoListUpdateHead(histoList, pair);

        return pair.CostDiff;
    }

    /// <summary>
    /// Update the cost diff and combo of a pair of histograms. This needs to be called when the histograms have been
    /// merged with a third one.
    /// </summary>
    private static void HistoListUpdatePair(
        Vp8LHistogram h1,
        Vp8LHistogram h2,
        Vp8LStreaks stats,
        Vp8LBitEntropy bitsEntropy,
        double threshold,
        HistogramPair pair)
    {
        double sumCost = h1.BitCost + h2.BitCost;
        pair.CostCombo = 0.0d;
        h1.GetCombinedHistogramEntropy(h2, stats, bitsEntropy, sumCost + threshold, costInitial: pair.CostCombo, out double cost);
        pair.CostCombo = cost;
        pair.CostDiff = pair.CostCombo - sumCost;
    }

    /// <summary>
    /// Check whether a pair in the list should be updated as head or not.
    /// </summary>
    private static void HistoListUpdateHead(List<HistogramPair> histoList, HistogramPair pair)
    {
        if (pair.CostDiff < histoList[0].CostDiff)
        {
            // Replace the best pair.
            int oldIdx = histoList.IndexOf(pair);
            histoList[oldIdx] = histoList[0];
            histoList[0] = pair;
        }
    }

    private static void HistogramAdd(Vp8LHistogram a, Vp8LHistogram b, Vp8LHistogram output)
    {
        a.Add(b, output);
        output.TrivialSymbol = a.TrivialSymbol == b.TrivialSymbol ? a.TrivialSymbol : NonTrivialSym;
    }

    private static double GetCombineCostFactor(int histoSize, uint quality)
    {
        double combineCostFactor = 0.16d;
        if (quality < 90)
        {
            if (histoSize > 256)
            {
                combineCostFactor /= 2.0d;
            }

            if (histoSize > 512)
            {
                combineCostFactor /= 2.0d;
            }

            if (histoSize > 1024)
            {
                combineCostFactor /= 2.0d;
            }

            if (quality <= 50)
            {
                combineCostFactor /= 2.0d;
            }
        }

        return combineCostFactor;
    }

    // Implement a Lehmer random number generator with a multiplicative constant of 48271 and a modulo constant of 2^31 - 1.
    [MethodImpl(InliningOptions.ShortMethod)]
    private static uint MyRand(ref uint seed)
    {
        seed = (uint)(((ulong)seed * 48271u) % 2147483647u);
        return seed;
    }
}

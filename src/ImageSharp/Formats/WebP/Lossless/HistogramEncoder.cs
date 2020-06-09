// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class HistogramEncoder
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

        public static void GetHistoImageSymbols(int xSize, int ySize, Vp8LBackwardRefs refs, int quality, int histoBits, int cacheBits, List<Vp8LHistogram> imageHisto, Vp8LHistogram tmpHisto, short[] histogramSymbols)
        {
            int histoXSize = histoBits > 0 ? LosslessUtils.SubSampleSize(xSize, histoBits) : 1;
            int histoYSize = histoBits > 0 ? LosslessUtils.SubSampleSize(ySize, histoBits) : 1;
            int imageHistoRawSize = histoXSize * histoYSize;
            int entropyCombineNumBins = BinSize;
            short[] mapTmp = new short[imageHistoRawSize];
            short[] clusterMappings = new short[imageHistoRawSize];
            int numUsed = imageHistoRawSize;
            var origHisto = new List<Vp8LHistogram>(imageHistoRawSize);
            for (int i = 0; i < imageHistoRawSize; i++)
            {
                origHisto.Add(new Vp8LHistogram(cacheBits));
            }

            // Construct the histograms from backward references.
            HistogramBuild(xSize, histoBits, refs, origHisto);

            // Copies the histograms and computes its bit_cost. histogramSymbols is optimized.
            HistogramCopyAndAnalyze(origHisto, imageHisto, ref numUsed, histogramSymbols);

            var entropyCombine = (numUsed > entropyCombineNumBins * 2) && (quality < 100);
            if (entropyCombine)
            {
                var binMap = mapTmp;
                var numClusters = numUsed;
                double combineCostFactor = GetCombineCostFactor(imageHistoRawSize, quality);
                HistogramAnalyzeEntropyBin(imageHisto, binMap);

                // Collapse histograms with similar entropy.
                HistogramCombineEntropyBin(imageHisto, ref numUsed, histogramSymbols, clusterMappings, tmpHisto, binMap, entropyCombineNumBins, combineCostFactor);

                OptimizeHistogramSymbols(imageHisto, clusterMappings, numClusters, mapTmp, histogramSymbols);
            }

            if (!entropyCombine)
            {
                float x = quality / 100.0f;

                // Cubic ramp between 1 and MaxHistoGreedy:
                int thresholdSize = (int)(1 + (x * x * x * (MaxHistoGreedy - 1)));
                bool doGreedy = HistogramCombineStochastic(imageHisto, ref numUsed, thresholdSize);
                if (doGreedy)
                {
                    HistogramCombineGreedy(imageHisto, ref numUsed);
                }
            }
        }

        /// <summary>
        /// Construct the histograms from backward references.
        /// </summary>
        private static void HistogramBuild(int xSize, int histoBits, Vp8LBackwardRefs backwardRefs, List<Vp8LHistogram> histograms)
        {
            int x = 0, y = 0;
            int histoXSize = LosslessUtils.SubSampleSize(xSize, histoBits);
            using List<PixOrCopy>.Enumerator backwardRefsEnumerator = backwardRefs.Refs.GetEnumerator();
            while (backwardRefsEnumerator.MoveNext())
            {
                PixOrCopy v = backwardRefsEnumerator.Current;
                int ix = ((y >> histoBits) * histoXSize) + (x >> histoBits);
                histograms[ix].AddSinglePixOrCopy(v, false);
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
        private static void HistogramAnalyzeEntropyBin(List<Vp8LHistogram> histograms, short[] binMap)
        {
            int histoSize = histograms.Count;
            var costRange = new DominantCostRange();

            // Analyze the dominant (literal, red and blue) entropy costs.
            for (int i = 0; i < histoSize; i++)
            {
                costRange.UpdateDominantCostRange(histograms[i]);
            }

            // bin-hash histograms on three of the dominant (literal, red and blue)
            // symbol costs and store the resulting bin_id for each histogram.
            for (int i = 0; i < histoSize; i++)
            {
                binMap[i] = (short)costRange.GetHistoBinIndex(histograms[i], NumPartitions);
            }
        }

        private static void HistogramCopyAndAnalyze(List<Vp8LHistogram> origHistograms, List<Vp8LHistogram> histograms, ref int numUsed, short[] histogramSymbols)
        {
            int numUsedOrig = numUsed;
            var indicesToRemove = new List<int>();
            for (int clusterId = 0, i = 0; i < origHistograms.Count; i++)
            {
                Vp8LHistogram histo = origHistograms[i];
                histo.UpdateHistogramCost();

                // Skip the histogram if it is completely empty, which can happen for tiles
                // with no information (when they are skipped because of LZ77).
                if (!histo.IsUsed[0] && !histo.IsUsed[1] && !histo.IsUsed[2] && !histo.IsUsed[3] && !histo.IsUsed[4])
                {
                    indicesToRemove.Add(i);
                }
                else
                {
                    // TODO: HistogramCopy(histo, histograms[i]);
                    histogramSymbols[i] = (short)clusterId++;
                }
            }

            foreach (int indice in indicesToRemove.OrderByDescending(v => v))
            {
                origHistograms.RemoveAt(indice);
                histograms.RemoveAt(indice);
            }
        }

        private static void HistogramCombineEntropyBin(List<Vp8LHistogram> histograms, ref int numUsed, short[] clusters, short[] clusterMappings, Vp8LHistogram curCombo, short[] binMap, int numBins, double combineCostFactor)
        {
            for (int idx = 0; idx < histograms.Count; idx++)
            {
                clusterMappings[idx] = (short)idx;
            }
        }

        /// <summary>
        /// Given a Histogram set, the mapping of clusters 'clusterMapping' and the
        /// current assignment of the cells in 'symbols', merge the clusters and
        /// assign the smallest possible clusters values.
        /// </summary>
        private static void OptimizeHistogramSymbols(List<Vp8LHistogram> histograms, short[] clusterMappings, int numClusters, short[] clusterMappingsTmp, short[] symbols)
        {
            int clusterMax;
            bool doContinue = true;

            // First, assign the lowest cluster to each pixel.
            while (doContinue)
            {
                doContinue = false;
                for (int i = 0; i < numClusters; i++)
                {
                    int k;
                    k = clusterMappings[i];
                    while (k != clusterMappings[k])
                    {
                        clusterMappings[k] = clusterMappings[clusterMappings[k]];
                        k = clusterMappings[k];
                    }

                    if (k != clusterMappings[i])
                    {
                        doContinue = true;
                        clusterMappings[i] = (short)k;
                    }
                }
            }

            // Create a mapping from a cluster id to its minimal version.
            clusterMax = 0;
            clusterMappingsTmp.AsSpan().Fill(0);

            // Re-map the ids.
            for (int i = 0; i < histograms.Count; i++)
            {
                int cluster;
                cluster = clusterMappings[symbols[i]];
                if (cluster > 0 && clusterMappingsTmp[cluster] == 0)
                {
                    clusterMax++;
                    clusterMappingsTmp[cluster] = (short)clusterMax;
                }

                symbols[i] = clusterMappingsTmp[cluster];
            }

            // Make sure all cluster values are used.
            clusterMax = 0;
            for (int i = 0; i < histograms.Count; i++)
            {
                if (symbols[i] <= clusterMax)
                {
                    continue;
                }

                clusterMax++;
            }
        }

        /// <summary>
        /// Perform histogram aggregation using a stochastic approach.
        /// </summary>
        /// <returns>true if a greedy approach needs to be performed afterwards, false otherwise.</returns>
        private static bool HistogramCombineStochastic(List<Vp8LHistogram> histograms, ref int numUsed, int minClusterSize)
        {
            var rand = new Random();
            int triesWithNoSuccess = 0;
            int outerIters = numUsed;
            int numTriesNoSuccess = outerIters / 2;

            // Priority queue of histogram pairs. Its size impacts the quality of the compression and the speed:
            // the smaller the faster but the worse for the compression.
            var histoPriorityList = new List<HistogramPair>();
            int histoQueueMaxSize = histograms.Count * histograms.Count;

            // Fill the initial mapping.
            int[] mappings = new int[histograms.Count];
            for (int j = 0, iter = 0; iter < histograms.Count; iter++)
            {
                mappings[j++] = iter;
            }

            // Collapse similar histograms
            for (int iter = 0; iter < outerIters && numUsed >= minClusterSize && ++triesWithNoSuccess < numTriesNoSuccess; iter++)
            {
                double bestCost = (histoPriorityList.Count == 0) ? 0.0d : histoPriorityList[0].CostDiff;
                int bestIdx1 = -1;
                int bestIdx2 = 1;
                int numTries = numUsed / 2; // TODO: should that be histogram.Count/2?
                uint randRange = (uint)((numUsed - 1) * numUsed);

                // Pick random samples.
                for (int j = 0; numUsed >= 2 && j < numTries; j++)
                {
                    // Choose two different histograms at random and try to combine them.
                    uint tmp = (uint)(rand.Next() % randRange);
                    double currCost;
                    int idx1 = (int)(tmp / (numUsed - 1));
                    int idx2 = (int)(tmp % (numUsed - 1));
                    if (idx2 >= idx1)
                    {
                        idx2++;
                    }

                    idx1 = mappings[idx1];
                    idx2 = mappings[idx2];

                    // Calculate cost reduction on combination.
                    currCost = HistoQueuePush(histoPriorityList, histoQueueMaxSize, histograms, idx1, idx2, bestCost);

                    // Found a better pair?
                    if (currCost < 0)
                    {
                        bestCost = currCost;

                        // Empty the queue if we reached full capacity.
                        if (histoPriorityList.Count == histoQueueMaxSize)
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
                bestIdx1 = histoPriorityList[0].Idx1;
                bestIdx2 = histoPriorityList[0].Idx2;

                // Pop bestIdx2 from mappings.
                var mappingIndex = Array.BinarySearch(mappings, bestIdx2);
                // TODO: memmove(mapping_index, mapping_index + 1, sizeof(*mapping_index) *((*num_used) - (mapping_index - mappings) - 1));

                // Merge the histograms and remove bestIdx2 from the queue.
                HistogramAdd(histograms[bestIdx2], histograms[bestIdx1], histograms[bestIdx1]);
                histograms.ElementAt(bestIdx1).BitCost = histoPriorityList[0].CostCombo;
                histograms.RemoveAt(bestIdx2);
                numUsed--;

                var indicesToRemove = new List<int>();
                int lastIndex = histoPriorityList.Count - 1;
                for (int j = 0; j < histoPriorityList.Count;)
                {
                    HistogramPair p = histoPriorityList.ElementAt(j);
                    bool isIdx1Best = p.Idx1 == bestIdx1 || p.Idx1 == bestIdx2;
                    bool isIdx2Best = p.Idx2 == bestIdx1 || p.Idx2 == bestIdx2;
                    bool doEval = false;

                    // The front pair could have been duplicated by a random pick so
                    // check for it all the time nevertheless.
                    if (isIdx1Best && isIdx2Best)
                    {
                        indicesToRemove.Add(lastIndex);
                        numUsed--;
                        lastIndex--;
                        continue;
                    }

                    // Any pair containing one of the two best indices should only refer to
                    // best_idx1. Its cost should also be updated.
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
                        int tmp = p.Idx2;
                        p.Idx2 = p.Idx1;
                        p.Idx1 = tmp;
                    }

                    if (doEval)
                    {
                        // Re-evaluate the cost of an updated pair.
                        HistoQueueUpdatePair(histograms[p.Idx1], histograms[p.Idx2], 0.0d, p);
                        if (p.CostDiff >= 0.0d)
                        {
                            indicesToRemove.Add(lastIndex);
                            lastIndex--;
                            numUsed--;
                            continue;
                        }
                    }

                    HistoQueueUpdateHead(histoPriorityList, p);
                    j++;
                }

                triesWithNoSuccess = 0;
            }

            bool doGreedy = numUsed <= minClusterSize;

            return doGreedy;
        }

        private static void HistogramCombineGreedy(List<Vp8LHistogram> histograms, ref int numUsed)
        {
            int histoSize = histograms.Count;

            // Priority list of histogram pairs.
            var histoPriorityList = new List<HistogramPair>();
            int maxHistoQueueSize = histoSize * histoSize;

            for (int i = 0; i < histograms.Count; i++)
            {
                for (int j = i + 1; j < histograms.Count; j++)
                {
                    // Initialize queue.
                    HistoQueuePush(histoPriorityList, maxHistoQueueSize, histograms, i, j, 0.0d);
                }
            }

            while (histoPriorityList.Count > 0)
            {
                int idx1 = histoPriorityList[0].Idx1;
                int idx2 = histoPriorityList[0].Idx2;
                HistogramAdd(histograms[idx2], histograms[idx1], histograms[idx1]);
                histograms[idx1].BitCost = histoPriorityList[0].CostCombo;

                // Remove merged histogram.
                histograms.RemoveAt(idx2);
                numUsed--;

                // Remove pairs intersecting the just combined best pair.
                for (int i = 0; i < histoPriorityList.Count;)
                {
                    HistogramPair p = histoPriorityList.ElementAt(i);
                    if (p.Idx1 == idx1 || p.Idx2 == idx1 || p.Idx1 == idx2 || p.Idx2 == idx2)
                    {
                        // Remove last entry from the queue.
                        p = histoPriorityList.ElementAt(histoPriorityList.Count - 1);
                        histoPriorityList.RemoveAt(histoPriorityList.Count - 1); // TODO: use list instead Queue?
                    }
                    else
                    {
                        HistoQueueUpdateHead(histoPriorityList, p);
                        i++;
                    }
                }

                // Push new pairs formed with combined histogram to the queue.
                for (int i = 0; i < histograms.Count; i++)
                {
                    if (i == idx1)
                    {
                        continue;
                    }

                    HistoQueuePush(histoPriorityList, maxHistoQueueSize, histograms, idx1, i, 0.0d);
                }
            }
        }

        /// <summary>
        /// // Create a pair from indices "idx1" and "idx2" provided its cost
        /// is inferior to "threshold", a negative entropy.
        /// </summary>
        /// <returns>The cost of the pair, or 0. if it superior to threshold.</returns>
        private static double HistoQueuePush(List<HistogramPair> histoQueue, int queueMaxSize, List<Vp8LHistogram> histograms, int idx1, int idx2, double threshold)
        {
            var pair = new HistogramPair();

            // Stop here if the queue is full.
            if (histoQueue.Count == queueMaxSize)
            {
                return 0.0d;
            }

            if (idx1 > idx2)
            {
                int tmp = idx2;
                idx2 = idx1;
                idx1 = tmp;
            }

            pair.Idx1 = idx1;
            pair.Idx2 = idx2;
            Vp8LHistogram h1 = histograms[idx1];
            Vp8LHistogram h2 = histograms[idx2];

            HistoQueueUpdatePair(h1, h2, threshold, pair);

            // Do not even consider the pair if it does not improve the entropy.
            if (pair.CostDiff >= threshold)
            {
                return 0.0d;
            }

            histoQueue.Add(pair);

            HistoQueueUpdateHead(histoQueue, pair);

            return pair.CostDiff;
        }

        /// <summary>
        /// Update the cost diff and combo of a pair of histograms. This needs to be
        /// called when the the histograms have been merged with a third one.
        /// </summary>
        private static void HistoQueueUpdatePair(Vp8LHistogram h1, Vp8LHistogram h2, double threshold, HistogramPair pair)
        {
            double sumCost = h1.BitCost + h2.BitCost;
            pair.CostCombo = GetCombinedHistogramEntropy(h1, h2, sumCost + threshold);
            pair.CostDiff = pair.CostCombo - sumCost;
        }

        private static double GetCombinedHistogramEntropy(Vp8LHistogram a, Vp8LHistogram b, double costThreshold)
        {
            double cost = 0.0d;
            int paletteCodeBits = a.PaletteCodeBits;
            bool trivialAtEnd = false;

            cost += GetCombinedEntropy(a.Literal, b.Literal, Vp8LHistogram.HistogramNumCodes(paletteCodeBits), a.IsUsed[0], b.IsUsed[0], false);

            cost += ExtraCostCombined(a.Literal.AsSpan(WebPConstants.NumLiteralCodes), b.Literal.AsSpan(WebPConstants.NumLiteralCodes), WebPConstants.NumLengthCodes);

            if (cost > costThreshold)
            {
                return 0;
            }

            if (a.TrivialSymbol != NonTrivialSym && a.TrivialSymbol == b.TrivialSymbol)
            {
                // A, R and B are all 0 or 0xff.
                uint color_a = (a.TrivialSymbol >> 24) & 0xff;
                uint color_r = (a.TrivialSymbol >> 16) & 0xff;
                uint color_b = (a.TrivialSymbol >> 0) & 0xff;
                if ((color_a == 0 || color_a == 0xff) &&
                    (color_r == 0 || color_r == 0xff) &&
                    (color_b == 0 || color_b == 0xff))
                {
                    trivialAtEnd = true;
                }
            }

            cost += GetCombinedEntropy(a.Red, b.Red, WebPConstants.NumLiteralCodes, a.IsUsed[1], b.IsUsed[1], trivialAtEnd);

            return cost;
        }

        private static double GetCombinedEntropy(uint[] x, uint[] y, int length, bool isXUsed, bool isYUsed, bool trivialAtEnd)
        {
            var stats = new Vp8LStreaks();
            if (trivialAtEnd)
            {
                // This configuration is due to palettization that transforms an indexed
                // pixel into 0xff000000 | (pixel << 8) in BundleColorMap.
                // BitsEntropyRefine is 0 for histograms with only one non-zero value.
                // Only FinalHuffmanCost needs to be evaluated.

                // Deal with the non-zero value at index 0 or length-1.
                stats.Streaks[1][0] = 1;

                // Deal with the following/previous zero streak.
                stats.Counts[0] = 1;
                stats.Streaks[0][1] = length - 1;

                return stats.FinalHuffmanCost();
            }

            var bitEntropy = new Vp8LBitEntropy();
            if (isXUsed)
            {
                if (isYUsed)
                {
                    bitEntropy.GetCombinedEntropyUnrefined(x, y, length, stats);
                }
                else
                {
                    bitEntropy.GetEntropyUnrefined(x, length, stats);
                }
            }
            else
            {
                if (isYUsed)
                {
                    bitEntropy.GetEntropyUnrefined(y, length, stats);
                }
                else
                {
                    stats.Counts[0] = 1;
                    stats.Streaks[0][length > 3 ? 1 : 0] = length;
                    bitEntropy.Init();
                }
            }

            return bitEntropy.BitsEntropyRefine() + stats.FinalHuffmanCost();
        }

        private static double ExtraCostCombined(Span<uint> x, Span<uint> y, int length)
        {
            double cost = 0.0d;
            for (int i = 2; i < length - 2; i++)
            {
                int xy = (int)(x[i + 2] + y[i + 2]);
                cost += (i >> 1) * xy;
            }

            return cost;
        }

        private static void HistogramAdd(Vp8LHistogram a, Vp8LHistogram b, Vp8LHistogram output)
        {
            // TODO: VP8LHistogramAdd(a, b, out);
            output.TrivialSymbol = (a.TrivialSymbol == b.TrivialSymbol)
                       ? a.TrivialSymbol
                       : NonTrivialSym;
        }

        /// <summary>
        /// Check whether a pair in the list should be updated as head or not.
        /// </summary>
        private static void HistoQueueUpdateHead(List<HistogramPair> histoQueue, HistogramPair pair)
        {
            if (pair.CostDiff < histoQueue[0].CostDiff)
            {
                // Replace the best pair.
                histoQueue.RemoveAt(0);
                histoQueue.Insert(0, pair);
            }
        }

        private static double GetCombineCostFactor(int histoSize, int quality)
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
    }
}

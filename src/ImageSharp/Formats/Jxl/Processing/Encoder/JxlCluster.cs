// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// Histogram clustering &amp; utilities.
/// </summary>
internal static class JxlCluster
{
    public static Vector<float> Entropy(
        Vector<float> count,
        Vector<float> inverseTotal,
        Vector<float> total)
        => Vector.ConditionalSelect(
            Vector.Equals(count, total),
            Vector<float>.Zero,
            Vector<float>.Zero - Vector.Multiply(count, Vector.Log2(inverseTotal * count)));

    public static void HistogramCondition(JxlHistogram histogram)
    {
        Vector<int> total = Vector<int>.Zero;
        int nzPos = -Vector<int>.Count;

        Span<int> countsSpan = CollectionsMarshal.AsSpan(histogram.Counts);
        ref int countsRef = ref MemoryMarshal.GetReference(countsSpan);

        for (int i = 0; i <= countsSpan.Length - Vector<int>.Count; i += Vector<int>.Count)
        {
            Vector<int> counts = Vector.LoadUnsafe(ref Unsafe.Add(ref countsRef, i));
            bool isNonZero = !Vector.AllWhereAllBitsSet(Vector.Equals(counts, Vector<int>.Zero));

            total += counts;

            // Set the position of the non-zero coefficient
            // if the value is non-zero
            if (isNonZero)
            {
                nzPos = i;
            }
        }

        histogram.Counts.Resize(nzPos + Vector<int>.Count);
        histogram.TotalCount = Vector.Sum(total);
    }

    public static void HistogramEntropy(JxlHistogram a)
    {
        a.Entropy = 0.0f;

        if (a.TotalCount == 0)
        {
            return;
        }

        Vector<float> inverseTotal = Vector.Create(1.0f / a.TotalCount);
        Vector<float> entropyLanes = Vector<float>.Zero;
        Vector<float> total = Vector.Create((float)a.TotalCount);

        Span<int> countsSpan = CollectionsMarshal.AsSpan(a.Counts);
        ref int countsRef = ref MemoryMarshal.GetReference(countsSpan);

        for (int i = 0; i <= countsSpan.Length - Vector<int>.Count; i += Vector<int>.Count)
        {
            Vector<int> counts = Vector.LoadUnsafe(ref Unsafe.Add(ref countsRef, i));
            entropyLanes += Entropy(
                Vector.ConvertToSingle(counts),
                inverseTotal,
                total);
        }

        a.Entropy += Vector.Sum(entropyLanes);
    }

    public static float HistogramDistance(JxlHistogram a, JxlHistogram b)
    {
        if (a.TotalCount == 0 || b.TotalCount == 0)
        {
            return 0.0f;
        }

        Vector<float> inverseTotal = Vector.Create(1.0f / (a.TotalCount + b.TotalCount));
        Vector<float> distanceLanes = Vector<float>.Zero;
        Vector<float> total = Vector.Create((float)(a.TotalCount + b.TotalCount));

        Span<int> aCountsSpan = CollectionsMarshal.AsSpan(a.Counts);
        ref int aCountsRef = ref MemoryMarshal.GetReference(aCountsSpan);

        Span<int> bCountsSpan = CollectionsMarshal.AsSpan(b.Counts);
        ref int bCountsRef = ref MemoryMarshal.GetReference(bCountsSpan);

        for (int i = 0; i <= Math.Max(aCountsSpan.Length, bCountsSpan.Length) - Vector<int>.Count; i += Vector<int>.Count)
        {
            Vector<int> aCounts = aCountsSpan.Length > i
                ? Vector.LoadUnsafe(ref Unsafe.Add(ref aCountsRef, i))
                : Vector<int>.Zero;

            Vector<int> bCounts = bCountsSpan.Length > i
                ? Vector.LoadUnsafe(ref Unsafe.Add(ref bCountsRef, i))
                : Vector<int>.Zero;

            Vector<float> counts = Vector.ConvertToSingle(aCounts + bCounts);
            distanceLanes += Entropy(counts, inverseTotal, total);
        }

        float totalDistance = Vector.Sum(distanceLanes);
        return totalDistance - a.Entropy - b.Entropy;
    }

    /// <summary>
    /// Computes difference of the actual histogram from the histogram
    /// we would use for encoding, based on the Kullback-Leibler
    /// divergence.
    /// https://en.wikipedia.org/wiki/Kullback%E2%80%93Leibler_divergence
    /// </summary>
    /// <param name="actual">The actual histogram.</param>
    /// <param name="coding">The histogram we use for encoding.</param>
    /// <returns>Divergence of the actual and encoding histogram.</returns>
    public static float HistogramKLDivergence(JxlHistogram actual, JxlHistogram coding)
    {
        if (actual.TotalCount == 0)
        {
            return 0;
        }

        if (coding.TotalCount == 0)
        {
            return float.PositiveInfinity;
        }

        Vector<float> codingInv = Vector.Create(1.0F / coding.TotalCount);
        Vector<float> costLanes = Vector<float>.Zero;

        ReadOnlySpan<int> actualCounts = CollectionsMarshal.AsSpan(actual.Counts);
        ReadOnlySpan<int> codingCounts = CollectionsMarshal.AsSpan(coding.Counts);

        for (int i = 0; i < actualCounts.Length; i += Vector<int>.Count)
        {
            Vector<int> counts = Vector.Create(actualCounts.Slice(i, Vector<int>.Count));

            Vector<int> codingCountsVector = codingCounts.Length > i
                ? Vector.Create(codingCounts.Slice(i, Vector<int>.Count))
                : Vector<int>.Zero;

            Vector<float> codingProbs = Vector.ConvertToSingle(codingCountsVector) * codingInv;

            Vector<float> negCodingCost = Vector.ConditionalSelect(
                Vector.Equals(counts, Vector<int>.Zero),
                Vector.ConditionalSelect(
                    Vector.Equals(codingCountsVector, Vector<int>.Zero),
                    Vector.Create(float.NegativeInfinity),
                    Vector.Log2(codingProbs)),
                Vector<float>.Zero);

            costLanes -= Vector.ConvertToSingle(counts) * negCodingCost;
        }

        float totalCost = Vector.Sum(costLanes);
        return totalCost - actual.Entropy;
    }

    public static bool FastClusterHistograms(
        IReadOnlyList<JxlHistogram> input,
        int maxHistograms,
        List<JxlHistogram> output,
        List<uint> histogramSymbols)
    {
        int previousHistograms = output.Count;
        _ = output.EnsureCapacity(maxHistograms);
        const float minDistanceForDistinct = 48.0F;

        histogramSymbols.Clear();

        for (int i = 0; i < input.Count; i++)
        {
            histogramSymbols.Add((uint)maxHistograms);
        }

        float[] distances = ArrayPool<float>.Shared.Rent(input.Count);
        Array.Fill(distances, float.MaxValue);

        int largestIndex = 0;

        for (int i = 0; i < input.Count; i++)
        {
            if (input[i].TotalCount == 0)
            {
                histogramSymbols[i] = 0;
                distances[i] = 0.0F;
                continue;
            }

            HistogramEntropy(input[i]);

            if (input[i].TotalCount > input[largestIndex].TotalCount)
            {
                largestIndex = i;
            }
        }

        if (previousHistograms > 0)
        {
            for (int j = 0; j < previousHistograms; j++)
            {
                HistogramEntropy(output[j]);
            }

            for (int i = 0; i < input.Count; i++)
            {
                if (distances[i] == 0.0F)
                {
                    continue;
                }

                for (int j = 0; j < previousHistograms; j++)
                {
                    distances[i] = MathF.Min(
                        HistogramKLDivergence(input[i], output[j]),
                        distances[i]);
                }
            }

            int maxDistanceIndex = 0;

            for (int i = 1; i < distances.Length; i++)
            {
                if (distances[i] > distances[maxDistanceIndex])
                {
                    maxDistanceIndex = i;
                }
            }

            if (distances[maxDistanceIndex] > 0.0F)
            {
                largestIndex = maxDistanceIndex;
            }
        }

        while (output.Count < maxHistograms)
        {
            histogramSymbols[largestIndex] = (uint)output.Count;

            output.Add(input[largestIndex]);

            distances[largestIndex] = 0.0F;

            largestIndex = 0;

            for (int i = 0; i < input.Count; i++)
            {
                if (distances[i] == 0.0F)
                {
                    continue;
                }

                distances[i] = MathF.Min(
                    HistogramDistance(input[i], output[^1]),
                    distances[i]);

                if (distances[i] > distances[largestIndex])
                {
                    largestIndex = i;
                }
            }

            if (distances[largestIndex] < minDistanceForDistinct)
            {
                break;
            }
        }

        for (int i = 0; i < input.Count; i++)
        {
            if (histogramSymbols[i] != (uint)maxHistograms)
            {
                continue;
            }

            int best = 0;
            float bestDistance = float.MaxValue;

            for (int j = 0; j < output.Count; j++)
            {
                float distance =
                    j < previousHistograms
                        ? HistogramKLDivergence(input[i], output[j])
                        : HistogramDistance(input[i], output[j]);

                if (distance < bestDistance)
                {
                    best = j;
                    bestDistance = distance;
                }
            }

            if (bestDistance >= float.MaxValue)
            {
                // distance must not overflow
                ArrayPool<float>.Shared.Return(distances);
                return false;
            }

            if (best >= previousHistograms)
            {
                output[best].AddHistogram(input[i]);
                HistogramEntropy(output[best]);
            }

            histogramSymbols[i] = (uint)best;
        }

        ArrayPool<float>.Shared.Return(distances);
        return true;
    }

    public static void HistogramReindex(List<JxlHistogram> output, int previousHistograms, List<uint> symbols)
    {
        List<JxlHistogram> temp = [.. output];
        Dictionary<uint, int> newIndex = [];

        for (int i = 0; i < previousHistograms; i++)
        {
            newIndex[(uint)i] = i;
        }

        int nextIndex = previousHistograms;

        foreach (uint symbol in symbols)
        {
            if (!newIndex.ContainsKey(symbol))
            {
                newIndex[symbol] = nextIndex;
                output[nextIndex] = temp[(int)symbol];
                nextIndex++;
            }
        }

        if (output.Count > nextIndex)
        {
            output.RemoveRange(nextIndex, output.Count - nextIndex);
        }

        for (int i = 0; i < symbols.Count; i++)
        {
            symbols[i] = (uint)newIndex[symbols[i]];
        }
    }

    public static bool ClusterHistograms(
        JxlHistogramParameters parameters,
        IReadOnlyList<JxlHistogram> input,
        uint maxHistograms,
        List<JxlHistogram> output,
        List<uint> histogramSymbols)
    {
        int previousHistograms = output.Count;

        maxHistograms = Math.Min(maxHistograms, parameters.MaxHistograms);
        maxHistograms = Math.Min(maxHistograms, (uint)input.Count);

        if (parameters.Clustering == JxlClusteringType.Fastest)
        {
            maxHistograms = Math.Min(maxHistograms, 4);
        }

        if (!FastClusterHistograms(input, (int)(previousHistograms + maxHistograms), output, histogramSymbols))
        {
            return false;
        }

        if (previousHistograms == 0 && parameters.Clustering == HistogramParams.ClusteringType.Best)
        {
            for (int i = 0; i < output.Count; i++)
            {
                output[i].Entropy = output[i].AnsPopulationCost();
            }

            uint nextVersion = 2;

            uint[] version = ArrayPool<uint>.Shared.Rent(output.Count);
            Array.Fill(version, 1u);

            uint[] renumbering = ArrayPool<uint>.Shared.Rent(output.Count);
            for (uint i = 0; i < renumbering.Length; i++)
            {
                renumbering[i] = i;
            }

            PriorityQueue<HistogramPair, (float Cost, uint First, uint Second, uint Version)> pairsToMerge = new();

            for (uint i = 0; i < output.Count; i++)
            {
                for (uint j = i + 1; j < output.Count; j++)
                {
                    JxlHistogram histogram = new(16);
                    histogram.AddHistogram(output[(int)i]);
                    histogram.AddHistogram(output[(int)j]);

                    float cost = histogram.AnsEntropyCost() - output[(int)i].Entropy - output[(int)j].Entropy;

                    if (cost >= 0)
                    {
                        continue;
                    }

                    uint versionValue = Math.Max(version[i], version[j]);
                    HistogramPair pair = new(cost, i, j, versionValue);
                    pairsToMerge.Enqueue(pair, (cost, i, j, versionValue));
                }
            }

            while (pairsToMerge.TryDequeue(out HistogramPair pair, out _))
            {
                uint first = pair.First;
                uint second = pair.Second;
                uint versionValue = pair.Version;

                if (versionValue != Math.Max(version[first], version[second]) || version[first] == 0 || version[second] == 0)
                {
                    continue;
                }

                output[(int)first].AddHistogram(output[(int)second]);
                output[(int)first].Entropy = output[(int)first].AnsPopulationCost();

                for (int i = 0; i < renumbering.Length; i++)
                {
                    if (renumbering[i] == second)
                    {
                        renumbering[i] = first;
                    }
                }

                version[second] = 0;
                version[first] = nextVersion++;

                for (uint j = 0; j < output.Count; j++)
                {
                    if (j == first || version[j] == 0)
                    {
                        continue;
                    }

                    JxlHistogram histogram = new(16);
                    histogram.AddHistogram(output[(int)first]);
                    histogram.AddHistogram(output[(int)j]);

                    float mergeCost = histogram.AnsEntropyCost() - output[(int)first].Entropy - output[(int)j].Entropy;

                    if (mergeCost >= 0)
                    {
                        continue;
                    }

                    uint firstIndex = Math.Min(first, j);
                    uint secondIndex = Math.Max(first, j);
                    uint mergeVersion = Math.Max(version[first], version[j]);

                    HistogramPair newPair = new(mergeCost, firstIndex, secondIndex, mergeVersion);
                    pairsToMerge.Enqueue(newPair, (mergeCost, firstIndex, secondIndex, mergeVersion));
                }
            }

            uint[] reverseRenumbering = ArrayPool<uint>.Shared.Rent(output.Count);
            Array.Fill(reverseRenumbering, uint.MaxValue);

            int numberAlive = 0;

            for (int i = 0; i < output.Count; i++)
            {
                if (version[i] == 0)
                {
                    continue;
                }

                output[numberAlive] = output[i];
                reverseRenumbering[i] = (uint)numberAlive;
                numberAlive++;
            }

            if (output.Count > numberAlive)
            {
                output.RemoveRange(numberAlive, output.Count - numberAlive);
            }

            for (int i = 0; i < histogramSymbols.Count; i++)
            {
                uint item = histogramSymbols[i];
                histogramSymbols[i] = reverseRenumbering[renumbering[item]];
            }

            ArrayPool<uint>.Shared.Return(renumbering);
            ArrayPool<uint>.Shared.Return(version);
            ArrayPool<uint>.Shared.Return(reverseRenumbering);
        }

        HistogramReindex(output, previousHistograms, histogramSymbols);
        return true;
    }

    private readonly record struct HistogramPair(float Cost, uint First, uint Second, uint Version);
}

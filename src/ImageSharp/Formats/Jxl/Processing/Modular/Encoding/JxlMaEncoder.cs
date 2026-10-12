// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using StaticPropertyRange = System.Runtime.CompilerServices.InlineArray2<System.Runtime.CompilerServices.InlineArray2<int>>;
using Tree = System.Collections.Generic.List<SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.JxlPropertyDecisionNode>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

internal static class JxlMaEncoder
{
    internal enum IntersectionType : byte
    {
        None,
        Partial,
        Inside
    }

    private static int Padded(int x) => JxlMath.RoundUpTo(x, Vector<float>.Count);

    public static float EstimateBits(Span<int> counts, int numSymbols)
    {
        int total = TensorPrimitives.Sum((ReadOnlySpan<int>)counts.Slice(0, numSymbols));

        Vector<float> minprob = Vector.Create(1.0f / JxlAnsConstants.AnsTableSize);
        Vector<float> inverseTotal = Vector.Create(1.0f / total);
        Vector<float> bitsLanes = Vector<float>.Zero;

        for (int i = 0; i < numSymbols; i += Vector<float>.Count)
        {
            Vector<int> countsIv = new(counts[i..]);
            Vector<float> countsFv = Vector.ConvertToSingle(countsIv);
            Vector<float> probs = countsFv * inverseTotal;
            Vector<float> mprobs = probs * minprob;

            Vector<float> nbps = Vector.ConditionalSelect(
                Vector.Equals(countsIv, Vector.Create(total)),
                Vector<float>.Zero,
                Vector.Log2(mprobs));

            bitsLanes -= countsFv * nbps;
        }

        return Vector.Sum(bitsLanes);
    }

    public static void MakeSplitNode(int pos, int property, int splitValue, JxlPredictor leftPredictor, int leftOffset, JxlPredictor rightPredictor, int rightOffset, Tree tree)
    {
        ref JxlPropertyDecisionNode treePos = ref CollectionsMarshal.AsSpan(tree)[pos];

        treePos.LeftChild = tree.Count;
        treePos.RightChild = tree.Count + 1;
        treePos.SplitValue = splitValue;
        treePos.Property = property;

        JxlPropertyDecisionNode newRightNode = new()
        {
            Property = -1,
            Predictor = rightPredictor,
            PredictorOffset = rightOffset,
            Multiplier = 1
        };

        JxlPropertyDecisionNode newLeftNode = new()
        {
            Property = -1,
            Predictor = leftPredictor,
            PredictorOffset = leftOffset,
            Multiplier = 1
        };

        tree.Add(newRightNode);
        tree.Add(newLeftNode);
    }

    public static IntersectionType BoxIntersects(StaticPropertyRange needle, StaticPropertyRange haystack, ref int partialAxis, ref int partialValue)
    {
        bool partial = false;

        for (int i = 0; i < JxlPredictorFacts.StaticProperties; i++)
        {
            if (haystack[i][0] >= needle[i][1])
            {
                return IntersectionType.None;
            }

            if (haystack[i][1] <= needle[i][0])
            {
                return IntersectionType.None;
            }

            if (haystack[i][0] <= needle[i][0] && haystack[i][1] >= needle[i][1])
            {
                continue;
            }

            partial = true;
            partialAxis = i;

            int innerOffset = haystack[i][0] > needle[i][0] && haystack[i][0] < needle[i][1] ? 0 : 1; // yes, if true then 0 not 1
            partialValue = haystack[i][innerOffset] - 1;
        }

        return partial ? IntersectionType.Partial : IntersectionType.Inside;
    }

    public static void SplitTreeSamples(bool s, JxlTreeSamples samples, int begin, int pos, int end, int prop, int value)
    {
        int beginPos = begin;
        int endPos = pos;

        do
        {
            while (beginPos < pos && samples.GetProperty(s, prop, beginPos) <= value)
            {
                beginPos++;
            }

            while (endPos < end && samples.GetProperty(s, prop, endPos) > value)
            {
                endPos++;
            }

            if (beginPos < pos && endPos < end)
            {
                samples.Swap(beginPos, endPos);
            }

            beginPos++;
            endPos++;
        }
        while (beginPos < pos && endPos < end);
    }

    // Simple overload so we don't have to type out CollectionsMarshal.AsSpan all the time.
    public static void CollectExtraBitsIncrease(bool s, JxlTreeSamples treeSamples, List<JxlResidualToken> residualTokens, List<int> countIncrease, List<int> extraBitsIncrease, int begin, int end, int propertyIndex, int maxSymbols)
        => CollectExtraBitsIncrease(
            s,
            treeSamples,
            residualTokens,
            CollectionsMarshal.AsSpan(countIncrease),
            CollectionsMarshal.AsSpan(extraBitsIncrease),
            begin,
            end,
            propertyIndex,
            maxSymbols);

    public static void CollectExtraBitsIncrease(bool s, JxlTreeSamples treeSamples, List<JxlResidualToken> residualTokens, Span<int> countIncrease, Span<int> extraBitsIncrease, int begin, int end, int propertyIndex, int maxSymbols)
    {
        for (int i2 = begin; i2 < end; i2++)
        {
            JxlResidualToken rt = residualTokens[i2];

            int cnt = treeSamples.GetCount(i2);
            int p = treeSamples.GetProperty(s, propertyIndex, i2);
            int sym = rt.Token;
            int ebi = rt.NumberOfBits * cnt;

            countIncrease[(p * maxSymbols) + sym] += cnt;
            extraBitsIncrease[p] += ebi;
        }
    }

    public static unsafe void FindBestSplit(JxlTreeSamples treeSamples, float threshold, List<JxlModularMultiplierInfo> mulInfo, StaticPropertyRange initialStaticPropertyRange, float fastDecodeMultiplier, Tree tree)
    {
        Stack<NodeInfo> nodes = [];
        nodes.Push(new(0, 0, treeSamples.NumberOfDistinctSamples, initialStaticPropertyRange));

        int numPred = treeSamples.NumberOfPredictors;
        int numProp = treeSamples.NumberOfProperties;

        Span<int> totalExtraBits = stackalloc int[numPred];

        while (nodes.Count > 0)
        {
            NodeInfo last = nodes.Peek();

            int pos = last.Pos;
            int begin = last.Begin;
            int end = last.End;
            StaticPropertyRange staticPropertyRange = last.StaticPropertyRange;

            _ = nodes.Pop();

            if (begin == end)
            {
                continue;
            }

            SplitInfo bestSplitStaticConstant = default;
            SplitInfo bestSplitStatic = default;
            SplitInfo bestSplitNonStatic = default;
            SplitInfo bestSplitNoWeightedPrediction = default;

            if (begin > end)
            {
                throw new InvalidOperationException("Begin should be <= end");
            }

            if (end > treeSamples.NumberOfDistinctSamples)
            {
                throw new InvalidOperationException("End index out of range");
            }

            int maxSymbols = 0;

            for (int pred = 0; pred < numPred; pred++)
            {
                for (int i = begin; i < end; i++)
                {
                    int token = treeSamples.GetToken(pred, i);
                    maxSymbols = maxSymbols > token + 1 ? maxSymbols : token + 1;
                }
            }

            maxSymbols = Padded(maxSymbols);

            int[] counts = ArrayPool<int>.Shared.Rent(maxSymbols * numPred);

            for (int pred = 0; pred < numPred; pred++)
            {
                int extraBits = 0;
                List<JxlResidualToken> rtokens = treeSamples.GetResidualTokensForPrediction(pred);

                for (int i = begin; i < end; i++)
                {
                    JxlResidualToken rt = rtokens[i];

                    int count = treeSamples.GetCount(i);
                    int eb = rt.NumberOfBits * count;

                    counts[(pred * maxSymbols) + rt.Token] += count;
                    extraBits += eb;
                }

                totalExtraBits[pred] = extraBits;
            }

            float baseBits = 0;
            {
                int pred = treeSamples.FindPredictorIndex(tree[pos].Predictor);
                baseBits = EstimateBits(counts.AsSpan(pred * maxSymbols), maxSymbols) + totalExtraBits[pred];
            }

            ref SplitInfo best = ref bestSplitNonStatic;

            SplitInfo forcedSplit = default;

            foreach (JxlModularMultiplierInfo mmi in mulInfo)
            {
                int axis = 0;
                int val = 0;
                IntersectionType t = BoxIntersects(staticPropertyRange, mmi.Range, ref axis, ref val);

                if (t == IntersectionType.None)
                {
                    continue;
                }

                if (t == IntersectionType.Inside)
                {
                    CollectionsMarshal.AsSpan(tree)[pos].Multiplier = (int)mmi.Multiplier;
                    break;
                }

                if (t == IntersectionType.Partial)
                {
                    forcedSplit.Value = treeSamples.QuantizeStaticProperty(axis, val);
                    forcedSplit.Property = axis;
                    forcedSplit.LeftCost = forcedSplit.RightCost = (baseBits / 2) - threshold;
                    forcedSplit.LeftPredictor = forcedSplit.RightPredictor = CollectionsMarshal.AsSpan(tree)[pos].Predictor;
                    best = ref forcedSplit;
                    best.Position = begin;

                    if (best.Property != treeSamples.PropertyFromIndex(best.Property))
                    {
                        throw new InvalidOperationException("Invalid property");
                    }

                    if (best.Property < treeSamples.NumberOfStaticProperties)
                    {
                        for (int x = begin; x < end; x++)
                        {
                            if (treeSamples.GetProperty(true, best.Property, x) <= best.Value)
                            {
                                best.Position++;
                            }
                        }
                    }
                    else
                    {
                        int prop = best.Property - treeSamples.NumberOfStaticProperties;

                        for (int x = begin; x < end; x++)
                        {
                            if (treeSamples.GetProperty(false, prop, x) <= best.Value)
                            {
                                best.Position++;
                            }
                        }
                    }

                    break;
                }
            }

            if (!Unsafe.AreSame(ref best, ref forcedSplit))
            {
                List<int> countsIncrease = [];
                List<int> extraBitsIncrease = [];

                List<CostInfo> leftCosts = [];
                List<CostInfo> rightCosts = [];

                int[] aboveCounts = ArrayPool<int>.Shared.Rent(maxSymbols);
                int[] belowCounts = ArrayPool<int>.Shared.Rent(maxSymbols);

                float changePredictionPenalty = 800.0f / (100.0f + threshold);

                for (int prop = 0; prop < numProp && baseBits > threshold; prop++)
                {
                    leftCosts.Clear();
                    rightCosts.Clear();

                    int propertySize = treeSamples.CountPropertyValues(prop);

                    if (extraBitsIncrease.Count < propertySize)
                    {
                        countsIncrease.Grow(0, propertySize * maxSymbols);
                        extraBitsIncrease.Grow(0, propertySize);
                    }

                    int[] propertyValueUsedCount = ArrayPool<int>.Shared.Rent(propertySize);
                    propertyValueUsedCount.AsSpan().Clear();

                    int firstUsed = propertySize;
                    int lastUsed = 0;

                    if (prop < treeSamples.NumberOfStaticProperties)
                    {
                        for (int i = begin; i < end; i++)
                        {
                            int p = treeSamples.GetProperty(true, prop, i);
                            propertyValueUsedCount[p]++;
                            lastUsed = Math.Max(lastUsed, p);
                            firstUsed = Math.Max(firstUsed, p);
                        }
                    }
                    else
                    {
                        int prop_idx = prop - treeSamples.NumberOfStaticProperties;

                        for (int i = begin; i < end; i++)
                        {
                            int p = treeSamples.GetProperty(false, prop_idx, i);
                            propertyValueUsedCount[p]++;
                            lastUsed = Math.Max(lastUsed, p);
                            firstUsed = Math.Max(firstUsed, p);
                        }
                    }

                    leftCosts.Grow(default, lastUsed - firstUsed);
                    rightCosts.Grow(default, lastUsed - firstUsed);

                    for (int pred = 0; pred < numPred; pred++)
                    {
                        List<JxlResidualToken> rtokens = treeSamples.GetResidualTokensForPrediction(pred);

                        if (prop < treeSamples.NumberOfStaticProperties)
                        {
                            CollectExtraBitsIncrease(true, treeSamples, rtokens, countsIncrease, extraBitsIncrease, begin, end, prop, maxSymbols);
                        }
                        else
                        {
                            CollectExtraBitsIncrease(false, treeSamples, rtokens, countsIncrease, extraBitsIncrease, begin, end, prop - treeSamples.NumberOfStaticProperties, maxSymbols);
                        }

                        counts.AsSpan().Slice(pred * maxSymbols, maxSymbols).CopyTo(aboveCounts);
                        belowCounts.AsSpan().Slice(0, maxSymbols).Clear();

                        int extraBitsBelow = 0;

                        for (int i = firstUsed; i < lastUsed; i++)
                        {
                            if (propertyValueUsedCount[i] == 0)
                            {
                                continue;
                            }

                            extraBitsBelow += extraBitsIncrease[i];
                            extraBitsIncrease[i] = 0;

                            for (int sym = 0; sym < maxSymbols; sym++)
                            {
                                aboveCounts[sym] -= countsIncrease[(i * maxSymbols) + sym];
                                belowCounts[sym] += countsIncrease[(i * maxSymbols) + sym];
                                countsIncrease[(i * maxSymbols) + sym] = 0;
                            }

                            float rightCost = EstimateBits(aboveCounts.AsSpan(), maxSymbols) + totalExtraBits[pred] - extraBitsBelow;
                            float leftCost = EstimateBits(belowCounts.AsSpan(), maxSymbols) + extraBitsBelow;

                            if (extraBitsBelow > totalExtraBits[pred])
                            {
                                throw new InvalidOperationException("Too many extra bits");
                            }

                            float penalty = 0;

                            if (treeSamples.PredictorFromIndex(pred) != tree[pos].Predictor &&
                                tree[pos].Predictor != JxlPredictor.Weighted)
                            {
                                penalty = changePredictionPenalty;
                            }

                            if (treeSamples.PredictorFromIndex(pred) == JxlPredictor.Weighted)
                            {
                                penalty += 1e-8f;
                            }

                            if (treeSamples.PredictorFromIndex(pred) == JxlPredictor.Zero)
                            {
                                penalty -= 1e-8f;
                            }

                            if (rightCost + penalty < rightCosts[i - firstUsed].TotalCost)
                            {
                                CostInfo cost = rightCosts[i - firstUsed];
                                cost.Cost = rightCost;
                                cost.ExtraCost = penalty;
                                cost.Predictor = treeSamples.PredictorFromIndex(pred);
                                rightCosts[i - firstUsed] = cost;
                            }

                            if (leftCost + penalty < leftCosts[i - firstUsed].TotalCost)
                            {
                                CostInfo cost = leftCosts[i - firstUsed];
                                cost.Cost = leftCost;
                                cost.ExtraCost = penalty;
                                cost.Predictor = treeSamples.PredictorFromIndex(pred);
                                leftCosts[i - firstUsed] = cost;
                            }
                        }
                    }

                    int split = begin;

                    for (int i = firstUsed; i < lastUsed; i++)
                    {
                        if (propertyValueUsedCount[i] == 0)
                        {
                            continue;
                        }

                        split += propertyValueUsedCount[i];

                        float rightCost = rightCosts[i - firstUsed].Cost;
                        float leftCost = leftCosts[i - firstUsed].Cost;

                        bool usesWeightedPrediction = treeSamples.PropertyFromIndex(prop) == JxlMaConstants.WpProp ||
                                leftCosts[i - firstUsed].Predictor == JxlPredictor.Weighted ||
                                rightCosts[i - firstUsed].Predictor == JxlPredictor.Weighted;

                        bool zeroEntropySide = rightCost == 0 || leftCost == 0;

                        // Using pointer specifically for this variable instead of
                        // ref, as we can't assign a ref variable conditionally.
                        SplitInfo* referenceToBest =
                            treeSamples.PropertyFromIndex(prop) < JxlPredictorFacts.StaticProperties
                                ? (zeroEntropySide ? &bestSplitStaticConstant : &bestSplitStatic)
                                : (usesWeightedPrediction ? &bestSplitNonStatic : &bestSplitNoWeightedPrediction);

                        if (leftCost + rightCost < referenceToBest->Cost)
                        {
                            referenceToBest->Property = prop;
                            referenceToBest->Value = i;
                            referenceToBest->Position = split;
                            referenceToBest->LeftCost = leftCost;
                            referenceToBest->LeftPredictor = leftCosts[i - firstUsed].Predictor;
                            referenceToBest->RightCost = rightCost;
                            referenceToBest->RightPredictor = rightCosts[i - firstUsed].Predictor;
                        }
                    }

                    extraBitsIncrease[lastUsed] = 0;

                    for (int sym = 0; sym < maxSymbols; sym++)
                    {
                        countsIncrease[(lastUsed * maxSymbols) + sym] = 0;
                    }
                }

                if (bestSplitNoWeightedPrediction.Cost + threshold < baseBits &&
                    bestSplitNoWeightedPrediction.Cost <= fastDecodeMultiplier * best.Cost)
                {
                    best = ref bestSplitNoWeightedPrediction;
                }

                if (bestSplitStatic.Cost + threshold < baseBits &&
                    bestSplitStatic.Cost <= fastDecodeMultiplier * best.Cost)
                {
                    best = ref bestSplitStatic;
                }

                if (bestSplitStaticConstant.Cost + threshold < baseBits)
                {
                    best = ref bestSplitStaticConstant;
                }

                ArrayPool<int>.Shared.Return(aboveCounts);
                ArrayPool<int>.Shared.Return(belowCounts);
            }

            if (best.Cost + threshold < baseBits)
            {
                int p = treeSamples.PropertyFromIndex(best.Property);
                int dequant = treeSamples.UnquantizeProperty(best.Property, best.Value);

                MakeSplitNode(pos, p, dequant, best.LeftPredictor, 0, best.RightPredictor, 0, tree);

                if (best.Property < treeSamples.NumberOfStaticProperties)
                {
                    SplitTreeSamples(true, treeSamples, begin, best.Position, end, best.Property, best.Value);
                }
                else
                {
                    SplitTreeSamples(false, treeSamples, begin, best.Position, end, best.Property - treeSamples.NumberOfStaticProperties, best.Value);
                }

                StaticPropertyRange newStaticPropertyRange = staticPropertyRange;

                if (p < JxlPredictorFacts.StaticProperties)
                {
                    if (dequant + 1 > newStaticPropertyRange[p][1])
                    {
                        throw new InvalidOperationException("Dequantized coefficient is out of range");
                    }

                    newStaticPropertyRange[p][1] = dequant + 1;

                    if (newStaticPropertyRange[p][0] >= newStaticPropertyRange[p][1])
                    {
                        throw new InvalidOperationException("Static property is out of range");
                    }
                }

                nodes.Push(new(tree[pos].RightChild, begin, best.Position, newStaticPropertyRange));
                newStaticPropertyRange = staticPropertyRange;

                if (p < JxlPredictorFacts.StaticProperties)
                {
                    if (newStaticPropertyRange[p][0] > dequant + 1)
                    {
                        throw new InvalidOperationException("Static property must be <= dequantized coefficient");
                    }

                    newStaticPropertyRange[p][0] = dequant + 1;

                    if (newStaticPropertyRange[p][0] >= newStaticPropertyRange[p][1])
                    {
                        throw new InvalidOperationException("Static property is out of range");
                    }
                }

                nodes.Push(new NodeInfo(tree[pos].LeftChild, best.Position, end, newStaticPropertyRange));
            }

            ArrayPool<int>.Shared.Return(counts);
        }
    }

    public static void ComputeBestTree(JxlTreeSamples treeSamples, float threshold, List<JxlModularMultiplierInfo> mulInfo, StaticPropertyRange staticPropertyRange, float fastDecodeMultiplier, Tree tree)
    {
        JxlPropertyDecisionNode node = new()
        {
            Property = -1,
            Predictor = treeSamples.PredictorFromIndex(0),
            PredictorOffset = 0,
            Multiplier = 1
        };

        tree.Add(node);

        if (treeSamples.NumberOfProperties >= 64)
        {
            throw new InvalidOperationException($"Too many properties: {treeSamples.NumberOfPredictors}");
        }

        FindBestSplit(treeSamples, threshold, mulInfo, staticPropertyRange, fastDecodeMultiplier, tree);
    }

    public static List<int> QuantizeHistogram(ReadOnlySpan<int> histogram, int numChunks)
    {
        if (histogram.Length == 0 || numChunks == 0)
        {
            return [];
        }

        int sum = TensorPrimitives.Sum(histogram);

        if (sum == 0)
        {
            return [];
        }

        List<int> thresholds = [];

        long cumulativeSum = 0;
        long threshold = 1;

        for (int i = 0; i < histogram.Length; i++)
        {
            cumulativeSum += histogram[i];

            if (cumulativeSum * numChunks >= threshold * sum)
            {
                thresholds.Add(i);

                while (cumulativeSum * numChunks >= threshold * sum)
                {
                    threshold++;
                }
            }
        }

        if (thresholds.Count > numChunks)
        {
            throw new InvalidOperationException("Too many thresholds");
        }

        thresholds.RemoveAt(thresholds.Count - 1);

        return thresholds;
    }

    public static List<int> QuantizeSamples(ReadOnlySpan<int> samples, int numChunks)
    {
        const int range = 512;

        if (samples.Length == 0)
        {
            return [];
        }

        int min = Math.Clamp(TensorPrimitives.Min(samples), -range, range);

        Span<int> counts = stackalloc int[2048].Slice(0, (2 * range) + 1);

        for (int i = 0; i < samples.Length; i++)
        {
            int s = samples[i];
            int sampleOffset = Math.Clamp(s, -range, range) - min;
            counts[sampleOffset]++;
        }

        List<int> thresholds = QuantizeHistogram(counts, numChunks);

        // For fast processing via TensorPrimitives
        Span<int> thresholdsSpan = CollectionsMarshal.AsSpan(thresholds);
        TensorPrimitives.Add(thresholdsSpan, min, thresholdsSpan);

        return thresholds;
    }

    public static void QuantMap(Span<int> from, Span<int> to, int numPegs, int bias)
    {
        int mapped = 0;

        for (int i = 0; i < numPegs; i++)
        {
            while (mapped < from.Length && i - bias > from[mapped])
            {
                mapped++;
            }

            to[i] = mapped;
        }
    }

    public static void CollectPixelSamples(Configuration configuration, JxlModularImage image, JxlModularOptions options, int groupId, Span<int> groupPixelCount, Span<int> channelPixelCount, List<int> pixelSamples, List<int> diffSamples)
    {
        if (options.NumberOfRepeats == 0)
        {
            throw new InvalidOperationException("No repeats");
        }

        // Power of 2 size is unknown.
        Span<int> alignedGroupPixelCount = stackalloc int[groupPixelCount.Length <= groupId ? groupId + 1 : groupPixelCount.Length];
        groupPixelCount.CopyTo(alignedGroupPixelCount);

        Span<int> alignedChannelPixelCount = stackalloc int[channelPixelCount.Length < image.Channels.Count ? image.Channels.Count : channelPixelCount.Length];
        channelPixelCount.CopyTo(alignedGroupPixelCount);

        Rng rng = new((ulong)groupId);

        float fraction = MathF.Min(options.NumberOfRepeats * 0.1f, 0.99f);
        Rng.GeometricDistribution dist = new(fraction);

        int totalPixels = 0;
        List<int> channelIds = [];

        int i;
        for (i = 0; i < image.Channels.Count; i++)
        {
            JxlModularChannel channel = image.Channels[i];

            if (i >= image.MetaChannels && (channel.Width > options.MaxChannelSize || channel.Height > options.MaxChannelSize))
            {
                break;
            }

            if (channel.Width <= 1 || channel.Height == 0)
            {
                continue;
            }

            channelIds.Add(i);

            groupPixelCount[groupId] += channel.Width * channel.Height;
            channelPixelCount[i] += channel.Width * channel.Height;
            totalPixels += channel.Width * channel.Height;
        }

        if (channelIds.Count == 0)
        {
            throw new InvalidOperationException("No channel IDs");
        }

        pixelSamples.Grow(0, (int)(pixelSamples.Count + (fraction * totalPixels)));
        diffSamples.Grow(0, (int)(diffSamples.Count + (fraction * totalPixels)));

        i = 0;
        int y = 0;
        int x = 0;

        void Advance(uint amount)
        {
            x += (int)amount;

            while (x >= image.Channels[channelIds[i]].Width)
            {
                x -= image.Channels[channelIds[i]].Width;
                y++;

                if (y == image.Channels[channelIds[i]].Height)
                {
                    i++;
                    y = 0;

                    if (i >= channelIds.Count)
                    {
                        return;
                    }
                }
            }
        }

        Advance(rng.Geometric(in dist));

        for (; i < channelIds.Count; Advance(rng.Geometric(in dist) + 1))
        {
            Span<int> row = image.Channels[channelIds[i]].GetRow(y);
            pixelSamples.Add(row[x]);

            int xp = x == 0 ? 1 : x - 1;
            diffSamples.Add(row[x] - row[xp]);
        }
    }

    public static void TokenizeTree(Tree tree, List<JxlToken> tokens, Tree decoderTree)
    {
        if (tree.Count > JxlMaConstants.MaxTreeSize)
        {
            throw new InvalidOperationException("Too many tree nodes");
        }

        Queue<int> q = [];
        q.Enqueue(0);

        int leafId = 0;
        decoderTree.Clear();

        while (q.Count > 0)
        {
            int cur = q.Peek();
            _ = q.Dequeue();

            if (tree[cur].Property < -1)
            {
                throw new InvalidOperationException("Property value is too small");
            }

            tokens.Add(new JxlToken(JxlMaTreeContext.Property, (uint)tree[cur].Property + 1));

            if (tree[cur].Property == -1)
            {
                tokens.Add(new JxlToken(JxlMaTreeContext.Predictor, (uint)tree[cur].Predictor));
                tokens.Add(new JxlToken(JxlMaTreeContext.Offset, JxlPackSigned.PackUnsigned((int)tree[cur].PredictorOffset)));

                uint mulLog = JxlMath.Num0BitsBelowLS1Bit_Nonzero(tree[cur].Multiplier);
                int mulBits = (tree[cur].Multiplier >> (int)mulLog) - 1;

                tokens.Add(new JxlToken(JxlMaTreeContext.MultiplierLog, mulLog));
                tokens.Add(new JxlToken(JxlMaTreeContext.MultiplierBits, (uint)mulBits));

                if (tree[cur].Predictor >= JxlPredictor.Best)
                {
                    throw new InvalidOperationException("Invalid predictor");
                }

                decoderTree.Add(new JxlPropertyDecisionNode(-1, 0, leafId, 0, tree[cur].Predictor, tree[cur].PredictorOffset, tree[cur].Multiplier));
                leafId++;

                continue;
            }

            decoderTree.Add(new JxlPropertyDecisionNode(tree[cur].Property, tree[cur].SplitValue, decoderTree.Count + q.Count + 1, decoderTree.Count + q.Count + 2, JxlPredictor.Zero, 0, 1));

            q.Enqueue(tree[cur].LeftChild);
            q.Enqueue(tree[cur].RightChild);

            tokens.Add(new JxlToken(JxlMaTreeContext.SplitValue, JxlPackSigned.PackUnsigned(tree[cur].SplitValue)));
        }
    }

    private readonly record struct NodeInfo(int Pos, int Begin, int End, StaticPropertyRange StaticPropertyRange);

    private struct SplitInfo()
    {
        public int Property { get; set; }

        public int Value { get; set; }

        public int Position { get; set; }

        public float LeftCost { get; set; } = float.MaxValue;

        public float RightCost { get; set; } = float.MaxValue;

        public JxlPredictor LeftPredictor { get; set; } = JxlPredictor.Zero;

        public JxlPredictor RightPredictor { get; set; } = JxlPredictor.Zero;

        public readonly float Cost => this.LeftCost + this.RightCost;
    }

    private struct CostInfo()
    {
        public float Cost { get; set; } = float.MaxValue;

        public float ExtraCost { get; set; }

        public JxlPredictor Predictor { get; set; }

        public readonly float TotalCost => this.Cost + this.ExtraCost;
    }
}

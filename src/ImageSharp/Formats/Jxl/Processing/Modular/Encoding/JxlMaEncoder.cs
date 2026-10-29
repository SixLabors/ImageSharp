// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using StaticPropertyRange = SixLabors.ImageSharp.InlineArray2<SixLabors.ImageSharp.InlineArray2<int>>;
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

    /// <summary>
    /// Estimates the ANS bit cost of a histogram with the codec's minimum nonzero symbol probability.
    /// </summary>
    /// <param name="counts">The nonnegative occurrence counts. Storage after numSymbols is not part of the histogram.</param>
    /// <param name="numSymbols">The logical number of histogram entries.</param>
    /// <returns>The estimated cost in bits. Empty and single-symbol histograms have zero cost.</returns>
    public static float EstimateBits(ReadOnlySpan<int> counts, int numSymbols)
    {
        ReadOnlySpan<int> histogram = counts[..numSymbols];
        int total = TensorPrimitives.Sum(histogram);
        HistogramBitCostOperator operation = new(total);
        ref int input = ref MemoryMarshal.GetReference(histogram);
        float cost = 0F;
        int i = 0;

        // Convert integer counts numerically. Keep the single-symbol comparison in integer lanes so counts above 2^24 cannot compare equal by rounding.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (histogram.Length - i).Vector512Count<int>();
            Vector512<int> totalCounts = Vector512.Create(total);
            Vector512<float> accumulated = Vector512<float>.Zero;

            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                Vector512<int> occurrences = Vector512.LoadUnsafe(ref input, (uint)i);
                Vector512<float> bits = operation.Invoke(Vector512.ConvertToSingle(occurrences));
                bits = Vector512.ConditionalSelect(Vector512.Equals(occurrences, totalCounts).AsSingle(), Vector512<float>.Zero, bits);
                accumulated += bits;
            }

            cost += Vector512.Sum(accumulated);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (histogram.Length - i).Vector256Count<int>();
            Vector256<int> totalCounts = Vector256.Create(total);
            Vector256<float> accumulated = Vector256<float>.Zero;

            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                Vector256<int> occurrences = Vector256.LoadUnsafe(ref input, (uint)i);
                Vector256<float> bits = operation.Invoke(Vector256.ConvertToSingle(occurrences));
                bits = Vector256.ConditionalSelect(Vector256.Equals(occurrences, totalCounts).AsSingle(), Vector256<float>.Zero, bits);
                accumulated += bits;
            }

            cost += Vector256.Sum(accumulated);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (histogram.Length - i).Vector128Count<int>();
            Vector128<int> totalCounts = Vector128.Create(total);
            Vector128<float> accumulated = Vector128<float>.Zero;

            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                Vector128<int> occurrences = Vector128.LoadUnsafe(ref input, (uint)i);
                Vector128<float> bits = operation.Invoke(Vector128.ConvertToSingle(occurrences));
                bits = Vector128.ConditionalSelect(Vector128.Equals(occurrences, totalCounts).AsSingle(), Vector128<float>.Zero, bits);
                accumulated += bits;
            }

            cost += Vector128.Sum(accumulated);
        }

        // The native single-symbol rule also covers total zero: all its counts are zero and contribute zero.
        // Scalar tails require no padding and use the same probability floor as complete registers.
        for (; i < histogram.Length; i++)
        {
            int value = Unsafe.Add(ref input, i);
            cost += value == total ? 0F : operation.Invoke(value);
        }

        return cost;
    }

    /// <summary>
    /// Calculates -count * log2(max(count / total, 1 / ANS table size)) for one histogram entry.
    /// </summary>
    /// <remarks>The caller handles a count equal to the histogram total before reduction, including the all-zero case.</remarks>
    private readonly struct HistogramBitCostOperator : TensorPrimitives_.IUnaryOperator
    {
        private const float MinimumProbability = 1F / JxlAnsConstants.AnsTableSize;

        private readonly float inverseTotal;

        /// <summary>
        /// Initializes a new instance of the <see cref="HistogramBitCostOperator"/> struct.
        /// </summary>
        /// <param name="total">The histogram's total occurrence count.</param>
        public HistogramBitCostOperator(int total) => this.inverseTotal = 1F / total;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
            => -(value * MathF.Log2(MathF.Max(value * this.inverseTotal, MinimumProbability)));

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // ANS cannot assign a nonzero symbol a probability below one table entry. Flooring probabilities also keeps empty bins away from log2(0).
            Vector128<float> probability = Vector128.Max(value * Vector128.Create(this.inverseTotal), Vector128.Create(MinimumProbability));
            return -(value * Vector128.Log2(probability));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // ANS cannot assign a nonzero symbol a probability below one table entry. Flooring probabilities also keeps empty bins away from log2(0).
            Vector256<float> probability = Vector256.Max(value * Vector256.Create(this.inverseTotal), Vector256.Create(MinimumProbability));
            return -(value * Vector256.Log2(probability));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // ANS cannot assign a nonzero symbol a probability below one table entry. Flooring probabilities also keeps empty bins away from log2(0).
            Vector512<float> probability = Vector512.Max(value * Vector512.Create(this.inverseTotal), Vector512.Create(MinimumProbability));
            return -(value * Vector512.Log2(probability));
        }
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

    /// <summary>
    /// Partitions samples around a property value at the selected split index.
    /// </summary>
    /// <param name="useStaticProperty">Whether to read the static property table.</param>
    /// <param name="samples">The samples to partition.</param>
    /// <param name="begin">The inclusive start of the sample range.</param>
    /// <param name="splitIndex">The boundary between the two sample partitions.</param>
    /// <param name="end">The exclusive end of the sample range.</param>
    /// <param name="propertyIndex">The property used for the split.</param>
    /// <param name="splitValue">The largest property value in the first partition.</param>
    public static void SplitTreeSamples(
        bool useStaticProperty,
        scoped ref JxlTreeSamples samples,
        int begin,
        int splitIndex,
        int end,
        int propertyIndex,
        int splitValue)
    {
        int beginPos = begin;
        int endPos = splitIndex;

        do
        {
            while (beginPos < splitIndex && samples.GetProperty(useStaticProperty, propertyIndex, beginPos) <= splitValue)
            {
                beginPos++;
            }

            while (endPos < end && samples.GetProperty(useStaticProperty, propertyIndex, endPos) > splitValue)
            {
                endPos++;
            }

            if (beginPos < splitIndex && endPos < end)
            {
                samples.Swap(beginPos, endPos);
            }

            beginPos++;
            endPos++;
        }
        while (beginPos < splitIndex && endPos < end);
    }

    /// <summary>
    /// Accumulates symbol counts and extra-bit costs for a candidate property.
    /// </summary>
    /// <param name="useStaticProperty">Whether to read the static property table.</param>
    /// <param name="treeSamples">The samples and their occurrence counts.</param>
    /// <param name="residualTokens">The encoded residual for each sample.</param>
    /// <param name="countIncrease">The destination occurrence counts, grouped by property value and symbol.</param>
    /// <param name="extraBitsIncrease">The destination extra-bit costs for each property value.</param>
    /// <param name="begin">The inclusive start of the sample range.</param>
    /// <param name="end">The exclusive end of the sample range.</param>
    /// <param name="propertyIndex">The candidate property index.</param>
    /// <param name="maxSymbols">The number of symbol entries reserved for each property value.</param>
    public static void CollectExtraBitsIncrease(
        bool useStaticProperty,
        scoped in JxlTreeSamples treeSamples,
        scoped ReadOnlySpan<JxlResidualToken> residualTokens,
        Span<int> countIncrease,
        Span<long> extraBitsIncrease,
        int begin,
        int end,
        int propertyIndex,
        int maxSymbols)
    {
        for (int i2 = begin; i2 < end; i2++)
        {
            JxlResidualToken rt = residualTokens[i2];

            int cnt = treeSamples.GetCount(i2);
            int p = treeSamples.GetProperty(useStaticProperty, propertyIndex, i2);
            int sym = rt.Token;
            int ebi = rt.NumberOfBits * cnt;

            countIncrease[(p * maxSymbols) + sym] += cnt;
            extraBitsIncrease[p] += ebi;
        }
    }

    /// <summary>
    /// Chooses prediction-tree splits with one reusable allocator-owned counter workspace.
    /// </summary>
    /// <param name="configuration">The configuration that owns search storage.</param>
    /// <param name="treeSamples">The prepared sample owner, partitioned in place as splits are accepted.</param>
    /// <param name="threshold">The minimum bit saving required to split a node.</param>
    /// <param name="mulInfo">The forced multiplier ranges.</param>
    /// <param name="initialStaticPropertyRange">The channel/group ranges covered by the root node.</param>
    /// <param name="fastDecodeMultiplier">The allowed cost ratio for simpler predictors.</param>
    /// <param name="tree">The root leaf and destination tree nodes.</param>
    public static void FindBestSplit(
        Configuration configuration,
        scoped ref JxlTreeSamples treeSamples,
        float threshold,
        List<JxlModularMultiplierInfo> mulInfo,
        StaticPropertyRange initialStaticPropertyRange,
        float fastDecodeMultiplier,
        Tree tree)
    {
        Stack<NodeInfo> nodes = [];
        nodes.Push(new(0, 0, treeSamples.NumberOfDistinctSamples, initialStaticPropertyRange));

        int numPred = treeSamples.NumberOfPredictors;
        int numProp = treeSamples.NumberOfProperties;

        int maximumPropertySize = 0;
        for (int property = 0; property < numProp; property++)
        {
            maximumPropertySize = Math.Max(maximumPropertySize, treeSamples.CountPropertyValues(property));
        }

        // Sample residuals use HybridUint(4,1,2): even UInt32.MaxValue produces token 239, inside the existing 256-symbol ANS alphabet.
        // Reserve that bounded alphabet once. Individual nodes still use their smaller active stride and never resize these buffers.
        int symbolCapacity = Padded(JxlAnsConstants.AnsMaxAlphabetSize);
        int counterLength = checked(((numPred + 2) * symbolCapacity) + (maximumPropertySize * (symbolCapacity + 1)));
        using IMemoryOwner<int> counterOwner = configuration.MemoryAllocator.Allocate<int>(counterLength);
        using IMemoryOwner<CostInfo> costOwner = configuration.MemoryAllocator.Allocate<CostInfo>(2 * maximumPropertySize);
        using IMemoryOwner<long> bitOwner = configuration.MemoryAllocator.Allocate<long>(maximumPropertySize + numPred);
        Span<int> counters = counterOwner.Memory.Span;
        Span<CostInfo> costs = costOwner.Memory.Span;
        Span<long> bitCounts = bitOwner.Memory.Span;
        int offset = numPred * symbolCapacity;
        Span<int> counts = counters[..offset];
        Span<int> aboveCounts = counters.Slice(offset, symbolCapacity);
        offset += symbolCapacity;
        Span<int> belowCounts = counters.Slice(offset, symbolCapacity);
        offset += symbolCapacity;
        Span<int> countsIncrease = counters.Slice(offset, maximumPropertySize * symbolCapacity);
        offset += countsIncrease.Length;
        Span<int> propertyValueUsedCount = counters.Slice(offset, maximumPropertySize);
        Span<long> extraBitsIncrease = bitCounts[..maximumPropertySize];
        Span<long> totalExtraBits = bitCounts[maximumPropertySize..];
        Span<CostInfo> leftCosts = costs[..maximumPropertySize];
        Span<CostInfo> rightCosts = costs[maximumPropertySize..];

        // Extra-bit totals use Int64: many valid 65535-count records can exceed an Int32 bit total.
        // Allocation contents are unspecified. The incremental buffers are cleared here, then drained after each predictor's scan.
        // Parent histograms, property usage, child histograms and candidate costs are initialized to their logical extent before each use.
        countsIncrease.Clear();
        extraBitsIncrease.Clear();

        while (nodes.Count > 0)
        {
            NodeInfo last = nodes.Pop();

            int pos = last.Pos;
            int begin = last.Begin;
            int end = last.End;
            StaticPropertyRange staticPropertyRange = last.StaticPropertyRange;

            if (begin == end)
            {
                continue;
            }

            SplitInfo bestSplitStaticConstant = new();
            SplitInfo bestSplitStatic = new();
            SplitInfo bestSplitNonStatic = new();
            SplitInfo bestSplitNoWeightedPrediction = new();

            if (begin > end)
            {
                throw new InvalidOperationException("Begin should be <= end");
            }

            if (end > treeSamples.NumberOfDistinctSamples)
            {
                throw new InvalidOperationException("End index out of range");
            }

            JxlPredictor currentPredictor = tree[pos].Predictor;
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

            counts[..(maxSymbols * numPred)].Clear();

            for (int pred = 0; pred < numPred; pred++)
            {
                long extraBits = 0;
                ReadOnlySpan<JxlResidualToken> rtokens = treeSamples.GetResidualTokensForPrediction(pred);

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
                int pred = treeSamples.FindPredictorIndex(currentPredictor);
                baseBits = EstimateBits(counts[(pred * maxSymbols)..], maxSymbols) + totalExtraBits[pred];
            }

            ref SplitInfo best = ref bestSplitNonStatic;

            SplitInfo forcedSplit = new();

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
                    forcedSplit.LeftPredictor = forcedSplit.RightPredictor = currentPredictor;
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
                float changePredictionPenalty = 800.0f / (100.0f + threshold);

                for (int prop = 0; prop < numProp && baseBits > threshold; prop++)
                {
                    int propertySize = treeSamples.CountPropertyValues(prop);

                    propertyValueUsedCount[..propertySize].Clear();

                    int firstUsed = propertySize;
                    int lastUsed = 0;

                    if (prop < treeSamples.NumberOfStaticProperties)
                    {
                        for (int i = begin; i < end; i++)
                        {
                            int p = treeSamples.GetProperty(true, prop, i);
                            propertyValueUsedCount[p]++;
                            lastUsed = Math.Max(lastUsed, p);
                            firstUsed = Math.Min(firstUsed, p);
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
                            firstUsed = Math.Min(firstUsed, p);
                        }
                    }

                    // CostInfo's constructor supplies maximum candidate costs. Default/cleared structs would incorrectly start at zero.
                    CostInfo initialCost = new();
                    leftCosts[..(lastUsed - firstUsed)].Fill(initialCost);
                    rightCosts[..(lastUsed - firstUsed)].Fill(initialCost);

                    for (int pred = 0; pred < numPred; pred++)
                    {
                        ReadOnlySpan<JxlResidualToken> rtokens = treeSamples.GetResidualTokensForPrediction(pred);

                        if (prop < treeSamples.NumberOfStaticProperties)
                        {
                            CollectExtraBitsIncrease(true, in treeSamples, rtokens, countsIncrease, extraBitsIncrease, begin, end, prop, maxSymbols);
                        }
                        else
                        {
                            CollectExtraBitsIncrease(
                                false,
                                in treeSamples,
                                rtokens,
                                countsIncrease,
                                extraBitsIncrease,
                                begin,
                                end,
                                prop - treeSamples.NumberOfStaticProperties,
                                maxSymbols);

                        }

                        counts.Slice(pred * maxSymbols, maxSymbols).CopyTo(aboveCounts);
                        belowCounts[..maxSymbols].Clear();

                        long extraBitsBelow = 0;

                        for (int i = firstUsed; i < lastUsed; i++)
                        {
                            if (propertyValueUsedCount[i] == 0)
                            {
                                continue;
                            }

                            extraBitsBelow += extraBitsIncrease[i];
                            extraBitsIncrease[i] = 0;

                            TransferHistogramCounts(aboveCounts[..maxSymbols], belowCounts[..maxSymbols], countsIncrease.Slice(i * maxSymbols, maxSymbols));

                            float rightCost = EstimateBits(aboveCounts, maxSymbols) + totalExtraBits[pred] - extraBitsBelow;
                            float leftCost = EstimateBits(belowCounts, maxSymbols) + extraBitsBelow;

                            if (extraBitsBelow > totalExtraBits[pred])
                            {
                                throw new InvalidOperationException("Too many extra bits");
                            }

                            float penalty = 0;

                            if (treeSamples.PredictorFromIndex(pred) != currentPredictor &&
                                currentPredictor != JxlPredictor.Weighted)
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

                        // No split moves the highest property bucket below the boundary. Its increments still belong only to this predictor.
                        extraBitsIncrease[lastUsed] = 0;
                        countsIncrease.Slice(lastUsed * maxSymbols, maxSymbols).Clear();
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

                        // Update the selected candidate in its owning slot. Conditional references require no pointer or unsafe scope.
                        ref SplitInfo referenceToBest = ref (treeSamples.PropertyFromIndex(prop) < JxlPredictorFacts.StaticProperties
                            ? ref (zeroEntropySide ? ref bestSplitStaticConstant : ref bestSplitStatic)
                            : ref (usesWeightedPrediction ? ref bestSplitNonStatic : ref bestSplitNoWeightedPrediction));

                        if (leftCost + rightCost < referenceToBest.Cost)
                        {
                            referenceToBest.Property = prop;
                            referenceToBest.Value = i;
                            referenceToBest.Position = split;
                            referenceToBest.LeftCost = leftCost;
                            referenceToBest.LeftPredictor = leftCosts[i - firstUsed].Predictor;
                            referenceToBest.RightCost = rightCost;
                            referenceToBest.RightPredictor = rightCosts[i - firstUsed].Predictor;
                        }
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

            }

            if (best.Cost + threshold < baseBits)
            {
                int p = treeSamples.PropertyFromIndex(best.Property);
                int dequant = treeSamples.UnquantizeProperty(best.Property, best.Value);

                MakeSplitNode(pos, p, dequant, best.LeftPredictor, 0, best.RightPredictor, 0, tree);

                if (best.Property < treeSamples.NumberOfStaticProperties)
                {
                    SplitTreeSamples(true, ref treeSamples, begin, best.Position, end, best.Property, best.Value);
                }
                else
                {
                    SplitTreeSamples(false, ref treeSamples, begin, best.Position, end, best.Property - treeSamples.NumberOfStaticProperties, best.Value);
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

        }
    }

    /// <summary>
    /// Initializes the root leaf and searches its prepared training samples for useful splits.
    /// </summary>
    /// <param name="configuration">The configuration that owns tree-search storage.</param>
    /// <param name="treeSamples">The sample owner, partitioned in place as splits are accepted.</param>
    /// <param name="threshold">The minimum bit saving required to split a node.</param>
    /// <param name="mulInfo">The forced multiplier ranges.</param>
    /// <param name="staticPropertyRange">The channel/group ranges of the complete sample set.</param>
    /// <param name="fastDecodeMultiplier">The allowed cost ratio for simpler predictors.</param>
    /// <param name="tree">The destination nodes.</param>
    public static void ComputeBestTree(
        Configuration configuration,
        scoped ref JxlTreeSamples treeSamples,
        float threshold,
        List<JxlModularMultiplierInfo> mulInfo,
        StaticPropertyRange staticPropertyRange,
        float fastDecodeMultiplier,
        Tree tree)
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
            throw new InvalidOperationException($"Too many properties: {treeSamples.NumberOfProperties}");
        }

        FindBestSplit(configuration, ref treeSamples, threshold, mulInfo, staticPropertyRange, fastDecodeMultiplier, tree);
    }

    /// <summary>
    /// Selects distinct cumulative-count thresholds for approximately equal-frequency property buckets.
    /// </summary>
    /// <param name="histogram">The nonnegative training counts.</param>
    /// <param name="numChunks">The requested number of buckets.</param>
    /// <param name="thresholds">Caller-owned storage for at most numChunks minus one thresholds.</param>
    /// <returns>The number of written thresholds. Empty or all-zero histograms produce none.</returns>
    public static int QuantizeHistogram(ReadOnlySpan<int> histogram, int numChunks, Span<int> thresholds)
    {
        if (histogram.Length == 0 || numChunks == 0)
        {
            return 0;
        }

        int sum = TensorPrimitives.Sum(histogram);
        if (sum == 0)
        {
            return 0;
        }

        long cumulativeSum = 0;
        long threshold = 1;
        int written = 0;

        // Integer cross-products retain exact bucket boundaries without rounding cumulative probabilities to float.
        // One occupied histogram entry can cross several quantiles; retain that entry only once.
        for (int i = 0; i < histogram.Length; i++)
        {
            cumulativeSum += histogram[i];

            if (cumulativeSum * numChunks >= threshold * sum)
            {
                // The final endpoint collects the complete histogram; native removes it after construction.
                // Excluding it here keeps the caller's required capacity at numChunks-1, without a temporary slot or copy.
                if (cumulativeSum != sum)
                {
                    thresholds[written++] = i;
                }

                while (cumulativeSum * numChunks >= threshold * sum)
                {
                    threshold++;
                }
            }
        }

        return written;
    }

    /// <summary>
    /// Builds property thresholds from a bounded histogram of the borrowed training samples.
    /// </summary>
    /// <param name="samples">The signed training values. Their storage remains unchanged.</param>
    /// <param name="numChunks">The requested number of property buckets.</param>
    /// <param name="thresholds">Caller-owned storage for at most numChunks minus one thresholds.</param>
    /// <returns>The number of written thresholds.</returns>
    public static int QuantizeSamples(ReadOnlySpan<int> samples, int numChunks, Span<int> thresholds)
    {
        const int range = 512;

        if (samples.Length == 0)
        {
            return 0;
        }

        int minimum = Math.Clamp(TensorPrimitives.Min(samples), -range, range);
        Span<int> counts = stackalloc int[(2 * range) + 1];
        counts.Clear();

        // Native bounds only this training histogram to [-512,512]. It does not clip the source pixels or stored residuals.
        // Shift by the bounded minimum so sparse signed values use the same cumulative-quantile rule as static properties.
        foreach (int sample in samples)
        {
            counts[Math.Clamp(sample, -range, range) - minimum]++;
        }

        int written = QuantizeHistogram(counts, numChunks, thresholds);
        TensorPrimitives.Add(thresholds[..written], minimum, thresholds[..written]);
        return written;
    }

    /// <summary>
    /// Maps bounded property values to the bucket selected by strictly-greater tree decisions.
    /// </summary>
    /// <param name="from">The ordered split thresholds.</param>
    /// <param name="to">Caller-owned mapping storage.</param>
    /// <param name="numPegs">The number of property values to map.</param>
    /// <param name="bias">The value subtracted from each mapping index.</param>
    public static void QuantMap(ReadOnlySpan<int> from, Span<int> to, int numPegs, int bias)
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

    /// <summary>
    /// Counts exact geometric training selections and prepares group/channel occurrence histograms without reading pixels.
    /// </summary>
    /// <param name="image">The borrowed modular channels.</param>
    /// <param name="options">The resolved sampling and maximum-channel settings.</param>
    /// <param name="groupId">The group index and deterministic RNG seed.</param>
    /// <param name="groupPixelCount">The initialized group histogram, updated once during preparation.</param>
    /// <param name="channelPixelCount">The initialized channel histogram, shared across groups.</param>
    /// <returns>The required capacity of each raw pixel/difference sample plane.</returns>
    public static int CountPixelSamples(
        JxlModularImage image,
        JxlModularOptions options,
        int groupId,
        Span<int> groupPixelCount,
        Span<int> channelPixelCount)
    {
        // Native CollectPixelSamples treats zero repeats as no training samples and leaves both histograms unchanged.
        if (options.NumberOfRepeats == 0)
        {
            return 0;
        }

        long totalPixels = 0;
        for (int c = 0; c < image.Channels.Count; c++)
        {
            JxlModularChannel channel = image.Channels[c];
            if (c >= image.MetaChannels && (channel.Width > options.MaxChannelSize || channel.Height > options.MaxChannelSize))
            {
                break;
            }

            // Difference samples need a horizontal neighbor. These are the same eligible channels visited during collection.
            if (channel.Width <= 1 || channel.Height == 0)
            {
                continue;
            }

            // These are working-size estimates at the reservation boundary, not arithmetic in the sample loop.
            // The plane's checked allocation already bounds one channel's area. Combining channels/groups still needs checked accounting.
            int area = channel.Width * channel.Height;
            groupPixelCount[groupId] = checked(groupPixelCount[groupId] + area);
            channelPixelCount[c] = checked(channelPixelCount[c] + area);
            totalPixels += area;
        }

        Rng rng = new((ulong)groupId);
        float fraction = MathF.Min(options.NumberOfRepeats * 0.1F, 0.99F);
        // Make prepares 1/log(1-p); the distribution's constructor accepts that prepared value, not p itself.
        Rng.GeometricDistribution distribution = Rng.GeometricDistribution.Make(fraction);
        long position = rng.Geometric(in distribution);
        long count = 0;

        // Flatten only the logical samples. Geometric gaps cross rows and eligible channel boundaries without touching image storage.
        // Collection starts from the same seed and repeats these gaps, so its two output planes need no growth or copies.
        while (position < totalPixels)
        {
            count++;
            position += rng.Geometric(in distribution) + 1L;
        }

        return checked((int)count);
    }

    /// <summary>
    /// Collects raw pixel and neighbor-difference training samples into the exact reserved output planes.
    /// </summary>
    /// <param name="image">The borrowed modular channels.</param>
    /// <param name="options">The resolved settings used during sample counting.</param>
    /// <param name="groupId">The group index and deterministic RNG seed.</param>
    /// <param name="pixelSamples">Caller-owned storage sized by CountPixelSamples.</param>
    /// <param name="diffSamples">Caller-owned storage with the same capacity as pixelSamples.</param>
    /// <returns>The number of written samples in each plane.</returns>
    public static int CollectPixelSamples(
        JxlModularImage image,
        JxlModularOptions options,
        int groupId,
        Span<int> pixelSamples,
        Span<int> diffSamples)
    {
        if (options.NumberOfRepeats == 0)
        {
            return 0;
        }

        Rng rng = new((ulong)groupId);
        float fraction = MathF.Min(options.NumberOfRepeats * 0.1F, 0.99F);
        Rng.GeometricDistribution distribution = Rng.GeometricDistribution.Make(fraction);
        long position = rng.Geometric(in distribution);
        int written = 0;

        for (int c = 0; c < image.Channels.Count; c++)
        {
            JxlModularChannel channel = image.Channels[c];
            if (c >= image.MetaChannels && (channel.Width > options.MaxChannelSize || channel.Height > options.MaxChannelSize))
            {
                break;
            }

            if (channel.Width <= 1 || channel.Height == 0)
            {
                continue;
            }

            long area = (long)channel.Width * channel.Height;
            if (position < area)
            {
                // Borrow this selected channel once. Row padding does not affect logical positions or RNG advancement.
                Span<int> samples = channel.GetRow(0);
                int stride = channel.Plane.PixelsPerRow;
                int row = 0;
                long rowStart = 0;

                while (position < area)
                {
                    // Nearby samples stay in this row. Divide only when a geometric gap crosses a row boundary.
                    if (position >= rowStart + channel.Width)
                    {
                        row = (int)(position / channel.Width);
                        rowStart = (long)row * channel.Width;
                    }

                    int x = (int)(position - rowStart);
                    int offset = (row * stride) + x;
                    int previous = x == 0 ? offset + 1 : offset - 1;
                    pixelSamples[written] = samples[offset];
                    diffSamples[written] = samples[offset] - samples[previous];
                    written++;
                    position += rng.Geometric(in distribution) + 1L;
                }
            }

            position -= area;
        }

        return written;
    }

    /// <summary>
    /// Moves one property's symbol counts from the above-threshold histogram to the below-threshold histogram.
    /// </summary>
    /// <param name="above">The remaining parent counts.</param>
    /// <param name="below">The accumulated lower-partition counts, with the same length as above.</param>
    /// <param name="increase">The disjoint property bucket, zeroed after its counts move.</param>
    private static void TransferHistogramCounts(Span<int> above, Span<int> below, Span<int> increase)
    {
        ref int upper = ref MemoryMarshal.GetReference(above);
        ref int lower = ref MemoryMarshal.GetReference(below);
        ref int bucket = ref MemoryMarshal.GetReference(increase);
        int i = 0;

        // Each lane represents one symbol. Load its three counts before storing either partition or clearing the consumed bucket.
        // Fusing the three updates avoids separate add, subtract and clear passes over every candidate bucket.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (above.Length - i).Vector512Count<int>();

            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                Vector512<int> value = Vector512.LoadUnsafe(ref bucket, (uint)i);
                Vector512<int> from = Vector512.LoadUnsafe(ref upper, (uint)i);
                Vector512<int> to = Vector512.LoadUnsafe(ref lower, (uint)i);
                (from - value).StoreUnsafe(ref upper, (uint)i);
                (to + value).StoreUnsafe(ref lower, (uint)i);
                Vector512<int>.Zero.StoreUnsafe(ref bucket, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (above.Length - i).Vector256Count<int>();

            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                Vector256<int> value = Vector256.LoadUnsafe(ref bucket, (uint)i);
                Vector256<int> from = Vector256.LoadUnsafe(ref upper, (uint)i);
                Vector256<int> to = Vector256.LoadUnsafe(ref lower, (uint)i);
                (from - value).StoreUnsafe(ref upper, (uint)i);
                (to + value).StoreUnsafe(ref lower, (uint)i);
                Vector256<int>.Zero.StoreUnsafe(ref bucket, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (above.Length - i).Vector128Count<int>();

            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                Vector128<int> value = Vector128.LoadUnsafe(ref bucket, (uint)i);
                Vector128<int> from = Vector128.LoadUnsafe(ref upper, (uint)i);
                Vector128<int> to = Vector128.LoadUnsafe(ref lower, (uint)i);
                (from - value).StoreUnsafe(ref upper, (uint)i);
                (to + value).StoreUnsafe(ref lower, (uint)i);
                Vector128<int>.Zero.StoreUnsafe(ref bucket, (uint)i);
            }
        }

        for (; i < above.Length; i++)
        {
            int value = Unsafe.Add(ref bucket, i);
            Unsafe.Add(ref upper, i) -= value;
            Unsafe.Add(ref lower, i) += value;
            Unsafe.Add(ref bucket, i) = 0;
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

    /// <summary>
    /// Stores one pending node's sample interval and static-property bounds.
    /// </summary>
    private readonly struct NodeInfo
    {
        /// <summary>Initializes a new instance of the <see cref="NodeInfo"/> struct.</summary>
        /// <param name="pos">The destination node index.</param>
        /// <param name="begin">The first retained sample in the node.</param>
        /// <param name="end">The first retained sample after the node.</param>
        /// <param name="staticPropertyRange">The node's half-open channel and group ranges.</param>
        public NodeInfo(int pos, int begin, int end, StaticPropertyRange staticPropertyRange)
        {
            this.Pos = pos;
            this.Begin = begin;
            this.End = end;
            this.StaticPropertyRange = staticPropertyRange;
        }

        /// <summary>Gets the destination node index.</summary>
        public int Pos { get; }

        /// <summary>Gets the first retained sample in the node.</summary>
        public int Begin { get; }

        /// <summary>Gets the first retained sample after the node.</summary>
        public int End { get; }

        /// <summary>Gets the half-open channel and group ranges.</summary>
        public StaticPropertyRange StaticPropertyRange { get; }
    }

    /// <summary>
    /// Stores one candidate split and the estimated costs of its lower and upper partitions.
    /// </summary>
    private struct SplitInfo
    {
        /// <summary>Initializes a new instance of the <see cref="SplitInfo"/> struct with no selected split.</summary>
        public SplitInfo()
        {
            this = default;
            this.LeftCost = float.MaxValue;
            this.RightCost = float.MaxValue;
        }

        /// <summary>Gets or sets the selected property's index.</summary>
        public int Property { get; set; }

        /// <summary>Gets or sets the quantized split value.</summary>
        public int Value { get; set; }

        /// <summary>Gets or sets the first retained sample above the split.</summary>
        public int Position { get; set; }

        /// <summary>Gets or sets the estimated bit cost at or below the split value.</summary>
        public float LeftCost { get; set; }

        /// <summary>Gets or sets the estimated bit cost above the split value.</summary>
        public float RightCost { get; set; }

        /// <summary>Gets or sets the predictor at or below the split value.</summary>
        public JxlPredictor LeftPredictor { get; set; }

        /// <summary>Gets or sets the predictor above the split value.</summary>
        public JxlPredictor RightPredictor { get; set; }

        /// <summary>Gets the combined estimated bit cost.</summary>
        public readonly float Cost => this.LeftCost + this.RightCost;
    }

    /// <summary>
    /// Stores the best predictor cost for one side of a possible split.
    /// </summary>
    private struct CostInfo
    {
        /// <summary>Initializes a new instance of the <see cref="CostInfo"/> struct with no selected predictor.</summary>
        public CostInfo()
        {
            this = default;
            this.Cost = float.MaxValue;
        }

        /// <summary>Gets or sets the estimated bit cost.</summary>
        public float Cost { get; set; }

        /// <summary>Gets or sets the predictor preference penalty.</summary>
        public float ExtraCost { get; set; }

        /// <summary>Gets or sets the selected predictor.</summary>
        public JxlPredictor Predictor { get; set; }

        /// <summary>Gets the estimated bit cost plus the predictor preference penalty.</summary>
        public readonly float TotalCost => this.Cost + this.ExtraCost;
    }
}

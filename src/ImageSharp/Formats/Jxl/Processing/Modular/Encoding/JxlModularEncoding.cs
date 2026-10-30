// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using FlatTree = System.Collections.Generic.List<SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction.JxlFlatDecisionNode>;
using Tree = System.Collections.Generic.List<SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.JxlPropertyDecisionNode>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

internal static class JxlModularEncoding
{
    public static bool TreeToLookupTable<T>(FlatTree tree, JxlTreeLut<T> lut)
    {
        bool hasOffsets = lut.Offsets.Length > 0;
        bool hasMultipliers = lut.Multipliers.Length > 0;

        Stack<TreeRange> ranges = [];
        ranges.Push(new(-JxlMaConstants.PropertyRangeFast - 1, JxlMaConstants.PropertyRangeFast - 1, 0));
        while (ranges.Count > 0)
        {
            TreeRange cur = ranges.Peek();
            _ = ranges.Pop();

            if (cur.Begin < -JxlMaConstants.PropertyRangeFast - 1 || cur.begin >= JxlMaConstants.PropertyRangeFast - 1 || cur.end > JxlMaConstants.PropertyRangeFast - 1)
            {
                // Tree is outside the allowed range, exit.
                return false;
            }

            JxlFlatDecisionNode node = tree[cur.Pos];

            // Leaf.
            if (node.Property0 == -1)
            {
                if (node.PredictorOffset is < sbyte.MinValue or > sbyte.MaxValue)
                {
                    return false;
                }

                if (node.Multiplier is < sbyte.MinValue or > sbyte.MaxValue)
                {
                    return false;
                }

                if (!hasMultipliers && node.Multiplier != 1)
                {
                    return false;
                }

                if (!hasOffsets && node.PredictorOffset != 0)
                {
                    return false;
                }

                for (int i = cur.Begin + 1; i < cur.End + 1; i++)
                {
                    lut.ContextLookup[i + JxlMaConstants.PropertyRangeFast] = node.ChildID;

                    if (hasMultipliers)
                    {
                        lut.Multipliers[i + JxlMaConstants.PropertyRangeFast] = node.Multiplier;
                    }

                    if (hasOffsets)
                    {
                        lut.Offsets[i + JxlMaConstants.PropertyRangeFast] = node.PredictorOffset;
                    }
                }

                continue;
            }

            if (node.Properties[0] >= JxlPredictorFacts.StaticProperties)
            {
                ranges.Push(new(node.SplitValues[0], cur.End, node.ChildID));
                ranges.Push(new(node.SplitValue0, node.SplitValues[0], node.ChildID + 1));
            }
            else
            {
                ranges.Push(new(node.SplitValue0, cur.End, node.ChildID));
            }

            // <= side
            if (node.Properties[1] >= JxlPredictorFacts.StaticProperties)
            {
                ranges.Push(
                    new(node.SplitValues[1], node.SplitValue0, node.ChildID + 2));
                ranges.Push(
                    new(cur.Begin, node.SplitValues[1], node.ChildID + 3));
            }
            else
            {
                ranges.Push(new(cur.Begin, node.SplitValue0, node.ChildID + 2));
            }
        }

        return true;
    }

    public static FlatTree FilterTree(Tree globalTree, Span<int> staticProps, ref int numProps, ref bool useWp, ref bool wpOnly, ref bool gradientOnly)
    {
        numProps = 0;
        bool hasWeightedPrediction = false;
        bool hasNonWeighted = false;
        gradientOnly = true;

        void MarkProperty(int p, ref bool gradientOnly)
        {
            if (p == WpProp)
            {
                hasWeightedPrediction = true;
            }
            else if (p >= JxlPredictorFacts.StaticProperties)
            {
                hasNonWeighted = true;
            }

            if (p >= JxlPredictorFacts.StaticProperties && p != GradientProp)
            {
                gradientOnly = false;
            }
        }

        FlatTree output = [];
        Queue<int> nodes = [];
        nodes.Enqueue(0);

        while (nodes.Count > 0)
        {
            int cur = nodes.Peek();
            _ = nodes.Dequeue();

            while (globalTree[cur].Property is < JxlPredictorFacts.StaticProperties and not -1)
            {
                if (staticProps[globalTree[cur].Property] > globalTree[cur].SplitValue)
                {
                    cur = globalTree[cur].LeftChild;
                }
                else
                {
                    cur = globalTree[cur].RightChild;
                }
            }

            JxlFlatDecisionNode flat = default;

            if (globalTree[cur].Property == -1)
            {
                flat.Property0 = -1;
                flat.ChildID = (uint)globalTree[cur].LeftChild;
                flat.Predictor = globalTree[cur].Predictor;
                flat.PredictorOffset = (int)globalTree[cur].PredictorOffset;
                flat.Multiplier = globalTree[cur].Multiplier;

                gradientOnly &= flat.Predictor == JxlPredictor.Gradient;

                hasWeightedPrediction |= flat.Predictor == JxlPredictor.Weighted;
                hasNonWeighted |= flat.Predictor != JxlPredictor.Weighted;
                output.Add(flat);
                continue;
            }

            flat.ChildID = (uint)(output.Count + nodes.Count + 1);

            flat.Property0 = globalTree[cur].Property;
            numProps = Math.Max(flat.Property0 + 1, numProps);
            flat.SplitValue0 = globalTree[cur].SplitValue;

            for (int i = 0; i < 2; i++)
            {
                int currentChild = i == 0 ? globalTree[cur].LeftChild : globalTree[cur].RightChild;

                while (globalTree[currentChild].Property < JxlPredictorFacts.StaticProperties && globalTree[currentChild].Property != -1)
                {
                    if (staticProps[globalTree[currentChild].Property] >
                        globalTree[currentChild].SplitValue)
                    {
                        currentChild = globalTree[currentChild].LeftChild;
                    }
                    else
                    {
                        currentChild = globalTree[currentChild].RightChild;
                    }
                }

                if (globalTree[currentChild].Property == -1)
                {
                    flat.Properties[i] = 0;
                    flat.SplitValues[i] = 0;
                    nodes.Enqueue(currentChild);
                    nodes.Enqueue(currentChild);
                }
                else
                {
                    flat.Properties[i] = globalTree[currentChild].Property;
                    flat.SplitValues[i] = globalTree[currentChild].SplitValue;
                    nodes.Enqueue(globalTree[currentChild].LeftChild);
                    nodes.Enqueue(globalTree[currentChild].RightChild);

                    numProps = Math.Max(flat.Properties[i] + 1, numProps);
                }
            }

            for (int i = 0; i < 2; i++)
            {
                short property = flat.Properties[i];
                MarkProperty(property, ref gradientOnly);
            }

            MarkProperty(flat.Property0, ref gradientOnly);

            output.Add(flat);
        }

        if (numProps > JxlMaConstants.NumTreeContexts)
        {
            numProps = (JxlMath.DivCeil(numProps - JxlMaConstants.NumTreeContexts, JxlContextPrediction.ExtraPropertiesPerChannel) * JxlContextPrediction.ExtraPropertiesPerChannel) + JxlMaConstants.NumTreeContexts;
        }
        else
        {
            numProps = JxlMaConstants.NumTreeContexts;
        }

        useWp = hasWeightedPrediction;
        wpOnly = hasWeightedPrediction && !hasNonWeighted;

        return output;
    }

    /// <summary>
    /// Decodes one modular channel using the prediction tree and an initialized entropy stream.
    /// </summary>
    /// <param name="configuration">The configuration that owns prediction storage.</param>
    /// <param name="usesLz77">Whether this entropy stream enables LZ77 references.</param>
    /// <param name="bitReader">The encoded channel data.</param>
    /// <param name="reader">The caller-owned entropy reader.</param>
    /// <param name="contextMap">The stable map used to resolve prediction-tree contexts.</param>
    /// <param name="window">The reader's LZ77 storage, borrowed before the channel loop.</param>
    /// <param name="globalTree">The shared prediction tree.</param>
    /// <param name="wpHeader">The weighted-prediction settings.</param>
    /// <param name="channelIdx">The channel to decode.</param>
    /// <param name="groupId">The group used for static prediction properties.</param>
    /// <param name="treeLookup">The reusable prediction lookup storage.</param>
    /// <param name="image">The image that owns the channel.</param>
    /// <param name="flRun">The remaining prefix-coded repetition count.</param>
    /// <param name="flV">The repeated prefix-coded value.</param>
    /// <param name="weightedState">The operation-owned scratch, prepared on its first weighted channel and reused by later channels.</param>
    /// <param name="maximumWidth">The maximum channel width prepared by the enclosing modular operation.</param>
    public static void DecodeModularChannelMAANS(
        Configuration configuration,
        bool usesLz77,
        JxlBitReader bitReader,
        JxlAnsSymbolReader reader,
        ReadOnlySpan<byte> contextMap,
        Span<int> window,
        Tree globalTree,
        JxlModularHeader wpHeader,
        int channelIdx,
        int groupId,
        JxlTreeLut<byte> treeLookup,
        JxlModularImage image,
        ref uint flRun,
        ref uint flV,
        ref JxlModularState? weightedState,
        int maximumWidth)
    {
        JxlModularChannel channel = image.Channels[channelIdx];

        InlineArray2<int> staticProps = default;
        staticProps[0] = channelIdx;
        staticProps[1] = groupId;

        // Keep the physical stride after metadata shrink. Borrow the allocation once; row and neighbor views use this span.
        Span<int> channelSamples = channel.GetRow(0);
        int onerow = channel.Plane.PixelsPerRow;

        bool treeHasWeightedPredictionPropOrPred = false;
        bool isWeightedPredictionOnly = false;
        bool isGradientOnly = false;
        int numProps = 0;

        FlatTree tree = FilterTree(globalTree, staticProps, ref numProps, ref treeHasWeightedPredictionPropOrPred, ref isWeightedPredictionOnly, ref isGradientOnly);
        Span<JxlFlatDecisionNode> span = CollectionsMarshal.AsSpan(tree);

        for (int i = 0; i < span.Length; i++)
        {
            ref JxlFlatDecisionNode node = ref span[i];

            if (node.Property0 == -1)
            {
                node.ChildID = contextMap[(int)node.ChildID];
            }
        }

        int MakePixel(uint v, int multiplier, int offset)
        {
            int value = JxlPackSigned.UnpackSigned(v);
            return (value * multiplier) + offset;
        }

        bool GlobalTreeIsAllGradientNoOp()
        {
            foreach (JxlPropertyDecisionNode n in globalTree)
            {
                if (n.Property == -1)
                {
                    if (n.Predictor != JxlPredictor.Gradient || n.PredictorOffset != 0 || n.Multiplier != 1)
                    {
                        return false;
                    }
                }
                else if (n.Property >= JxlPredictorFacts.StaticProperties)
                {
                    return false;
                }
            }

            return true;
        }

        if (tree.Count == 1)
        {
            JxlPredictor predictor = tree[0].Predictor;
            int offset = tree[0].PredictorOffset;
            int multiplier = tree[0].Multiplier;
            uint ctx_id = tree[0].ChildID;

            if (predictor == JxlPredictor.Zero)
            {
                if (reader.IsSingleValueAndAdvance((int)ctx_id, channel.Width * channel.Height, out uint value, window))
                {
                    int v = MakePixel(value, multiplier, offset);

                    for (int y = 0; y < channel.Height; y++)
                    {
                        Span<int> r = channelSamples[(y * onerow)..];
                        r[..channel.Width].Fill(v);
                    }
                }
                else
                {
                    if (multiplier == 1 && offset == 0)
                    {
                        for (int y = 0; y < channel.Height; y++)
                        {
                            Span<int> r = channelSamples[(y * onerow)..];

                            for (int x = 0; x < channel.Width; x++)
                            {
                                uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, (int)ctx_id, bitReader, window);

                                r[x] = JxlPackSigned.UnpackSigned(v);
                            }
                        }
                    }
                    else
                    {
                        for (int y = 0; y < channel.Height; y++)
                        {
                            Span<int> r = channelSamples[(y * onerow)..];

                            for (int x = 0; x < channel.Width; x++)
                            {
                                uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, (int)ctx_id, bitReader, window);

                                r[x] = MakePixel(v, multiplier, offset);
                            }
                        }
                    }
                }

                return;
            }
            else if (usesLz77 && reader.IsHuffRleOnly && GlobalTreeIsAllGradientNoOp())
            {
                int sv = JxlPackSigned.UnpackSigned(flV);

                for (int y = 0; y < channel.Height; y++)
                {
                    Span<int> r = channelSamples[(y * onerow)..];
                    Span<int> rtop = y > 0 ? channelSamples[((y - 1) * onerow)..] : r;
                    int guess0 = y > 0 ? rtop[0] : 0;

                    if (flRun == 0)
                    {
                        reader.ReadHybridUIntClusteredHuffRleOnly((int)ctx_id, bitReader, ref flV, ref flRun);
                        sv = JxlPackSigned.UnpackSigned(flV);
                    }
                    else
                    {
                        flRun--;
                    }

                    r[0] = sv + guess0;

                    for (int x = 1; x < channel.Width; x++)
                    {
                        int left = r[x - 1];
                        int top = y > 0 ? rtop[x] : left;
                        int topleft = y > 0 ? rtop[x - 1] : left;
                        int guess = JxlContextPrediction.ClampedGradient(top, left, topleft);

                        if (flRun == 0)
                        {
                            reader.ReadHybridUIntClusteredHuffRleOnly((int)ctx_id, bitReader, ref flV, ref flRun);
                            sv = JxlPackSigned.UnpackSigned(flV);
                        }
                        else
                        {
                            flRun--;
                        }

                        r[x] = sv + guess;
                    }
                }

                return;
            }
            else if (predictor == JxlPredictor.Gradient && offset == 0 && multiplier == 1)
            {
                for (int y = 0; y < channel.Height; y++)
                {
                    Span<int> r = channelSamples[(y * onerow)..];

                    for (int x = 0; x < channel.Width; x++)
                    {
                        // Neighbors
                        int left = x > 0 ? r[x - 1] : y > 0 ? channelSamples[((y - 1) * onerow) + x] : 0;
                        int top = y > 0 ? channelSamples[((y - 1) * onerow) + x] : left;
                        int topleft = x > 0 && y > 0 ? channelSamples[((y - 1) * onerow) + x - 1] : left;

                        int guess = JxlContextPrediction.ClampedGradient(top, left, topleft);
                        uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, (int)ctx_id, bitReader, window);

                        r[x] = MakePixel(v, 1, guess);
                    }
                }

                return;
            }
        }

        if (isWeightedPredictionOnly)
        {
            isWeightedPredictionOnly = TreeToLookupTable(tree, treeLookup);
        }

        if (isGradientOnly)
        {
            isGradientOnly = TreeToLookupTable(tree, treeLookup);
        }

        if (isGradientOnly)
        {
            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> r = channelSamples[(y * onerow)..];

                for (int x = 0; x < channel.Width; x++)
                {
                    // Neighbors
                    int left = x > 0 ? r[x - 1] : y > 0 ? channelSamples[((y - 1) * onerow) + x] : 0;
                    int top = y > 0 ? channelSamples[((y - 1) * onerow) + x] : left;
                    int topleft = x > 0 && y > 0 ? channelSamples[((y - 1) * onerow) + x - 1] : left;

                    int guess = JxlContextPrediction.ClampedGradient(top, left, topleft);
                    int pos =
                        JxlMaConstants.PropertyRangeFast +
                        Math.Min(
                            Math.Max(-JxlMaConstants.PropertyRangeFast, top + left - topleft),
                            JxlMaConstants.PropertyRangeFast - 1);

                    uint ctx_id = treeLookup.ContextLookup[pos];
                    uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, (int)ctx_id, bitReader, window);
                    r[x] = MakePixel(v, 1, guess);
                }
            }
        }
        else if (!usesLz77 && isWeightedPredictionOnly && channel.Width > 8)
        {
            // Allocate only when this operation first selects a weighted channel. Later channels reset the same fixed-capacity owner.
            weightedState ??= new(configuration, wpHeader, maximumWidth, 1);
            JxlModularState wpState = weightedState;
            Span<uint> weightedStorage = wpState.Reset(wpHeader, channel.Width);
            Span<int> properties = [0];

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> r = channelSamples[(y * onerow)..];
                Span<int> rtop = y > 0 ? channelSamples[((y - 1) * onerow)..] : r;
                Span<int> rtoptop = y > 1 ? channelSamples[((y - 2) * onerow)..] : rtop;

                // Interior columns have a valid west sample. Select unavailable first-row neighbors as west once per row.
                // For row one, the unavailable two-up sample is north. These offsets are only loaded for 1 <= x < width-1.
                int northOffset = y > 0 ? -onerow : -1;
                int northWestOffset = y > 0 ? -onerow - 1 : -1;
                int northEastOffset = y > 0 ? -onerow + 1 : -1;
                int twoUpOffset = y > 1 ? -2 * onerow : northOffset;

                int x = 0;
                {
                    int offset = 0;
                    int left = y > 0 ? rtop[x] : 0;
                    int toptop = y > 0 ? rtoptop[x] : 0;
                    int topright = x + 1 < channel.Width && y > 0 ? rtop[x + 1] : left;

                    int guess = (int)wpState.Predict(true, x, y, weightedStorage, 0, left, left, topright, left, toptop, properties, offset);
                    int pos = JxlMaConstants.PropertyRangeFast + Math.Clamp(
                        properties[0],
                        -JxlMaConstants.PropertyRangeFast,
                        JxlMaConstants.PropertyRangeFast - 1);

                    uint ctx_id = treeLookup.ContextLookup[pos];
                    uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, (int)ctx_id, bitReader, window);

                    r[x] = MakePixel(v, 1, guess);
                    wpState.UpdatePredictionErrors(r[x], x, y, weightedStorage, 0);
                }

                for (x = 1; x + 1 < channel.Width; x++)
                {
                    int offset = 0;
                    ref int current = ref r[x];
                    int guess = (int)wpState.Predict(
                        true,
                        x,
                        y,
                        weightedStorage,
                        0,
                        Unsafe.Add(ref current, northOffset),
                        r[x - 1],
                        Unsafe.Add(ref current, northEastOffset),
                        Unsafe.Add(ref current, northWestOffset),
                        Unsafe.Add(ref current, twoUpOffset),
                        properties,
                        offset);

                    int pos = JxlMaConstants.PropertyRangeFast + Math.Clamp(
                        properties[0],
                        -JxlMaConstants.PropertyRangeFast,
                        JxlMaConstants.PropertyRangeFast - 1);

                    int ctx_id = treeLookup.ContextLookup[pos];
                    uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, (int)ctx_id, bitReader, window);

                    r[x] = MakePixel(v, 1, guess);
                    wpState.UpdatePredictionErrors(r[x], x, y, weightedStorage, 0);
                }

                {
                    int offset = 0;
                    int guess = (int)wpState.Predict(
                        true,
                        x,
                        y,
                        weightedStorage,
                        0,
                        y > 0 ? rtop[x] : r[x - 1],
                        r[x - 1],
                        y > 0 ? rtop[x] : r[x - 1],
                        y > 0 ? rtop[x - 1] : r[x - 1],
                        y > 0 ? rtoptop[x] : r[x - 1],
                        properties,
                        offset);

                    int pos = JxlMaConstants.PropertyRangeFast + Math.Clamp(
                        properties[0],
                        -JxlMaConstants.PropertyRangeFast,
                        JxlMaConstants.PropertyRangeFast - 1);

                    int ctx_id = treeLookup.ContextLookup[pos];
                    uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, (int)ctx_id, bitReader, window);

                    r[x] = MakePixel(v, 1, guess);
                    wpState.UpdatePredictionErrors(r[x], x, y, weightedStorage, 0);
                }
            }
        }
        else if (!treeHasWeightedPredictionPropOrPred)
        {
            JxlMaTreeLookup tree_lookup = new(tree);
            Span<int> properties = stackalloc int[numProps];
            using JxlModularChannel references = new(configuration, properties.Length - JxlMaConstants.NumNonrefProperties, channel.Width, 0, 0);
            Span<int> referenceStorage = references.GetRow(0);
            int referenceStride = references.Plane.PixelsPerRow;

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> p = channelSamples[(y * onerow)..];
                JxlContextPrediction.PrecomputeReferences(channel, y, image, channelIdx, references, referenceStorage);
                JxlContextPrediction.InitializePropertiesForRow(properties, staticProps, y);

                if (y > 1 && channel.Width > 8 && references.Width == 0)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        JxlPredictionResult res = JxlContextPrediction.PredictTreeNoWeightedPrediction(
                            properties,
                            channel.Width,
                            ref p[x],
                            onerow,
                            x,
                            y,
                            tree_lookup,
                            referenceStorage.Slice(x * referenceStride, references.Width));

                        uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, res.Context, bitReader, window);
                        p[x] = MakePixel(v, res.Multiplier, res.Guess);
                    }

                    for (int x = 2; x < channel.Width - 2; x++)
                    {
                        JxlPredictionResult res = JxlContextPrediction.PredictTreeNoWeightedPredictionNoEdgeCases(
                            properties,
                            channel.Width,
                            ref p[x],
                            onerow,
                            x,
                            y,
                            tree_lookup,
                            referenceStorage.Slice(x * referenceStride, references.Width));

                        uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, res.Context, bitReader, window);
                        p[x] = MakePixel(v, res.Multiplier, res.Guess);
                    }

                    for (int x = channel.Width - 2; x < channel.Width; x++)
                    {
                        JxlPredictionResult res = JxlContextPrediction.PredictTreeNoWeightedPrediction(
                            properties,
                            channel.Width,
                            ref p[x],
                            onerow,
                            x,
                            y,
                            tree_lookup,
                            referenceStorage.Slice(x * referenceStride, references.Width));

                        uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, res.Context, bitReader, window);
                        p[x] = MakePixel(v, res.Multiplier, res.Guess);
                    }
                }
                else
                {
                    for (int x = 0; x < channel.Width; x++)
                    {
                        JxlPredictionResult res = JxlContextPrediction.PredictTreeNoWeightedPrediction(
                            properties,
                            channel.Width,
                            ref p[x],
                            onerow,
                            x,
                            y,
                            tree_lookup,
                            referenceStorage.Slice(x * referenceStride, references.Width));

                        uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, res.Context, bitReader, window);
                        p[x] = MakePixel(v, res.Multiplier, res.Guess);
                    }
                }
            }
        }
        else
        {
            JxlMaTreeLookup tree_lookup = new(tree);
            Span<int> properties = stackalloc int[numProps];
            using JxlModularChannel references = new(configuration, properties.Length - JxlMaConstants.NumNonrefProperties, channel.Width, 0, 0);
            Span<int> referenceStorage = references.GetRow(0);
            int referenceStride = references.Plane.PixelsPerRow;
            // Allocate only when this operation first selects a weighted channel. Later channels reset the same fixed-capacity owner.
            weightedState ??= new(configuration, wpHeader, maximumWidth, 1);
            JxlModularState wpState = weightedState;
            Span<uint> weightedStorage = wpState.Reset(wpHeader, channel.Width);

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> p = channelSamples[(y * onerow)..];
                JxlContextPrediction.InitializePropertiesForRow(properties, staticProps, y);
                JxlContextPrediction.PrecomputeReferences(channel, y, image, channelIdx, references, referenceStorage);

                if (!usesLz77 && y > 1 && channel.Width > 8 && references.Width == 0)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        JxlPredictionResult res = JxlContextPrediction.PredictTreeWeightedPrediction(
                            properties,
                            channel.Width,
                            ref p[x],
                            onerow,
                            x,
                            y,
                            tree_lookup,
                            referenceStorage.Slice(x * referenceStride, references.Width),
                            wpState,
                            weightedStorage,
                            0);

                        uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, res.Context, bitReader, window);

                        p[x] = MakePixel(v, res.Multiplier, res.Guess);
                        wpState.UpdatePredictionErrors(p[x], x, y, weightedStorage, 0);
                    }

                    for (int x = 2; x < channel.Width - 2; x++)
                    {
                        JxlPredictionResult res = JxlContextPrediction.PredictTreeWeightedPredictionNoEdgeCases(
                            properties,
                            channel.Width,
                            ref p[x],
                            onerow,
                            x,
                            y,
                            tree_lookup,
                            referenceStorage.Slice(x * referenceStride, references.Width),
                            wpState,
                            weightedStorage,
                            0);

                        uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, res.Context, bitReader, window);

                        p[x] = MakePixel(v, res.Multiplier, res.Guess);
                        wpState.UpdatePredictionErrors(p[x], x, y, weightedStorage, 0);
                    }

                    for (int x = channel.Width - 2; x < channel.Width; x++)
                    {
                        JxlPredictionResult res = JxlContextPrediction.PredictTreeWeightedPrediction(
                            properties,
                            channel.Width,
                            ref p[x],
                            onerow,
                            x,
                            y,
                            tree_lookup,
                            referenceStorage.Slice(x * referenceStride, references.Width),
                            wpState,
                            weightedStorage,
                            0);

                        uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, res.Context, bitReader, window);

                        p[x] = MakePixel(v, res.Multiplier, res.Guess);
                        wpState.UpdatePredictionErrors(p[x], x, y, weightedStorage, 0);
                    }
                }
                else
                {
                    for (int x = 0; x < channel.Width; x++)
                    {
                        JxlPredictionResult res = JxlContextPrediction.PredictTreeWeightedPrediction(
                            properties,
                            channel.Width,
                            ref p[x],
                            onerow,
                            x,
                            y,
                            tree_lookup,
                            referenceStorage.Slice(x * referenceStride, references.Width),
                            wpState,
                            weightedStorage,
                            0);

                        uint v = (uint)reader.ReadHybridUIntClustered(usesLz77, res.Context, bitReader, window);

                        p[x] = MakePixel(v, res.Multiplier, res.Guess);
                        wpState.UpdatePredictionErrors(p[x], x, y, weightedStorage, 0);
                    }
                }
            }
        }
    }

    public static void ValidateChannelDimensions(JxlModularImage image, JxlModularOptions options)
    {
        int nbChannels = image.Channels.Count;

        foreach (bool isDc in (bool[])[true, false])
        {
            int groupDimensions = options.group_dim * (isDc ? JxlFrameDimensions.BlockDimensions : 1);
            int c = image.MetaChannels;

            for (; c < nbChannels; c++)
            {
                JxlModularChannel ch = image.Channels[c];
                if (ch.Width > options.group_dim || ch.Height > options.group_dim)
                {
                    break;
                }
            }

            for (; c < nbChannels; c++)
            {
                JxlModularChannel ch = image.Channels[c];
                if (ch.Width == 0 || ch.Height == 0)
                {
                    continue;
                }

                bool isDcChannel = Math.Min(ch.HorizontalShift, ch.VerticalShift) >= 3;
                if (isDcChannel != isDc)
                {
                    continue;
                }

                int tileDimensions = groupDimensions >> Math.Max(ch.HorizontalShift, ch.VerticalShift);
                if (tileDimensions == 0)
                {
                    throw new InvalidOperationException("Inconsistent transforms");
                }
            }
        }
    }

    /// <summary>Reads one modular group using either its local entropy tables or a complete borrowed global context.</summary>
    /// <param name="configuration">The configuration used by decoded samples and prediction scratch.</param>
    /// <param name="br">The source reader positioned at the group header.</param>
    /// <param name="image">The owned destination channels.</param>
    /// <param name="header">The owned destination for this group's fields.</param>
    /// <param name="groupId">The format's modular stream identifier.</param>
    /// <param name="options">The modular decode limits.</param>
    /// <param name="globalTree">The borrowed global tree, or null for a standalone stream.</param>
    /// <param name="globalCode">The matching global entropy code, or null when no global context exists.</param>
    /// <param name="globalContextMap">The matching global context map, or null when no global context exists.</param>
    /// <param name="allowTruncatedGroup">Whether an incomplete group may retain decoded sample data.</param>
    /// <returns>Whether group decoding and terminal entropy checks succeeded.</returns>
    public static bool ModularDecode(
        Configuration configuration,
        JxlBitReader br,
        JxlModularImage image,
        JxlGroupHeader header,
        int groupId,
        JxlModularOptions options,
        Tree? globalTree,
        JxlAnsCode? globalCode,
        List<byte>? globalContextMap,
        bool allowTruncatedGroup)
    {
        if (image.Channels.Count == 0)
        {
            return true;
        }

        if (!JxlBundle.Read(br, header) && !allowTruncatedGroup)
        {
            throw new InvalidOperationException("Could not read a bundle");
        }

        image.Transforms = header.Transforms;

        foreach (JxlTransform transform in image.Transforms)
        {
            transform.MetaApply(configuration, image);
        }

        if (image.IsInvalid)
        {
            throw new InvalidOperationException("Corrupt file. Aborting.");
        }

        ValidateChannelDimensions(image, options);

        int numberOfChannels = image.Channels.Count;
        int numChannels = 0;
        int distanceMultiplier = 0;

        for (int i = 0; i < numberOfChannels; i++)
        {
            JxlModularChannel channel = image.Channels[i];
            if (i >= image.MetaChannels && (channel.Width > options.MaxChannelSize || channel.Height > options.MaxChannelSize))
            {
                break;
            }

            if (channel.Width == 0 || channel.Height == 0)
            {
                continue;  // skip empty channels
            }

            if (channel.Width > distanceMultiplier)
            {
                distanceMultiplier = channel.Width;
            }

            numChannels++;
        }

        if (numChannels == 0)
        {
            return true;
        }

        int nextChannel = 0;
        bool clearPendingChannels = !allowTruncatedGroup;
        try
        {
            Tree treeStorage = [];
            List<byte> contextMapStorage = [];
            JxlAnsCode codeStorage = new();

            Tree tree = treeStorage;
            JxlAnsCode code = codeStorage;
            List<byte> contextMap = contextMapStorage;

            if (!header.UseGlobalTree)
            {
                ulong maxTreeSize = 1024;

                for (int i = 0; i < numberOfChannels; i++)
                {
                    JxlModularChannel channel = image.Channels[i];
                    if (i >= image.MetaChannels && (channel.Width > options.MaxChannelSize || channel.Height > options.MaxChannelSize))
                    {
                        break;
                    }

                    ulong pixels = (ulong)channel.Width * (ulong)channel.Height;
                    maxTreeSize += pixels;
                }

                maxTreeSize = Math.Min(1uL << 20, maxTreeSize);
                JxlMaDecoder.DecodeTree(configuration, br, treeStorage, (int)maxTreeSize);
                JxlAnsReader.DecodeHistograms(configuration, br, (treeStorage.Count + 1) / 2, codeStorage, ref contextMapStorage, false);
            }
            else
            {
                // Standalone modular streams have no shared context. A wire request for a global tree requires all three parts.
                if (globalTree is null || globalCode is null || globalContextMap is null || globalTree.Count == 0)
                {
                    throw new InvalidOperationException("No global tree available but one was requested");
                }

                tree = globalTree;
                code = globalCode;
                contextMap = globalContextMap;
            }

            using JxlAnsSymbolReader reader = JxlAnsSymbolReader.Create(configuration, code, br, distanceMultiplier);
            JxlTreeLut<byte> treeLut = new(false, false);

            // All channels share this reader and distance history. Borrow storage before the channel loop,
            // then pass the same spans through prediction loops without acquiring Memory.Span for each symbol.
            Span<int> window = reader.Lz77Window;
            bool usesLz77 = reader.UsesLz77;
            ReadOnlySpan<byte> contexts = CollectionsMarshal.AsSpan(contextMap);

            uint flRun = 0;
            uint flV = 0;

            JxlModularState? weightedState = null;
            try
            {
                for (; nextChannel < numberOfChannels; nextChannel++)
                {
                    JxlModularChannel channel = image.Channels[nextChannel];
                    if (nextChannel >= image.MetaChannels &&
                        (channel.Width > options.MaxChannelSize ||
                         channel.Height > options.MaxChannelSize))
                    {
                        break;
                    }

                    if (channel.Width == 0 || channel.Height == 0)
                    {
                        continue;  // skip empty channels
                    }

                    DecodeModularChannelMAANS(
                        configuration,
                        usesLz77,
                        br,
                        reader,
                        contexts,
                        window,
                        tree,
                        header.WeightedHeader,
                        nextChannel,
                        groupId,
                        treeLut,
                        image,
                        ref flRun,
                        ref flV,
                        ref weightedState,
                        distanceMultiplier);
                }
            }
            finally
            {
                weightedState?.Dispose();
            }

            clearPendingChannels = false;

            if (!reader.CheckAnsFinalState())
            {
                throw new InvalidOperationException("ANS decode final state failed");
            }

            return true;
        }
        finally
        {
            // Strict failures clear channels that decoding has not completed. Truncated-group mode retains the decoded samples.
            // The success path disables this cleanup before the final ANS check, matching the native decoder's boundary.
            if (clearPendingChannels)
            {
                for (int c = nextChannel; c < image.Channels.Count; c++)
                {
                    image.Channels[c].Plane.Clear();
                }
            }
        }
    }

    /// <summary>Decodes a modular group and optionally reverses its transforms.</summary>
    /// <param name="configuration">The configuration used by owned samples and scratch.</param>
    /// <param name="br">The source reader positioned at the group header.</param>
    /// <param name="image">The owned destination channels.</param>
    /// <param name="header">The owned group header, retained for inverse transforms.</param>
    /// <param name="groupId">The format's modular stream identifier.</param>
    /// <param name="options">The modular decode limits.</param>
    /// <param name="undoTransforms">Whether to restore the requested channel layout after decoding.</param>
    /// <param name="tree">The borrowed global tree, or null for a standalone stream.</param>
    /// <param name="code">The matching global entropy code, or null when no global context exists.</param>
    /// <param name="contextMap">The matching global context map, or null when no global context exists.</param>
    /// <param name="allowTruncatedGroup">Whether an incomplete group may retain decoded sample data.</param>
    /// <returns>Whether decoded samples and the final channel layout are valid.</returns>
    public static bool ModularGenericDecompress(
        Configuration configuration,
        JxlBitReader br,
        JxlModularImage image,
        JxlGroupHeader header,
        int groupId,
        JxlModularOptions options,
        bool undoTransforms,
        Tree? tree,
        JxlAnsCode? code,
        List<byte>? contextMap,
        bool allowTruncatedGroup)
    {
        List<Size> reqSizes = new(capacity: image.Channels.Count);

        foreach (JxlModularChannel c in image.Channels)
        {
            reqSizes.Add(new(c.Width, c.Height));
        }

        bool decStatus = ModularDecode(configuration, br, image, header, groupId, options, tree, code, contextMap, allowTruncatedGroup);

        if (!allowTruncatedGroup)
        {
            if (!decStatus)
            {
                return false;
            }
        }

        if (undoTransforms)
        {
            image.UndoTransforms(configuration, header.WeightedHeader);
        }

        if (image.IsInvalid)
        {
            throw new InvalidOperationException("Corrupt file. Aborting");
        }

        if (undoTransforms)
        {
            if (image.Channels.Count != reqSizes.Count)
            {
                return false;
            }

            for (int c = 0; c < reqSizes.Count; c++)
            {
                if (reqSizes[c].Width != image.Channels[c].Width || reqSizes[c].Height != image.Channels[c].Height)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private readonly record struct TreeRange(int Begin, int End, int Pos);
}

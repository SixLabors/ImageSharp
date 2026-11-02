// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;
using FlatTree = System.Collections.Generic.List<SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction.JxlFlatDecisionNode>;
using StaticPropertyRange = SixLabors.ImageSharp.InlineArray2<SixLabors.ImageSharp.InlineArray2<int>>;
using Tree = System.Collections.Generic.List<SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.JxlPropertyDecisionNode>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

/// <summary>
/// Modular-based coding encoder.
/// </summary>
internal static class JxlModularEncodingEncoder
{
    public static InlineArray3<byte> PredictorColor(JxlPredictor p)
    {
        InlineArray3<byte> result = default;

        switch (p)
        {
            case JxlPredictor.Zero:
                break; // result is all 0 by default

            case JxlPredictor.Left:
                result[0] = 255; // [255, 0, 0]
                break;

            case JxlPredictor.Top:
                result[1] = 255; // [0, 255, 0]
                break;

            case JxlPredictor.Average0:
                result[2] = 255; // [0, 0, 255]
                break;

            case JxlPredictor.Average4:
                // [192, 128, 128]
                result[0] = 192;
                result[1] = 128;
                result[2] = 128;
                break;

            case JxlPredictor.Select:
                // [255, 255, 0]
                result[0] = 255;
                result[1] = 255;
                break;

            case JxlPredictor.Gradient:
                // [255, 0, 255]
                result[1] = 255;
                result[2] = 255;
                break;

            case JxlPredictor.Weighted:
                // [0, 255, 255]
                result[1] = 255;
                result[2] = 255;
                break;

            default:
                // [255, 255, 255]
                result[0] = 255;
                result[1] = 255;
                result[2] = 255;
                break;
        }

        return result;
    }

    /// <summary>Builds a fixed predictor tree, with fewer splits for small channel populations.</summary>
    /// <param name="property">The context property used for each split.</param>
    /// <param name="cutoffs">The ordered split thresholds.</param>
    /// <param name="pred">The predictor used by the leaves.</param>
    /// <param name="numPixels">The complete pixel population, including all selected channels.</param>
    /// <param name="bitDepth">The source sample precision.</param>
    /// <returns>The fixed prediction tree.</returns>
    public static Tree MakeFixedTree(int property, Span<int> cutoffs, JxlPredictor pred, long numPixels, int bitDepth)
    {
        int logPx = (int)JxlMath.CeilLog2Nonzero(numPixels);
        int minGap = 0;

        if (logPx < 14)
        {
            minGap = 8 * (14 - logPx);
        }

        int shift = bitDepth > 11 ? Math.Min(4, bitDepth - 11) : 0;
        int mul = 1 << shift;

        Tree tree = [];
        Queue<NodeInfo> q = [];

        tree.Add(JxlPropertyDecisionNode.Leaf(pred));
        q.Enqueue(new NodeInfo(0, cutoffs.Length, 0));

        while (q.Count > 0)
        {
            NodeInfo info = q.Peek();
            _ = q.Dequeue();

            if (info.Begin + minGap >= info.End)
            {
                continue;
            }

            int split = (info.Begin + info.End) / 2;
            int cutoff = cutoffs[split] * mul;

            tree[info.Pos] = JxlPropertyDecisionNode.Split(property, cutoff, tree.Count);

            q.Enqueue(new NodeInfo(split + 1, info.End, tree.Count));
            tree.Add(JxlPropertyDecisionNode.Leaf(pred));

            q.Enqueue(new NodeInfo(info.Begin, split, tree.Count));
            tree.Add(JxlPropertyDecisionNode.Leaf(pred));
        }

        return tree;
    }

    /// <summary>
    /// Collects prediction samples for one channel, using scratch shared by the complete tree-learning operation.
    /// </summary>
    /// <param name="configuration">The configuration used for channel-reference storage.</param>
    /// <param name="image">The source modular image.</param>
    /// <param name="channelIndex">The source channel index.</param>
    /// <param name="groupId">The group used for static context properties.</param>
    /// <param name="wpHeader">The weighted-prediction settings for this image.</param>
    /// <param name="options">The sampling and predictor options.</param>
    /// <param name="treeSamples">The destination prediction samples.</param>
    /// <param name="wpState">The operation-owned scratch, reset for this channel without allocating.</param>
    /// <param name="totalPixels">The running number of visited samples.</param>
    public static void GatherTreeData(
        Configuration configuration,
        JxlModularImage image,
        int channelIndex,
        int groupId,
        JxlModularHeader wpHeader,
        JxlModularOptions options,
        ref JxlTreeSamples treeSamples,
        JxlModularState wpState,
        ref long totalPixels)
    {
        JxlModularChannel channel = image.Channels[channelIndex];

        InlineArray2<int> staticProperties = default;
        staticProperties[0] = channelIndex;
        staticProperties[1] = groupId;

        Span<int> properties = stackalloc int[JxlMaConstants.NumNonrefProperties + (JxlContextPrediction.ExtraPropertiesPerChannel * options.MaxProperties)];

        ulong threshold = SamplingThreshold(channel.Width, channel.Height, options.NumberOfRepeats);
        Rng samplingRng = new(0);

        Span<int> channelSamples = channel.GetRow(0);
        int pixelsPerRow = channel.Plane.PixelsPerRow;
        using JxlModularChannel references = new(configuration, properties.Length - JxlMaConstants.NumNonrefProperties, channel.Width, 0, 0);
        Span<int> referenceStorage = references.GetRow(0);
        int referenceStride = references.Plane.PixelsPerRow;
        Span<uint> weightedStorage = wpState.Reset(wpHeader, channel.Width);

        bool haveMultiplePredictors = treeSamples.NumberOfPredictors != 1;

        // Pass spans into the local operation. Its captured state contains no borrowed spans.
        void ComputeSample(
            scoped Span<int> p,
            int x,
            int y,
            scoped Span<int> properties,
            scoped Span<uint> predictionStorage,
            scoped ReadOnlySpan<int> referenceProperties,
            ref long totalPixels,
            ref JxlTreeSamples treeSamples,
            ref Rng samplingRng)
        {
            Span<int> pred = stackalloc int[JxlPredictorFacts.ModularPredictorsAlignment].Slice(0, JxlPredictorFacts.ModularPredictors);

            if (haveMultiplePredictors)
            {
                _ = JxlContextPrediction.PredictLearnAll(
                    properties,
                    channel.Width,
                    ref p[x],
                    pixelsPerRow,
                    x,
                    y,
                    referenceProperties,
                    wpState,
                    predictionStorage,
                    0,
                    pred);
            }
            else
            {
                pred[(int)treeSamples.PredictorFromIndex(0)] = JxlContextPrediction.PredictLearn(
                    properties,
                    channel.Width,
                    ref p[x],
                    pixelsPerRow,
                    x,
                    y,
                    treeSamples.PredictorFromIndex(0),
                    referenceProperties,
                    wpState,
                    predictionStorage,
                    0).Guess;
            }

            totalPixels++;

            if ((samplingRng.Next() >> 32) <= threshold)
            {
                treeSamples.AddSample(p[x], properties, pred);
            }

            wpState.UpdatePredictionErrors(p[x], x, y, predictionStorage, 0);
        }

        Span<int> pred = stackalloc int[JxlPredictorFacts.ModularPredictorsAlignment].Slice(0, JxlPredictorFacts.ModularPredictors);

        for (int y = 0; y < channel.Height; y++)
        {
            Span<int> p = channelSamples[(y * pixelsPerRow)..];
            JxlContextPrediction.PrecomputeReferences(channel, y, image, channelIndex, references, referenceStorage);
            JxlContextPrediction.InitializePropertiesForRow(properties, staticProperties, y);

            if (y > 1 && channel.Width > 8 && references.Width == 0)
            {
                for (int x = 0; x < 2; x++)
                {
                    ComputeSample(
                        p,
                        x,
                        y,
                        properties,
                        weightedStorage,
                        referenceStorage.Slice(x * referenceStride, references.Width),
                        ref totalPixels,
                        ref treeSamples,
                        ref samplingRng);

                }

                for (int x = 2; x < channel.Width - 2; x++)
                {
                    if (haveMultiplePredictors)
                    {
                        _ = JxlContextPrediction.PredictLearnAllNoEdgeCases(
                            properties,
                            channel.Width,
                            ref p[x],
                            pixelsPerRow,
                            x,
                            y,
                            referenceStorage.Slice(x * referenceStride, references.Width),
                            wpState,
                            weightedStorage,
                            0,
                            pred);
                    }
                    else
                    {
                        pred[(int)treeSamples.PredictorFromIndex(0)] = JxlContextPrediction.PredictLearnNoEdgeCases(
                            properties,
                            channel.Width,
                            ref p[x],
                            pixelsPerRow,
                            x,
                            y,
                            treeSamples.PredictorFromIndex(0),
                            referenceStorage.Slice(x * referenceStride, references.Width),
                            wpState,
                            weightedStorage,
                            0).Guess;
                    }

                    totalPixels++;

                    if ((samplingRng.Next() >> 32) <= threshold)
                    {
                        treeSamples.AddSample(p[x], properties, pred);
                    }

                    wpState.UpdatePredictionErrors(p[x], x, y, weightedStorage, 0);
                }

                for (int x = channel.Width - 2; x < channel.Width; x++)
                {
                    ComputeSample(
                        p,
                        x,
                        y,
                        properties,
                        weightedStorage,
                        referenceStorage.Slice(x * referenceStride, references.Width),
                        ref totalPixels,
                        ref treeSamples,
                        ref samplingRng);

                }
            }
            else
            {
                for (int x = 0; x < channel.Width; x++)
                {
                    ComputeSample(
                        p,
                        x,
                        y,
                        properties,
                        weightedStorage,
                        referenceStorage.Slice(x * referenceStride, references.Width),
                        ref totalPixels,
                        ref treeSamples,
                        ref samplingRng);

                }
            }
        }
    }

    /// <summary>Learns the tree from the retained records, then releases collection-only deduplication storage.</summary>
    /// <param name="configuration">The configuration used for tree-search scratch.</param>
    /// <param name="treeSamples">The prepared sample owner, partitioned in place during tree search.</param>
    /// <param name="totalPixels">The population used to normalize the sampling cost.</param>
    /// <param name="options">The tree-search options.</param>
    /// <param name="info">The forced multiplier splits.</param>
    /// <param name="staticPropertyRange">The channel and group ranges.</param>
    /// <returns>The learned prediction tree.</returns>
    public static Tree LearnTree(
        Configuration configuration,
        ref JxlTreeSamples treeSamples,
        long totalPixels,
        JxlModularOptions options,
        List<JxlModularMultiplierInfo> info,
        StaticPropertyRange staticPropertyRange)
    {
        Tree tree = [];

        for (int i = 0; i < 2; i++)
        {
            if (staticPropertyRange[i][1] == 0)
            {
                staticPropertyRange[i][1] = int.MaxValue;
            }
        }

        if (!treeSamples.HasSamples)
        {
            JxlPropertyDecisionNode node = new()
            {
                Predictor = treeSamples.PredictorFromIndex(0),
                Property = -1,
                PredictorOffset = 0,
                Multiplier = 1
            };

            tree.Add(node);
            return tree;
        }

        float pixelFraction = treeSamples.NumberOfSamples * 1.0f / totalPixels;
        float requiredCost = (pixelFraction * 0.9f) + 0.1f;

        treeSamples.AllSamplesDone();

        JxlMaEncoder.ComputeBestTree(
            configuration,
            ref treeSamples,
            options.SplittingHeuristicsNodeThreshold * requiredCost,
            info,
            staticPropertyRange,
            options.FastDecodeMultiplier,
            tree);

        return tree;
    }

    /// <summary>
    /// Encodes one channel's prediction residuals into the caller's token storage.
    /// </summary>
    /// <param name="configuration">The configuration that owns prediction and reference storage.</param>
    /// <param name="image">The source modular image.</param>
    /// <param name="channelIndex">The source channel index.</param>
    /// <param name="wpHeader">The weighted-prediction settings for this group.</param>
    /// <param name="globalTree">The prediction tree shared by the encoded channels.</param>
    /// <param name="tokens">The destination token storage.</param>
    /// <param name="groupId">The group used for static context properties.</param>
    /// <param name="skipEncoderFastPath">Whether to use general prediction instead of the specialized channel paths.</param>
    /// <param name="weightedState">The operation-owned scratch, prepared on its first weighted channel and reused by later channels.</param>
    /// <param name="maximumWidth">The maximum channel width prepared by the enclosing modular operation.</param>
    public static void EncodeModularChannelMAANS(
        Configuration configuration,
        JxlModularImage image,
        int channelIndex,
        JxlModularHeader wpHeader,
        Tree globalTree,
        Span<JxlToken> tokens,
        int groupId,
        bool skipEncoderFastPath,
        ref JxlModularState? weightedState,
        int maximumWidth)
    {
        JxlModularChannel channel = image.Channels[channelIndex];

        if (channel.Width == 0 || channel.Height == 0)
        {
            throw new InvalidOperationException("Width or height is 0");
        }

        InlineArray2<int> staticProperties = default;
        staticProperties[0] = channelIndex;
        staticProperties[1] = groupId;

        bool useWp = false;
        bool isWpOnly = false;
        bool isGradientOnly = false;
        int numProps = 0;

        FlatTree tree = JxlModularEncoding.FilterTree(globalTree, staticProperties, ref numProps, ref useWp, ref isWpOnly, ref isGradientOnly);
        JxlMaTreeLookup treeLookup = new(tree);

        JxlTreeLut<ushort> treeLut = new(false, false);
        int tokenp = 0; // Pointer to next token

        if (isWpOnly)
        {
            isWpOnly = JxlModularEncoding.TreeToLookupTable(tree, treeLut);
        }

        if (isGradientOnly)
        {
            isGradientOnly = JxlModularEncoding.TreeToLookupTable(tree, treeLut);
        }

        // Neighbor references remain inside this allocation, including physical row padding after metadata shrink.
        Span<int> channelSamples = channel.GetRow(0);
        int onerow = channel.Plane.PixelsPerRow;
        int treeCount = tree.Count;

        if (isWpOnly && !skipEncoderFastPath)
        {
            // Prepare once on the first weighted channel. The enclosing operation owns disposal and fixed maximum-width capacity.
            weightedState ??= new(configuration, wpHeader, maximumWidth, 1);
            JxlModularState wpState = weightedState;
            Span<uint> weightedStorage = wpState.Reset(wpHeader, channel.Width);
            Span<int> properties = [0];
            bool unhealthy = false;

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> r = channelSamples[(y * onerow)..];

                for (int x = 0; x < channel.Width; x++)
                {
                    int offset = 0;

                    // Neighbors
                    int left = x > 0 ? r[x - 1] : y > 0 ? channelSamples[((y - 1) * onerow) + x] : 0;
                    int top = y > 0 ? channelSamples[((y - 1) * onerow) + x] : left;
                    int topleft = x > 0 && y > 0 ? channelSamples[((y - 1) * onerow) + x - 1] : left;
                    int topright = x + 1 < channel.Width && y > 0 ? channelSamples[((y - 1) * onerow) + x + 1] : top;
                    int toptop = y > 1 ? channelSamples[((y - 2) * onerow) + x] : top;

                    int guess = (int)wpState.Predict(true, x, y, weightedStorage, 0, top, left, topright, topleft, toptop, properties, offset);
                    int pos = JxlMaConstants.PropertyRangeFast + Math.Clamp(
                        properties[0],
                        -JxlMaConstants.PropertyRangeFast,
                        JxlMaConstants.PropertyRangeFast - 1);

                    int ctxId = treeLut.ContextLookup[pos];
                    unhealthy |= JxlMath.SubOverflow(r[x], guess, out int residual);

                    tokens[tokenp++] = new JxlToken((JxlMaTreeContext)ctxId, JxlPackSigned.PackUnsigned(residual));
                    wpState.UpdatePredictionErrors(r[x], x, y, weightedStorage, 0);
                }
            }

            ThrowIfResidualUnderflow(unhealthy);
        }
        else if (treeCount == 1 && tree[0].Predictor == JxlPredictor.Gradient && tree[0].Multiplier == 1 && tree[0].PredictorOffset == 0 && !skipEncoderFastPath)
        {
            bool unhealthy = false;

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> r = channelSamples[(y * onerow)..];

                for (int x = 0; x < channel.Width; x++)
                {
                    int left = x > 0 ? r[x - 1] : y > 0 ? channelSamples[((y - 1) * onerow) + x] : 0;
                    int top = y > 0 ? channelSamples[((y - 1) * onerow) + x] : left;
                    int topleft = x > 0 && y > 0 ? channelSamples[((y - 1) * onerow) + x - 1] : left;

                    int guess = JxlContextPrediction.ClampedGradient(top, left, topleft);
                    unhealthy |= JxlMath.SubOverflow(r[x], guess, out int residual);

                    tokens[tokenp++] = new JxlToken((JxlMaTreeContext)tree[0].ChildID, JxlPackSigned.PackUnsigned(residual));
                }
            }

            ThrowIfResidualUnderflow(unhealthy);
        }
        else if (isGradientOnly && !skipEncoderFastPath)
        {
            bool unhealthy = false;

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> r = channelSamples[(y * onerow)..];

                for (int x = 0; x < channel.Width; x++)
                {
                    int left = x > 0 ? r[x - 1] : y > 0 ? channelSamples[((y - 1) * onerow) + x] : 0;
                    int top = y > 0 ? channelSamples[((y - 1) * onerow) + x] : left;
                    int topleft = x > 0 && y > 0 ? channelSamples[((y - 1) * onerow) + x - 1] : left;

                    int guess = JxlContextPrediction.ClampedGradient(top, left, topleft);
                    int pos = JxlMaConstants.PropertyRangeFast + Math.Min(
                            Math.Max(-JxlMaConstants.PropertyRangeFast, top + left - topleft),
                            JxlMaConstants.PropertyRangeFast - 1);

                    uint ctxId = treeLut.ContextLookup[pos];
                    unhealthy |= JxlMath.SubOverflow(r[x], guess, out int residual);

                    tokens[tokenp++] = new JxlToken((JxlMaTreeContext)ctxId, JxlPackSigned.PackUnsigned(residual));
                }
            }

            ThrowIfResidualUnderflow(unhealthy);
        }
        else if (treeCount == 1 && tree[0].Predictor == JxlPredictor.Zero && tree[0].Multiplier == 1 && tree[0].PredictorOffset == 0 && !skipEncoderFastPath)
        {
            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> p = channelSamples[(y * onerow)..];

                for (int x = 0; x < channel.Width; x++)
                {
                    tokens[tokenp++] = new JxlToken((JxlMaTreeContext)tree[0].ChildID, JxlPackSigned.PackUnsigned(p[x]));
                }
            }
        }
        else if (treeCount == 1 && tree[0].Predictor != JxlPredictor.Weighted && (tree[0].Multiplier & (tree[0].Multiplier - 1)) == 0 && tree[0].PredictorOffset == 0 && !skipEncoderFastPath)
        {
            uint mulShift = JxlMath.FloorLog2Nonzero((uint)tree[0].Multiplier);

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> r = channelSamples[(y * onerow)..];

                for (int x = 0; x < channel.Width; x++)
                {
                    JxlPredictionResult pred = JxlContextPrediction.PredictNoTreeNoWeightedPrediction(channel.Width, ref r[x], onerow, x, y, tree[0].Predictor);
                    int residual = r[x] - pred.Guess;

                    if ((residual >> (int)mulShift) * tree[0].Multiplier != residual)
                    {
                        throw new InvalidOperationException("Residual coefficient is not valid");
                    }

                    tokens[tokenp++] = new JxlToken((JxlMaTreeContext)tree[0].ChildID, JxlPackSigned.PackUnsigned(residual >> (int)mulShift));
                }
            }
        }
        else if (!useWp && !skipEncoderFastPath)
        {
            Span<int> properties = stackalloc int[numProps];
            using JxlModularChannel references = new(configuration, properties.Length - JxlMaConstants.NumNonrefProperties, channel.Width, 0, 0);
            Span<int> referenceStorage = references.GetRow(0);
            int referenceStride = references.Plane.PixelsPerRow;

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> p = channelSamples[(y * onerow)..];

                JxlContextPrediction.PrecomputeReferences(channel, y, image, channelIndex, references, referenceStorage);
                JxlContextPrediction.InitializePropertiesForRow(properties, staticProperties, y);

                for (int x = 0; x < channel.Width; x++)
                {
                    JxlPredictionResult res = JxlContextPrediction.PredictTreeNoWeightedPrediction(
                        properties,
                        channel.Width,
                        ref p[x],
                        onerow,
                        x,
                        y,
                        treeLookup,
                        referenceStorage.Slice(x * referenceStride, references.Width));

                    int residual = p[x] - res.Guess;

                    if ((residual % res.Multiplier) != 0)
                    {
                        throw new InvalidOperationException("Residual coefficient is invalid");
                    }

                    tokens[tokenp++] = new JxlToken((JxlMaTreeContext)res.Context, JxlPackSigned.PackUnsigned(residual / res.Multiplier));
                }
            }
        }
        else
        {
            Span<int> properties = stackalloc int[numProps];
            using JxlModularChannel references = new(configuration, properties.Length - JxlMaConstants.NumNonrefProperties, channel.Width, 0, 0);
            Span<int> referenceStorage = references.GetRow(0);
            int referenceStride = references.Plane.PixelsPerRow;

            // Prepare once on the first weighted channel. The enclosing operation owns disposal and fixed maximum-width capacity.
            weightedState ??= new(configuration, wpHeader, maximumWidth, 1);
            JxlModularState wpState = weightedState;
            Span<uint> weightedStorage = wpState.Reset(wpHeader, channel.Width);

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> p = channelSamples[(y * onerow)..];
                JxlContextPrediction.PrecomputeReferences(channel, y, image, channelIndex, references, referenceStorage);
                JxlContextPrediction.InitializePropertiesForRow(properties, staticProperties, y);

                for (int x = 0; x < channel.Width; x++)
                {
                    JxlPredictionResult res = JxlContextPrediction.PredictTreeWeightedPrediction(
                        properties,
                        channel.Width,
                        ref p[x],
                        onerow,
                        x,
                        y,
                        treeLookup,
                        referenceStorage.Slice(x * referenceStride, references.Width),
                        wpState,
                        weightedStorage,
                        0);

                    int residual = p[x] - res.Guess;

                    if ((residual % res.Multiplier) != 0)
                    {
                        throw new InvalidOperationException("Residual coefficient is invalid");
                    }

                    tokens[tokenp++] = new JxlToken((JxlMaTreeContext)res.Context, JxlPackSigned.PackUnsigned(residual / res.Multiplier));
                    wpState.UpdatePredictionErrors(p[x], x, y, weightedStorage, 0);
                }
            }
        }
    }

    /// <summary>Selects a predefined modular prediction tree.</summary>
    /// <param name="treeKind">The requested predefined tree.</param>
    /// <param name="totalPixels">The complete pixel population, including all selected channels.</param>
    /// <param name="bitDepth">The source sample precision.</param>
    /// <param name="prevProp">The number of reference properties available from earlier channels.</param>
    /// <returns>The selected prediction tree.</returns>
    public static Tree PredefinedTree(JxlTreeKind treeKind, long totalPixels, int bitDepth, int prevProp)
    {
        switch (treeKind)
        {
            case JxlTreeKind.JpegTranscodeAcMeta:
                return [JxlPropertyDecisionNode.Leaf(JxlPredictor.Zero)];

            case JxlTreeKind.TrivialTreeNoPredictor:
                return [JxlPropertyDecisionNode.Leaf(JxlPredictor.Zero)];

            case JxlTreeKind.FalconAcMeta:
                return [JxlPropertyDecisionNode.Leaf(JxlPredictor.Left)];

            case JxlTreeKind.AcMeta:
            {
                if (totalPixels < 1024)
                {
                    return [JxlPropertyDecisionNode.Leaf(JxlPredictor.Left)];
                }

                Tree tree = [];

                tree.Add(JxlPropertyDecisionNode.Split(0, 1, 1));
                tree.Add(JxlPropertyDecisionNode.Split(0, 2, 3));
                tree.Add(JxlPropertyDecisionNode.Split(0, 0, 5));
                tree.Add(JxlPropertyDecisionNode.Split(6, 3, 21));
                tree.Add(JxlPropertyDecisionNode.Split(2, 0, 7));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Gradient));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Gradient));
                tree.Add(JxlPropertyDecisionNode.Split(7, 5, 9));
                tree.Add(JxlPropertyDecisionNode.Split(7, 5, 15));
                tree.Add(JxlPropertyDecisionNode.Split(7, 11, 11));
                tree.Add(JxlPropertyDecisionNode.Split(7, 3, 13));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Left));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Left));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Left));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Left));
                tree.Add(JxlPropertyDecisionNode.Split(7, 11, 17));
                tree.Add(JxlPropertyDecisionNode.Split(7, 3, 19));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Zero));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Zero));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Zero));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Zero));
                tree.Add(JxlPropertyDecisionNode.Split(7, 3, 23));
                tree.Add(JxlPropertyDecisionNode.Split(7, 3, 25));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Zero));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Zero));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Zero));
                tree.Add(JxlPropertyDecisionNode.Leaf(JxlPredictor.Zero));

                return tree;
            }

            case JxlTreeKind.WpFixedDc:
            {
                Span<int> cutoffs = [
                    -500, -392, -255, -191, -127, -95, -63, -47, -31, -23, -15,
                    -11,  -7,   -4,   -3,   -1,   0,   1,   3,   5,   7,   11,
                    15,   23,   31,   47,   63,   95,  127, 191, 255, 392, 500];

                return MakeFixedTree(JxlMaConstants.WpProp, cutoffs, JxlPredictor.Weighted, totalPixels, bitDepth);
            }

            case JxlTreeKind.GradientFixedDc:
            {
                Span<int> cutoffs = [
                    -500, -392, -255, -191, -127, -95, -63, -47, -31, -23, -15,
                    -11,  -7,   -4,   -3,   -1,   0,   1,   3,   5,   7,   11,
                    15,   23,   31,   47,   63,   95,  127, 191, 255, 392, 500];

                return MakeFixedTree(prevProp > 0 ? JxlMaConstants.NumNonrefProperties + 2 : JxlMaConstants.GradientProp, cutoffs, JxlPredictor.Gradient, totalPixels, bitDepth);
            }

            case JxlTreeKind.Learn:
            {
                throw new InvalidOperationException($"{nameof(JxlTreeKind.Learn)} is not a predefined tree");
            }
        }

        throw new InvalidOperationException("Invalid tree type: " + treeKind);
    }

    /// <summary>
    /// Learns a prediction tree from the selected images, reusing weighted-prediction storage across their channels.
    /// </summary>
    /// <param name="configuration">The configuration that owns temporary prediction storage.</param>
    /// <param name="images">The modular source images.</param>
    /// <param name="options">The matching sampling and prediction options.</param>
    /// <param name="start">The first image index to include.</param>
    /// <param name="stop">The first image index to exclude.</param>
    /// <param name="multiplierInfo">The forced multiplier ranges, or an empty list when none are required.</param>
    /// <returns>The learned prediction tree.</returns>
    public static Tree LearnTree(
        Configuration configuration,
        ReadOnlySpan<JxlModularImage> images,
        ReadOnlySpan<JxlModularOptions> options,
        int start,
        int stop,
        List<JxlModularMultiplierInfo> multiplierInfo)
    {
        JxlTreeSamples samples = new(configuration);
        try
        {
            samples.SetPredictor(options[start].Predictor, options[start].WpTreeMode);
            samples.SetProperties(CollectionsMarshal.AsSpan(options[start].SplittingHeuristicsProperties), options[start].WpTreeMode);

            int maxC = 0;
            int maximumWidth = 0;

            int acceptedSamples = 0;
            int previousPixels = -1;
            ulong previousThreshold = 0;
            int previousAccepted = 0;

            for (int i = start; i < stop; i++)
            {
                maxC = Math.Max(images[i].Channels.Count, maxC);

                for (int c = 0; c < images[i].Channels.Count; c++)
                {
                    JxlModularChannel channel = images[i].Channels[c];

                    if (c >= images[i].MetaChannels && (channel.Width > options[i].MaxChannelSize || channel.Height > options[i].MaxChannelSize))
                    {
                        break;
                    }

                    maximumWidth = Math.Max(maximumWidth, channel.Width);
                    if (images[i].Width == 0 || images[i].Height == 0 || channel.Width == 0 || channel.Height == 0)
                    {
                        continue;
                    }

                    int pixels = checked(channel.Width * channel.Height);
                    ulong threshold = SamplingThreshold(channel.Width, channel.Height, options[i].NumberOfRepeats);
                    if (pixels != previousPixels || threshold != previousThreshold)
                    {
                        previousAccepted = CountAcceptedSamples(pixels, threshold);
                        previousPixels = pixels;
                        previousThreshold = threshold;
                    }

                    acceptedSamples = checked(acceptedSamples + previousAccepted);
                }
            }

            // Count the actual deterministic selections before allocating sample planes. Repeated pixel counts and thresholds
            // reuse the preceding count. A fraction of one uses the channel pixel count without an RNG counting pass.
            samples.PrepareForSamples(acceptedSamples);
            using IMemoryOwner<int> histogramOwner = configuration.MemoryAllocator.Allocate<int>(checked(stop + maxC), AllocationOptions.Clean);
            Span<int> histogramStorage = histogramOwner.Memory.Span;
            Span<int> groupPixelCount = histogramStorage[..stop];
            Span<int> channelPixelCount = histogramStorage[stop..];
            int rawSampleCount = 0;
            for (int i = start; i < stop; i++)
            {
                rawSampleCount = checked(rawSampleCount + JxlMaEncoder.CountPixelSamples(images[i], options[i], i, groupPixelCount, channelPixelCount));
            }

            // Pixel and residual training values have the same selection count. Keep both in one owned allocation.
            using IMemoryOwner<int> trainingOwner = configuration.MemoryAllocator.Allocate<int>(checked(2 * rawSampleCount));
            Span<int> trainingStorage = trainingOwner.Memory.Span;
            Span<int> pixelSamples = trainingStorage[..rawSampleCount];
            Span<int> diffSamples = trainingStorage[rawSampleCount..];
            int sampleOffset = 0;
            for (int i = start; i < stop; i++)
            {
                sampleOffset += JxlMaEncoder.CollectPixelSamples(images[i], options[i], i, pixelSamples[sampleOffset..], diffSamples[sampleOffset..]);
            }

            StaticPropertyRange range = default;
            InlineArray2<int> currRange = default;
            currRange[0] = 0;
            currRange[1] = maxC;
            range[0] = currRange;
            currRange[0] = start;
            currRange[1] = stop;
            range[1] = currRange;

            samples.PreQuantizeProperties(
                range,
                CollectionsMarshal.AsSpan(multiplierInfo),
                groupPixelCount,
                channelPixelCount,
                pixelSamples,
                diffSamples,
                options[start].MaxPropertyValues);

            // Individual planes fit Int32 storage. Their combined population can exceed Int32 when sampling is sparse.
            long totalPixels = 0;

            for (int i = 0; i < images[start].Channels.Count; i++)
            {
                if (i >= images[start].MetaChannels &&
                    (images[start].Channels[i].Width > options[start].MaxChannelSize ||
                     images[start].Channels[i].Height > options[start].MaxChannelSize))
                {
                    break;
                }

                totalPixels += images[start].Channels[i].Width * images[start].Channels[i].Height;
            }

            totalPixels = Math.Max(totalPixels, 1);

            JxlModularHeader wpHeader = new();
            using JxlModularState weightedState = new(configuration, wpHeader, maximumWidth, 1);

            for (int i = start; i < stop; i++)
            {
                int numChannels = images[i].Channels.Count;

                if (images[i].Width == 0 || images[i].Height == 0 || numChannels < 1)
                {
                    continue;
                }

                if (images[i].IsInvalid)
                {
                    throw new InvalidOperationException("Invalid image");
                }

                if (options[i].TreeKind != JxlTreeKind.Learn)
                {
                    throw new InvalidOperationException("Tree type must be Learn");
                }

                JxlBundle.Init(wpHeader);

                if (JxlContextPrediction.IsWeightedPredictor(options[i].Predictor))
                {
                    JxlContextPrediction.SetPredictorMode(options[i].WpMode, wpHeader);
                }

                for (int c = 0; c < numChannels; c++)
                {
                    if (c >= images[i].MetaChannels &&
                        (images[i].Channels[c].Width > options[i].MaxChannelSize ||
                         images[i].Channels[c].Height > options[i].MaxChannelSize))
                    {
                        break;
                    }

                    if (images[i].Channels[c].Width == 0 || images[i].Channels[c].Height == 0)
                    {
                        continue;  // skip empty channels
                    }

                    GatherTreeData(configuration, images[i], c, i, wpHeader, options[i], ref samples, weightedState, ref totalPixels);
                }
            }

            Tree tree = LearnTree(configuration, ref samples, totalPixels, options[start], multiplierInfo, range);

            return tree;
        }
        finally
        {
            samples.Dispose();
        }
    }

    /// <summary>Computes the native acceptance threshold shared by sample counting and collection.</summary>
    /// <param name="width">The channel width.</param>
    /// <param name="height">The channel height.</param>
    /// <param name="repeats">The requested sampling fraction.</param>
    /// <returns>The inclusive threshold for the high 32 RNG bits.</returns>
    private static ulong SamplingThreshold(int width, int height, float repeats)
    {
        double fraction = Math.Min(1F, repeats);
        if (fraction > 0)
        {
            fraction = Math.Max(fraction, Math.Min(1D, 1024D / ((long)width * height)));
        }

        return (ulong)(uint.MaxValue * fraction);
    }

    /// <summary>Counts the exact accepted samples without reading or copying channel samples.</summary>
    /// <param name="pixelCount">The number of channel pixels visited by collection.</param>
    /// <param name="threshold">The inclusive native acceptance threshold.</param>
    /// <returns>The capacity required before collecting this channel.</returns>
    private static int CountAcceptedSamples(int pixelCount, ulong threshold)
    {
        if (threshold == uint.MaxValue)
        {
            return pixelCount;
        }

        Rng rng = new(0);
        int accepted = 0;
        for (int i = 0; i < pixelCount; i++)
        {
            if ((rng.Next() >> 32) <= threshold)
            {
                accepted++;
            }
        }

        return accepted;
    }

    /// <summary>
    /// Prepares one modular group's prediction header and tokens, sharing weighted scratch across its sequential channels.
    /// </summary>
    /// <param name="configuration">The configuration that owns temporary prediction storage.</param>
    /// <param name="image">The source modular image.</param>
    /// <param name="options">The prediction and channel-selection options.</param>
    /// <param name="groupId">The group used for static context properties.</param>
    /// <param name="tree">The prediction tree shared by this group.</param>
    /// <param name="header">The destination group header.</param>
    /// <param name="tokens">The destination token list.</param>
    /// <param name="width">The maximum encoded channel width, used by the entropy encoder.</param>
    public static void ModularCompress(
        Configuration configuration,
        JxlModularImage image,
        JxlModularOptions options,
        int groupId,
        Tree tree,
        JxlGroupHeader header,
        List<JxlToken> tokens,
        ref int width)
    {
        int numChannels = image.Channels.Count;

        if (image.Width == 0 || image.Height == 0 || numChannels < 1)
        {
            return;
        }

        if (image.IsInvalid)
        {
            throw new InvalidOperationException("Invalid image");
        }

        JxlBundle.Init(header);

        if (JxlContextPrediction.IsWeightedPredictor(options.Predictor))
        {
            JxlContextPrediction.SetPredictorMode(options.WpMode, header.WeightedHeader);
        }

        header.Transforms = image.Transforms;
        header.UseGlobalTree = true;

        int imageWidth = 0;
        int totalTokens = 0;

        for (int i = 0; i < numChannels; i++)
        {
            if (i >= image.MetaChannels &&
                (image.Channels[i].Width > options.MaxChannelSize ||
                 image.Channels[i].Height > options.MaxChannelSize))
            {
                break;
            }

            if (image.Channels[i].Width > imageWidth)
            {
                imageWidth = image.Channels[i].Width;
            }

            totalTokens += image.Channels[i].Width * image.Channels[i].Height;
        }

        if (options.ZeroTokens)
        {
            tokens.Grow(default, tokens.Count + totalTokens);
        }
        else
        {
            int pos = tokens.Count;
            tokens.Grow(default, pos + totalTokens);

            int tokenp = pos;

            JxlModularState? weightedState = null;
            try
            {
                for (int i = 0; i < numChannels; i++)
                {
                    if (i >= image.MetaChannels &&
                        (image.Channels[i].Width > options.MaxChannelSize ||
                         image.Channels[i].Height > options.MaxChannelSize))
                    {
                        break;
                    }

                    if (image.Channels[i].Width == 0 || image.Channels[i].Height == 0)
                    {
                        continue;
                    }

                    EncodeModularChannelMAANS(
                        configuration,
                        image,
                        i,
                        header.WeightedHeader,
                        tree,
                        CollectionsMarshal.AsSpan(tokens)[tokenp..],
                        groupId,
                        options.SkipEncoderFastPath,
                        ref weightedState,
                        imageWidth);
                }
            }
            finally
            {
                weightedState?.Dispose();
            }

            if (tokenp != tokens.Count)
            {
                throw new InvalidOperationException("Tokens were not written");
            }
        }

        width = imageWidth;
    }

    public static void ModularGenericCompress(Configuration configuration, JxlModularImage image, JxlModularOptions options, JxlBitWriter writer, JxlAuxiliaryOutput auxOut, JxlLayerType layerType, int groupId)
    {
        int numChannels = image.Channels.Count;

        if (image.Width == 0 || image.Height == 0 || numChannels < 1)
        {
            return;
        }

        if (image.IsInvalid)
        {
            throw new InvalidOperationException("Invalid image");
        }

        JxlModularOptions modularOptions = options.DeepClone(); // Make a copy to modify it

        if (modularOptions.Predictor == JxlPredictor.Undefined)
        {
            modularOptions.Predictor = JxlPredictor.Gradient;
        }

        long bits = writer.BitsWritten;

        JxlGroupHeader groupHeader = new();
        JxlBundle.Init(groupHeader);

        if (JxlContextPrediction.IsWeightedPredictor(modularOptions.Predictor))
        {
            JxlContextPrediction.SetPredictorMode(modularOptions.WpMode, groupHeader.WeightedHeader);
        }

        groupHeader.Transforms = image.Transforms;

        JxlBundle.Write(groupHeader, writer, layerType, auxOut);

        Tree tree = [];

        if (modularOptions.TreeKind == JxlTreeKind.Learn)
        {
            // Borrow the single image and its resolved options. Tree preparation reads these references only during this call.
            ReadOnlySpan<JxlModularImage> images = MemoryMarshal.CreateReadOnlySpan(ref image, 1);
            ReadOnlySpan<JxlModularOptions> treeOptions = MemoryMarshal.CreateReadOnlySpan(ref modularOptions, 1);
            tree = LearnTree(configuration, images, treeOptions, 0, 1, []);
        }
        else
        {
            // It's a predefined tree.
            long totalPixels = 0;

            for (int i = 0; i < numChannels; i++)
            {
                if (i >= image.MetaChannels &&
                    (image.Channels[i].Width > options.MaxChannelSize
                    || image.Channels[i].Height > options.MaxChannelSize))
                {
                    break;
                }

                totalPixels += image.Channels[i].Width * image.Channels[i].Height;
            }

            totalPixels = Math.Max(totalPixels, 1);

            tree = PredefinedTree(options.TreeKind, totalPixels, image.BitDepth, options.MaxProperties);
        }

        Tree decodedTree = [];
        List<List<JxlToken>> treeTokens = [[]];

        JxlMaEncoder.TokenizeTree(tree, treeTokens[0], decodedTree);

        if (tree.Count != decodedTree.Count)
        {
            throw new InvalidOperationException("Tree lengths mismatch");
        }

        tree = decodedTree;

        // TODO: missing types here
        JxlEntropyEncodingData code = default;
        int cost = BuildAndEncodeHistograms(configuration, options.HistogramParameters, JxlMaConstants.NumTreeContexts, treeTokens, code, writer, JxlLayerType.ModularTree, auxOut);
        WriteTokens(treeTokens[0], code, 0, writer, JxlLayerType.ModularTree, auxOut);

        int imageWidth = 0;
        List<List<JxlToken>> tokens = [[]];
        ModularCompress(configuration, image, options, groupId, tree, groupHeader, tokens[0], ref imageWidth);

        code = default;
        JxlHistogramParameters histoParameters = options.HistogramParameters;
        histoParameters.ImageWidths.Add(imageWidth);
        _ = BuildAndEncodeHistograms(configuration, histoParameters, (tree.Count + 1) / 2, tokens, ref code, writer, layerType, auxOut);

        WriteTokens(tokens[0], code, 0, writer, layerType, auxOut);
        bits = writer.BitsWritten - bits;
    }

    private static void ThrowIfResidualUnderflow(bool isUnderflow)
    {
        if (isUnderflow)
        {
            throw new InvalidOperationException("Residual coefficient underflow");
        }
    }

    private struct NodeInfo
    {
        public NodeInfo(int begin, int end, int pos)
        {
            this.Begin = begin;
            this.End = end;
            this.Pos = pos;
        }

        public int Begin { get; set; }

        public int End { get; set; }

        public int Pos { get; set; }
    }
}

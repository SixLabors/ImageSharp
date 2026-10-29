// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using FlatTree = System.Collections.Generic.List<SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction.JxlFlatDecisionNode>;
using StaticPropertyRange = System.Runtime.CompilerServices.InlineArray2<System.Runtime.CompilerServices.InlineArray2<int>>;
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

    public static Tree MakeFixedTree(int property, Span<int> cutoffs, JxlPredictor pred, int numPixels, int bitDepth)
    {
        int logPx = JxlMath.CeilLog2Nonzero(numPixels);
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
        JxlTreeSamples treeSamples,
        JxlModularState wpState,
        ref int totalPixels)
    {
        JxlModularChannel channel = image.Channels[channelIndex];

        InlineArray2<int> staticProperties = default;
        staticProperties[0] = channelIndex;
        staticProperties[1] = groupId;

        Span<int> properties = stackalloc int[JxlMaConstants.NumNonrefProperties + (JxlContextPrediction.ExtraPropertiesPerChannel * options.MaxProperties)];

        double pixelFraction = Math.Min(1.0f, options.NumberOfRepeats);

        if (pixelFraction > 0)
        {
            pixelFraction = Math.Max(pixelFraction, Math.Min(1.0, 1024.0 / (channel.Width * channel.Height)));
        }

        ulong threshold = (ulong)((ulong.MaxValue >> 32) * pixelFraction);

        ulong s1 = 0x94D049BB133111EBuL;
        ulong s2 = 0xBF58476D1CE4E5B9uL;

        bool UseSample()
        {
            ulong bits = s2 + s1;
            s1 = s2;
            s1 ^= s1 << 23;
            s1 ^= s2 ^ (s1 >> 18) ^ (s2 >> 5);
            s2 = s1;
            return (bits >> 32) <= threshold;
        }

        Span<int> channelSamples = channel.GetRow(0);
        int pixelsPerRow = channel.Plane.PixelsPerRow;
        using JxlModularChannel references = new(configuration, properties.Length - JxlMaConstants.NumNonrefProperties, channel.Width, 0, 0);
        Span<int> referenceStorage = references.GetRow(0);
        int referenceStride = references.Plane.PixelsPerRow;
        Span<uint> weightedStorage = wpState.Reset(wpHeader, channel.Width);

        treeSamples.PrepareForSamples((int)(pixelFraction * channel.Height * channel.Width) + 64);
        bool haveMultiplePredictors = treeSamples.NumberOfPredictors != 1;

        // Pass spans into the local operation. Its captured state contains no borrowed spans.
        void ComputeSample(
            Span<int> p,
            int x,
            int y,
            Span<int> properties,
            Span<uint> predictionStorage,
            ReadOnlySpan<int> referenceProperties,
            ref int totalPixels)
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

            if (UseSample())
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
                    ComputeSample(p, x, y, properties, weightedStorage, referenceStorage.Slice(x * referenceStride, references.Width), ref totalPixels);
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

                    if (UseSample())
                    {
                        treeSamples.AddSample(p[x], properties, pred);
                    }

                    wpState.UpdatePredictionErrors(p[x], x, y, weightedStorage, 0);
                }

                for (int x = channel.Width - 2; x < channel.Width; x++)
                {
                    ComputeSample(p, x, y, properties, weightedStorage, referenceStorage.Slice(x * referenceStride, references.Width), ref totalPixels);
                }
            }
            else
            {
                for (int x = 0; x < channel.Width; x++)
                {
                    ComputeSample(p, x, y, properties, weightedStorage, referenceStorage.Slice(x * referenceStride, references.Width), ref totalPixels);
                }
            }
        }
    }

    public static Tree LearnTree(JxlTreeSamples treeSamples, int totalPixels, JxlModularOptions options, List<JxlModularMultiplierInfo>? info = null, StaticPropertyRange staticPropertyRange = default)
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

        JxlMaEncoder.ComputeBestTree(treeSamples, options.SplittingHeuristicsNodeThreshold * requiredCost, info, staticPropertyRange, options.FastDecodeMultiplier, tree);
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

    public static Tree PredefinedTree(JxlTreeKind treeKind, int totalPixels, int bitDepth, int prevProp)
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
    /// <param name="multiplierInfo">The optional learned multiplier information.</param>
    /// <returns>The learned prediction tree.</returns>
    public static Tree LearnTree(
        Configuration configuration,
        JxlModularImage[] images,
        Span<JxlModularOptions> options,
        int start,
        int stop,
        List<JxlModularMultiplierInfo>? multiplierInfo = null)
    {
        multiplierInfo ??= [];

        JxlTreeSamples samples = new();
        samples.SetPredictor(options[start].Predictor, options[start].WpTreeMode);
        samples.SetProperties(options[start].SplittingHeuristicsProperties, options[start].WpTreeMode);

        int maxC = 0;
        int maximumWidth = 0;

        List<int> pixelSamples = [];
        List<int> diffSamples = [];
        List<int> groupPixelCount = [];
        List<int> channelPixelCount = [];

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
            }

            JxlMaEncoder.CollectPixelSamples(configuration, images[i], options[i], i, groupPixelCount, channelPixelCount, pixelSamples, diffSamples);
        }

        // StaticPropRange range;
        // range[0] = { { 0, max_c } };
        // range[1] = { { start, stop } };
        StaticPropertyRange range = default;
        InlineArray2<int> currRange = default;
        currRange[0] = 0;
        currRange[1] = maxC;
        range[0] = currRange;
        currRange[0] = start;
        currRange[1] = stop;
        range[1] = currRange;

        samples.PreQuantizeProperties(configuration, range, multiplierInfo, groupPixelCount, channelPixelCount, pixelSamples, diffSamples, options[start].MaxPropertyValues);

        int totalPixels = 0;

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

                GatherTreeData(configuration, images[i], c, i, wpHeader, options[i], samples, weightedState, ref totalPixels);
            }
        }

        Tree tree = LearnTree(samples, totalPixels, options[start], multiplierInfo, range);

        return tree;
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
            tree = LearnTree(configuration, image, options, 0, 1);
        }
        else
        {
            // It's a predefined tree.
            int totalPixels = 0;

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

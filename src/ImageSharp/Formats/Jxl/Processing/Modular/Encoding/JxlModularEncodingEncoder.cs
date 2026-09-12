// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
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

    public static void GatherTreeData(Configuration configuration, JxlModularImage image, int channelIndex, int groupId, JxlModularHeader wpHeader, JxlModularOptions options, JxlTreeSamples treeSamples, ref int totalPixels)
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

        int pixelsPerRow = channel.Plane.PixelsPerRow;
        using JxlModularChannel references = new(configuration, properties.Length - JxlMaConstants.NumNonrefProperties, channel.Width, 0, 0);
        JxlModularState wpState = new(wpHeader, channel.Width);

        treeSamples.PrepareForSamples((int)(pixelFraction * channel.Height * channel.Width) + 64);
        bool haveMultiplePredictors = treeSamples.NumberOfPredictors != 1;

        void ComputeSample(Span<int> p, int x, int y, Span<int> properties, ref int totalPixels)
        {
            Span<int> pred = stackalloc int[JxlPredictorFacts.ModularPredictorsAlignment].Slice(0, JxlPredictorFacts.ModularPredictors);

            if (haveMultiplePredictors)
            {
                _ = JxlContextPrediction.PredictLearnAll(properties, channel.Width, ref p[x], pixelsPerRow, x, y, references, wpState, pred);
            }
            else
            {
                pred[(int)treeSamples.PredictorFromIndex(0)] = JxlContextPrediction.PredictLearn(properties, channel.Width, ref p[x], pixelsPerRow, x, y, treeSamples.PredictorFromIndex(0), references, wpState).Guess;
            }

            totalPixels++;

            if (UseSample())
            {
                treeSamples.AddSample(p[x], properties, pred);
            }

            wpState.UpdatePredictionErrors(p[x], x, y, channel.Width);
        }

        Span<int> pred = stackalloc int[JxlPredictorFacts.ModularPredictorsAlignment].Slice(0, JxlPredictorFacts.ModularPredictors);

        for (int y = 0; y < channel.Height; y++)
        {
            Span<int> p = channel.GetRow(y);
            JxlContextPrediction.PrecomputeReferences(channel, y, image, channelIndex, references);
            JxlContextPrediction.InitializePropertiesForRow(properties, staticProperties, y);

            if (y > 1 && channel.Width > 8 && references.Width == 0)
            {
                for (int x = 0; x < 2; x++)
                {
                    ComputeSample(p, x, y, properties, ref totalPixels);
                }

                for (int x = 2; x < channel.Width - 2; x++)
                {
                    if (haveMultiplePredictors)
                    {
                        _ = JxlContextPrediction.PredictLearnAllNoEdgeCases(properties, channel.Width, ref p[x], pixelsPerRow, x, y, references, wpState, pred);
                    }
                    else
                    {
                        pred[(int)treeSamples.PredictorFromIndex(0)] = JxlContextPrediction.PredictLearnNoEdgeCases(properties, channel.Width, ref p[x], pixelsPerRow, x, y, treeSamples.PredictorFromIndex(0), references, wpState).Guess;
                    }

                    totalPixels++;

                    if (UseSample())
                    {
                        treeSamples.AddSample(p[x], properties, pred);
                    }

                    wpState.UpdatePredictionErrors(p[x], x, y, channel.Width);
                }

                for (int x = channel.Width - 2; x < channel.Width; x++)
                {
                    ComputeSample(p, x, y, properties, ref totalPixels);
                }
            }
            else
            {
                for (int x = 0; x < channel.Width; x++)
                {
                    ComputeSample(p, x, y, properties, ref totalPixels);
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

    public static void EncodeModularChannelMAANS(Configuration configuration, JxlModularImage image, int channelIndex, JxlModularHeader wpHeader, Tree globalTree, Span<JxlToken> tokens, int groupId, bool skipEncoderFastPath)
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

        int onerow = channel.Plane.PixelsPerRow;
        int treeCount = tree.Count;

        if (isWpOnly && !skipEncoderFastPath)
        {
            JxlModularState wpState = new(wpHeader, channel.Width);
            Span<int> properties = [0];
            bool unhealthy = false;

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> r = channel.GetRow(y);

                for (int x = 0; x < channel.Width; x++)
                {
                    int offset = 0;

                    // Neighbors
                    int left = x > 0 ? channel.GetRowPlus(y, x - 1)[0] : y > 0 ? channel.GetRowPlus(y, x - onerow)[0] : 0;
                    int top = y > 0 ? channel.GetRowPlus(y, x - onerow)[0] : left;
                    int topleft = x > 0 && y > 0 ? channel.GetRowPlus(y, x - 1 - onerow)[0] : left;
                    int topright = x + 1 < channel.Width && y > 0 ? channel.GetRowPlus(y, x + 1 - onerow)[0] : top;
                    int toptop = y > 1 ? channel.GetRowPlus(y, x - onerow - onerow)[0] : top;

                    int guess = (int)wpState.Predict(true, x, y, channel.Width, top, left, topright, topleft, toptop, properties, offset);
                    int pos = JxlMaConstants.PropertyRangeFast + Math.Clamp(properties[0], -JxlMaConstants.PropertyRangeFast, JxlMaConstants.PropertyRangeFast - 1);

                    int ctxId = treeLut.ContextLookup[pos];
                    unhealthy |= JxlMath.SubOverflow(r[x], guess, out int residual);

                    tokens[tokenp++] = new JxlToken((JxlMaTreeContext)ctxId, JxlPackSigned.PackUnsigned(residual));
                    wpState.UpdatePredictionErrors(r[x], x, y, channel.Width);
                }
            }

            ThrowIfResidualUnderflow(unhealthy);
        }
        else if (treeCount == 1 && tree[0].Predictor == JxlPredictor.Gradient && tree[0].Multiplier == 1 && tree[0].PredictorOffset == 0 && !skipEncoderFastPath)
        {
            bool unhealthy = false;

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> r = channel.GetRow(y);

                for (int x = 0; x < channel.Width; x++)
                {
                    int left = x > 0 ? channel.GetRowPlus(y, x - 1)[0] : y > 0 ? channel.GetRowPlus(y, x - onerow)[0] : 0;
                    int top = y > 0 ? channel.GetRowPlus(y, x - onerow)[0] : left;
                    int topleft = x > 0 && y > 0 ? channel.GetRowPlus(y, x - 1 - onerow)[0] : left;

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
                Span<int> r = channel.GetRow(y);

                for (int x = 0; x < channel.Width; x++)
                {
                    int left = x > 0 ? channel.GetRowPlus(y, x - 1)[0] : y > 0 ? channel.GetRowPlus(y, x - onerow)[0] : 0;
                    int top = y > 0 ? channel.GetRowPlus(y, x - onerow)[0] : left;
                    int topleft = x > 0 && y > 0 ? channel.GetRowPlus(y, x - 1 - onerow)[0] : left;

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
                Span<int> p = channel.GetRow(y);

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
                Span<int> r = channel.GetRow(y);

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

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> p = channel.GetRow(y);

                JxlContextPrediction.PrecomputeReferences(channel, y, image, channelIndex, references);
                JxlContextPrediction.InitializePropertiesForRow(properties, staticProperties, y);

                for (int x = 0; x < channel.Width; x++)
                {
                    JxlPredictionResult res = JxlContextPrediction.PredictTreeNoWeightedPrediction(properties, channel.Width, ref p[x], onerow, x, y, treeLookup, references);
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

            JxlModularState wpState = new(wpHeader, channel.Width);

            for (int y = 0; y < channel.Height; y++)
            {
                Span<int> p = channel.GetRow(y);
                JxlContextPrediction.PrecomputeReferences(channel, y, image, channelIndex, references);
                JxlContextPrediction.InitializePropertiesForRow(properties, staticProperties, y);

                for (int x = 0; x < channel.Width; x++)
                {
                    JxlPredictionResult res = JxlContextPrediction.PredictTreeWeightedPrediction(properties, channel.Width, ref p[x], onerow, x, y, treeLookup, references, wpState);

                    int residual = p[x] - res.Guess;

                    if ((residual % res.Multiplier) != 0)
                    {
                        throw new InvalidOperationException("Residual coefficient is invalid");
                    }

                    tokens[tokenp++] = new JxlToken((JxlMaTreeContext)res.Context, JxlPackSigned.PackUnsigned(residual / res.Multiplier));
                    wpState.UpdatePredictionErrors(p[x], x, y, channel.Width);
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

    public static Tree LearnTree(Configuration configuration, JxlModularImage[] images, Span<JxlModularOptions> options, int start, int stop, List<JxlModularMultiplierInfo>? multiplierInfo = null)
    {
        multiplierInfo ??= [];

        JxlTreeSamples samples = new();
        samples.SetPredictor(options[start].Predictor, options[start].WpTreeMode);
        samples.SetProperties(options[start].SplittingHeuristicsProperties, options[start].WpTreeMode);

        int maxC = 0;

        List<int> pixelSamples = [];
        List<int> diffSamples = [];
        List<int> groupPixelCount = [];
        List<int> channelPixelCount = [];

        for (int i = start; i < stop; i++)
        {
            maxC = Math.Max(images[i].Channels.Count, maxC);
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

                GatherTreeData(configuration, images[i], c, i, wpHeader, options[i], samples, totalPixels);
            }
        }

        Tree tree = LearnTree(samples, totalPixels, options[start], multiplierInfo, range);

        return tree;
    }

    public static void ModularCompress(JxlModularImage image, JxlModularOptions options, int groupId, Tree tree, JxlGroupHeader header, List<JxlToken> tokens, ref int width)
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

                EncodeModularChannelMAANS(configuration, image, i, header.WeightedHeader, tree, CollectionsMarshal.AsSpan(tokens)[tokenp..], groupId, options.SkipEncoderFastPath);
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
        ModularCompress(image, options, groupId, tree, groupHeader, tokens[0], ref imageWidth);

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

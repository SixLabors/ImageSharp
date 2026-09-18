// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using Tree = System.Collections.Generic.List<SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.JxlPropertyDecisionNode>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

internal static class JxlMaDecoder
{
    private enum NextAction
    {
        CheckAndGoLeft,
        GoRight,
        Pop
    }

    public static void ValidateTree(Tree tree)
    {
        const int heightLimit = 2048;

        if (tree.Count == 0)
        {
            return;
        }

        int numProperties = tree.Max(x => x.Property + 1);
        Span<(int First, int Second)> propertyRanges = stackalloc (int First, int Second)[numProperties];

        for (int i = 0; i < numProperties; i++)
        {
            propertyRanges[i].First = int.MinValue;
            propertyRanges[i].Second = int.MaxValue;
        }

        Stack<WorkItem> stack = [];
        stack.Push(new WorkItem(0, 0, 0, NextAction.CheckAndGoLeft));

        while (stack.Count > 0)
        {
            if (stack.Count > heightLimit)
            {
                throw new InvalidOperationException("Tree too tall");
            }

            WorkItem item = stack.Peek();
            JxlPropertyDecisionNode node = tree[item.NodeIndex];

            switch (item.Action)
            {
                case NextAction.CheckAndGoLeft:
                {
                    int p = node.Property;
                    if (p == -1)
                    {
                        _ = stack.Pop();
                        continue;
                    }

                    int v = node.SplitValue;
                    int l = propertyRanges[p].First;
                    int u = propertyRanges[p].Second;

                    if (l > v || u <= v)
                    {
                        throw new InvalidOperationException("Invalid tree");
                    }

                    item.OriginalL = l;
                    item.OriginalU = u;
                    item.Action = NextAction.GoRight;
                    propertyRanges[node.Property].First = node.SplitValue + 1;

                    stack.Push(new WorkItem(node.LeftChild, 0, 0, NextAction.CheckAndGoLeft));
                    continue;
                }

                case NextAction.GoRight:
                    item.Action = NextAction.Pop;
                    propertyRanges[node.Property].First = item.OriginalL;
                    propertyRanges[node.Property].Second = node.SplitValue;
                    stack.Push(new WorkItem(node.LeftChild, 0, 0, NextAction.CheckAndGoLeft));
                    continue;

                case NextAction.Pop:
                    propertyRanges[node.Property].Second = item.OriginalU;
                    _ = stack.Pop();
                    continue;
            }
        }
    }

    public static void DecodeTree(JxlBitReader br, JxlAnsSymbolReader reader, List<byte> contextMap, Tree tree, int treeSizeLimit)
    {
        int leafId = 0;
        int toDecode = 1;
        tree.Clear();

        while (toDecode > 0)
        {
            if (tree.Count > treeSizeLimit)
            {
                throw new InvalidOperationException("Tree is too large");
            }

            toDecode--;
            int prop1 = reader.ReadHybridUint(JxlMaTreeContext.Property, br, contextMap);
            if (prop1 > 256)
            {
                throw new InvalidOperationException("Invalid tree property value");
            }

            int property = prop1 - 1;
            if (property == -1)
            {
                int predictor = reader.ReadHybridUint(JxlMaTreeContext.Predictor, br, contextMap);

                if (predictor >= JxlPredictorFacts.ModularPredictors)
                {
                    throw new InvalidOperationException("Invalid predictor");
                }

                int predictor_offset = JxlPackSigned.UnpackSigned(reader.ReadHybridUint(JxlMaTreeContext.Offset, br, contextMap));
                int mul_log = reader.ReadHybridUint(JxlMaTreeContext.MultiplierLog, br, contextMap);

                if (mul_log >= 31)
                {
                    throw new InvalidOperationException("Invalid multiplier logarithm");
                }

                int mul_bits = reader.ReadHybridUint(JxlMaTreeContext.MultiplierBits, br, contextMap);

                if (mul_bits >= (1 << (31 - mul_log)) - 1)
                {
                    throw new InvalidOperationException("Invalid multiplier");
                }

                int multiplier = (mul_bits + 1) << mul_log;
                JxlPredictor p = (JxlPredictor)predictor;

                tree.Add(new JxlPropertyDecisionNode(-1, 0, leafId, 0, p, predictor_offset, multiplier));
                leafId++;
                continue;
            }

            int splitval = JxlPackSigned.UnpackSigned(reader.ReadHybridUint(JxlMaTreeContext.SplitValue, br, contextMap));
            tree.Add(
                new JxlPropertyDecisionNode(
                    property,
                    splitval,
                    tree.Count + toDecode + 1,
                    tree.Count + toDecode + 2,
                    JxlPredictor.Zero,
                    0,
                    1));

            toDecode += 2;
        }

        ValidateTree(tree);
    }

    public static void DecodeTree(Configuration configuration, JxlBitReader reader, Tree tree, int treeSizeLimit)
    {
        List<byte> treeContextMap = [];
        JxlAnsCode code = new();

        JxlAnsReader.DecodeHistograms(configuration, reader, JxlMaConstants.NumTreeContexts, code, treeContextMap);

        if (code.DegenerateSymbols[treeContextMap[(int)JxlMaTreeContext.Property]] > 0)
        {
            throw new InvalidOperationException("Infinite tree");
        }

        JxlAnsSymbolReader symbolReader = JxlAnsSymbolReader.Create(code, reader);
        DecodeTree(reader, symbolReader, treeContextMap, tree, Math.Min(treeSizeLimit, JxlMaConstants.MaxTreeSize));

        if (!symbolReader.CheckAnsFinalState())
        {
            throw new InvalidOperationException("ANS decode final state failed");
        }
    }

    private struct WorkItem(int nodeIndex, int origL, int origU, NextAction action)
    {
        public int NodeIndex { get; set; } = nodeIndex;

        public int OriginalL { get; set; } = origL;

        public int OriginalU { get; set; } = origU;

        public NextAction Action { get; set; } = action;
    }
}

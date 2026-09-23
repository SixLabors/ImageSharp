// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal static class JxlContextMapDecoder
{
    private const int MaxClusters = 256;

    public static void VerifyContextMap(List<byte> contextMap, int numHTrees)
    {
        Span<bool> haveHTree = stackalloc bool[numHTrees];

        int numFound = 0;

        foreach (byte htree in contextMap)
        {
            if (htree >= numHTrees)
            {
                throw new InvalidOperationException("Invalid histogram index in context map");
            }

            if (!haveHTree[htree])
            {
                haveHTree[htree] = true;
                numFound++;
            }
        }

        if (numFound != numHTrees)
        {
            throw new InvalidOperationException("Incomplete context map");
        }
    }

    public static void DecodeContextMap(Configuration configuration, List<byte> contextMap, ref int numHTrees, JxlBitReader input)
    {
        // True - simple context map, without any ANS entropy coding,
        // false - the context map uses entropy coding and LZ77.
        bool isSimpleContextMap = input.ReadBoolean();

        Span<byte> cmSpan = CollectionsMarshal.AsSpan(contextMap);

        if (isSimpleContextMap)
        {
            uint bitsPerEntry = input.ReadBits32(2);

            if (bitsPerEntry != 0)
            {
                for (int i = 0; i < cmSpan.Length; i++)
                {
                    cmSpan[i] = (byte)input.ReadBits32(bitsPerEntry);
                }
            }
            else
            {
                // No context map at all
                cmSpan.Clear();
            }
        }
        else
        {
            bool useMtf = input.ReadBoolean(); // use Move to Front transform?
            JxlAnsCode ansCode = new();
            List<byte> sinkContextMap = [];

            if (!JxlAnsReader.DecodeHistograms(configuration, input, 1, ansCode, sinkContextMap, contextMap.Count <= 2))
            {
                throw new InvalidOperationException("Context map histogram couldn't be decoded");
            }

            JxlAnsSymbolReader ans = JxlAnsSymbolReader.Create(ansCode, input);

            int i = 0;
            int maxSymbols = 0;

            while (i < cmSpan.Length)
            {
                uint sym = ans.ReadHybridUInt(true, 0, input, sinkContextMap);
                maxSymbols = sym > maxSymbols ? (int)sym : maxSymbols;
                cmSpan[i] = (byte)sym;
                i++;
            }

            if (maxSymbols >= MaxClusters)
            {
                throw new InvalidOperationException("Invalid cluster ID");
            }

            if (!ans.CheckAnsFinalState())
            {
                throw new InvalidOperationException("Invalid context map");
            }

            if (useMtf)
            {
                JxlInverseMtf.InverseMoveToFrontTransform(cmSpan);
            }
        }

        numHTrees = TensorPrimitives.Max((ReadOnlySpan<byte>)cmSpan) + 1;
        VerifyContextMap(contextMap, numHTrees);
    }
}

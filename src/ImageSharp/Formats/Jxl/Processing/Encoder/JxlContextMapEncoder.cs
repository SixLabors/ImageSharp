// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlContextMapEncoder
{
    public const int ClustersLimit = 128;

    private static int IndexOf(List<byte> v, byte value)
    {
        int i = 0;
        for (; i < v.Count; ++i)
        {
            if (v[i] == value)
            {
                return i;
            }
        }

        return i;
    }

    private static void MoveToFront(List<byte> v, int index)
    {
        byte value = v[index];

        for (int i = index; i != 0; --i)
        {
            v[i] = v[i - 1];
        }

        v[0] = value;
    }

    private static List<byte> MoveToFrontTransform(IReadOnlyList<byte> v)
    {
        if (v.Count == 0)
        {
            return [];
        }

        byte maxValue = 0;
        for (int i = 0; i < v.Count; ++i)
        {
            if (v[i] > maxValue)
            {
                maxValue = v[i];
            }
        }

        List<byte> mtf = new(maxValue + 1);
        for (int i = 0; i <= maxValue; ++i)
        {
            mtf.Add((byte)i);
        }

        List<byte> result = new(v.Count);
        for (int i = 0; i < v.Count; ++i)
        {
            int index = IndexOf(mtf, v[i]);

            if (index >= mtf.Count)
            {
                throw new InvalidOperationException("Move To Front index out of range");
            }

            result.Add((byte)index);
            MoveToFront(mtf, index);
        }

        return result;
    }

    private static bool EncodeContextMap(IReadOnlyList<byte> contextMap, int numHistograms, JxlBitWriter writer)
    {
        if (numHistograms == 1)
        {
            // Simple code.
            writer.Write(1, 1);

            // 0 bits per entry.
            writer.Write(2, 0);
            return true;
        }

        List<byte> transformedSymbols = MoveToFrontTransform(contextMap);

        List<List<JxlToken>> jxlTokens =
        [
            new List<JxlToken>(contextMap.Count)
        ];

        List<List<JxlToken>> mtfJxlTokens =
        [
            new List<JxlToken>(transformedSymbols.Count)
        ];

        for (int i = 0; i < contextMap.Count; ++i)
        {
            jxlTokens[0].Add(new JxlToken(0, contextMap[i]));
        }

        for (int i = 0; i < transformedSymbols.Count; ++i)
        {
            mtfJxlTokens[0].Add(new JxlToken(0, transformedSymbols[i]));
        }

        JxlHistogramParameters parameters = new()
        {
            UIntMethod = JxlHybridUIntMethod.ContextMap
        };

        int ansCost;
        int mtfCost;
        {
            JxlEntropyEncodingData codes = new();
            ansCost = BuildAndEncodeHistograms(parameters, 1, jxlTokens, codes, null, null);
        }

        {
            JxlEntropyEncodingData codes = new();
            mtfCost = BuildAndEncodeHistograms(parameters, 1, mtfJxlTokens, codes, null, null);
        }

        bool useMtf = mtfCost < ansCost;

        jxlTokens[0].Clear();

        for (int i = 0; i < transformedSymbols.Count; ++i)
        {
            jxlTokens[0].Add(new JxlToken(0, useMtf ? transformedSymbols[i] : contextMap[i]));
        }

        int entryBits = JxlMath.CeilLog2Nonzero(numHistograms);
        int simpleCost = entryBits * contextMap.Count;

        if (entryBits < 4 && simpleCost < ansCost && simpleCost < mtfCost)
        {
            return writer.WithMaxBits((ulong)(3 + (entryBits * contextMap.Count)), () =>
            {
                writer.Write(1, 1);
                writer.Write(2, entryBits);

                for (int i = 0; i < contextMap.Count; ++i)
                {
                    writer.Write(entryBits, contextMap[i]);
                }

                return true;
            });
        }

        return writer.WithMaxBits(2 + (jxlTokens[0].Count * 24), () =>
        {
            writer.Write(1, 0);
            writer.Write(1, useMtf ? 1 : 0);

            JxlEntropyEncodingData codes = new();
            BuildAndEncodeHistograms(parameters, 1, jxlTokens, codes, writer);

            WriteJxlTokens(jxlTokens[0], codes, 0, writer);
            return true;
        });
    }

    public static bool EncodeBlockContextMap(JxlBlockContextMap blockCtxMap, JxlBitWriter writer)
    {
        List<int>[] dct = blockCtxMap.DcThresholds;
        List<uint> qf = blockCtxMap.QfThresholds;
        byte[] ctxMap = blockCtxMap.ContextMap;

        int maxBits = ((dct[0].Count + dct[1].Count + dct[2].Count + qf.Count) * 34) +
                      1 +
                      4 +
                      4 +
                      (ctxMap.Length * 10) +
                      1024;

        return writer.WithMaxBits(maxBits, () =>
        {
            if (dct[0].Count == 0 &&
                dct[1].Count == 0 &&
                dct[2].Count == 0 &&
                qf.Count == 0 &&
                ctxMap.Length == 21 &&
                ctxMap.AsSpan().SequenceEqual(JxlBlockContextMap.DefaultContextMap))
            {
                writer.Write(1, 1); // all_default = 1
                return true;
            }

            writer.Write(1, 0); // all_default = 0

            for (int j = 0; j < 3; ++j)
            {
                writer.Write(4, dct[j].Count);

                foreach (int value in dct[j])
                {
                    if (!JxlU32Coder.Write(DcThresholdDist, JxlPackSigned.PackUnsigned(value), writer))
                    {
                        throw new InvalidOperationException("Failed to encode DC threshold");
                    }
                }
            }

            writer.Write(4, qf.Count);

            foreach (uint value in qf)
            {
                if (!JxlU32Coder.Write(QfThresholdDist, value - 1, writer))
                {
                    throw new InvalidOperationException("Failed to encode QF threshold");
                }
            }

            return EncodeContextMap(ctxMap, blockCtxMap.ContextCount, writer);
        });
    }
}

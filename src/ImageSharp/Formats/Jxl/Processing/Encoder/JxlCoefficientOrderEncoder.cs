// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlCoefficientOrderEncoder
{
    private struct PosAndCount
    {
        public uint Pos;
        public ulong CountAndIdx;
    }

    private static (uint UsedOrders, uint CustomizeOrders) ComputeUsedOrders(JxlSpeedTier speed, JxlAcStrategyImage acStrategy, Rectangle rect)
    {
        if (speed >= JxlSpeedTier.Falcon)
        {
            return (1, 1);
        }

        uint ret = 0;
        uint retCustomize = 0;
        int xSizeBlocks = rect.Width;
        int ySizeBlocks = rect.Height;

        for (int by = 0; by < ySizeBlocks; by++)
        {
            JxlAcStrategyRow acsRow = acStrategy.GetRow(rect, by);

            for (int bx = 0; bx < xSizeBlocks; bx++)
            {
                byte ord = JxlCoefficientOrder.StrategyOrder[acsRow[bx].RawStrategy];

                ret |= 1u << ord;

                if (ord > 6)
                {
                    continue;
                }

                retCustomize |= 1u << ord;
            }
        }

        if (acStrategy.XSize < 5 && acStrategy.YSize < 5)
        {
            return (ret, 0);
        }

        return (ret, retCustomize);
    }

    private static bool ComputeCoeffOrder(JxlSpeedTier speed, IJxlDctAcImage acImage, JxlAcStrategyImage acStrategy, JxlFrameDimensions frameDim, ref uint allUsedOrders, uint prevUsedAcs, uint currentUsedAcs, uint currentUsedOrders, Span<int> order)
    {
        int[] numZeros = new int[JxlCoefficientOrder.CoefficientOrderMaxSize];
        double blockFraction = 1.0;

        if (speed >= JxlSpeedTier.Squirrel && currentUsedOrders == 1)
        {
            blockFraction = 0.5;
        }

        if (currentUsedOrders != 0)
        {
            ulong threshold = uint.MaxValue * (ulong)blockFraction;

            ulong s0 = 0x94D049BB133111EBUL;
            ulong s1 = 0xBF58476D1CE4E5B9UL;

            bool UseSample()
            {
                ulong s = s0;
                ulong s0Local = s1;
                ulong bits = s + s0Local;

                s0 = s0Local;

                s ^= s << 23;
                s ^= s0Local ^ (s >> 18) ^ (s0Local >> 5);
                s1 = s;

                return (bits >> 32) <= threshold;
            }

            for (int groupIndex = 0; groupIndex < frameDim.NumGroups; ++groupIndex)
            {
                int gx = groupIndex % frameDim.XSizeGroups;
                int gy = groupIndex / frameDim.XSizeGroups;

                Rectangle rect = RectangleUtils.CreateRectangle(
                    gx * JxlFrameDimensions.GroupDimensionsInBlocks,
                    gy * JxlFrameDimensions.GroupDimensionsInBlocks,
                    JxlFrameDimensions.GroupDimensionsInBlocks,
                    JxlFrameDimensions.GroupDimensionsInBlocks,
                    frameDim.XSizeBlocks,
                    frameDim.YSizeBlocks);

                int acOffset = 0;
                JxlDctAcType type = acImage.Type;

                for (int by = 0; by < rect.Height; ++by)
                {
                    JxlAcStrategyRow acsRow = acStrategy.GetRow(rect, by);

                    for (int bx = 0; bx < rect.Width; ++bx)
                    {
                        JxlAcStrategy acs = acsRow[bx];

                        if (!acs.IsFirstBlock)
                        {
                            continue;
                        }

                        if (!UseSample())
                        {
                            continue;
                        }

                        int size = JxlFrameDimensions.DctBlockSize << acs.Log2CoveredBlocks;

                        for (int c = 0; c < 3; ++c)
                        {
                            int orderOffset = JxlCoefficientOrder.CoeffOrderOffset(JxlCoefficientOrder.StrategyOrder[acs.RawStrategy], c);

                            if (type == JxlDctAcType.Ac16)
                            {
                                for (int k = 0; k < size; ++k)
                                {
                                    bool isZero = acImage.GetPlaneRow(c, groupIndex, 0).Pointer16[acOffset + k] == 0;
                                    if (isZero)
                                    {
                                        ++numZeros[orderOffset + k];
                                    }
                                }
                            }
                            else
                            {
                                for (int k = 0; k < size; ++k)
                                {
                                    bool isZero = acImage.GetPlaneRow(c, groupIndex, 0).Pointer32[acOffset + k] == 0;
                                    if (isZero)
                                    {
                                        ++numZeros[orderOffset + k];
                                    }
                                }
                            }

                            int cx = acs.CoveredBlocksX;
                            int cy = acs.CoveredBlocksY;

                            JxlForwardCoefficientOrder.CoefficientLayout(ref cy, ref cx);

                            for (int iy = 0; iy < cy; ++iy)
                            {
                                for (int ix = 0; ix < cx; ++ix)
                                {
                                    numZeros[orderOffset + (iy * JxlFrameDimensions.BlockDimensions * cx) + ix] = -1;
                                }
                            }
                        }

                        acOffset += size;
                    }
                }
            }
        }

        PosAndCount[] posAndVal = new PosAndCount[JxlAcStrategy.MaximumCoefficientArea];
        int[] naturalOrderBuffer = [];

        ushort computed = 0;

        for (byte o = 0; o < JxlAcStrategy.NumberOfValidStrategies; ++o)
        {
            byte ord = JxlCoefficientOrder.StrategyOrder[o];

            if ((computed & (1 << ord)) != 0)
            {
                continue;
            }

            computed |= (ushort)(1 << ord);

            JxlAcStrategy acs = new(o);

            int sz = JxlFrameDimensions.DctBlockSize * acs.CoveredBlocksX * acs.CoveredBlocksY;

            if (sz > (1 << 16))
            {
                throw new InvalidOperationException("Inavlid size");
            }

            if (((1u << ord) & ~currentUsedAcs) != 0)
            {
                continue;
            }

            if (((1u << ord) & prevUsedAcs) != 0)
            {
                continue;
            }

            if (((1u << ord) & allUsedOrders) != 0)
            {
                continue;
            }

            if (naturalOrderBuffer.Length < sz)
            {
                // Not sure how large can sz get...
                naturalOrderBuffer = new int[sz];
            }

            acs.ComputeNaturalCoefficientOrder(naturalOrderBuffer);

            if (((1u << ord) & ~currentUsedOrders) != 0)
            {
                for (int c = 0; c < 3; ++c)
                {
                    int offset = JxlCoefficientOrder.CoeffOrderOffset(ord, c);

                    if (JxlCoefficientOrder.CoeffOrderOffset(ord, c + 1) - offset != sz)
                    {
                        throw new InvalidOperationException("Invalid coefficient order");
                    }

                    naturalOrderBuffer.AsSpan(0, sz).CopyTo(order.Slice(offset, sz));
                }

                continue;
            }

            bool isNonDefault = false;

            for (byte c = 0; c < 3; ++c)
            {
                int offset = JxlCoefficientOrder.CoeffOrderOffset(ord, c);

                if (JxlCoefficientOrder.CoeffOrderOffset(ord, c + 1) - offset != sz)
                {
                    throw new InvalidOperationException("Invalid coefficient order");
                }

                float invSqrtSz = 1.0f / MathF.Sqrt(sz);

                for (int i = 0; i < sz; ++i)
                {
                    int pos = naturalOrderBuffer[i];

                    posAndVal[i].Pos = (uint)pos;
                    ulong count = (ulong)((numZeros[offset + pos] * invSqrtSz) + 0.1f);

                    if (count >= 1uL << 48)
                    {
                        throw new InvalidOperationException("Count is too large");
                    }

                    posAndVal[i].CountAndIdx = (count << 16) | (uint)i;
                }

                Array.Sort(posAndVal, 0, sz, PosAndCountComparer.Instance);

                for (int i = 0; i < sz; ++i)
                {
                    order[offset + i] = (int)posAndVal[i].Pos;

                    isNonDefault |= naturalOrderBuffer[i] != posAndVal[i].Pos;
                }
            }

            if (!isNonDefault)
            {
                currentUsedOrders &= ~(1u << ord);
            }
        }

        allUsedOrders |= currentUsedOrders;
        return true;
    }

    private static bool TokenizePermutation(ReadOnlySpan<uint> order, int skip, int size, List<JxlToken> tokens)
    {
        uint[] lehmer = new uint[size];
        uint[] temp = new uint[size + 1];

        if (!JxlLehmerCode.ComputeLehmerCode(order, temp, size, lehmer))
        {
            return false;
        }

        int end = size;
        while (end > skip && lehmer[end - 1] == 0)
        {
            --end;
        }

        tokens.Add(new JxlToken((JxlMaTreeContext)JxlCoefficientOrder.CoeffOrderContext((uint)size), (uint)(end - skip)));

        uint last = 0;
        for (int i = skip; i < end; ++i)
        {
            tokens.Add(new JxlToken((JxlMaTreeContext)JxlCoefficientOrder.CoeffOrderContext(last), lehmer[i]));
            last = lehmer[i];
        }

        return true;
    }

    private static bool EncodePermutation(ReadOnlySpan<uint> order, int skip, int size, JxlBitWriter writer)
    {
        List<JxlToken>[] tokens = [[]];

        if (!TokenizePermutation(order, skip, size, tokens[0]))
        {
            return false;
        }

        JxlEntropyEncodingData codes = new();

        if (!BuildAndEncodeHistograms(configuration, new JxlHistogramParameters(), PermutationContext, tokens, out codes, writer, out _))
        {
            return false;
        }

        return WriteTokens(tokens[0], codes, 0, writer);
    }

    private static bool EncodeCoeffOrder(ReadOnlySpan<uint> order, JxlAcStrategy acs, List<JxlToken> tokens, Span<uint> orderZigzag, Span<int> naturalOrderLut)
    {
        int llf = acs.CoveredBlocksX * acs.CoveredBlocksY;
        int size = JxlFrameDimensions.DctBlockSize * llf;

        for (int i = 0; i < size; ++i)
        {
            orderZigzag[i] = (uint)naturalOrderLut[(int)order[i]];
        }

        return TokenizePermutation(orderZigzag[..size], llf, size, tokens);
    }

    public static bool EncodeCoeffOrders(ushort usedOrders, ReadOnlySpan<uint> order, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput auxOut)
    {
        int memSize = JxlAcStrategy.MaximumCoefficientArea;

        // TODO: pool this? or allocate with Configuration.MemoryAllocator? memSize=65536
        uint[] orderZigzag = new uint[memSize];

        ushort computed = 0;
        List<JxlToken>[] tokens = [[]];
        int[] naturalOrderLut = [];

        for (byte o = 0; o < JxlAcStrategy.NumberOfValidStrategies; ++o)
        {
            byte ord = JxlCoefficientOrder.StrategyOrder[o];

            if ((computed & (1 << ord)) != 0)
            {
                continue;
            }

            computed |= (ushort)(1 << ord);

            if ((usedOrders & (1 << ord)) == 0)
            {
                continue;
            }

            JxlAcStrategy acs = new(o);

            int llf = acs.CoveredBlocksX * acs.CoveredBlocksY;
            int size = JxlFrameDimensions.DctBlockSize * llf;

            if (naturalOrderLut.Length < size)
            {
                naturalOrderLut = new int[size];
            }

            acs.ComputeNaturalCoefficientOrderLookup(naturalOrderLut);

            for (int c = 0; c < 3; ++c)
            {
                int offset = JxlCoefficientOrder.CoeffOrderOffset(ord, c);

                if (!EncodeCoeffOrder(order.Slice(offset, size), acs, tokens[0], orderZigzag.AsSpan(0, size), naturalOrderLut))
                {
                    return false;
                }
            }
        }

        if (usedOrders != 0)
        {
            JxlEntropyEncodingData codes = new();

            if (!BuildAndEncodeHistograms(configuration, new JxlHistogramParameters(), PermutationContexts, tokens, out codes, writer, layer, auxOut, out _))
            {
                return false;
            }

            if (!WriteTokens(tokens[0], codes, 0, writer, layer, auxOut))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class PosAndCountComparer : IComparer<PosAndCount>
    {
        public static readonly PosAndCountComparer Instance = new();

        public int Compare(PosAndCount a, PosAndCount b) => a.CountAndIdx.CompareTo(b.CountAndIdx);
    }
}

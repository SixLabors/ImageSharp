// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Lz77;

internal sealed class JxlLz77HashChain
{
    private readonly int maxChainLength;
    private readonly int size;
    private readonly IMemoryOwner<uint> data;

    private uint hashNumValues = 32768;
    private uint hashMask = 32767;
    private uint hashShift = 5;

    private readonly IMemoryOwner<int> head;
    private readonly IMemoryOwner<uint> chain;
    private readonly IMemoryOwner<int> value;

    private readonly IMemoryOwner<int> headZ;
    private readonly IMemoryOwner<uint> chainZ;
    private readonly IMemoryOwner<uint> zeros;
    private uint numZeros;

    private readonly int windowSize;
    private readonly int windowMask;
    private readonly int minLength;
    private readonly int maxLength;

    private readonly Dictionary<int, int> specialDistTable = [];
    private readonly int numSpecialDistances;

    private readonly Configuration configuration;

    public JxlLz77HashChain(
        Configuration configuration,
        int maxChainLength,
        Span<JxlToken> data,
        int size,
        int windowSize,
        int minLength,
        int maxLength,
        int distanceMultiplier)
    {
        this.maxChainLength = maxChainLength;
        this.size = size;
        this.windowSize = windowSize;
        this.windowMask = windowSize - 1;
        this.minLength = minLength;
        this.maxLength = maxLength;
        this.configuration = configuration;

        this.data = configuration.MemoryAllocator.Allocate<uint>(size);
        Span<uint> dataSpan = this.data.GetSpan();

        for (int i = 0; i < size; i++)
        {
            dataSpan[i] = data[i].Value;
        }

        this.head = configuration.MemoryAllocator.Allocate<int>((int)this.hashNumValues);
        this.value = configuration.MemoryAllocator.Allocate<int>(this.windowSize);
        this.chain = configuration.MemoryAllocator.Allocate<uint>(this.windowSize);

        this.head.GetSpan().Fill(-1);
        this.value.GetSpan().Fill(-1);

        Span<uint> chainSpan = this.chain.GetSpan();

        for (uint i = 0; i < this.windowSize; ++i)
        {
            // Same value as index indicates uninitialized
            chainSpan[unchecked((int)i)] = i;
        }

        this.zeros = configuration.MemoryAllocator.Allocate<uint>(this.windowSize);
        this.headZ = configuration.MemoryAllocator.Allocate<int>(this.windowSize + 1);
        this.chainZ = configuration.MemoryAllocator.Allocate<uint>(this.windowSize);

        this.headZ.GetSpan().Fill(-1);
        Span<uint> chainZSpan = this.chainZ.GetSpan();

        for (uint i = 0; i < this.windowSize; ++i)
        {
            chainZSpan[unchecked((int)i)] = i;
        }

        // Translation of distance to special distance code
        if (distanceMultiplier > 0)
        {
            // Count down, so if due to small distance multiplier multiple distances
            // map to the same code, the smallest code will be used in the end.
            for (int i = JxlAnsReader.NumSpecialDistances - 1; i >= 0; --i)
            {
                this.specialDistTable[JxlAnsReader.SpecialDistance(i, distanceMultiplier)] = i;
            }

            this.numSpecialDistances = JxlAnsReader.NumSpecialDistances;
        }
    }

    public uint CountZeros(int pos, uint previousPos)
    {
        int end = pos + this.windowSize;

        if (end > this.size)
        {
            end = this.size;
        }

        Span<uint> dataSpan = this.data.GetSpan();

        if (previousPos > 0)
        {
            if (previousPos >= this.windowMask
                && dataSpan[end - 1] == 0 &&
                end == pos + this.windowSize)
            {
                return previousPos;
            }
            else
            {
                return previousPos - 1;
            }
        }

        uint num = 0;

        while (pos + num < end &&
            dataSpan[unchecked((int)(pos + num))] == 0)
        {
            num++;
        }

        return num;
    }

    public void Update(int pos)
    {
        uint hashValue = JxlLz77.GetHash(pos, this.data.GetSpan()) & this.hashMask;
        int wpos = pos & this.windowMask;

        Span<int> valueSpan = this.value.GetSpan();
        Span<int> headSpan = this.head.GetSpan();
        Span<uint> chainSpan = this.chain.GetSpan();
        Span<uint> dataSpan = this.data.GetSpan();
        Span<uint> zerosSpan = this.zeros.GetSpan();
        Span<int> headZSpan = this.headZ.GetSpan();
        Span<uint> chainZSpan = this.chainZ.GetSpan();

        valueSpan[wpos] = (int)hashValue;
        if (headSpan[(int)hashValue] != -1)
        {
            chainSpan[wpos] = (uint)headSpan[(int)hashValue];
        }

        headSpan[(int)hashValue] = wpos;
        if (pos > 0 && dataSpan[pos] != dataSpan[pos - 1])
        {
            this.numZeros = 0;
        }

        this.numZeros = this.CountZeros(pos, this.numZeros);

        zerosSpan[wpos] = this.numZeros;
        if (headZSpan[(int)this.numZeros] != -1)
        {
            chainZSpan[wpos] = (uint)headZSpan[(int)this.numZeros];
        }

        headZSpan[(int)this.numZeros] = wpos;
    }

    public void Update(int pos, int len)
    {
        for (int i = 0; i < len; i++)
        {
            this.Update(pos + i);
        }
    }

    public void FindMatches(int pos, int maxDist, JxlLz77MatchFoundCallback foundMatch)
    {
        Span<uint> data = this.data.GetSpan();
        Span<uint> chain = this.chain.GetSpan();
        Span<uint> zeros = this.zeros.GetSpan();
        Span<uint> chainz = this.chainZ.GetSpan();
        Span<int> headz = this.headZ.GetSpan();
        Span<int> value = this.value.GetSpan();

        int wpos = pos & this.windowMask;
        uint hashValue = JxlLz77.GetHash(pos, data) & this.hashMask;
        uint hashPos = chain[wpos];

        int previousDistance = 0;
        int end = Math.Min(pos + this.maxLength, this.size);

        int chainLength = 0;
        int bestLength = 0;

        while (true)
        {
            int dist = hashPos <= wpos
                ? (wpos - (int)hashPos)
                : (wpos - (int)hashPos + this.windowMask + 1);

            if (dist < previousDistance)
            {
                break;
            }

            previousDistance = dist;
            uint len = 0;

            if (dist > 0)
            {
                int i = pos;
                int j = pos - dist;

                if (this.numZeros > 3)
                {
                    int r = (int)Math.Min(this.numZeros - 1, zeros[(int)hashPos]);

                    if (i + r >= end)
                    {
                        r = end - i - 1;
                    }

                    i += r;
                    j += r;
                }

                while (i < end && data[i] == data[j])
                {
                    i++;
                    j++;
                }

                len = (uint)(i - pos);

                // This can trigger even if the new length is slightly smaller than the
                // best length, since it's possible for a slightly cheaper distance
                // symbol to occur.
                if (len >= this.minLength && len + 2 >= bestLength)
                {
                    int distSymbol = this.specialDistTable.TryGetValue(dist, out int symbol)
                        ? symbol
                        : this.numSpecialDistances + dist - 1;

                    foundMatch(len, distSymbol);

                    if (len > bestLength)
                    {
                        bestLength = (int)len;
                    }
                }
            }

            chainLength++;
            if (chainLength >= this.maxChainLength)
            {
                break;
            }

            if (this.numZeros >= 3 && len > this.numZeros)
            {
                if (hashPos == chainz[(int)hashPos])
                {
                    break;
                }

                hashPos = chainz[(int)hashPos];
                if (zeros[(int)hashPos] != this.numZeros)
                {
                    break;
                }
            }
            else
            {
                if (hashPos == chain[(int)hashPos])
                {
                    break;
                }

                hashPos = chain[(int)hashPos];
                if (value[(int)hashPos] != (int)hashValue)
                {
                    // outdated hash value
                    break;
                }
            }
        }
    }

    public unsafe void FindMatch(int pos, int maxDist, int* resultDistSymbol, int* resultLength)
    {
        *resultDistSymbol = 0;
        *resultLength = 1;

        this.FindMatches(pos, maxDist, (len, distSymbol) =>
        {
            if (len > *resultLength ||
                (len == *resultLength && *resultDistSymbol > distSymbol))
            {
                *resultLength = (int)len;
                *resultDistSymbol = distSymbol;
            }
        });
    }

    public unsafe void FindMatch(int pos, int maxDist, ref int resultDistSymbol, ref int resultLength)
    {
        int* pDistSymbol = (int*)Unsafe.AsPointer(ref resultDistSymbol);
        int* pLength = (int*)Unsafe.AsPointer(ref resultLength);
        this.FindMatch(pos, maxDist, pDistSymbol, pLength);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal static class JxlAnsReader
{
    private const int WindowSize = 1 << 20;

    private const int NumSpecialDistances = 120;

    // Prefer jagged arrays over multidimensional arrays
    // for performance. Collection expressions help represent
    // jagged arrays easily.
    private static readonly byte[][] HuffmanLookup =
    [
        [3, 10], [7, 12], [3, 7], [4, 3], [3, 6], [3, 8], [3, 9], [4, 5],
        [3, 10], [4, 4],  [3, 7], [4, 1], [3, 6], [3, 8], [3, 9], [4, 2],
        [3, 10], [5, 0],  [3, 7], [4, 3], [3, 6], [3, 8], [3, 9], [4, 5],
        [3, 10], [4, 4],  [3, 7], [4, 1], [3, 6], [3, 8], [3, 9], [4, 2],
        [3, 10], [6, 11], [3, 7], [4, 3], [3, 6], [3, 8], [3, 9], [4, 5],
        [3, 10], [4, 4],  [3, 7], [4, 1], [3, 6], [3, 8], [3, 9], [4, 2],
        [3, 10], [5, 0],  [3, 7], [4, 3], [3, 6], [3, 8], [3, 9], [4, 5],
        [3, 10], [4, 4],  [3, 7], [4, 1], [3, 6], [3, 8], [3, 9], [4, 2],
        [3, 10], [7, 13], [3, 7], [4, 3], [3, 6], [3, 8], [3, 9], [4, 5],
        [3, 10], [4, 4],  [3, 7], [4, 1], [3, 6], [3, 8], [3, 9], [4, 2],
        [3, 10], [5, 0],  [3, 7], [4, 3], [3, 6], [3, 8], [3, 9], [4, 5],
        [3, 10], [4, 4],  [3, 7], [4, 1], [3, 6], [3, 8], [3, 9], [4, 2],
        [3, 10], [6, 11], [3, 7], [4, 3], [3, 6], [3, 8], [3, 9], [4, 5],
        [3, 10], [4, 4],  [3, 7], [4, 1], [3, 6], [3, 8], [3, 9], [4, 2],
        [3, 10], [5, 0],  [3, 7], [4, 3], [3, 6], [3, 8], [3, 9], [4, 5],
        [3, 10], [4, 4],  [3, 7], [4, 1], [3, 6], [3, 8], [3, 9], [4, 2],
    ];

    private static readonly sbyte[][] SpecialDistances =
    [
        [0, 1],  [1, 0],  [1, 1],  [-1, 1], [0, 2],  [2, 0],  [1, 2],  [-1, 2],
        [2, 1],  [-2, 1], [2, 2],  [-2, 2], [0, 3],  [3, 0],  [1, 3],  [-1, 3],
        [3, 1],  [-3, 1], [2, 3],  [-2, 3], [3, 2],  [-3, 2], [0, 4],  [4, 0],
        [1, 4],  [-1, 4], [4, 1],  [-4, 1], [3, 3],  [-3, 3], [2, 4],  [-2, 4],
        [4, 2],  [-4, 2], [0, 5],  [3, 4],  [-3, 4], [4, 3],  [-4, 3], [5, 0],
        [1, 5],  [-1, 5], [5, 1],  [-5, 1], [2, 5],  [-2, 5], [5, 2],  [-5, 2],
        [4, 4],  [-4, 4], [3, 5],  [-3, 5], [5, 3],  [-5, 3], [0, 6],  [6, 0],
        [1, 6],  [-1, 6], [6, 1],  [-6, 1], [2, 6],  [-2, 6], [6, 2],  [-6, 2],
        [4, 5],  [-4, 5], [5, 4],  [-5, 4], [3, 6],  [-3, 6], [6, 3],  [-6, 3],
        [0, 7],  [7, 0],  [1, 7],  [-1, 7], [5, 5],  [-5, 5], [7, 1],  [-7, 1],
        [4, 6],  [-4, 6], [6, 4],  [-6, 4], [2, 7],  [-2, 7], [7, 2],  [-7, 2],
        [3, 7],  [-3, 7], [7, 3],  [-7, 3], [5, 6],  [-5, 6], [6, 5],  [-6, 5],
        [8, 0],  [4, 7],  [-4, 7], [7, 4],  [-7, 4], [8, 1],  [8, 2],  [6, 6],
        [-6, 6], [8, 3],  [5, 7],  [-5, 7], [7, 5],  [-7, 5], [8, 4],  [6, 7],
        [-6, 7], [7, 6],  [-7, 6], [8, 5],  [7, 7],  [-7, 7], [8, 6],  [8, 7]
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SpecialDistance(int index, int multiplier)
    {
        Span<sbyte> indexDistance = SpecialDistances[index];
        int dist = indexDistance[0] + (multiplier * indexDistance[1]);
        return dist > 1 ? dist : 1;
    }

    public static uint DecodeVariableLengthUint8(JxlBitReader reader)
    {
        if (reader.ReadBoolean())
        {
            uint bitCount = reader.ReadBits32(3u);

            return bitCount == 0
                ? 1u
                : (reader.ReadBits32(bitCount) + (1u << (int)bitCount));
        }

        return 0u;
    }

    public static uint DecodeVariableLengthUint16(JxlBitReader reader)
    {
        if (reader.ReadBoolean())
        {
            uint bitCount = reader.ReadBits32(4u);

            return bitCount == 0
                ? 1u
                : (reader.ReadBits32(bitCount) + (1u << (int)bitCount));
        }

        return 0u;
    }

    // NOTE: this method returns null on failure.
    // If the return value is a valid IMemoryOwner object,
    // then it succeeded.
    public static IMemoryOwner<uint>? ReadHistogram(Configuration configuration, int precisionBits, JxlBitReader reader)
    {
        int range = 1 << precisionBits;
        bool isSimpleCode = reader.ReadBoolean();

        IMemoryOwner<uint> counts;

        if (isSimpleCode)
        {
            Span<uint> symbols = stackalloc uint[2];
            symbols.Clear();

            uint maxSymbol = 0u;
            uint symCount = reader.ReadBits32(1u) + 1u;
            for (uint i = 0; i < symCount; i++)
            {
                uint symbol = DecodeVariableLengthUint8(reader);
                if (symbol > maxSymbol)
                {
                    maxSymbol = symbol;
                }

                symbols[(int)i] = symbol;
            }

            // Up to 256 items
            counts = configuration.MemoryAllocator.Allocate<uint>((int)maxSymbol + 1);
            Span<uint> countsSpan = counts.Memory.Span;

            if (symCount == 1)
            {
                countsSpan[(int)symbols[0]] = (uint)range;
            }
            else
            {
                if (symbols[0] == symbols[1])
                {
                    counts.Dispose();
                    throw new InvalidOperationException("The data is corrupt");
                }

                countsSpan[(int)symbols[0]] = reader.ReadBits32((uint)precisionBits);
                countsSpan[(int)symbols[1]] = (uint)range - countsSpan[(int)symbols[0]];
            }
        }
        else
        {
            bool isFlat = reader.ReadBoolean();

            if (isFlat)
            {
                uint alphabetSize = DecodeVariableLengthUint8(reader) + 1u;
                if (alphabetSize <= range)
                {
                    return null;
                }

                counts = JxlAnsHelper.CreateFlatHistogram(configuration, (int)alphabetSize, range);
                return counts;
            }

            int upperBoundLog = JxlMath.FloorLog2Nonzero(JxlAnsConstants.AnsLogTableSize + 1);
            int log = 0;

            for (; log < upperBoundLog; log++)
            {
                bool logIncrementBit = reader.ReadBoolean();

                if (!logIncrementBit)
                {
                    break;
                }
            }

            uint shift = (reader.ReadBits32((uint)log) | (1u << log)) - 1u;

            if (shift > JxlAnsConstants.AnsLogTableSize + 1)
            {
                throw new InvalidOperationException("Invalid shift");
            }

            uint length = DecodeVariableLengthUint8(reader) + 3u;

            counts = configuration.MemoryAllocator.Allocate<uint>((int)length);
            Span<uint> countsSpan = counts.Memory.Span;

            uint totalCount = 0;

            // The length variable can represent up to 258 elements,
            // so it'd be more beneficial to allocate on the stack
            // than pool an array.
            Span<int> logCounts = stackalloc int[(int)length];

            // stackalloc doesn't zero-init, so clear just in case.
            logCounts.Clear();

            int omitLog = -1;
            int omitPos = -1;

            // See comments for logCounts definition
            Span<int> same = stackalloc int[(int)length];
            same.Clear();

            for (int i = 0; i < length; i++)
            {
                uint index = reader.PeekBits32(7);
                reader.SkipBits32(HuffmanLookup[index][0]);
                logCounts[i] = HuffmanLookup[index][1] - 1;

                if (logCounts[i] == JxlAnsConstants.AnsLogTableSize)
                {
                    uint rleLength = DecodeVariableLengthUint8(reader);
                    same[i] = (int)rleLength + 5;
                    i += (int)rleLength + 3;
                    continue;
                }

                if (logCounts[i] > omitLog)
                {
                    omitLog = logCounts[i];
                    omitPos = i;
                }
            }

            if (omitPos < 0)
            {
                counts.Dispose();
                throw new InvalidOperationException("The histogram is corrupt or invalid.");
            }

            if (omitPos + 1 < length && logCounts[omitPos + 1] == JxlAnsConstants.AnsLogTableSize)
            {
                counts.Dispose();
                throw new InvalidOperationException("The histogram is corrupt or invalid.");
            }

            int previous = 0;
            int sameCount = 0;

            for (int i = 0; i < length; i++)
            {
                if (same[i] > 0)
                {
                    sameCount = same[i] - 1;
                    previous = i > 0 ? (int)countsSpan[i - 1] : 0;
                }

                if (sameCount > 0)
                {
                    countsSpan[i] = (uint)previous;
                    sameCount--;
                }
                else
                {
                    int code = logCounts[i];

                    if (i == omitPos || code < 0)
                    {
                        continue;
                    }
                    else if (shift == 0 || code == 0)
                    {
                        countsSpan[i] = 1u << code;
                    }
                    else
                    {
                        int bitCount = JxlAnsHelper.GetPopulationCountPrecision(code, (int)shift);
                        countsSpan[i] = (1u << code) + (reader.ReadBits32((uint)bitCount) << (code - bitCount));
                    }
                }

                totalCount += countsSpan[i];
            }

            countsSpan[omitPos] = (uint)range - totalCount;

            if (countsSpan[omitPos] <= 0)
            {
                counts.Dispose();
                throw new InvalidOperationException("The histogram count is incorrect.");
            }
        }

        return counts;
    }
}

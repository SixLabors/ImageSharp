// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.IO;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;

internal static class JxlAnsReader
{
    public const int WindowSize = 1 << 20;

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

    /// <summary>
    /// Reads one normalized histogram and rejects malformed counts.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns the histogram storage.</param>
    /// <param name="precisionBits">The exponent of the required total frequency.</param>
    /// <param name="reader">The encoded histogram fields.</param>
    /// <returns>The histogram owner, which the caller must dispose.</returns>
    public static IMemoryOwner<uint> ReadHistogram(Configuration configuration, int precisionBits, JxlBitReader reader)
    {
        int range = 1 << precisionBits;
        bool isSimpleCode = reader.ReadBoolean();

        IMemoryOwner<uint>? counts = null;

        try
        {
            if (isSimpleCode)
            {
                Span<uint> symbols = [0, 0];

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

                // Only the encoded symbols receive frequencies. All gaps must remain zero when the allocator reuses storage.
                counts = configuration.MemoryAllocator.Allocate<uint>((int)maxSymbol + 1, AllocationOptions.Clean);
                Span<uint> countsSpan = counts.GetSpan();

                if (symCount == 1)
                {
                    countsSpan[(int)symbols[0]] = (uint)range;
                }
                else
                {
                    if (symbols[0] == symbols[1])
                    {
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
                    if (alphabetSize > range)
                    {
                        throw new InvalidOperationException("The histogram alphabet exceeds its frequency range.");
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

                // Zero-frequency symbols do not write a count. Clean storage also supplies the omitted count until it is reconstructed.
                counts = configuration.MemoryAllocator.Allocate<uint>((int)length, AllocationOptions.Clean);
                Span<uint> countsSpan = counts.GetSpan();

                uint totalCount = 0;

                // The encoded length is at most 258 symbols. Stack storage bounds this temporary table;
                // clear skipped RLE positions so their log-frequency flags have the same zero state as the native vector.
                Span<int> logCounts = stackalloc int[(int)length];
                logCounts.Clear();

                int omitLog = -1;
                int omitPos = -1;

                // Only RLE starts write a run length. All other entries must remain zero.
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
                    throw new InvalidOperationException("The histogram is corrupt or invalid.");
                }

                if (omitPos + 1 < length && logCounts[omitPos + 1] == JxlAnsConstants.AnsLogTableSize)
                {
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

                // The omitted symbol must retain at least one unit of frequency. Check before unsigned subtraction can wrap.
                if (totalCount >= (uint)range)
                {
                    throw new InvalidOperationException("The histogram count is incorrect.");
                }

                countsSpan[omitPos] = (uint)range - totalCount;
            }

            return counts;
        }
        catch
        {
            // Ownership reaches the caller only after every field is valid. Truncation and malformed fields must release pooled counts.
            counts?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads the prefix decoders or ANS alias tables for all mapped histograms.
    /// </summary>
    /// <param name="configuration">The configuration used for temporary histogram storage.</param>
    /// <param name="numHistograms">The histogram count established by the context map.</param>
    /// <param name="maxAlphabetSize">The largest symbol count allowed by the hybrid-integer configuration.</param>
    /// <param name="input">The encoded histogram fields.</param>
    /// <param name="result">The code that retains the decoded tables and symbol metadata.</param>
    public static void DecodeAnsCodes(Configuration configuration, int numHistograms, int maxAlphabetSize, JxlBitReader input, JxlAnsCode result)
    {
        // Set the logical length before taking a span. List capacity alone does not create writable elements.
        CollectionsMarshal.SetCount(result.DegenerateSymbols, numHistograms);
        CollectionsMarshal.AsSpan(result.DegenerateSymbols).Fill(-1);

        if (result.UsePrefixCode)
        {
            result.HuffmanData.Clear();
            result.HuffmanData.EnsureCapacity(numHistograms);
            Span<ushort> alphabetSizes = stackalloc ushort[numHistograms];

            for (int c = 0; c < numHistograms; c++)
            {
                // The encoded maximum plus one can equal 65536. Validate before narrowing to the table's ushort size.
                uint alphabetSize = DecodeVariableLengthUint16(input) + 1;

                if (alphabetSize > maxAlphabetSize)
                {
                    throw new InvalidOperationException($"Alphabet size is too long ({alphabetSize})");
                }

                alphabetSizes[c] = (ushort)alphabetSize;
            }

            for (int c = 0; c < numHistograms; c++)
            {
                // Each histogram needs a decoder instance; extending a list of references would only add null entries.
                JxlHuffmanDecoder decoder = new();
                result.HuffmanData.Add(decoder);

                if (alphabetSizes[c] > 1)
                {
                    if (!decoder.ReadFromBitStream(alphabetSizes[c], input))
                    {
                        throw new InvalidOperationException($"Invalid huffman tree number: {c}, alphabet size = {alphabetSizes[c]}");
                    }
                }
                else
                {
                    // 0-bit codes don't require extension tables
                    decoder.Table = new JxlHuffmanCode[1u << JxlHuffmanDecoder.HuffmanTableBits];
                }

                foreach (JxlHuffmanCode h in decoder.Table)
                {
                    if (h.Bits <= JxlHuffmanDecoder.HuffmanTableBits)
                    {
                        result.UpdateMaxNumBits(c, h.Value);
                    }
                }
            }
        }
        else
        {
            if (maxAlphabetSize > JxlAnsConstants.AnsMaxAlphabetSize)
            {
                throw new InvalidOperationException("Alphabet size overflows");
            }

            int size = numHistograms * (1 << result.LogAlphaSize);
            result.AliasTables = new JxlAnsEntry[size];

            Span<JxlAnsEntry> aliasTables = result.AliasTables.AsSpan();

            for (int c = 0; c < numHistograms; c++)
            {
                using IMemoryOwner<uint> countsMemory = ReadHistogram(configuration, JxlAnsConstants.AnsLogTableSize, input);

                Span<uint> counts = countsMemory.GetSpan();

                if (counts.Length > maxAlphabetSize)
                {
                    throw new InvalidOperationException("Alphabet size is too long");
                }

                while (counts.Length > 0 && counts[^1] == 0)
                {
                    counts = counts[..^1];
                }

                for (int s = 0; s < counts.Length; s++)
                {
                    if (counts[s] != 0)
                    {
                        result.UpdateMaxNumBits(c, s);
                    }
                }

                int degenerateSymbol = counts.Length == 0 ? 0 : counts.Length - 1;
                for (int s = 0; s < degenerateSymbol; ++s)
                {
                    if (counts[s] != 0)
                    {
                        degenerateSymbol = -1;
                        break;
                    }
                }

                result.DegenerateSymbols[c] = degenerateSymbol;

                if (!JxlAnsHelper.InitAliasTable(
                    counts,
                    JxlAnsConstants.AnsLogTableSize,
                    result.LogAlphaSize,
                    aliasTables[(c * (1 << result.LogAlphaSize))..]))
                {
                    throw new InvalidOperationException("Could not initialize alias table");
                }
            }
        }
    }

    public static void DecodeUIntConfiguration(int logAlphaSize, out JxlAnsHybridUIntConfiguration uintConfig, JxlBitReader br)
    {
        uint splitExponent = br.ReadBits32((uint)JxlMath.CeilLog2Nonzero(logAlphaSize + 1));
        uint msbInToken = 0;
        uint lsbInToken = 0;

        if (splitExponent != logAlphaSize)
        {
            uint numBits = JxlMath.CeilLog2Nonzero(splitExponent + 1);
            msbInToken = br.ReadBits32(numBits);

            if (msbInToken > splitExponent)
            {
                throw new InvalidOperationException("Invalid hybrid uint configuration");
            }

            numBits = JxlMath.CeilLog2Nonzero(splitExponent - msbInToken + 1);
            lsbInToken = br.ReadBits32(numBits);
        }

        if (lsbInToken + msbInToken > splitExponent)
        {
            throw new InvalidOperationException("Invalid hybrid uint configuration");
        }

        uintConfig = new(splitExponent, msbInToken, lsbInToken);
    }

    public static void DecodeUIntConfigurations(int logAlphaSize, Span<JxlAnsHybridUIntConfiguration> uintConfig, JxlBitReader br)
    {
        for (int i = 0; i < uintConfig.Length; i++)
        {
            DecodeUIntConfiguration(logAlphaSize, out JxlAnsHybridUIntConfiguration currentCfg, br);
            uintConfig[i] = currentCfg;
        }
    }

    /// <summary>
    /// Reads context mapping, hybrid-integer configurations, and their entropy tables.
    /// </summary>
    /// <param name="configuration">The configuration used for histogram storage.</param>
    /// <param name="br">The encoded entropy fields.</param>
    /// <param name="numContexts">The data-context count, before any LZ77 distance context.</param>
    /// <param name="code">The decoded entropy code.</param>
    /// <param name="contextMap">The map from data contexts to histograms.</param>
    /// <param name="disallowLz77">Whether the owning stream forbids LZ77 references.</param>
    public static void DecodeHistograms(
        Configuration configuration,
        JxlBitReader br,
        int numContexts,
        JxlAnsCode code,
        ref List<byte> contextMap,
        bool disallowLz77)
    {
        if (!JxlBundle.Read(br, code.Lz77))
        {
            throw new InvalidOperationException("Could not decode lz77 parameters");
        }

        if (code.Lz77.Enabled)
        {
            numContexts++;
            DecodeUIntConfiguration(8, out JxlAnsHybridUIntConfiguration cfg, br);
            code.Lz77.LengthUintConfig = cfg;
        }

        if (code.Lz77.Enabled && disallowLz77)
        {
            throw new InvalidOperationException("Using LZ77 even though it is explicitly disallowed");
        }

        int numHistograms = 1;
        CollectionsMarshal.SetCount(contextMap, numContexts);

        if (numContexts > 1)
        {
            JxlContextMapDecoder.DecodeContextMap(configuration, contextMap, ref numHistograms, br);
        }
        else
        {
            // A single context has no encoded map. It always selects histogram zero, including when a code object is reused.
            contextMap[0] = 0;
        }

        code.Lz77.NonserializedDistanceContext = contextMap[^1];
        code.UsePrefixCode = br.ReadBoolean();

        code.LogAlphaSize = code.UsePrefixCode
            ? (byte)JxlAnsConstants.PrefixMaxBits
            : (byte)(br.ReadBits32(2) + 5);

        CollectionsMarshal.SetCount(code.UIntConfig, numHistograms);

        DecodeUIntConfigurations(code.LogAlphaSize, CollectionsMarshal.AsSpan(code.UIntConfig), br);

        int maxAlphabetSize = 1 << code.LogAlphaSize;
        DecodeAnsCodes(configuration, numHistograms, maxAlphabetSize, br, code);
    }
}

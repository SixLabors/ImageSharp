// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Lz77;

/// <summary>
/// LZ77 Compression helper. <see href="https://en.wikipedia.org/wiki/LZ77_and_LZ78"/>
/// </summary>
internal static class JxlLz77
{
    public static List<JxlToken>[] Apply(
        Configuration configuration,
        JxlHistogramParameters parameters,
        int numContexts,
        List<JxlToken>[] tokens,
        JxlAnsLz77Parameters lz77)
        => parameters.Lz77Method switch
        {
            JxlLz77Method.Rle => ApplyRunLengthEncoding(configuration, parameters, numContexts, tokens, lz77),
            JxlLz77Method.Lz77b1w3f => ApplyLz77Default(parameters, numContexts, tokens, lz77, 1, 3, false),
            JxlLz77Method.Lz77b3w3f => ApplyLz77Default(parameters, numContexts, tokens, lz77, 3, 3, false),
            JxlLz77Method.Lz77b7w3f => ApplyLz77Default(parameters, numContexts, tokens, lz77, 7, 3, false),
            JxlLz77Method.Lz77b15w3f => ApplyLz77Default(parameters, numContexts, tokens, lz77, 15, 3, false),
            JxlLz77Method.Lz77b31w3f => ApplyLz77Default(parameters, numContexts, tokens, lz77, 31, 3, false),
            JxlLz77Method.Lz77b1w3t => ApplyLz77Default(parameters, numContexts, tokens, lz77, 1, 3, true),
            JxlLz77Method.Lz77b3w3t => ApplyLz77Default(parameters, numContexts, tokens, lz77, 3, 3, true),
            JxlLz77Method.Lz77b7w3t => ApplyLz77Default(parameters, numContexts, tokens, lz77, 7, 3, true),
            JxlLz77Method.Lz77b15w3t => ApplyLz77Default(parameters, numContexts, tokens, lz77, 15, 3, true),
            JxlLz77Method.Lz77b31w3t => ApplyLz77Default(parameters, numContexts, tokens, lz77, 31, 3, true),
            JxlLz77Method.Optc1 => ApplyOptimal(configuration, 1, parameters, numContexts, tokens, lz77),
            JxlLz77Method.Optc3 => ApplyOptimal(configuration, 3, parameters, numContexts, tokens, lz77),
            JxlLz77Method.Optc8 => ApplyOptimal(configuration, 8, parameters, numContexts, tokens, lz77),
            JxlLz77Method.Optc256 => ApplyOptimal(configuration, 256, parameters, numContexts, tokens, lz77),
            _ => [],
        };

    public static List<JxlToken>[] ApplyRunLengthEncoding(
        Configuration configuration,
        JxlHistogramParameters parameters,
        int numContexts,
        List<JxlToken>[] tokens,
        JxlAnsLz77Parameters lz77)
    {
        List<JxlToken>[] tokensLz77 = ArrayHelpers.CreateAndInitialize<List<JxlToken>>(tokens.Length, () => []);
        JxlLz77SymbolCostEstimator sce = new(numContexts, parameters.ForceHuffman, tokens, lz77);

        float bitDecrease = 0;
        int totalSymbols = 0;
        JxlAnsHybridUIntConfiguration uintConfig = new();

        for (int stream = 0; stream < tokens.Length; stream++)
        {
            float distanceMultiplier = parameters.ImageWidths.Count > stream
                ? parameters.ImageWidths[stream]
                : 0;

            List<JxlToken> input = tokens[stream];
            List<JxlToken> output = tokensLz77[stream];
            totalSymbols += input.Count;

            using IMemoryOwner<float> symbolCost = configuration.MemoryAllocator.Allocate<float>(input.Count + 1);
            Span<float> symbolCostSpan = symbolCost.GetSpan();

            for (int i = 0; i < input.Count; i++)
            {
                uintConfig.Encode(input[i].Value, out uint token, out uint numBits, out _);
                symbolCostSpan[i + 1] = sce.GetBits((int)input[i].Context, (int)token) + numBits + symbolCostSpan[i];
            }

            for (int i = 0; i < input.Count; i++)
            {
                int numToCopy = 0;
                int distanceSymbol = 0; // 1 for RLE.

                if (distanceMultiplier != 0)
                {
                    distanceSymbol = 1; // Special distance 1 if enabled.
                }

                if (i > 0)
                {
                    for (; i + numToCopy < input.Count; numToCopy++)
                    {
                        if (input[i + numToCopy].Value != input[i - 1].Value)
                        {
                            break;
                        }
                    }
                }

                if (numToCopy == 0)
                {
                    output.Add(input[i]);
                    continue;
                }

                float cost = symbolCostSpan[i + numToCopy] - symbolCostSpan[i];

                // This subtraction might overflow, but that's OK.
                int lz77Length = numToCopy - (int)lz77.MinimumLength;
                float lz77Cost = numToCopy >= lz77.MinimumLength
                            ? JxlMath.CeilLog2Nonzero(lz77Length + 1) + 1
                            : 0;

                if (numToCopy < lz77.MinimumLength || cost <= lz77Cost)
                {
                    for (int j = 0; j < numToCopy; j++)
                    {
                        output.Add(input[i + j]);
                    }

                    i += numToCopy - 1;
                    continue;
                }

                output.Add(new JxlToken(input[i].Context, (uint)lz77Length));
                output[^1] = output[^1] with
                {
                    IsLz77Length = true
                };

                i += numToCopy - 1;
                bitDecrease += cost - lz77Cost;

                // Output the LZ77 copy distance.
                output.Add(
                    new JxlToken(
                        (JxlMaTreeContext)lz77.NonserializedDistanceContext,
                        (uint)distanceSymbol));
            }
        }

        if (bitDecrease > (totalSymbols * 0.2f) + 16)
        {
            return tokensLz77;
        }

        return [];
    }

    private static List<JxlToken>[] ApplyLz77Default(
        JxlHistogramParameters parameters,
        int numContexts,
        List<JxlToken>[] tokens,
        JxlAnsLz77Parameters lz77,
        int bucketSize = 7,
        int hashSize = 3,
        bool runtimeCostComparison = false)
    {
        List<JxlToken>[] tokensLz77 = new List<JxlToken>[tokens.Length];
        JxlLz77SymbolCostEstimator sce = new(numContexts, parameters.ForceHuffman, tokens, lz77);
        float bitDecrease = 0;
        int totalSymbols = 0;
        JxlAnsHybridUIntConfiguration uintConfig = new();
        List<float> symCost = [];

        for (int stream = 0; stream < tokens.Length; stream++)
        {
            int distanceMultiplier = parameters.ImageWidths.Count > stream ? parameters.ImageWidths[stream] : 0;
            List<JxlToken> input = tokens[stream];
            List<JxlToken> output = tokensLz77[stream];

            totalSymbols += input.Count;
            List<uint> data = new(input.Count);

            symCost.Resize(input.Count + 1);
            for (int pos = 0; pos < input.Count; pos++)
            {
                uintConfig.Encode(input[pos].Value, out uint tok, out uint nbits, out _);
                symCost[pos + 1] = sce.GetBits((int)input[pos].Context, (int)tok) + nbits + symCost[pos];
                data[pos] = input[pos].Value;
            }

            _ = output.EnsureCapacity(input.Count);
            int mininumLength = (int)lz77.MinimumLength;

            if (mininumLength < 3)
            {
                throw new InvalidOperationException("Minimum length is below 3");
            }

            JxlLz77HashMap hashMap = new(data, distanceMultiplier, bucketSize, hashSize);

            for (int pos = 0; pos < input.Count; pos++)
            {
                output.Add(input[pos]);

                // Guard against reading past input bounds
                if (pos + hashSize >= input.Count)
                {
                    continue;
                }

                hashMap.FindMatch(pos, out int len, out int dist_symbol, mininumLength);

                if (len < mininumLength)
                {
                    continue;
                }

                // Bit cost comparison. Raw tokens vs LZ77 pair
                float cost = symCost[pos + len] - symCost[pos];
                int lz77Length = len - (int)lz77.MinimumLength;
                float lz77Cost = JxlLz77Costs.GetLengthCost(lz77Length) + JxlLz77Costs.GetDistanceCost(dist_symbol);

                if (runtimeCostComparison && lz77Cost > cost)
                {
                    for (int offset = 1; offset < len; offset++)
                    {
                        output.Add(input[pos + offset]);
                        hashMap.Update(pos + offset);
                    }

                    pos += len - 1;
                    continue;
                }

                bitDecrease += cost - lz77Cost - sce.GetAddSymbolCost((int)output[^1].Context);

                // Emit LZ77 length and distance tokens
                output[^1] = output[^1] with
                {
                    Value = (uint)(len - mininumLength),
                    IsLz77Length = true
                };

                output.Add(
                    new JxlToken(
                        (JxlMaTreeContext)lz77.NonserializedDistanceContext,
                        (uint)dist_symbol));

                // Update hash map for skipped positions inside the match
                for (int offset = 1; offset < len; offset++)
                {
                    hashMap.Update(pos + offset);
                }

                pos += len - 1;
            }
        }

        // Return LZ77 streams only if net bit savings justify overhead
        if (bitDecrease > (totalSymbols * 0.2f) + 16)
        {
            return tokensLz77;
        }

        return [];
    }

    public static List<JxlToken>[] ApplyOptimal(
        Configuration configuration,
        int maxChainLength,
        JxlHistogramParameters parameters,
        int numContexts,
        List<JxlToken>[] tokens,
        JxlAnsLz77Parameters lz77)
    {
        List<JxlToken>[] tokensForCostEstimate = ApplyLz77Default(parameters, numContexts, tokens, lz77);

        if (tokensForCostEstimate.Length == 0)
        {
            return [];
        }

        JxlLz77SymbolCostEstimator sce = new(numContexts + 1, parameters.ForceHuffman, tokensForCostEstimate, lz77);
        List<JxlToken>[] tokensLz77 = new List<JxlToken>[tokens.Length];

        JxlAnsHybridUIntConfiguration uintConfig = new();
        List<float> symbolCost = [];
        List<int> distanceSymbols = [];

        for (int stream = 0; stream < tokens.Length; stream++)
        {
            int distanceMultiplier = parameters.ImageWidths.Count > stream ? parameters.ImageWidths[stream] : 0;
            List<JxlToken> input = tokens[stream];
            List<JxlToken> output = tokensLz77[stream];

            symbolCost.Resize(input.Count + 1);

            for (int i = 0; i < input.Count; i++)
            {
                uintConfig.Encode(input[i].Value, out uint tok, out uint nbits, out uint unusedBits);
                symbolCost[i + 1] = sce.GetBits((int)input[i].Context, (int)tok) + nbits + symbolCost[i];
            }

            _ = output.EnsureCapacity(output.Count + input.Count);

            int maxDistance = input.Count;
            int minLength = (int)lz77.MinimumLength;

            if (minLength < 3)
            {
                throw new InvalidOperationException("Minimum length is below 3");
            }

            int maxLength = input.Count;
            int windowSize = 1;

            while (windowSize < maxDistance && windowSize < WindowSize)
            {
                windowSize <<= 1;
            }

            JxlLz77HashChain chain = new(configuration, maxChainLength, input, windowSize, minLength, maxLength, distanceMultiplier);

            static MatchInfo CreateMatchInfo() => new()
            {
                Length = 0,
                DistanceSymbol = 0,
                Context = 0,
                TotalCost = float.MaxValue
            };

            MatchInfo[] prefixCosts = new MatchInfo[input.Count + 1];

            for (int i = 0; i < prefixCosts.Length; i++)
            {
                prefixCosts[i] = CreateMatchInfo();
            }

            prefixCosts[0].TotalCost = 0;

            int rleLength = 0;
            int skipLz77 = 0;

            for (int i = 0; i < input.Count; i++)
            {
                chain.Update(i);

                float litCost = prefixCosts[i].TotalCost + symbolCost[i + 1] - symCost[i];

                if (prefixCosts[i + 1].TotalCost > litCost)
                {
                    prefixCosts[i + 1].DistanceSymbol = 0;
                    prefixCosts[i + 1].Length = 1;
                    prefixCosts[i + 1].Context = (int)input[i].Context;
                    prefixCosts[i + 1].TotalCost = litCost;
                }

                if (skipLz77 > 0)
                {
                    skipLz77--;
                    continue;
                }

                distanceSymbols.Clear();

                chain.FindMatches(i, maxDistance, (uint ulen, int distSymbol) =>
                {
                    int len = (int)ulen;
                    if (distanceSymbols.Count <= len)
                    {
                        int oldCount = distanceSymbols.Count;
                        distanceSymbols.Resize(len + 1);

                        for (int j = oldCount; j < distanceSymbols.Count; j++)
                        {
                            distanceSymbols[j] = distSymbol;
                        }
                    }

                    if (distSymbol < distanceSymbols[len])
                    {
                        distanceSymbols[len] = distSymbol;
                    }
                });

                if (distanceSymbols.Count <= minLength)
                {
                    continue;
                }

                {
                    int bestCost = distanceSymbols[^1];

                    for (int j = distanceSymbols.Count - 1; j >= minLength; j--)
                    {
                        if (distanceSymbols[j] < bestCost)
                        {
                            bestCost = distanceSymbols[j];
                        }

                        distanceSymbols[j] = bestCost;
                    }
                }

                for (int j = minLength; j < distanceSymbols.Count; j++)
                {
                    float lz77Cost = sce.GetLengthCost((int)input[i].Context, j - minLength, lz77) + sce.GetDistanceCost(distanceSymbols[j], lz77);
                    float cost = prefixCosts[i].TotalCost + lz77Cost;

                    if (prefixCosts[i + j].TotalCost > cost)
                    {
                        prefixCosts[i + j].Length = j;
                        prefixCosts[i + j].DistanceSymbol = distanceSymbols[j] + 1;
                        prefixCosts[i + j].Context = (int)input[i].Context;
                        prefixCosts[i + j].TotalCost = cost;
                    }
                }

                // Skip most symbols in long RLE sequences to avoid quadratic costs.
                if ((distanceSymbols[^1] == 0 && distanceMultiplier == 0) || (distanceSymbols[^1] == 1 && distanceMultiplier != 0))
                {
                    rleLength++;
                }
                else
                {
                    rleLength = 0;
                }

                if (rleLength >= 8 && distanceSymbols.Count > 9)
                {
                    skipLz77 = distanceSymbols.Count - 10;
                    rleLength = 0;
                }
            }

            int pos = input.Count;

            while (pos > 0)
            {
                bool isLz77Length = prefixCosts[pos].DistanceSymbol != 0;

                if (isLz77Length)
                {
                    int distSymbol = prefixCosts[pos].DistanceSymbol - 1;
                    output.Add(new JxlToken((JxlMaTreeContext)lz77.NonserializedDistanceContext, (uint)distSymbol));
                }

                uint val = isLz77Length
                    ? (uint)(prefixCosts[pos].Length - minLength)
                    : input[pos - 1].Value;

                output.Add(new JxlToken((JxlMaTreeContext)prefixCosts[pos].Context, val));
                output[^1] = output[^1] with
                {
                    IsLz77Length = isLz77Length
                };

                pos -= prefixCosts[pos].Length;
            }

            output.Reverse();
        }

        return tokensLz77;
    }

    /// <summary>
    /// Computes a Murmur-style mix hash.
    /// </summary>
    /// <param name="hashSize">
    /// Number of items to compute the hash for.
    /// </param>
    /// <param name="pos">
    /// The offset of the first item.
    /// </param>
    /// <param name="data">
    /// Actual values to find the hash.
    /// </param>
    /// <returns>
    /// The 32-bit hash
    /// </returns>
    public static uint GetHash(int hashSize, int pos, ReadOnlySpan<uint> data)
    {
        if (pos + hashSize <= data.Length)
        {
            // do the hash at offset 'pos', 'hashSize' times
            uint h = 0;

            for (int i = 0; i < hashSize; i++)
            {
                h ^= data[pos + i] + 0x9e3779b9 + (h << 6) + (h >> 2);
            }

            h ^= h >> 16;
            h *= 0x85ebca6bu;
            h ^= h >> 13;
            h *= 0xc2b2ae35u;
            h ^= h >> 16;

            return h;
        }
        else
        {
            // data is out of bounds
            return 0;
        }
    }

    /// <summary>
    /// Computes a Murmur-style mix hash.
    /// </summary>
    /// <param name="hashSize">
    /// Number of items to compute the hash for.
    /// </param>
    /// <param name="pos">
    /// The offset of the first item.
    /// </param>
    /// <param name="data">
    /// Actual values to find the hash.
    /// </param>
    /// <returns>
    /// The 32-bit hash
    /// </returns>
    public static uint GetHash(int hashSize, int pos, List<uint> data)
        => GetHash(hashSize, pos, CollectionsMarshal.AsSpan(data));

    private struct MatchInfo()
    {
        public int Length { get; set; }

        public int DistanceSymbol { get; set; }

        public int Context { get; set; }

        public float TotalCost { get; set; } = float.MaxValue;
    }
}

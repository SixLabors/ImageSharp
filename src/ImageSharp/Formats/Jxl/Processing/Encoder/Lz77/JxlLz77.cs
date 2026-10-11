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
    public static List<JxlToken>[] ApplyRunLengthEncoding(
        Configuration configuration,
        JxlHistogramParameters parameters,
        int numContexts,
        List<List<JxlToken>> tokens,
        JxlAnsLz77Parameters lz77)
    {
        List<JxlToken>[] tokensLz77 = ArrayHelpers.CreateAndInitialize<List<JxlToken>>(tokens.Count, () => []);
        JxlLz77SymbolCostEstimator sce = new(numContexts, parameters.ForceHuffman, tokens, lz77);

        float bitDecrease = 0;
        int totalSymbols = 0;
        JxlAnsHybridUIntConfiguration uintConfig = new();

        for (int stream = 0; stream < tokens.Count; stream++)
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
}

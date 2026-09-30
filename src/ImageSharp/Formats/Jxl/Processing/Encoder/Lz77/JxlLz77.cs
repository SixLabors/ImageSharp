// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Lz77;

/// <summary>
/// LZ77 Compression helper. <see href="https://en.wikipedia.org/wiki/LZ77_and_LZ78"/>
/// </summary>
internal static class JxlLz77
{
    public static List<List<JxlToken>> ApplyRunLengthEncoding(
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
                symbolCost
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Lz77;

internal sealed class JxlLz77SymbolCostEstimator
{
    private readonly int maxAlphabetSize;
    private readonly float[] bits;
    private readonly float[] addSymbolCost;

    public JxlLz77SymbolCostEstimator(int numContexts, bool forceHuffman, List<List<JxlToken>> tokens, JxlAnsLz77Parameters lz77)
    {
        JxlHistogram[] builder = ArrayPool<JxlHistogram>.Shared.Rent(numContexts);

        for (int i = 0; i < numContexts; i++)
        {
            builder[i] = new(numContexts);
        }

        // build histograms for estimating lz77 savings
        JxlAnsHybridUIntConfiguration uintConfig = new();

        foreach (List<JxlToken> stream in tokens)
        {
            foreach (JxlToken token in stream)
            {
                uint tokenValue;
                uint numBits;
                uint bits;

                if (token.IsLz77Length)
                {
                    lz77.LengthUintConfig.Encode(token.Value, out tokenValue, out numBits, out bits);
                }
                else
                {
                    uintConfig.Encode(token.Value, out tokenValue, out numBits, out bits);
                }

                tokenValue += token.IsLz77Length ? lz77.MinimumSymbol : 0;

                if ((int)token.Context >= numContexts)
                {
                    throw new InvalidOperationException("Context is out of bounds");
                }

                builder[(int)token.Context].Add((int)tokenValue);
            }
        }

        this.maxAlphabetSize = 0;

        for (int i = 0; i < numContexts; i++)
        {
            this.maxAlphabetSize = Math.Max(this.maxAlphabetSize, builder[i].Counts.Count);
        }

        this.bits = new float[numContexts * this.maxAlphabetSize];
        this.addSymbolCost = new float[numContexts];

        for (int i = 0; i < numContexts; i++)
        {
            float inv_total = 1.0f / (builder[i].TotalCount + 1e-8f);
            float total_cost = 0;

            for (int j = 0; j < builder[i].Counts.Count; j++)
            {
                int cnt = builder[i].Counts[j];
                float cost = 0;
                if (cnt != 0 && cnt != builder[i].TotalCount)
                {
                    cost = -MathF.Log2(cnt * inv_total);
                    if (forceHuffman)
                    {
                        cost = MathF.Ceiling(cost);
                    }
                }
                else if (cnt == 0)
                {
                    cost = JxlAnsConstants.AnsLogTableSize; // Highest possible cost.
                }

                this.bits[(i * this.maxAlphabetSize) + j] = cost;
                total_cost += cost * builder[i].Counts[j];
            }

            // Penalty for adding a lz77 symbol to this contest (only used for static
            // cost model). Higher penalty for contexts that have a very low
            // per-symbol entropy.
            this.addSymbolCost[i] = MathF.Max(0.0f, 6.0f - (total_cost * inv_total));
        }
    }

    public float GetBits(int ctx, int symbol) => this.bits[(ctx * this.maxAlphabetSize) + symbol];

    public float GetLengthCost(int context, int length, JxlAnsLz77Parameters parameters)
    {
        parameters.LengthUintConfig.Encode((uint)length, out uint token, out uint numBits, out uint bits);
        token += parameters.MinimumSymbol;
        return numBits + this.GetBits(context, (int)token);
    }

    public float GetDistanceCost(int length, JxlAnsLz77Parameters parameters)
    {
        parameters.LengthUintConfig.Encode((uint)length, out uint token, out uint numBits, out uint bits);
        return numBits + this.GetBits(parameters.NonserializedDistanceContext, (int)token);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetAddSymbolCost(int index) => this.addSymbolCost[index];
}

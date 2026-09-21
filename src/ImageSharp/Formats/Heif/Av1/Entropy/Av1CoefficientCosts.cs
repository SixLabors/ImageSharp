// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Stores coefficient rates for one encoder cost-update interval.
/// </summary>
internal readonly ref struct Av1CoefficientCosts
{
    private const int TransformSizeCount = 5;
    private const int PlaneCount = 2;
    private const int SkipContextCount = 13;
    private const int BaseEndOfBlockContextCount = 4;
    private const int BaseContextCount = 42;
    private const int ExtraContextCount = 9;
    private const int SignContextCount = 3;
    private const int RangeContextCount = 21;
    private const int RangeLength = 13;
    private const int BaseEndOfBlockOffset = SkipContextCount * 2;
    public const int BaseOffset = BaseEndOfBlockOffset + (BaseEndOfBlockContextCount * 3);
    private const int ExtraOffset = BaseOffset + (BaseContextCount * 8);
    private const int SignOffset = ExtraOffset + (ExtraContextCount * 2);
    public const int RangeOffset = SignOffset + (SignContextCount * 2);
    private const int PlaneLength = RangeOffset + (RangeContextCount * RangeLength * 2);
    private const int EndOfBlockOffset = TransformSizeCount * PlaneCount * PlaneLength;
    private const int EndOfBlockSizeCount = 7;
    private const int EndOfBlockClassCount = 2;
    private const int EndOfBlockAlphabetSize = 11;

    /// <summary>
    /// The number of integer rate entries.
    /// </summary>
    public const int StorageLength = EndOfBlockOffset + (EndOfBlockSizeCount * PlaneCount * EndOfBlockClassCount * EndOfBlockAlphabetSize);

    private readonly Span<int> costs;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1CoefficientCosts"/> struct.
    /// </summary>
    /// <param name="costs">The rate storage.</param>
    public Av1CoefficientCosts(Span<int> costs) => this.costs = costs;

    /// <summary>
    /// Updates the coefficient rates from the supplied probabilities.
    /// </summary>
    /// <param name="context">The entropy context.</param>
    public void Update(Av1FrameEntropyContext context)
    {
        // This scratch holds the four symbols for one range distribution. It is reused for every
        // context and never allocated inside coefficient or candidate traversal.
        Span<int> rangeRates = stackalloc int[4];
        for (int size = 0; size < TransformSizeCount; size++)
        {
            for (int plane = 0; plane < PlaneCount; plane++)
            {
                Span<int> destination = this.costs.Slice(((size * PlaneCount) + plane) * PlaneLength, PlaneLength);
                for (int ctx = 0; ctx < SkipContextCount; ctx++)
                {
                    Av1ProbabilityCost.FillSymbolCosts(context.TransformBlockSkip[size][ctx], destination.Slice(ctx * 2, 2));
                }

                for (int ctx = 0; ctx < BaseEndOfBlockContextCount; ctx++)
                {
                    Av1ProbabilityCost.FillSymbolCosts(context.BaseEndOfBlock[size][plane][ctx], destination.Slice(BaseEndOfBlockOffset + (ctx * 3), 3));
                }

                for (int ctx = 0; ctx < BaseContextCount; ctx++)
                {
                    Span<int> row = destination.Slice(BaseOffset + (ctx * 8), 8);
                    Av1ProbabilityCost.FillSymbolCosts(context.CoefficientsBase[size][plane][ctx], row);

                    // The second half records the rate change when a level is reduced by one.
                    // Reducing level one also removes its sign bit.
                    row[4] = 0;
                    row[5] = row[1] + Av1ProbabilityCost.GetLiteralCost(1) - row[0];
                    row[6] = row[2] - row[1];
                    row[7] = row[3] - row[2];
                }

                for (int ctx = 0; ctx < ExtraContextCount; ctx++)
                {
                    // Tokens zero through two have no context-coded suffix bit.
                    Av1ProbabilityCost.FillSymbolCosts(context.EndOfBlockExtra[size][plane][ctx + 3], destination.Slice(ExtraOffset + (ctx * 2), 2));
                }

                for (int ctx = 0; ctx < SignContextCount; ctx++)
                {
                    Av1ProbabilityCost.FillSymbolCosts(context.DcSign[plane][ctx], destination.Slice(SignOffset + (ctx * 2), 2));
                }

                for (int ctx = 0; ctx < RangeContextCount; ctx++)
                {
                    Av1ProbabilityCost.FillSymbolCosts(context.CoefficientsBaseRange[Math.Min(size, 3)][plane][ctx], rangeRates);
                    Span<int> row = destination.Slice(RangeOffset + (ctx * RangeLength * 2), RangeLength * 2);
                    int accumulated = 0;
                    for (int level = 0; level < RangeLength - 1; level += 3)
                    {
                        for (int remainder = 0; remainder < 3; remainder++)
                        {
                            row[level + remainder] = accumulated + rangeRates[remainder];
                        }

                        accumulated += rangeRates[3];
                    }

                    // The escape consumes four continuation symbols without a terminating remainder.
                    // The second half stores adjacent-level differences for coefficient refinement.
                    row[RangeLength - 1] = accumulated;
                    row[RangeLength] = row[0];
                    for (int level = 1; level < RangeLength; level++)
                    {
                        row[RangeLength + level] = row[level] - row[level - 1];
                    }
                }
            }
        }

        for (int size = 0; size < EndOfBlockSizeCount; size++)
        {
            for (int plane = 0; plane < PlaneCount; plane++)
            {
                for (int cls = 0; cls < EndOfBlockClassCount; cls++)
                {
                    int offset = EndOfBlockOffset + (((((size * PlaneCount) + plane) * EndOfBlockClassCount) + cls) * EndOfBlockAlphabetSize);
                    Av1ProbabilityCost.FillSymbolCosts(context.EndOfBlockFlag[size][plane][cls], this.costs.Slice(offset, EndOfBlockAlphabetSize));
                }
            }
        }
    }

    /// <summary>
    /// Gets the coefficient rates for a transform size and component plane.
    /// </summary>
    /// <param name="size">The square transform-size context.</param>
    /// <param name="plane">The luma or chroma plane index.</param>
    /// <returns>The coefficient rate entries.</returns>
    public ReadOnlySpan<int> GetPlane(int size, int plane) => this.costs.Slice(((size * PlaneCount) + plane) * PlaneLength, PlaneLength);

    /// <summary>
    /// Gets an end-of-block token rate.
    /// </summary>
    /// <param name="size">The transform area context.</param>
    /// <param name="plane">The luma or chroma plane index.</param>
    /// <param name="cls">The transform class context.</param>
    /// <param name="symbol">The token symbol.</param>
    /// <returns>The rate in 1/512-bit units.</returns>
    public int GetEndOfBlock(int size, int plane, int cls, int symbol)
        => this.costs[EndOfBlockOffset + (((((size * PlaneCount) + plane) * EndOfBlockClassCount) + cls) * EndOfBlockAlphabetSize) + symbol];

    /// <summary>
    /// Gets a skip rate.
    /// </summary>
    /// <param name="plane">The coefficient rate entries.</param>
    /// <param name="context">The symbol context.</param>
    /// <param name="symbol">The symbol index.</param>
    /// <returns>The rate in 1/512-bit units.</returns>
    public static int GetSkip(ReadOnlySpan<int> plane, int context, int symbol) => plane[0 + (context * 2) + symbol];

    /// <summary>
    /// Gets a base end of block rate.
    /// </summary>
    /// <param name="plane">The coefficient rate entries.</param>
    /// <param name="context">The symbol context.</param>
    /// <param name="symbol">The symbol index.</param>
    /// <returns>The rate in 1/512-bit units.</returns>
    public static int GetBaseEndOfBlock(ReadOnlySpan<int> plane, int context, int symbol) => plane[BaseEndOfBlockOffset + (context * 3) + symbol];

    /// <summary>
    /// Gets a base rate.
    /// </summary>
    /// <param name="plane">The coefficient rate entries.</param>
    /// <param name="context">The symbol context.</param>
    /// <param name="symbol">The symbol index.</param>
    /// <returns>The rate in 1/512-bit units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBase(ReadOnlySpan<int> plane, int context, int symbol) => plane[BaseOffset + (context * 8) + symbol];

    /// <summary>
    /// Gets a extra rate.
    /// </summary>
    /// <param name="plane">The coefficient rate entries.</param>
    /// <param name="context">The symbol context.</param>
    /// <param name="symbol">The symbol index.</param>
    /// <returns>The rate in 1/512-bit units.</returns>
    public static int GetExtra(ReadOnlySpan<int> plane, int context, int symbol) => plane[ExtraOffset + (context * 2) + symbol];

    /// <summary>
    /// Gets a sign rate.
    /// </summary>
    /// <param name="plane">The coefficient rate entries.</param>
    /// <param name="context">The symbol context.</param>
    /// <param name="symbol">The symbol index.</param>
    /// <returns>The rate in 1/512-bit units.</returns>
    public static int GetSign(ReadOnlySpan<int> plane, int context, int symbol) => plane[SignOffset + (context * 2) + symbol];

    /// <summary>
    /// Gets a range rate.
    /// </summary>
    /// <param name="plane">The coefficient rate entries.</param>
    /// <param name="context">The symbol context.</param>
    /// <param name="symbol">The symbol index.</param>
    /// <returns>The rate in 1/512-bit units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRange(ReadOnlySpan<int> plane, int context, int symbol) => plane[RangeOffset + (context * 26) + symbol];
}

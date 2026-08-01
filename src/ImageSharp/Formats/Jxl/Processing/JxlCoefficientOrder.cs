// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Security.Principal;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Helps with reordering coefficients.
/// </summary>
internal static class JxlCoefficientOrder
{
    public const int Limit = 6156;

    public const int CoefficientOrderMaxSize = Limit * JxlFrameDimensions.DctBlockSize;

    public const int PermutationContexts = 8;

    /// <summary>
    /// Gets the pattern which coefficients must follow to compute offsets.
    /// </summary>
    public static ReadOnlySpan<int> CoefficientOrderOffsets =>
    [
        0,    1,    2,    3,    4,    5,    6,    10,   14,   18,
        34,   50,   66,   68,   70,   72,   76,   80,   84,   92,
        100,  108,  172,  236,  300,  332,  364,  396,  652,  908,
        1164, 1292, 1420, 1548, 2572, 3596, 4620, 5132, 5644, Limit
    ];

    /// <summary>
    /// Gets the pattern which coefficients must follow to compute offsets.
    /// </summary>
    public static ReadOnlySpan<byte> StrategyOrder =>
    [
        0, 1, 1, 1, 2, 3, 4, 4, 5,  5,  6,  6,  1,  1,
        1, 1, 1, 1, 7, 8, 8, 9, 10, 10, 11, 12, 12,
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CoeffOrderOffset(int o, int c) => CoefficientOrderOffsets[(3 * o) + c] * JxlFrameDimensions.DctBlockSize;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint CoeffOrderContext(uint value)
    {
        uint token = 0;
        uint nbits = 0;
        uint bits = 0;

        new JxlAnsHybridUIntConfiguration(0, 0, 0).Encode(value, ref token, ref nbits, ref bits);

        return Math.Min(token, PermutationContexts - 1u);
    }

    public static bool ReadPermutation(int skip, int size, Span<int> order, JxlBitReader bitReader, JxlAnsSymbolReader reader, Span<byte> contextMap)
    {
        Span<uint> lehmer = stackalloc uint[size];
        lehmer.Clear();

        Span<uint> temp = stackalloc uint[size * 2];
        temp.Clear();

        uint end = reader.ReadHybridUnsignedInteger(CoeffOrderContext((int)size), bitReader, contextMap) + skip;

        if (end > size)
        {
            throw new InvalidOperationException("Invalid permutation size");
        }

        uint last = 0;

        for (int i = skip; i < end; i++)
        {
            lehmer[i] = reader.ReadHybridUnsignedInteger(CoeffOrderContext(last), bitReader, contextMap);
            last = lehmer[i];
            if (lehmer[i] >= size - i)
            {
                throw new InvalidOperationException("Invalid lehmer code");
            }
        }

        if (order.IsEmpty)
        {
            return true;
        }

        return JxlLehmerCode.DecodeLehmerCode(lehmer, temp, size, order);
    }
}

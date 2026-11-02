// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

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
    public static int CoeffOrderOffset(int o, int c) => CoefficientOrderOffsets[(3 * o) + c] * JxlFrameDimensions.DctBlockSize;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint CoeffOrderContext(uint value)
    {
        new JxlAnsHybridUIntConfiguration(0, 0, 0).Encode(value, out uint token, out uint nbits, out uint bits);

        return Math.Min(token, PermutationContexts - 1u);
    }

    /// <summary>
    /// Reads a complete permutation stream, including its entropy tables and final state.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns entropy history and permutation scratch.</param>
    /// <param name="skip">The leading entries that keep their original order.</param>
    /// <param name="size">The permutation length, already limited by the owning field.</param>
    /// <param name="order">The destination permutation.</param>
    /// <param name="bitReader">The reader at the permutation's entropy header.</param>
    /// <returns>Whether the permutation and final entropy state are valid.</returns>
    public static bool DecodePermutation(Configuration configuration, int skip, int size, Span<uint> order, JxlBitReader bitReader)
    {
        JxlAnsCode code = new();
        List<byte> contextMap = [];
        JxlAnsReader.DecodeHistograms(configuration, bitReader, PermutationContexts, code, ref contextMap, false);
        using JxlAnsSymbolReader reader = JxlAnsSymbolReader.Create(configuration, code, bitReader, 0);
        ReadOnlySpan<byte> contexts = CollectionsMarshal.AsSpan(contextMap);

        return ReadPermutation(configuration, skip, size, order, bitReader, reader, contexts) && reader.CheckAnsFinalState();
    }

    /// <summary>
    /// Reads a coefficient permutation from an initialized entropy stream.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns permutation scratch.</param>
    /// <param name="skip">The leading coefficients that keep their original order.</param>
    /// <param name="size">The coefficient count.</param>
    /// <param name="order">The destination permutation, or an empty span when only validation is required.</param>
    /// <param name="bitReader">The encoded permutation fields.</param>
    /// <param name="reader">The caller-owned entropy reader.</param>
    /// <param name="contextMap">The stable mapping from permutation contexts to histograms.</param>
    /// <returns>Whether the Lehmer code describes a valid permutation.</returns>
    public static bool ReadPermutation(
        Configuration configuration,
        int skip,
        int size,
        Span<uint> order,
        JxlBitReader bitReader,
        JxlAnsSymbolReader reader,
        ReadOnlySpan<byte> contextMap)
    {
        DebugGuard.MustBeLessThanOrEqualTo(size, 65536, nameof(size));

        // Borrow the reader's window once; all coefficient symbols advance the same entropy and LZ77 state.
        Span<int> window = reader.Lz77Window;
        uint encodedEnd = unchecked((uint)reader.ReadHybridUInt((int)CoeffOrderContext((uint)size), bitReader, contextMap, window));
        if (encodedEnd > (uint)(size - skip))
        {
            return false;
        }

        int end = skip + (int)encodedEnd;

        // The largest TOC has 65,536 entries. Keep its Lehmer codes and tree scratch in one allocator-owned buffer, not on the stack.
        // Omitted Lehmer entries mean zero. The tree decoder initializes its own next-power-of-two workspace.
        using IMemoryOwner<uint> storage = configuration.MemoryAllocator.Allocate<uint>(size * 3);
        Span<uint> scratch = storage.Memory.Span;
        Span<uint> lehmer = scratch[..size];
        Span<uint> temp = scratch[size..];
        lehmer.Clear();

        uint last = 0;

        for (int i = skip; i < end; i++)
        {
            lehmer[i] = unchecked((uint)reader.ReadHybridUInt((int)CoeffOrderContext(last), bitReader, contextMap, window));
            last = lehmer[i];
            if (lehmer[i] >= size - i)
            {
                return false;
            }
        }

        if (order.IsEmpty)
        {
            return true;
        }

        return JxlLehmerCode.DecodeLehmerCode(lehmer, temp, size, order);
    }
}

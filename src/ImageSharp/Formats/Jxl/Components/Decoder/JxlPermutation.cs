// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Decodes the Lehmer permutations shared by frame sections and VarDCT coefficient orders.
/// </summary>
internal static class JxlPermutation
{
    /// <summary>
    /// The number of entropy contexts shared by permutation lengths and Lehmer digits.
    /// </summary>
    public const int ContextCount = 8;

    // Poll cancellation at loop indices divisible by 1024, rather than reading the token for every entry.
    // Rank initialization and digit decoding count their own entries. The implicit tail uses the output index.
    // Each periodic check therefore measures loop positions, not bytes read or elapsed time.
    // Keep this value a power of two: the loops select check positions with index & (interval - 1).
    // This value controls cancellation responsiveness. JPEG XL does not prescribe it, and no benchmark determined it.
    private const int CancellationInterval = 1024;

    /// <summary>
    /// Reads and validates the encoded number of Lehmer digits without resetting the shared entropy history.
    /// </summary>
    /// <param name="reader">The bounded permutation bit reader.</param>
    /// <param name="symbols">The entropy reader retained across related permutations.</param>
    /// <param name="size">The complete positive permutation length.</param>
    /// <param name="skip">The fixed leading prefix, excluded from the encoded permutation.</param>
    /// <param name="history">The shared circular history, or empty before the first value or without LZ77.</param>
    /// <param name="position">The number of values already decoded from this entropy stream, updated after the length.</param>
    /// <returns>The encoded digit count, at most <paramref name="size"/> minus <paramref name="skip"/>.</returns>
    public static uint ReadLength(
        ref JxlBitReader reader,
        ref JxlEntropyDecoder.Reader symbols,
        int size,
        int skip,
        Span<uint> history,
        ref uint position)
    {
        uint length = symbols.ReadUInt32(ref reader, GetContext((uint)size), history, position++);
        if (length > (uint)(size - skip))
        {
            throw new InvalidImageContentException("The JPEG XL permutation exceeds its table size.");
        }

        return length;
    }

    /// <summary>
    /// Reads validated Lehmer digits and optionally writes their mapped permutation using caller-owned rank storage.
    /// </summary>
    /// <param name="reader">The bounded permutation bit reader.</param>
    /// <param name="symbols">The entropy reader retained across related permutations.</param>
    /// <param name="size">The complete permutation length.</param>
    /// <param name="skip">The fixed leading prefix.</param>
    /// <param name="length">The digit count returned by <see cref="ReadLength"/>.</param>
    /// <param name="mapping">The complete natural coefficient order, or empty for a nonidentity section permutation.</param>
    /// <param name="destination">The complete output order, or empty when only validation is required.</param>
    /// <param name="ranks">Space for size minus skip Fenwick counts when retaining decoded digits. Unused for zero digits or discarded output.</param>
    /// <param name="history">The shared circular symbol history, or empty without LZ77.</param>
    /// <param name="position">The number of entropy values already decoded, updated for each digit.</param>
    /// <param name="cancellationToken">The token checked during large rank-table and digit traversals.</param>
    public static void ReadDigits(
        ref JxlBitReader reader,
        ref JxlEntropyDecoder.Reader symbols,
        int size,
        int skip,
        uint length,
        ReadOnlySpan<uint> mapping,
        Span<uint> destination,
        Span<uint> ranks,
        Span<uint> history,
        ref uint position,
        CancellationToken cancellationToken)
    {
        // A zero digit count retains the natural coefficient order. Section identities return before this method,
        // so a retained output here always has its complete natural mapping. Unused families have empty output.
        if (length == 0)
        {
            if (!destination.IsEmpty)
            {
                mapping.CopyTo(destination);
            }

            return;
        }

        uint highestBit = 0;
        if (!destination.IsEmpty)
        {
            // The prefix is fixed before permutation. Coefficient orders map that prefix through their natural order;
            // section orders instead use direct indices. Only the remaining positions participate in rank selection.
            for (int i = 0; i < skip; i++)
            {
                destination[i] = mapping.IsEmpty ? (uint)i : mapping[i];
            }

            ranks = ranks[..(size - skip)];
            highestBit = 1U << BitOperations.Log2((uint)ranks.Length);
            for (int i = 0; i < ranks.Length; i++)
            {
                if ((i & (CancellationInterval - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                uint node = (uint)i + 1;
                ranks[i] = node & (~node + 1);
            }
        }

        uint previous = 0;
        for (int i = 0; i < (int)length; i++)
        {
            if ((i & (CancellationInterval - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            uint value = symbols.ReadUInt32(ref reader, GetContext(previous), history, position++);
            int index = skip + i;
            if (value >= (uint)(size - index))
            {
                throw new InvalidImageContentException("The JPEG XL permutation contains an invalid Lehmer digit.");
            }

            previous = value;
            if (!destination.IsEmpty)
            {
                uint selected = (uint)skip + TakeRank(ranks, value + 1, highestBit);
                destination[index] = mapping.IsEmpty ? selected : mapping[(int)selected];
            }
        }

        if (!destination.IsEmpty)
        {
            // Omitted digits are zero, so the tail consumes the first remaining entry each time. Compose with the
            // natural coefficient order during selection, rather than making a second full-size composition pass.
            for (int i = skip + (int)length; i < size; i++)
            {
                if ((i & (CancellationInterval - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                uint selected = (uint)skip + TakeRank(ranks, 1, highestBit);
                destination[i] = mapping.IsEmpty ? selected : mapping[(int)selected];
            }
        }
    }

    /// <summary>
    /// Selects the permutation entropy context from an unsigned value's bit length.
    /// </summary>
    /// <param name="value">The complete permutation size or the preceding Lehmer digit.</param>
    /// <returns>The context index, from zero through seven.</returns>
    private static int GetContext(uint value) => Math.Min(32 - BitOperations.LeadingZeroCount(value), ContextCount - 1);

    /// <summary>
    /// Removes and returns the zero-based entry with the requested one-based rank among unused positions.
    /// </summary>
    /// <param name="tree">The Fenwick counts for positions after the skipped prefix.</param>
    /// <param name="rank">The validated one-based rank.</param>
    /// <param name="highestBit">The highest power of two no greater than the tree length.</param>
    /// <returns>The selected zero-based position relative to the skipped prefix.</returns>
    private static uint TakeRank(Span<uint> tree, uint rank, uint highestBit)
    {
        uint length = (uint)tree.Length;
        uint next = 0;
        for (uint bit = highestBit; bit != 0; bit >>= 1)
        {
            uint candidate = next + bit;
            if (candidate <= length && tree[(int)candidate - 1] < rank)
            {
                rank -= tree[(int)candidate - 1];
                next = candidate;
            }
        }

        // Each containing node loses one unused entry. Unsigned positions can reach 2^31 after an int-sized tree;
        // that position then leaves the loop without wrapping to a negative index.
        for (uint node = next + 1; node <= length; node += node & (~node + 1))
        {
            tree[(int)node - 1]--;
        }

        return next;
    }
}

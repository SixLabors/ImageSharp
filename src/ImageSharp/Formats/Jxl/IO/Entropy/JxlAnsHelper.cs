// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

/// <summary>
/// Utilities for Asymmetric Numeral Systems, a kind of high-performance
/// entropy coding used by the JPEG XL format.
/// </summary>
internal static class JxlAnsHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPopulationCountPrecision(int logCount, int shift)
        => Math.Max(0, Math.Min(logCount, shift - ((JxlAnsConstants.AnsLogTableSize - logCount) >> 1)));

    /// <summary>
    /// Distributes a total frequency as evenly as possible across an alphabet.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns the returned counts.</param>
    /// <param name="length">The alphabet size, from one through the total frequency.</param>
    /// <param name="totalCount">The total frequency required by the entropy table.</param>
    /// <returns>The count owner, which the caller must dispose.</returns>
    public static IMemoryOwner<uint> CreateFlatHistogram(Configuration configuration, int length, int totalCount)
    {
        DebugGuard.MustBeGreaterThan(length, 0, nameof(length));
        DebugGuard.MustBeLessThanOrEqualTo(length, totalCount, nameof(length));

        int count = totalCount / length;
        IMemoryOwner<uint> result = configuration.MemoryAllocator.Allocate<uint>(length);
        Span<uint> resultSpan = result.GetSpan();
        resultSpan.Fill((uint)count);

        // Add the division remainder to the first symbols so the table retains the exact required total.
        int remCounts = totalCount % length;
        for (int i = 0; i < remCounts; i++)
        {
            resultSpan[i]++;
        }

        return result;
    }

    public static JxlAnsSymbol Lookup(ReadOnlySpan<JxlAnsEntry> table, int value, int logEntrySize, int entrySizeMinus1)
    {
        int i = value >> logEntrySize;
        int pos = value & entrySizeMinus1;

        JxlAnsEntry entry = table[i];

        int cutoff = entry.Cutoff;
        int rightValue = entry.RightValue;
        int freq0 = entry.Frequency0;

        bool greater = pos >= cutoff;

        int offsets1or0 = greater ? entry.Offsets1 : 0;
        int freq1xorfreq0or0 = greater ? entry.Frequency1XorFrequency0 : 0;

        JxlAnsSymbol symbol = new()
        {
            Value = greater ? rightValue : i,
            Offset = offsets1or0 + pos,
            Frequency = freq0 ^ freq1xorfreq0or0
        };

        return symbol;
    }

    /// <summary>
    /// Builds alias buckets whose frequencies sum to the supplied range.
    /// </summary>
    /// <param name="preDistribution">The symbol frequencies. Trailing zeros are ignored; an empty alphabet represents symbol zero.</param>
    /// <param name="logRange">The exponent of the total frequency.</param>
    /// <param name="logAlphaSize">The exponent of the bucket count, no greater than the range exponent.</param>
    /// <param name="entries">Storage for every alias bucket.</param>
    /// <returns>Whether the frequencies form a valid normalized distribution.</returns>
    public static bool InitAliasTable(ReadOnlySpan<uint> preDistribution, uint logRange, int logAlphaSize, Span<JxlAnsEntry> entries)
    {
        DebugGuard.MustBeLessThanOrEqualTo(logAlphaSize, (int)logRange, nameof(logAlphaSize));

        int range = 1 << (int)logRange;
        int tableSize = 1 << logAlphaSize;

        int distributionPointer = preDistribution.Length - 1;

        while (distributionPointer >= 0 && preDistribution[distributionPointer] == 0)
        {
            distributionPointer--;
        }

        ReadOnlySpan<uint> distribution = preDistribution[..(distributionPointer + 1)];

        if (distribution.Length > tableSize)
        {
            throw new InvalidOperationException("Too many items in the distribution");
        }

        int entrySize = range >> logAlphaSize;

        // An empty alphabet is the degenerate symbol zero, with the full range as its frequency.
        // Keep the input unchanged; this case writes its complete table below without a temporary distribution.
        int singleSymbol = distribution.IsEmpty ? 0 : -1;
        uint sum = distribution.IsEmpty ? (uint)range : 0;

        for (int sym = 0; sym < distribution.Length; sym++)
        {
            uint value = distribution[sym];
            sum += value;

            if (value == range)
            {
                if (singleSymbol != -1)
                {
                    return false;
                }

                singleSymbol = sym;
            }
        }

        if (sum != range)
        {
            return false;
        }

        if (singleSymbol != -1)
        {
            byte sym = (byte)singleSymbol;
            if (singleSymbol != sym)
            {
                return false;
            }

            for (int i = 0; i < tableSize; i++)
            {
                ref JxlAnsEntry jxlEntry = ref entries[i];

                jxlEntry.RightValue = sym;
                jxlEntry.Cutoff = 0;
                jxlEntry.Offsets1 = (ushort)(entrySize * i);
                jxlEntry.Frequency0 = 0;
                jxlEntry.Frequency1XorFrequency0 = (ushort)range;
            }

            return true;
        }

        // Missing symbols still occupy empty buckets. Each work list must therefore cover the full table,
        // rather than only the encoded alphabet. Counts below delimit initialized stack entries.
        Span<uint> underfullPosn = stackalloc uint[tableSize];
        Span<uint> overfullPosn = stackalloc uint[tableSize];
        Span<uint> cutoffs = stackalloc uint[tableSize];

        int underfullCount = 0;
        int overfullCount = 0;

        for (int i = 0; i < distribution.Length; i++)
        {
            uint currentCutoff = distribution[i];

            cutoffs[i] = currentCutoff;

            if (currentCutoff > entrySize)
            {
                overfullPosn[overfullCount] = (uint)i;
                overfullCount++;
            }
            else if (currentCutoff < entrySize)
            {
                underfullPosn[underfullCount] = (uint)i;
                underfullCount++;
            }
        }

        for (int i = distribution.Length; i < tableSize; i++)
        {
            cutoffs[i] = 0;
            underfullPosn[underfullCount] = (uint)i;
            underfullCount++;
        }

        uint unsignedEntrySize = (uint)entrySize;

        // Fill one underfull bucket from the tail of one overfull symbol. Pop before reading because
        // the counters hold element counts. Only the donor can need another work-list entry afterward.
        while (overfullCount > 0)
        {
            uint overfullIndex = overfullPosn[--overfullCount];

            if (underfullCount == 0)
            {
                return false;
            }

            uint underfullIndex = underfullPosn[--underfullCount];

            int signedOverfullIndex = (int)overfullIndex;
            int signedUnderfullIndex = (int)underfullIndex;

            uint underfullBy = unsignedEntrySize - cutoffs[signedUnderfullIndex];
            cutoffs[signedOverfullIndex] -= underfullBy;

            ref JxlAnsEntry currentEntry = ref entries[signedUnderfullIndex];

            currentEntry.RightValue = unchecked((byte)overfullIndex);
            currentEntry.Offsets1 = unchecked((ushort)cutoffs[signedOverfullIndex]);

            uint currentCutoff = cutoffs[signedOverfullIndex];

            if (currentCutoff < entrySize)
            {
                underfullPosn[underfullCount] = overfullIndex;
                underfullCount++;
            }
            else if (currentCutoff > entrySize)
            {
                overfullPosn[overfullCount] = overfullIndex;
                overfullCount++;
            }
        }

        for (uint i = 0; i < tableSize; i++)
        {
            uint currentCutoff = cutoffs[(int)i];
            ref JxlAnsEntry entry = ref entries[(int)i];

            if (currentCutoff == entrySize)
            {
                entry.RightValue = (byte)i;
                entry.Offsets1 = 0;
                entry.Cutoff = 0;
            }
            else
            {
                // The borrowed range starts after this bucket's own symbols. Remove that prefix from
                // the donor offset so Lookup can add the bucket-local position for either symbol.
                entry.Offsets1 -= (ushort)currentCutoff;
                entry.Cutoff = (byte)currentCutoff;
            }

            uint freq0 = i < distribution.Length ? distribution[(int)i] : 0;
            int i1 = entry.RightValue;
            uint freq1 = i1 < distribution.Length ? distribution[i1] : 0;

            entry.Frequency0 = (ushort)freq0;
            entry.Frequency1XorFrequency0 = (ushort)(freq1 ^ freq0);
        }

        return true;
    }
}

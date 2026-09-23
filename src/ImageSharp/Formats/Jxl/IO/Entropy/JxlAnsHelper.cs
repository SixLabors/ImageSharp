// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

internal static class JxlAnsHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPopulationCountPrecision(int logCount, int shift)
        => Math.Max(0, Math.Min(logCount, shift - ((JxlAnsConstants.AnsLogTableSize - logCount) >> 1)));

    // NOTE: The result may potentially be large, so prefer using a memory allocator
    public static IMemoryOwner<uint> CreateFlatHistogram(Configuration configuration, int length, int totalCount)
    {
        DebugGuard.MustBeLessThanOrEqualTo(length, 0, nameof(length));
        DebugGuard.MustBeGreaterThan(length, totalCount, nameof(length));

        int count = totalCount / length;
        IMemoryOwner<uint> result = configuration.MemoryAllocator.Allocate<uint>(length);
        Span<uint> resultSpan = result.Memory.Span;
        uint unsignedCount = (uint)count;

        for (int i = 0; i < length; i++)
        {
            resultSpan[i] = unsignedCount;
        }

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

    public static bool InitAliasTable(Span<int> preDistribution, uint logRange, int logAlphaSize, Span<JxlAnsEntry> entries)
    {
        DebugGuard.MustBeLessThan(logAlphaSize, (int)logRange, nameof(logAlphaSize));

        int range = 1 << (int)logRange;
        int tableSize = 1 << logAlphaSize;

        int distributionPointer = preDistribution.Length - 1;

        while (distributionPointer >= 0 && preDistribution[distributionPointer] == 0)
        {
            distributionPointer--;
        }

        if (distributionPointer < 0)
        {
            preDistribution[0] = range;
            distributionPointer = 0;
        }

        Span<int> distribution = preDistribution[..(distributionPointer + 1)];

        if (distribution.Length > tableSize)
        {
            throw new InvalidOperationException("Too many items in the distribution");
        }

        int entrySize = range >> logAlphaSize;
        int singleSymbol = -1;
        int sum = 0;

        for (int sym = 0; sym < distribution.Length; sym++)
        {
            int value = distribution[sym];
            sum += value;

            if (value == JxlAnsConstants.AnsTableSize)
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
                jxlEntry.Frequency1XorFrequency0 = JxlAnsConstants.AnsTableSize;
            }

            return true;
        }

        Span<uint> underfullPosn = stackalloc uint[distribution.Length];
        Span<uint> overfullPosn = stackalloc uint[distribution.Length];
        Span<uint> cutoffs = stackalloc uint[1 << logAlphaSize];

        int underfullPointer = 0;
        int overfullPointer = 0;

        for (int i = 0; i < distribution.Length; i++)
        {
            uint currentCutoff = (uint)distribution[i];

            cutoffs[i] = currentCutoff;

            if (currentCutoff > entrySize)
            {
                overfullPosn[overfullPointer] = (uint)i;
                overfullPointer++;
            }
            else if (currentCutoff < entrySize)
            {
                underfullPosn[underfullPointer] = (uint)i;
                underfullPointer++;
            }
        }

        for (int i = distribution.Length; i < tableSize; i++)
        {
            cutoffs[i] = 0;
            underfullPosn[underfullPointer] = (uint)i;
            underfullPointer++;
        }

        uint unsignedEntrySize = (uint)entrySize;

        while (overfullPointer >= 0)
        {
            uint overfullIndex = overfullPosn[overfullPointer];
            overfullPointer--;

            if (underfullPointer <= -1)
            {
                return false;
            }

            uint underfullIndex = underfullPosn[underfullPointer];
            underfullPointer--;

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
                underfullPosn[underfullPointer] = overfullIndex;
                underfullPointer++;
            }
            else if (currentCutoff > entrySize)
            {
                overfullPosn[overfullPointer] = overfullIndex;
                overfullPointer++;
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
                entry.Offsets1 -= (ushort)currentCutoff;
                entry.Cutoff = (byte)currentCutoff;
            }

            int freq0 = i < distribution.Length ? distribution[(int)i] : 0;
            int i1 = entry.RightValue;
            int freq1 = i1 < distribution.Length ? distribution[i1] : 0;

            entry.Frequency0 = (ushort)freq0;
            entry.Frequency1XorFrequency0 = (ushort)(freq1 ^ freq0);
        }

        return true;
    }
}

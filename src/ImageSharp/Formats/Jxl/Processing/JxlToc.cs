// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Table of Contents encoding
/// </summary>
internal static class JxlToc
{
    private static readonly JxlU32Enc TocDistribution = new(
        JxlFieldExpressions.Bits(10),
        JxlFieldExpressions.BitsOffset(14, 2024),
        JxlFieldExpressions.BitsOffset(22, 17408),
        JxlFieldExpressions.BitsOffset(30, 4211712));

    public static int AcGroupIndex(int pass, int group, int numGroups, int numDcGroups)
        => 2 + numDcGroups + (pass * numGroups) + group;

    public static int NumberOfTocEntries(int numGroups, int numDcGroups, int numPasses)
    {
        if (numGroups == 1 && numPasses == 1)
        {
            return 1;
        }

        return AcGroupIndex(0, 0, numGroups, numDcGroups) + (numGroups * numPasses);
    }

    private const int BitsPerByte = 8;
    private const int MaxTocEntries = 65536;

    public static bool ReadToc(
        Configuration configuration,
        int tocEntries,
        JxlBitReader reader,
        List<uint> sizes,
        List<byte> permutation)
    {
        if (tocEntries > MaxTocEntries)
        {
            return false; // too many TOC entries
        }

        sizes.Clear();
        sizes.Capacity = tocEntries;

        for (int i = 0; i < tocEntries; i++)
        {
            sizes.Add(0);
        }

        if (reader.TotalBitsConsumed >= reader.TotalBytes * BitsPerByte)
        {
            return false; // not enough bytes
        }

        bool CheckBitBudget(int numEntries)
        {
            long minimalBitCost = numEntries * (2 + 10);
            long bitBudget = reader.TotalBytes * BitsPerByte;
            long expenses = reader.TotalBitsConsumed;

            return expenses <= bitBudget &&
                minimalBitCost <= bitBudget - expenses;
        }

        if (tocEntries <= 0)
        {
            return false;
        }

        if (reader.ReadBits32(1) == 1)
        {
            if (!CheckBitBudget(tocEntries))
            {
                return false;
            }

            permutation.Clear();

            for (int i = 0; i < tocEntries; i++)
            {
                permutation.Add(default);
            }

            if (!DecodePermutation(
                    configuration,
                    0,
                    tocEntries,
                    permutation,
                    reader))
            {
                return false;
            }
        }

        if (!reader.JumpToByteBoundary())
        {
            return false;
        }

        if (!CheckBitBudget(tocEntries))
        {
            return false;
        }

        for (int i = 0; i < tocEntries; i++)
        {
            sizes[i] = JxlU32Coder.Read(TocDistribution, reader);
        }

        if (!reader.JumpToByteBoundary())
        {
            return false;
        }

        return CheckBitBudget(0);
    }

    public static bool ReadGroupOffsets(
        Configuration configuration,
        int tocEntries,
        JxlBitReader reader,
        List<ulong> offsets,
        List<uint> sizes,
        out ulong totalSize)
    {
        totalSize = 0;

        List<byte> permutation = [];

        if (!ReadToc(
                configuration,
                tocEntries,
                reader,
                sizes,
                permutation))
        {
            return false;
        }

        offsets.Clear();
        offsets.Capacity = tocEntries;

        for (int i = 0; i < tocEntries; i++)
        {
            offsets.Add(0);
        }

        ulong offset = 0;

        for (int i = 0; i < tocEntries; i++)
        {
            ulong size = sizes[i];

            if (offset + size < offset)
            {
                return false;
            }

            offsets[i] = offset;
            offset += size;
        }

        totalSize = offset;

        if (permutation.Count != 0)
        {
            List<ulong> permutedOffsets = new(tocEntries);
            List<uint> permutedSizes = new(tocEntries);

            foreach (byte index in permutation)
            {
                permutedOffsets.Add(offsets[index]);
                permutedSizes.Add(sizes[index]);
            }

            offsets.Clear();
            offsets.AddRange(permutedOffsets);

            sizes.Clear();
            sizes.AddRange(permutedSizes);
        }

        return true;
    }
}

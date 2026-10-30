// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Table of Contents encoding
/// </summary>
internal static class JxlToc
{
    private const int MaxTocEntries = 65536;

    public static readonly JxlU32Enc TocDistribution = new(
        JxlFieldExpressions.Bits(10),
        JxlFieldExpressions.BitsOffset(14, 1024),
        JxlFieldExpressions.BitsOffset(22, 17408),
        JxlFieldExpressions.BitsOffset(30, 4211712));

    public static int MaxBits(int numSizes)
    {
        int entryBits = JxlU32Coder.MaxEncodedBits(TocDistribution) * numSizes;
        return 1 + JxlMath.BitsPerByte + entryBits + JxlMath.BitsPerByte;
    }

    public static int AcGroupIndex(int pass, int group, int numGroups, int numDcGroups)
        => 2 + numDcGroups + (pass * numGroups) + group;

    /// <summary>
    /// Gets the section count for a frame and rejects counts above the format decoder's allocation limit.
    /// </summary>
    /// <param name="numGroups">The number of transform or modular groups.</param>
    /// <param name="numDcGroups">The number of DC groups.</param>
    /// <param name="numPasses">The frame's progressive pass count.</param>
    /// <returns>The section count.</returns>
    public static int NumberOfTocEntries(int numGroups, int numDcGroups, int numPasses)
    {
        if (numGroups == 1 && numPasses == 1)
        {
            return 1;
        }

        // Frame dimensions originate in untrusted headers. Widen before multiplication, not after an Int32 count has wrapped.
        long count = 2L + numDcGroups + ((long)numGroups * numPasses);
        if (count > MaxTocEntries)
        {
            throw new InvalidImageContentException("The JPEG XL frame has too many sections.");
        }

        return (int)count;
    }

    /// <summary>
    /// Reads physical section sizes and an optional permutation of logical section identifiers.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns permutation scratch and entropy history.</param>
    /// <param name="tocEntries">The section count validated by <see cref="NumberOfTocEntries"/> from the parsed frame header.</param>
    /// <param name="reader">The reader at the section table. Truncated fields raise <see cref="EndOfStreamException"/>.</param>
    /// <param name="sizes">The decoded sizes, in physical stream order.</param>
    /// <param name="permutation">The logical-to-physical permutation, or an empty list when the table uses natural order.</param>
    /// <returns>Whether the section count and optional permutation are valid.</returns>
    public static bool ReadToc(Configuration configuration, int tocEntries, JxlBitReader reader, List<uint> sizes, List<uint> permutation)
    {
        CollectionsMarshal.SetCount(sizes, tocEntries);
        permutation.Clear();

        // The shared bit reader rejects truncated fields. It needs no seekable input or copied byte-budget state here.
        // Permutation indices are UInt32 values: a valid table can contain far more than 256 sections.
        if (reader.ReadBits32(1) == 1)
        {
            CollectionsMarshal.SetCount(permutation, tocEntries);
            if (!JxlCoefficientOrder.DecodePermutation(configuration, 0, tocEntries, CollectionsMarshal.AsSpan(permutation), reader))
            {
                return false;
            }
        }

        reader.JumpToByteBoundary();

        Span<uint> sectionSizes = CollectionsMarshal.AsSpan(sizes);
        for (int i = 0; i < tocEntries; i++)
        {
            sectionSizes[i] = JxlU32Coder.Read(TocDistribution, reader);
        }

        reader.JumpToByteBoundary();

        return true;
    }
}

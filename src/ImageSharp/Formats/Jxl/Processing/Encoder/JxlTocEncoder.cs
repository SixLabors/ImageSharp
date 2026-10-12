// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlTocEncoder
{
    public static void WriteTocPermutation(Memory<int> permutation, JxlBitWriter writer)
    {
        int nPermutation = permutation.Length;

        _ = writer.WithMaxBits((ulong)JxlToc.MaxBits(0), () =>
        {
            if (nPermutation > 0)
            {
                writer.Write(1, 1); // permutation present
                if (!JxlCoefficientOrderEncoder.EncodePermutation(
                    MemoryMarshal.Cast<int, uint>(permutation.Span),
                    0,
                    permutation.Length,
                    writer))
                {
                    return false;
                }
            }
            else
            {
                writer.Write(1, 0); // no permutation
            }

            writer.ZeroPadToByte();
            return true;
        });
    }

    public static void WriteTocSizes(Memory<int> groupSizes, JxlBitWriter writer) =>
        _ = writer.WithMaxBits((ulong)JxlToc.MaxBits(groupSizes.Length), () =>
        {
            Span<int> groupSizesSpan = groupSizes.Span;

            for (int i = 0; i < groupSizesSpan.Length; i++)
            {
                if (!JxlU32Coder.Write(in JxlToc.TocDistribution, unchecked((uint)groupSizesSpan[i]), writer))
                {
                    return false;
                }
            }

            writer.ZeroPadToByte();
            return true;
        });
}

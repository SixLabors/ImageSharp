// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Sums the samples assigned to each palette color. Reference: the accumulation loop of calc_centroids().
/// </content>
internal static partial class Av1PaletteKMeans
{
    /// <summary>
    /// Sums the samples and, when requested, counts the samples assigned to each palette color.
    /// </summary>
    /// <remarks>
    /// Each palette color is compared with a whole vector of indices, and its lanes add the samples that the
    /// comparison selects, so the sums stay in lanes until the end. A block holds at most 4096 samples of at most
    /// twelve bits, so a 32-bit sum and a 16-bit count per lane cannot overflow.
    /// </remarks>
    /// <param name="samples">The block samples.</param>
    /// <param name="indices">The palette index of each sample.</param>
    /// <param name="sums">Receives the sum of each color's samples.</param>
    /// <param name="counts">Receives the number of each color's samples, or is empty to skip counting.</param>
    internal static void SumByIndex(ReadOnlySpan<short> samples, ReadOnlySpan<byte> indices, Span<int> sums, Span<int> counts)
    {
        int colorCount = sums.Length;
        bool counting = !counts.IsEmpty;
        sums.Clear();
        counts.Clear();
        ref short sampleBase = ref MemoryMarshal.GetReference(samples);
        ref byte indexBase = ref MemoryMarshal.GetReference(indices);
        int offset = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            InlineArray8<Vector512<int>> laneSums = default;
            InlineArray8<Vector512<short>> laneCounts = default;
            Vector512<short> ones = Vector512.Create((short)1);
            for (; offset <= samples.Length - Vector512<short>.Count; offset += Vector512<short>.Count)
            {
                Vector512<short> sample = Vector512.LoadUnsafe(ref sampleBase, (nuint)offset);
                Vector512<short> index = Vector512_.Widen(Vector256.LoadUnsafe(ref indexBase, (nuint)offset));
                for (int color = 0; color < colorCount; color++)
                {
                    Vector512<short> selected = Vector512.Equals(index, Vector512.Create((short)color));
                    laneSums[color] += Vector512_.MultiplyAddAdjacent(sample & selected, ones);
                    laneCounts[color] -= selected;
                }
            }

            for (int color = 0; color < colorCount; color++)
            {
                sums[color] += Vector512.Sum(laneSums[color]);
                if (counting)
                {
                    counts[color] += Vector512.Sum(Vector512_.MultiplyAddAdjacent(laneCounts[color], ones));
                }
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            InlineArray8<Vector256<int>> laneSums = default;
            InlineArray8<Vector256<short>> laneCounts = default;
            Vector256<short> ones = Vector256.Create((short)1);
            for (; offset <= samples.Length - Vector256<short>.Count; offset += Vector256<short>.Count)
            {
                Vector256<short> sample = Vector256.LoadUnsafe(ref sampleBase, (nuint)offset);
                Vector256<short> index = Vector256_.Widen(Vector128.LoadUnsafe(ref indexBase, (nuint)offset));
                for (int color = 0; color < colorCount; color++)
                {
                    Vector256<short> selected = Vector256.Equals(index, Vector256.Create((short)color));
                    laneSums[color] += Vector256_.MultiplyAddAdjacent(sample & selected, ones);
                    laneCounts[color] -= selected;
                }
            }

            for (int color = 0; color < colorCount; color++)
            {
                sums[color] += Vector256.Sum(laneSums[color]);
                if (counting)
                {
                    counts[color] += Vector256.Sum(Vector256_.MultiplyAddAdjacent(laneCounts[color], ones));
                }
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            InlineArray8<Vector128<int>> laneSums = default;
            InlineArray8<Vector128<short>> laneCounts = default;
            Vector128<short> ones = Vector128.Create((short)1);
            for (; offset <= samples.Length - Vector128<short>.Count; offset += Vector128<short>.Count)
            {
                Vector128<short> sample = Vector128.LoadUnsafe(ref sampleBase, (nuint)offset);
                Vector128<short> index = Vector128.WidenLower(
                    Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref indexBase, offset))).AsByte()).AsInt16();

                for (int color = 0; color < colorCount; color++)
                {
                    Vector128<short> selected = Vector128.Equals(index, Vector128.Create((short)color));
                    laneSums[color] += Vector128_.MultiplyAddAdjacent(sample & selected, ones);
                    laneCounts[color] -= selected;
                }
            }

            for (int color = 0; color < colorCount; color++)
            {
                sums[color] += Vector128.Sum(laneSums[color]);
                if (counting)
                {
                    counts[color] += Vector128.Sum(Vector128_.MultiplyAddAdjacent(laneCounts[color], ones));
                }
            }
        }

        for (; offset < samples.Length; offset++)
        {
            int color = indices[offset];
            sums[color] += samples[offset];
            if (counting)
            {
                counts[color]++;
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Counts the samples of each palette color and sums their components, for the centroid update of the clustering.
/// </content>
internal static partial class Av1PaletteKMeans
{
    /// <summary>
    /// Counts the samples assigned to each palette color and sums their first and, when present, second components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The method takes the colors one at a time over the whole block, which stays in the first-level cache. For one color,
    /// a vector of indices compares with the color once. That mask selects the samples of both components and counts them.
    /// As a result, the three accumulators stay in registers for the whole pass, and no lane depends on another.
    /// </para>
    /// <para>
    /// One multiply-add by ones adds two adjacent masked samples into one thirty-two-bit sum lane. A count lane subtracts
    /// the all-ones mask, which adds one per selected sample. A block holds at most 4096 samples of at most twelve bits.
    /// Thus neither a sixteen-bit count lane nor a thirty-two-bit sum lane can overflow.
    /// </para>
    /// </remarks>
    /// <param name="first">The first component of each sample.</param>
    /// <param name="second">The second component of each sample, or empty for a one-component palette.</param>
    /// <param name="indices">The palette index of each sample, each below the palette size.</param>
    /// <param name="counts">Receives the number of samples of each color. Its length is the palette size.</param>
    /// <param name="firstSums">Receives the sum of the first component of each color.</param>
    /// <param name="secondSums">Receives the sum of the second component of each color, when it is present.</param>
    internal static void SumByIndex(
        ReadOnlySpan<short> first,
        ReadOnlySpan<short> second,
        ReadOnlySpan<byte> indices,
        Span<int> counts,
        Span<int> firstSums,
        Span<int> secondSums)
    {
        bool hasSecond = !second.IsEmpty;
        ref short firstBase = ref MemoryMarshal.GetReference(first);
        ref short secondBase = ref MemoryMarshal.GetReference(second);
        ref byte indexBase = ref MemoryMarshal.GetReference(indices);
        int length = first.Length;

        // The widest vector covers the bulk of the block. The scalar loop at the end adds the rest, which is fewer samples
        // than one vector. Only one width runs, so the tail starts at the same place for every color.
        int vectorLength = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            vectorLength = length & ~(Vector512<short>.Count - 1);
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            vectorLength = length & ~(Vector256<short>.Count - 1);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            vectorLength = length & ~(Vector128<short>.Count - 1);
        }

        for (int color = 0; color < counts.Length; color++)
        {
            int count = 0;
            int firstSum = 0;
            int secondSum = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<short> target = Vector512.Create((short)color);
                Vector512<short> ones = Vector512<short>.One;
                Vector512<short> countLanes = Vector512<short>.Zero;
                Vector512<int> firstLanes = Vector512<int>.Zero;
                Vector512<int> secondLanes = Vector512<int>.Zero;
                for (int i = 0; i < vectorLength; i += Vector512<short>.Count)
                {
                    // Thirty-two index bytes widen to the sixteen-bit lanes of the samples they belong to.
                    Vector512<short> index = Vector512_.Widen(Vector256.LoadUnsafe(ref indexBase, (nuint)i));
                    Vector512<short> selected = Vector512.Equals(index, target);
                    countLanes -= selected;
                    firstLanes += Vector512_.MultiplyAddAdjacent(Vector512.LoadUnsafe(ref firstBase, (nuint)i) & selected, ones);
                    if (hasSecond)
                    {
                        secondLanes += Vector512_.MultiplyAddAdjacent(Vector512.LoadUnsafe(ref secondBase, (nuint)i) & selected, ones);
                    }
                }

                count = Vector512.Sum(Vector512_.MultiplyAddAdjacent(countLanes, ones));
                firstSum = Vector512.Sum(firstLanes);
                secondSum = Vector512.Sum(secondLanes);
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                Vector256<short> target = Vector256.Create((short)color);
                Vector256<short> ones = Vector256<short>.One;
                Vector256<short> countLanes = Vector256<short>.Zero;
                Vector256<int> firstLanes = Vector256<int>.Zero;
                Vector256<int> secondLanes = Vector256<int>.Zero;
                for (int i = 0; i < vectorLength; i += Vector256<short>.Count)
                {
                    // Sixteen index bytes widen to the sixteen-bit lanes of the samples they belong to.
                    Vector256<short> index = Vector256_.Widen(Vector128.LoadUnsafe(ref indexBase, (nuint)i));
                    Vector256<short> selected = Vector256.Equals(index, target);
                    countLanes -= selected;
                    firstLanes += Vector256_.MultiplyAddAdjacent(Vector256.LoadUnsafe(ref firstBase, (nuint)i) & selected, ones);
                    if (hasSecond)
                    {
                        secondLanes += Vector256_.MultiplyAddAdjacent(Vector256.LoadUnsafe(ref secondBase, (nuint)i) & selected, ones);
                    }
                }

                count = Vector256.Sum(Vector256_.MultiplyAddAdjacent(countLanes, ones));
                firstSum = Vector256.Sum(firstLanes);
                secondSum = Vector256.Sum(secondLanes);
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                Vector128<short> target = Vector128.Create((short)color);
                Vector128<short> ones = Vector128<short>.One;
                Vector128<short> countLanes = Vector128<short>.Zero;
                Vector128<int> firstLanes = Vector128<int>.Zero;
                Vector128<int> secondLanes = Vector128<int>.Zero;
                for (int i = 0; i < vectorLength; i += Vector128<short>.Count)
                {
                    // Eight index bytes widen to the sixteen-bit lanes of the samples they belong to.
                    Vector128<short> index = Vector128.WidenLower(
                        Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref indexBase, (nuint)i))).AsByte()).AsInt16();

                    Vector128<short> selected = Vector128.Equals(index, target);
                    countLanes -= selected;
                    firstLanes += Vector128_.MultiplyAddAdjacent(Vector128.LoadUnsafe(ref firstBase, (nuint)i) & selected, ones);
                    if (hasSecond)
                    {
                        secondLanes += Vector128_.MultiplyAddAdjacent(Vector128.LoadUnsafe(ref secondBase, (nuint)i) & selected, ones);
                    }
                }

                count = Vector128.Sum(Vector128_.MultiplyAddAdjacent(countLanes, ones));
                firstSum = Vector128.Sum(firstLanes);
                secondSum = Vector128.Sum(secondLanes);
            }

            counts[color] = count;
            firstSums[color] = firstSum;
            if (hasSecond)
            {
                secondSums[color] = secondSum;
            }
        }

        // The scalar tail adds the samples past the last whole vector to the totals of every color in one pass.
        for (int i = vectorLength; i < length; i++)
        {
            int color = Unsafe.Add(ref indexBase, (nuint)i);
            counts[color]++;
            firstSums[color] += Unsafe.Add(ref firstBase, (nuint)i);
            if (hasSecond)
            {
                secondSums[color] += Unsafe.Add(ref secondBase, (nuint)i);
            }
        }
    }
}

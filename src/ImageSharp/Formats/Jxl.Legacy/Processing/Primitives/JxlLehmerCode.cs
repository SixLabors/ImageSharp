// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Converts permutations to and from ranks among the remaining unused indices.
/// </summary>
internal static class JxlLehmerCode
{
    /// <summary>
    /// Returns the Fenwick-tree step for one positive one-based index.
    /// </summary>
    /// <param name="n">The one-based tree index.</param>
    /// <returns>The value of its lowest set bit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ValueOfLowest1Bit(int n) => n & -n;

    /// <summary>
    /// Writes each permutation entry's rank after the preceding entries are removed.
    /// </summary>
    /// <param name="permutation">The first n entries are unique indices from zero through n minus one.</param>
    /// <param name="temp">Borrowed scratch with at least n plus one entries. Its required prefix is initialized here; remaining entries are unchanged.</param>
    /// <param name="n">The permutation length, already established by the owning field or coefficient order.</param>
    /// <param name="code">The destination for n ranks. Remaining entries are unchanged.</param>
    /// <returns>Whether every entry has a nonnegative remaining rank.</returns>
    public static bool ComputeLehmerCode(ReadOnlySpan<uint> permutation, Span<uint> temp, int n, Span<uint> code)
    {
        // The one-based Fenwick tree records indices already used by this permutation. Scratch can contain a previous
        // encode or decode state, so initialize this call's n-plus-one prefix and retain any following caller scratch.
        temp[..(n + 1)].Clear();

        for (int idx = 0; idx < n; idx++)
        {
            uint s = permutation[idx];

            uint penalty = 0u;
            uint i = s + 1u;

            // Prefix counts tell how many preceding entries were at or below this value. Subtracting them gives
            // the value's rank among unused indices, without moving a remaining-values array for every entry.
            while (i != 0u)
            {
                penalty += temp[(int)i];
                i &= i - 1u;    // Clear lowest bit
            }

            if (s < penalty)
            {
                return false;
            }

            code[idx] = s - penalty;
            i = s + 1u;

            while (i < n + 1u)
            {
                temp[(int)i]++;
                i += (uint)ValueOfLowest1Bit((int)i);
            }
        }

        return true;
    }

    /// <summary>
    /// Restores a permutation by selecting each stored rank from the remaining unused indices.
    /// </summary>
    /// <param name="code">The first n entries are ranks among the remaining indices.</param>
    /// <param name="temp">Borrowed scratch with at least the next power of two of n entries. The required prefix is initialized here.</param>
    /// <param name="n">The permutation length, already limited by the owning field or coefficient order.</param>
    /// <param name="permutation">The destination for n indices. Remaining entries are unchanged.</param>
    /// <returns>Whether n is positive and each rank identifies an unused index.</returns>
    public static bool DecodeLehmerCode(ReadOnlySpan<uint> code, Span<uint> temp, int n, Span<uint> permutation)
    {
        if (n == 0)
        {
            return false;
        }

        int log2n = JxlMath.CeilLog2Nonzero(n);
        int paddedN = 1 << log2n;

        // A tree of ones initially makes every index available. Power-of-two padding permits the same binary search
        // at every length. Valid ranks are limited by n minus the decoded count, so padded indices are never selected.
        for (int i = 0; i < paddedN; i++)
        {
            int i1 = i + 1;
            temp[i] = (uint)ValueOfLowest1Bit(i1);
        }

        for (int i = 0; i < n; i++)
        {
            if (code[i] + i >= n)
            {
                return false;
            }

            uint rank = code[i] + 1;

            int bit = paddedN;
            int next = 0;

            // Descend by decreasing tree steps. Skip a complete subtree only when it contains fewer available
            // indices than the requested rank; otherwise, keep searching within that subtree.
            for (int b = 0; b <= log2n; b++)
            {
                int cand = next + bit;

                if (cand < 1)
                {
                    return false;
                }

                bit >>= 1;

                if (temp[cand - 1] < rank)
                {
                    next = cand;
                    rank -= temp[cand - 1];
                }
            }

            permutation[i] = unchecked((uint)next);

            next++;
            while (next <= paddedN)
            {
                temp[next - 1]--;
                next += ValueOfLowest1Bit(next);
            }
        }

        return true;
    }
}

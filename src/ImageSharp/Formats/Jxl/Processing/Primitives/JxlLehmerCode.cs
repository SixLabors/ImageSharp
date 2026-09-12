// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

internal static class JxlLehmerCode
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ValueOfLowest1Bit(int n) => n & -n;

    public static bool ComputeLehmerCode(ReadOnlySpan<uint> permutation, Span<uint> temp, int n, Span<uint> code)
    {
        temp[(n + 1)..].Clear();

        for (int idx = 0; idx < n; idx++)
        {
            uint s = permutation[idx];

            uint penalty = 0u;
            uint i = s + 1u;

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

    public static bool DecodeLehmerCode(ReadOnlySpan<uint> code, Span<uint> temp, int n, Span<uint> permutation)
    {
        if (n == 0)
        {
            return false;
        }

        int log2n = JxlMath.CeilLog2Nonzero(n);
        int paddedN = 1 << log2n;

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

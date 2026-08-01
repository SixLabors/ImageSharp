// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlXorShift
{
    public const int Generators = 8;

    private readonly ulong[] s0 = new ulong[8];
    private readonly ulong[] s1 = new ulong[8];

    public void XorShift128Plus(ulong seed)
    {
        this.s0[0] = SplitMix64(seed + 0x9E3779B97F4A7C15L);
        this.s1[0] = SplitMix64(this.s0[0]);
        for (int i = 1; i < 8; ++i)
        {
            this.s0[i] = SplitMix64(this.s1[i - 1]);
            this.s1[i] = SplitMix64(this.s0[i]);
        }
    }

    public void XorShift128Plus(uint seed1, uint seed2, uint seed3, uint seed4)
    {
        this.s0[0] = SplitMix64((((ulong)seed1 << 32) + seed2) + 0x9E3779B97F4A7C15uL);
        this.s1[0] = SplitMix64((((ulong)seed3 << 32) + seed4) + 0x9E3779B97F4A7C15uL);
        for (int i = 1; i < 8; ++i)
        {
            this.s0[i] = SplitMix64(this.s0[i - 1]);
            this.s1[i] = SplitMix64(this.s1[i - 1]);
        }
    }

    // TODO: SIMD
    public void Fill(Span<ulong> randomBits)
    {
        for (int i = 0; i < 8; ++i)
        {
            ulong s1 = this.s0[i];
            ulong s0 = this.s1[i];
            ulong bits = s1 + s0;
            this.s0[i] = s0;
            s1 ^= s1 << 23;
            randomBits[i] = bits;
            s1 ^= s0 ^ (s1 >> 18) ^ (s0 >> 5);
            this.s1[i] = s1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong SplitMix64(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9uL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBuL;
        return z ^ (z >> 31);
    }
}

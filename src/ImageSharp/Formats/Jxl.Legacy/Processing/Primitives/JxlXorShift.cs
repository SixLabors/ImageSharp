// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// XorShift is a kind of Random Number Generator (RNG) used by the
/// JPEG-XL codec. Specifically, this is a XorShift128Plus version
/// of the XorShift.
/// </summary>
/// <remarks>
/// https://github.com/vpxyz/xorshift
/// More precisely: https://github.com/vpxyz/xorshift/tree/master/xorshift128plus
/// </remarks>
internal sealed class JxlXorShift
{
    /// <summary>
    /// Number of generators.
    /// </summary>
    public const int Generators = 8;

    /// <summary>The SplitMix64 seed increment used by both native seed conventions.</summary>
    private const ulong SeedIncrement = 0x9E3779B97F4A7C15UL;

    // Each lane has its own two-word state. Inline storage keeps all eight generators in this owner without separate arrays.
    private InlineArray8<ulong> s0;
    private InlineArray8<ulong> s1;

    /// <summary>
    /// Initializes eight independent generators from one seed using the native SplitMix64 sequence.
    /// </summary>
    /// <param name="seed">The initial seed.</param>
    public JxlXorShift(ulong seed)
    {
        this.s0[0] = SplitMix64(seed + SeedIncrement);
        this.s1[0] = SplitMix64(this.s0[0]);

        for (int i = 1; i < Generators; ++i)
        {
            this.s0[i] = SplitMix64(this.s1[i - 1]);
            this.s1[i] = SplitMix64(this.s0[i]);
        }
    }

    /// <summary>
    /// Initializes the native frame-and-position sequence from four seed words.
    /// </summary>
    /// <param name="seed1">The high word of the first seed.</param>
    /// <param name="seed2">The low word of the first seed.</param>
    /// <param name="seed3">The high word of the second seed.</param>
    /// <param name="seed4">The low word of the second seed.</param>
    public JxlXorShift(uint seed1, uint seed2, uint seed3, uint seed4)
    {
        this.s0[0] = SplitMix64((((ulong)seed1 << 32) + seed2) + SeedIncrement);
        this.s1[0] = SplitMix64((((ulong)seed3 << 32) + seed4) + SeedIncrement);

        for (int i = 1; i < Generators; ++i)
        {
            this.s0[i] = SplitMix64(this.s0[i - 1]);
            this.s1[i] = SplitMix64(this.s1[i - 1]);
        }
    }

    /// <summary>
    /// Advances every generator once and writes its previous state sum in lane order.
    /// </summary>
    /// <param name="randomBits">Storage for at least eight output words. Remaining values are unchanged.</param>
    public void Fill(Span<ulong> randomBits)
    {
        Span<ulong> state0 = this.s0;
        Span<ulong> state1 = this.s1;
        ref ulong first0 = ref MemoryMarshal.GetReference(state0);
        ref ulong first1 = ref MemoryMarshal.GetReference(state1);
        ref ulong output = ref MemoryMarshal.GetReference(randomBits);

        // The eight states are independent lanes. Every width applies the native XOR/shift recurrence without cross-lane operations.
        // Unsigned addition wraps modulo 2^64. The output uses the old words, before either state is advanced.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<ulong> previous0 = Vector512.LoadUnsafe(ref first0);
            Vector512<ulong> previous1 = Vector512.LoadUnsafe(ref first1);
            (previous0 + previous1).StoreUnsafe(ref output);
            previous1.StoreUnsafe(ref first0);
            previous0 ^= previous0 << 23;
            (previous0 ^ previous1 ^ (previous0 >> 18) ^ (previous1 >> 5)).StoreUnsafe(ref first1);
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            for (uint i = 0; i < Generators; i += (uint)Vector256<ulong>.Count)
            {
                Vector256<ulong> previous0 = Vector256.LoadUnsafe(ref first0, i);
                Vector256<ulong> previous1 = Vector256.LoadUnsafe(ref first1, i);
                (previous0 + previous1).StoreUnsafe(ref output, i);
                previous1.StoreUnsafe(ref first0, i);
                previous0 ^= previous0 << 23;
                (previous0 ^ previous1 ^ (previous0 >> 18) ^ (previous1 >> 5)).StoreUnsafe(ref first1, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            for (uint i = 0; i < Generators; i += (uint)Vector128<ulong>.Count)
            {
                Vector128<ulong> previous0 = Vector128.LoadUnsafe(ref first0, i);
                Vector128<ulong> previous1 = Vector128.LoadUnsafe(ref first1, i);
                (previous0 + previous1).StoreUnsafe(ref output, i);
                previous1.StoreUnsafe(ref first0, i);
                previous0 ^= previous0 << 23;
                (previous0 ^ previous1 ^ (previous0 >> 18) ^ (previous1 >> 5)).StoreUnsafe(ref first1, i);
            }
        }
        else
        {
            // Eight is divisible by every supported ulong lane count, so only the scalar feature mode reaches this path.
            for (int i = 0; i < Generators; ++i)
            {
                ulong previous0 = state0[i];
                ulong previous1 = state1[i];
                randomBits[i] = unchecked(previous0 + previous1);
                state0[i] = previous1;
                previous0 ^= previous0 << 23;
                state1[i] = previous0 ^ previous1 ^ (previous0 >> 18) ^ (previous1 >> 5);
            }
        }
    }

    /// <summary>
    /// Mixes a seed word with the native unsigned SplitMix64 recurrence.
    /// </summary>
    /// <param name="z">The seed word to mix.</param>
    /// <returns>The mixed state word.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong SplitMix64(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9uL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBuL;
        return z ^ (z >> 31);
    }
}

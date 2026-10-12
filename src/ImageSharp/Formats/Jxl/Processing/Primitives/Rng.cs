// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Deterministic random number generator used for compatibility
/// with JPEG XL.
/// </summary>
internal struct Rng
{
    private ulong s0;
    private ulong s1;

    public Rng(ulong seed)
    {
        this.s0 = 0x94D049BB133111EBUL;
        this.s1 = 0xBF58476D1CE4E5B9UL + seed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Next()
    {
        ulong s1 = this.s0;
        ulong s0 = this.s1;
        ulong bits = s1 + s0;
        this.s0 = s0;

        s1 ^= s1 << 23;
        s1 ^= s0 ^ (s1 >> 18) ^ (s0 >> 5);

        this.s1 = s1;

        return bits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long UniformI(long begin, long end) => (long)(this.Next() % (ulong)(end - begin)) + begin;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong UniformU(ulong begin, ulong end) => (this.Next() % (end - begin)) + begin;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float UniformF(float begin, float end)
    {
        uint u = (uint)(this.Next() >> (64 - 23)) | 0x3F800000u;
        float f = BitConverter.UInt32BitsToSingle(u);

        return ((end - begin) * (f - 1.0f)) + begin;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Bernoulli(float p) => this.UniformF(0, 1) < p;

    internal readonly struct GeometricDistribution
    {
        public readonly float Value { get; }

        public GeometricDistribution(float value) => this.Value = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GeometricDistribution Make(float p) => new(1.0f / MathF.Log(1.0f - p));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Geometric(in GeometricDistribution dist)
    {
        float f = this.UniformF(0, 1);
        float invLog1mp = dist.Value;

        float log = MathF.Log(1.0f - f) * invLog1mp;

        return (uint)log;
    }

    public void Shuffle<T>(Span<T> span)
    {
        for (nuint i = 0; i + 1 < (nuint)span.Length; i++)
        {
            nuint a = (nuint)this.UniformU(i, (nuint)span.Length);
            RuntimeUtility.Swap(ref span[(int)a], ref span[(int)i]);
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.Vectorization;

[Config(typeof(Config.Short))]
public class WidenBytesToUInt32
{
    private byte[] source;

    private uint[] dest;

    private const int Count = 64;

    [GlobalSetup]
    public void Setup()
    {
        this.source = new byte[Count];
        this.dest = new uint[Count];
    }

    [Benchmark(Baseline = true)]
    public void Standard()
    {
        const int N = Count / 8;

        ref InlineArray8<byte> sBase = ref Unsafe.As<byte, InlineArray8<byte>>(ref this.source[0]);
        ref InlineArray8<uint> dBase = ref Unsafe.As<uint, InlineArray8<uint>>(ref this.dest[0]);

        for (nuint i = 0; i < N; i++)
        {
            ref InlineArray8<byte> source = ref Unsafe.Add(ref sBase, i);
            ref InlineArray8<uint> destination = ref Unsafe.Add(ref dBase, i);

            destination[0] = source[0];
            destination[1] = source[1];
            destination[2] = source[2];
            destination[3] = source[3];
            destination[4] = source[4];
            destination[5] = source[5];
            destination[6] = source[6];
            destination[7] = source[7];
        }
    }

    [Benchmark]
    public void Simd()
    {
        nuint n = Count / (uint)Vector<byte>.Count;

        ref Vector<byte> sBase = ref Unsafe.As<byte, Vector<byte>>(ref this.source[0]);
        ref Vector<uint> dBase = ref Unsafe.As<uint, Vector<uint>>(ref this.dest[0]);

        for (nuint i = 0; i < n; i++)
        {
            Vector<byte> b = Unsafe.Add(ref sBase, i);

            Vector.Widen(b, out Vector<ushort> s0, out Vector<ushort> s1);
            Vector.Widen(s0, out Vector<uint> w0, out Vector<uint> w1);
            Vector.Widen(s1, out Vector<uint> w2, out Vector<uint> w3);

            ref Vector<uint> d = ref Unsafe.Add(ref dBase, i * 4);
            d = w0;
            Unsafe.Add(ref d, 1) = w1;
            Unsafe.Add(ref d, 2) = w2;
            Unsafe.Add(ref d, 3) = w3;
        }
    }
}

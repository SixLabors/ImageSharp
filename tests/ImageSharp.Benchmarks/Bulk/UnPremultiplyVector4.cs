// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

public class UnPremultiplyVector4
{
    private static readonly Vector4[] Vectors = Vector4Factory.CreateVectors();

    [Benchmark(Baseline = true)]
    public void UnPremultiplyBaseline()
    {
        ref Vector4 baseRef = ref MemoryMarshal.GetReference<Vector4>(Vectors);

        for (nuint i = 0; i < (uint)Vectors.Length; i++)
        {
            ref Vector4 v = ref Unsafe.Add(ref baseRef, i);

            UnPremultiply(ref v);
        }
    }

    [Benchmark]
    public void UnPremultiply()
    {
        ref Vector4 baseRef = ref MemoryMarshal.GetReference<Vector4>(Vectors);

        for (nuint i = 0; i < (uint)Vectors.Length; i++)
        {
            ref Vector4 v = ref Unsafe.Add(ref baseRef, i);
            Numerics.UnPremultiply(ref v);
        }
    }

    [Benchmark]
    public void UnPremultiplyBulk() => Numerics.UnPremultiply(Vectors);

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void UnPremultiply(ref Vector4 source)
    {
        float w = source.W;
        source /= w;
        source.W = w;
    }
}

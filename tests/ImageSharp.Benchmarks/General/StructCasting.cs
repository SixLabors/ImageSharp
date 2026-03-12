// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General;

public class StructCasting
{
    [Benchmark(Baseline = true)]
    public short ExplicitCast()
    {
        const int x = 5 * 2;
        return (short)x;
    }

    [Benchmark]
    public short UnsafeCast()
    {
        int x = 5 * 2;
        return Unsafe.As<int, short>(ref x);
    }

    [Benchmark]
    public short UnsafeCastRef()
    {
        int x = 5 * 2;
        return Unsafe.As<int, short>(ref Unsafe.AsRef(ref x));
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace ImageSharp.Benchmarks.General.Vectorization;

#pragma warning disable SA1649 // File name should match first type name
public class MulUInt32 : SIMDBenchmarkBase<uint>.Multiply
#pragma warning restore SA1649 // File name should match first type name
{
    protected override uint GetTestValue() => 42u;

    [Benchmark(Baseline = true)]
    public void Standard()
    {
        uint v = this.TestValue;
        for (int i = 0; i < this.Input.Length; i++)
        {
            this.Result[i] = this.Input[i] * v;
        }
    }
}

public class MulInt32 : SIMDBenchmarkBase<int>.Multiply
{
    [Benchmark(Baseline = true)]
    public void Standard()
    {
        int v = this.TestValue;
        for (int i = 0; i < this.Input.Length; i++)
        {
            this.Result[i] = this.Input[i] * v;
        }
    }
}

public class MulInt16 : SIMDBenchmarkBase<short>.Multiply
{
    protected override short GetTestValue() => 42;

    protected override Vector<short> GetTestVector() => new Vector<short>(new short[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 });

    [Benchmark(Baseline = true)]
    public void Standard()
    {
        short v = this.TestValue;
        for (int i = 0; i < this.Input.Length; i++)
        {
            this.Result[i] = (short)(this.Input[i] * v);
        }
    }
}

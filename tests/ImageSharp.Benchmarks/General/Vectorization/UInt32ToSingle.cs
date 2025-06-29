// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.Vectorization;

[Config(typeof(Config.Short))]
public class UInt32ToSingle
{
    private float[] data;

    private const uint Count = 32;

    [GlobalSetup]
    public void Setup()
    {
        this.data = new float[Count];
    }

    [Benchmark(Baseline = true)]
    public void MagicMethod()
    {
        ref Vector<float> b = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);

        nuint n = Count / (uint)Vector<float>.Count;

        Vector<float> bVec = new(256.0f / 255.0f);
        Vector<float> magicFloat = new(32768.0f);
        Vector<uint> magicInt = new(1191182336); // reinterpreted value of 32768.0f
        Vector<uint> mask = new(255);

        for (nuint i = 0; i < n; i++)
        {
            ref Vector<float> df = ref Unsafe.Add(ref b, i);

            Vector<uint> vi = Vector.AsVectorUInt32(df);
            vi &= mask;
            vi |= magicInt;

            Vector<float> vf = Vector.AsVectorSingle(vi);
            vf = (vf - magicFloat) * bVec;

            df = vf;
        }
    }

    [Benchmark]
    public void StandardSimd()
    {
        nuint n = Count / (uint)Vector<float>.Count;

        ref Vector<float> bf = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);
        ref Vector<uint> bu = ref Unsafe.As<Vector<float>, Vector<uint>>(ref bf);

        Vector<float> scale = new(1f / 255f);

        for (nuint i = 0; i < n; i++)
        {
            Vector<uint> u = Unsafe.Add(ref bu, i);
            Vector<float> v = Vector.ConvertToSingle(u);
            v *= scale;
            Unsafe.Add(ref bf, i) = v;
        }
    }

    [Benchmark]
    public void StandardSimdFromInt()
    {
        nuint n = Count / (uint)Vector<float>.Count;

        ref Vector<float> bf = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);
        ref Vector<int> bu = ref Unsafe.As<Vector<float>, Vector<int>>(ref bf);

        Vector<float> scale = new(1f / 255f);

        for (nuint i = 0; i < n; i++)
        {
            Vector<int> u = Unsafe.Add(ref bu, i);
            Vector<float> v = Vector.ConvertToSingle(u);
            v *= scale;
            Unsafe.Add(ref bf, i) = v;
        }
    }

    [Benchmark]
    public void StandardSimdFromInt_RefCast()
    {
        nuint n = Count / (uint)Vector<float>.Count;

        ref Vector<float> bf = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);
        Vector<float> scale = new(1f / 255f);

        for (nuint i = 0; i < n; i++)
        {
            ref Vector<float> fRef = ref Unsafe.Add(ref bf, i);

            Vector<int> du = Vector.AsVectorInt32(fRef);
            Vector<float> v = Vector.ConvertToSingle(du);
            v *= scale;

            fRef = v;
        }
    }
}

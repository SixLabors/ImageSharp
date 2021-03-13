// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.Vectorization
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class UInt32ToSingle
    {
        private float[] data;

        private const int Count = 32;

        [GlobalSetup]
        public void Setup()
        {
            this.data = new float[Count];
        }

        [Benchmark(Baseline = true)]
        public void MagicMethod()
        {
            ref Vector<float> b = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);

            int n = Count / Vector<float>.Count;

            var bVec = new Vector<float>(256.0f / 255.0f);
            var magicFloat = new Vector<float>(32768.0f);
            var magicInt = new Vector<uint>(1191182336); // reinterpreted value of 32768.0f
            var mask = new Vector<uint>(255);

            for (int i = 0; i < n; i++)
            {
                ref Vector<float> df = ref Unsafe.Add(ref b, i);

                var vi = Vector.AsVectorUInt32(df);
                vi &= mask;
                vi |= magicInt;

                var vf = Vector.AsVectorSingle(vi);
                vf = (vf - magicFloat) * bVec;

                df = vf;
            }
        }

        [Benchmark]
        public void StandardSimd()
        {
            int n = Count / Vector<float>.Count;

            ref Vector<float> bf = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);
            ref Vector<uint> bu = ref Unsafe.As<Vector<float>, Vector<uint>>(ref bf);

            var scale = new Vector<float>(1f / 255f);

            for (int i = 0; i < n; i++)
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
            int n = Count / Vector<float>.Count;

            ref Vector<float> bf = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);
            ref Vector<int> bu = ref Unsafe.As<Vector<float>, Vector<int>>(ref bf);

            var scale = new Vector<float>(1f / 255f);

            for (int i = 0; i < n; i++)
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
            int n = Count / Vector<float>.Count;

            ref Vector<float> bf = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);
            var scale = new Vector<float>(1f / 255f);

            for (int i = 0; i < n; i++)
            {
                ref Vector<float> fRef = ref Unsafe.Add(ref bf, i);

                Vector<int> du = Vector.AsVectorInt32(fRef);
                Vector<float> v = Vector.ConvertToSingle(du);
                v *= scale;

                fRef = v;
            }
        }
    }
}

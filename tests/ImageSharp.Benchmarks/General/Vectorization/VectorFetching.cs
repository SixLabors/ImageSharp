// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Benchmarks.General.Vectorization
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using BenchmarkDotNet.Attributes;

    /// <summary>
    /// This benchmark compares different methods for fetching memory data into <see cref="Vector{T}"/>
    /// checking if JIT has limitations. Normally SIMD acceleration should be here for all methods.
    /// </summary>
    public class VectorFetching
    {
        private float testValue;

        private float[] data;

        [Params(64)]
        public int InputSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.data = new float[this.InputSize];
            this.testValue = 42;

            for (int i = 0; i < this.InputSize; i++)
            {
                this.data[i] = i;
            }
        }

        [Benchmark(Baseline = true)]
        public void Baseline()
        {
            float v = this.testValue;
            for (int i = 0; i < this.data.Length; i++)
            {
                this.data[i] = this.data[i] * v;
            }
        }

        [Benchmark]
        public void FetchWithVectorConstructor()
        {
            var v = new Vector<float>(this.testValue);

            for (int i = 0; i < this.data.Length; i += Vector<uint>.Count)
            {
                var a = new Vector<float>(this.data, i);
                a = a * v;
                a.CopyTo(this.data, i);
            }
        }

        [Benchmark]
        public void FetchWithUnsafeCast()
        {
            var v = new Vector<float>(this.testValue);
            ref Vector<float> start = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);

            int n = this.InputSize / Vector<uint>.Count;

            for (int i = 0; i < n; i++)
            {
                ref Vector<float> p = ref Unsafe.Add(ref start, i);

                Vector<float> a = p;
                a = a * v;

                p = a;
            }
        }

        [Benchmark]
        public void FetchWithUnsafeCastNoTempVector()
        {
            var v = new Vector<float>(this.testValue);
            ref Vector<float> start = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);

            int n = this.InputSize / Vector<uint>.Count;

            for (int i = 0; i < n; i++)
            {
                ref Vector<float> a = ref Unsafe.Add(ref start, i);
                a = a * v;
            }
        }

        [Benchmark]
        public void FetchWithUnsafeCastFromReference()
        {
            var v = new Vector<float>(this.testValue);

            var span = new Span<float>(this.data);

            ref Vector<float> start = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(span));

            int n = this.InputSize / Vector<uint>.Count;

            for (int i = 0; i < n; i++)
            {
                ref Vector<float> a = ref Unsafe.Add(ref start, i);
                a = a * v;
            }
        }
    }
}

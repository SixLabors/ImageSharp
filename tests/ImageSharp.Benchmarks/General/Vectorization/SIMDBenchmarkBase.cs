// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace ImageSharp.Benchmarks.General.Vectorization
{
    public abstract class SIMDBenchmarkBase<T>
        where T : struct
    {
        protected T[] input;

        protected T[] result;

        protected T testValue;

        protected Vector<T> testVector;

        protected virtual T GetTestValue() => default;

        protected virtual Vector<T> GetTestVector() => new Vector<T>(this.GetTestValue());

        [Params(32)]
        public int InputSize { get; set; }

        [GlobalSetup]
        public virtual void Setup()
        {
            this.input = new T[this.InputSize];
            this.result = new T[this.InputSize];
            this.testValue = this.GetTestValue();
            this.testVector = this.GetTestVector();
        }

        public abstract class Multiply : SIMDBenchmarkBase<T>
        {
            [Benchmark]
            public void Simd()
            {
                Vector<T> v = this.testVector;

                for (int i = 0; i < this.input.Length; i += Vector<uint>.Count)
                {
                    Vector<T> a = Unsafe.As<T, Vector<T>>(ref this.input[i]);
                    a = a * v;
                    Unsafe.As<T, Vector<T>>(ref this.result[i]) = a;
                }
            }
        }

        public abstract class Divide : SIMDBenchmarkBase<T>
        {
            [Benchmark]
            public void Simd()
            {
                Vector<T> v = this.testVector;

                for (int i = 0; i < this.input.Length; i += Vector<uint>.Count)
                {
                    Vector<T> a = Unsafe.As<T, Vector<T>>(ref this.input[i]);
                    a = a / v;
                    Unsafe.As<T, Vector<T>>(ref this.result[i]) = a;
                }
            }
        }
    }
}

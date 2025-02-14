// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace ImageSharp.Benchmarks.General.Vectorization;

public abstract class SIMDBenchmarkBase<T>
    where T : struct
{
    protected virtual T GetTestValue() => default;

    protected virtual Vector<T> GetTestVector() => new(this.GetTestValue());

    [Params(32)]
    public int InputSize { get; set; }

    protected T[] Input { get; set; }

    protected T[] Result { get; set; }

    protected T TestValue { get; set; }

    protected Vector<T> TestVector { get; set; }

    [GlobalSetup]
    public virtual void Setup()
    {
        this.Input = new T[this.InputSize];
        this.Result = new T[this.InputSize];
        this.TestValue = this.GetTestValue();
        this.TestVector = this.GetTestVector();
    }

    public abstract class Multiply : SIMDBenchmarkBase<T>
    {
        [Benchmark]
        public void Simd()
        {
            Vector<T> v = this.TestVector;

            for (int i = 0; i < this.Input.Length; i += Vector<uint>.Count)
            {
                Vector<T> a = Unsafe.As<T, Vector<T>>(ref this.Input[i]);
                a *= v;
                Unsafe.As<T, Vector<T>>(ref this.Result[i]) = a;
            }
        }
    }

    public abstract class Divide : SIMDBenchmarkBase<T>
    {
        [Benchmark]
        public void Simd()
        {
            Vector<T> v = this.TestVector;

            for (int i = 0; i < this.Input.Length; i += Vector<uint>.Count)
            {
                Vector<T> a = Unsafe.As<T, Vector<T>>(ref this.Input[i]);
                a /= v;
                Unsafe.As<T, Vector<T>>(ref this.Result[i]) = a;
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.Vectorization
{
    public class BitwiseOrUInt32
    {
        private uint[] input;

        private uint[] result;

        [Params(32)]
        public int InputSize { get; set; }

        private uint testValue;

        [GlobalSetup]
        public void Setup()
        {
            this.input = new uint[this.InputSize];
            this.result = new uint[this.InputSize];
            this.testValue = 42;

            for (int i = 0; i < this.InputSize; i++)
            {
                this.input[i] = (uint)i;
            }
        }

        [Benchmark(Baseline = true)]
        public void Standard()
        {
            uint v = this.testValue;
            for (int i = 0; i < this.input.Length; i++)
            {
                this.result[i] = this.input[i] | v;
            }
        }

        [Benchmark]
        public void Simd()
        {
            var v = new Vector<uint>(this.testValue);

            for (int i = 0; i < this.input.Length; i += Vector<uint>.Count)
            {
                var a = new Vector<uint>(this.input, i);
                a = Vector.BitwiseOr(a, v);
                a.CopyTo(this.result, i);
            }
        }
    }
}

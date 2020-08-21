// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.Vectorization
{
    public class ReinterpretUInt32AsFloat
    {
        private uint[] input;

        private float[] result;

        [Params(32)]
        public int InputSize { get; set; }

        [StructLayout(LayoutKind.Explicit)]
        private struct UIntFloatUnion
        {
            [FieldOffset(0)]
            public float F;

            [FieldOffset(0)]
            public uint I;
        }

        [GlobalSetup]
        public void Setup()
        {
            this.input = new uint[this.InputSize];
            this.result = new float[this.InputSize];
            for (int i = 0; i < this.InputSize; i++)
            {
                this.input[i] = (uint)i;
            }
        }

        [Benchmark(Baseline = true)]
        public void Standard()
        {
            UIntFloatUnion u = default;
            for (int i = 0; i < this.input.Length; i++)
            {
                u.I = this.input[i];
                this.result[i] = u.F;
            }
        }

        [Benchmark]
        public void Simd()
        {
            for (int i = 0; i < this.input.Length; i += Vector<uint>.Count)
            {
                var a = new Vector<uint>(this.input, i);
                var b = Vector.AsVectorSingle(a);
                b.CopyTo(this.result, i);
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath
{
    [LongRunJob]
    public class ModuloPowerOfTwoConstant
    {
        private readonly int value = 42;

        [Benchmark(Baseline = true)]
        public int Standard()
        {
            return this.value % 8;
        }

        [Benchmark]
        public int Bitwise()
        {
            return Numerics.Modulo8(this.value);
        }
    }
}

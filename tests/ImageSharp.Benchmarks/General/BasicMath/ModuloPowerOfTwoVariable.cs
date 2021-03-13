// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath
{
    [LongRunJob]
    public class ModuloPowerOfTwoVariable
    {
        private readonly int value = 42;

        private readonly int m = 32;

        [Benchmark(Baseline = true)]
        public int Standard()
        {
            return this.value % this.m;
        }

        [Benchmark]
        public int Bitwise()
        {
            return Numerics.ModuloP2(this.value, this.m);
        }

        // RESULTS:
        //
        //    Method |      Mean |     Error |    StdDev |    Median | Scaled | ScaledSD |
        // --------- |----------:|----------:|----------:|----------:|-------:|---------:|
        //  Standard | 1.2465 ns | 0.0093 ns | 0.0455 ns | 1.2423 ns |   1.00 |     0.00 |
        //   Bitwise | 0.0265 ns | 0.0103 ns | 0.0515 ns | 0.0000 ns |   0.02 |     0.04 |
    }
}

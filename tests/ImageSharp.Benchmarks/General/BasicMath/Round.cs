// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath
{
    public class Round
    {
        private const float Input = .51F;

        [Benchmark]
        public int ConvertTo() => Convert.ToInt32(Input);

        [Benchmark]
        public int MathRound() => (int)Math.Round(Input);

        // Results 20th Jan 2019
        //    Method |      Mean |     Error |    StdDev |    Median |
        //---------- |----------:|----------:|----------:|----------:|
        // ConvertTo | 3.1967 ns | 0.1234 ns | 0.2129 ns | 3.2340 ns |
        // MathRound | 0.0528 ns | 0.0374 ns | 0.1079 ns | 0.0000 ns |
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath
{
    public class Abs
    {
        [Params(-1, 1)]
        public int X { get; set; }

        [Benchmark(Baseline = true, Description = "Maths Abs")]
        public int MathAbs()
        {
            int x = this.X;
            return Math.Abs(x);
        }

        [Benchmark(Description = "Conditional Abs")]
        public int ConditionalAbs()
        {
            int x = this.X;
            return x < 0 ? -x : x;
        }

        [Benchmark(Description = "Bitwise Abs")]
        public int AbsBitwise()
        {
            int x = this.X;
            return (x ^ (x >> 31)) - (x >> 31);
        }

        [Benchmark(Description = "Bitwise Abs With Variable")]
        public int AbsBitwiseVer()
        {
            int x = this.X;
            int y = x >> 31;
            return (x ^ y) - y;
        }
    }
}

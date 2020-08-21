// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath
{
    public class Pow
    {
        [Params(-1.333F, 1.333F)]
        public float X { get; set; }

        [Benchmark(Baseline = true, Description = "Math.Pow 2")]
        public float MathPow()
        {
            float x = this.X;
            return (float)Math.Pow(x, 2);
        }

        [Benchmark(Description = "Pow x2")]
        public float PowMult()
        {
            float x = this.X;
            return x * x;
        }

        [Benchmark(Description = "Math.Pow 3")]
        public float MathPow3()
        {
            float x = this.X;
            return (float)Math.Pow(x, 3);
        }

        [Benchmark(Description = "Pow x3")]
        public float PowMult3()
        {
            float x = this.X;
            return x * x * x;
        }
    }
}

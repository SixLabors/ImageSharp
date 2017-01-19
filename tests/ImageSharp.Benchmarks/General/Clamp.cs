// <copyright file="Clamp.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.General
{
    using System;

    using BenchmarkDotNet.Attributes;

    public class Clamp
    {
        [Params(-1, 0, 255, 256)]
        public int Value { get; set; }

        [Benchmark(Baseline = true, Description = "Maths Clamp")]
        public byte ClampMaths()
        {
            int value = this.Value;
            return (byte)Math.Min(Math.Max(0, value), 255);
        }

        [Benchmark(Description = "No Maths Clamp")]
        public byte ClampNoMaths()
        {
            int value = this.Value;
            value = value >= 255 ? 255 : value;
            return (byte)(value <= 0 ? 0 : value);
        }

        [Benchmark(Description = "No Maths No Equals Clamp")]
        public byte ClampNoMathsNoEquals()
        {
            int value = this.Value;
            value = value > 255 ? 255 : value;
            return (byte)(value < 0 ? 0 : value);
        }

        [Benchmark(Description = "No Maths Clamp No Ternary")]
        public byte ClampNoMathsNoTernary()
        {
            int value = this.Value;

            if (value >= 255)
            {
                return 255;
            }

            if (value <= 0)
            {
                return 0;
            }

            return (byte)value;
        }

        [Benchmark(Description = "No Maths No Equals Clamp No Ternary")]
        public byte ClampNoMathsEqualsNoTernary()
        {
            int value = this.Value;

            if (value > 255)
            {
                return 255;
            }

            if (value < 0)
            {
                return 0;
            }

            return (byte)value;
        }
    }
}

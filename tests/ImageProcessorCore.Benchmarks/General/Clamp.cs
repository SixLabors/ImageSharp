// <copyright file="Clamp.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Benchmarks.General
{
    using System;

    using BenchmarkDotNet.Attributes;

    public class Clamp
    {
        [Benchmark(Baseline = true, Description = "Maths Clamp")]
        public byte ClampMaths()
        {
            double value = 256;
            return (byte)Math.Min(Math.Max(0, value), 255);
        }

        [Benchmark(Description = "No Maths Clamp")]
        public byte ClampNoMaths()
        {
            double value = 256;
            value = (value > 255) ? 255 : value;
            value = (value < 0) ? 0 : value;
            return (byte)value;
        }

        [Benchmark(Description = "No Maths Clamp No Ternary")]
        public byte ClampNoMathsNoTernary()
        {
            double value = 256;

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

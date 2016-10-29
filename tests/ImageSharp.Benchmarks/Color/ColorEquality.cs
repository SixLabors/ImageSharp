// <copyright file="ColorEquality.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks
{
    using BenchmarkDotNet.Attributes;

    using CoreColor = ImageSharp.Color;
    using SystemColor = System.Drawing.Color;

    public class ColorEquality
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Color Equals")]
        public bool SystemDrawingColorEqual()
        {
            return SystemColor.FromArgb(128, 128, 128, 128).Equals(SystemColor.FromArgb(128, 128, 128, 128));
        }

        [Benchmark(Description = "ImageSharp Color Equals")]
        public bool ColorEqual()
        {
            return new CoreColor(128, 128, 128, 128).Equals(new CoreColor(128, 128, 128, 128));
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks
{
    public class YcbCrToRgb
    {
        [Benchmark(Baseline = true, Description = "Floating Point Conversion")]
        public Vector3 YcbCrToRgba()
        {
            int y = 255;
            int cb = 128;
            int cr = 128;

            int ccb = cb - 128;
            int ccr = cr - 128;

            byte r = (byte)Numerics.Clamp(y + (1.402F * ccr), 0, 255);
            byte g = (byte)Numerics.Clamp(y - (0.34414F * ccb) - (0.71414F * ccr), 0, 255);
            byte b = (byte)Numerics.Clamp(y + (1.772F * ccb), 0, 255);

            return new Vector3(r, g, b);
        }

        [Benchmark(Description = "Scaled Integer Conversion")]
        public Vector3 YcbCrToRgbaScaled()
        {
            int y = 255;
            int cb = 128;
            int cr = 128;

            int ccb = cb - 128;
            int ccr = cr - 128;

            // Scale by 1024, add .5F and truncate value
            int r0 = 1436 * ccr; // (1.402F * 1024) + .5F
            int g0 = 352 * ccb; // (0.34414F * 1024) + .5F
            int g1 = 731 * ccr; // (0.71414F  * 1024) + .5F
            int b0 = 1815 * ccb; // (1.772F * 1024) + .5F

            byte r = (byte)Numerics.Clamp(y + (r0 >> 10), 0, 255);
            byte g = (byte)Numerics.Clamp(y - (g0 >> 10) - (g1 >> 10), 0, 255);
            byte b = (byte)Numerics.Clamp(y + (b0 >> 10), 0, 255);

            return new Vector3(r, g, b);
        }
    }
}

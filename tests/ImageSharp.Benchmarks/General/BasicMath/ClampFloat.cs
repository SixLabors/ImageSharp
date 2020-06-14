// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath
{
    public class ClampFloat
    {
        private readonly float min = -1.5f;
        private readonly float max = 2.5f;
        private static readonly float[] Values = { -10, -5, -3, -1.5f, -0.5f, 0f, 1f, 1.5f, 2.5f, 3, 10 };

        [Benchmark(Baseline = true)]
        public float UsingMathF()
        {
            float acc = 0;

            for (int i = 0; i < Values.Length; i++)
            {
                acc += ClampUsingMathF(Values[i], this.min, this.max);
            }

            return acc;
        }

        [Benchmark]
        public float UsingBranching()
        {
            float acc = 0;

            for (int i = 0; i < Values.Length; i++)
            {
                acc += ClampUsingBranching(Values[i], this.min, this.max);
            }

            return acc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ClampUsingMathF(float x, float min, float max)
        {
            return Math.Min(max, Math.Max(min, x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ClampUsingBranching(float x, float min, float max)
        {
            if (x >= max)
            {
                return max;
            }

            if (x <= min)
            {
                return min;
            }

            return x;
        }

        // RESULTS:
        //          Method |     Mean |     Error |    StdDev | Scaled |
        // --------------- |---------:|----------:|----------:|-------:|
        //      UsingMathF | 30.37 ns | 0.3764 ns | 0.3337 ns |   1.00 |
        //  UsingBranching | 18.66 ns | 0.1043 ns | 0.0871 ns |   0.61 |
    }
}

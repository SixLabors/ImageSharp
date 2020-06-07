// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath
{
    public class ClampVector4
    {
        private readonly float min = -1.5f;
        private readonly float max = 2.5f;
        private static readonly float[] Values = { -10, -5, -3, -1.5f, -0.5f, 0f, 1f, 1.5f, 2.5f, 3, 10 };

        [Benchmark(Baseline = true)]
        public Vector4 UsingVectorClamp()
        {
            Vector4 acc = Vector4.Zero;

            for (int i = 0; i < Values.Length; i++)
            {
                acc += ClampUsingVectorClamp(Values[i], this.min, this.max);
            }

            return acc;
        }

        [Benchmark]
        public Vector4 UsingVectorMinMax()
        {
            Vector4 acc = Vector4.Zero;

            for (int i = 0; i < Values.Length; i++)
            {
                acc += ClampUsingVectorMinMax(Values[i], this.min, this.max);
            }

            return acc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 ClampUsingVectorClamp(float x, float min, float max)
        {
            return Vector4.Clamp(new Vector4(x), new Vector4(min), new Vector4(max));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 ClampUsingVectorMinMax(float x, float min, float max)
        {
            return Vector4.Min(new Vector4(max), Vector4.Max(new Vector4(min), new Vector4(x)));
        }

        // RESULTS
        // |            Method |     Mean |    Error |   StdDev | Ratio |
        // |------------------ |---------:|---------:|---------:|------:|
        // |  UsingVectorClamp | 75.21 ns | 1.572 ns | 4.057 ns |  1.00 |
        // | UsingVectorMinMax | 15.35 ns | 0.356 ns | 0.789 ns |  0.20 |
    }
}

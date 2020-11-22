// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath
{
    public class ClampSpan
    {
        private static readonly int[] A = new int[2048];
        private static readonly int[] B = new int[2048];

        public void Setup()
        {
            var r = new Random();

            for (int i = 0; i < A.Length; i++)
            {
                int x = r.Next();
                A[i] = x;
                B[i] = x;
            }
        }

        [Benchmark(Baseline = true)]
        public void ClampNoIntrinsics()
        {
            for (int i = 0; i < A.Length; i++)
            {
                ref int x = ref A[i];
                x = x.Clamp(64, 128);
            }
        }

        [Benchmark]
        public void ClampVectorIntrinsics()
        {
            Numerics.Clamp(B, 64, 128);
        }
    }
}

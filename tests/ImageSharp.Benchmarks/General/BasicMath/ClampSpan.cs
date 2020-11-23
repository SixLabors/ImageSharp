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
                x = Numerics.Clamp(x, 64, 128);
            }
        }

        [Benchmark]
        public void ClampVectorIntrinsics()
        {
            Numerics.Clamp(B, 64, 128);
        }
    }
}

// 23-11-2020
// ##########
//
// BenchmarkDotNet = v0.12.1, OS = Windows 10.0.19041.630(2004 /?/ 20H1)
// Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
// .NET Core SDK=5.0.100
//  [Host]     : .NET Core 3.1.10 (CoreCLR 4.700.20.51601, CoreFX 4.700.20.51901), X64 RyuJIT
//  DefaultJob : .NET Core 3.1.10 (CoreCLR 4.700.20.51601, CoreFX 4.700.20.51901), X64 RyuJIT
//
//
// |                Method |       Mean |     Error |     StdDev |  Ratio |
// |---------------------- |-----------:| ---------:| ----------:| ------:|
// |     ClampNoIntrinsics | 3,629.9 ns |  70.80 ns |  129.47 ns |   1.00 |
// | ClampVectorIntrinsics |   131.9 ns |   2.68 ns |  6.66 ns   |   0.04 |

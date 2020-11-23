// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.ShortCore31))]
    public class PremultiplyVector4
    {
        private static readonly Vector4[] Vectors = CreateVectors();

        [Benchmark(Baseline = true)]
        public void PremultiplyBaseline()
        {
            ref Vector4 baseRef = ref MemoryMarshal.GetReference<Vector4>(Vectors);

            for (int i = 0; i < Vectors.Length; i++)
            {
                ref Vector4 v = ref Unsafe.Add(ref baseRef, i);
                Premultiply(ref v);
            }
        }

        [Benchmark]
        public void Premultiply()
        {
            Numerics.Premultiply(Vectors);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Premultiply(ref Vector4 source)
        {
            float w = source.W;
            source *= w;
            source.W = w;
        }

        private static Vector4[] CreateVectors()
        {
            var rnd = new Random(42);
            return GenerateRandomVectorArray(rnd, 2048, 0, 1);
        }

        private static Vector4[] GenerateRandomVectorArray(Random rnd, int length, float minVal, float maxVal)
        {
            var values = new Vector4[length];

            for (int i = 0; i < length; i++)
            {
                ref Vector4 v = ref values[i];
                v.X = GetRandomFloat(rnd, minVal, maxVal);
                v.Y = GetRandomFloat(rnd, minVal, maxVal);
                v.Z = GetRandomFloat(rnd, minVal, maxVal);
                v.W = GetRandomFloat(rnd, minVal, maxVal);
            }

            return values;
        }

        private static float GetRandomFloat(Random rnd, float minVal, float maxVal)
            => ((float)rnd.NextDouble() * (maxVal - minVal)) + minVal;
    }
}

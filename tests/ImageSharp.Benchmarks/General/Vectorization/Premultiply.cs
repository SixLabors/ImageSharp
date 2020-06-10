// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.Vectorization
{
    public class Premultiply
    {
        [Benchmark(Baseline = true)]
        public Vector4 PremultiplyByVal()
        {
            var input = new Vector4(.5F);
            return Vector4Utils.Premultiply(input);
        }

        [Benchmark]
        public Vector4 PremultiplyByRef()
        {
            var input = new Vector4(.5F);
            Vector4Utils.PremultiplyRef(ref input);
            return input;
        }

        [Benchmark]
        public Vector4 PremultiplyRefWithPropertyAssign()
        {
            var input = new Vector4(.5F);
            Vector4Utils.PremultiplyRefWithPropertyAssign(ref input);
            return input;
        }
    }

    internal static class Vector4Utils
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Vector4 Premultiply(Vector4 source)
        {
            float w = source.W;
            Vector4 premultiplied = source * w;
            premultiplied.W = w;
            return premultiplied;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PremultiplyRef(ref Vector4 source)
        {
            float w = source.W;
            source *= w;
            source.W = w;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PremultiplyRefWithPropertyAssign(ref Vector4 source)
        {
            float w = source.W;
            source *= new Vector4(w) { W = 1 };
        }
    }
}

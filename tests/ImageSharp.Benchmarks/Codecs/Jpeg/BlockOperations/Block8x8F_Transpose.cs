// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg.BlockOperations
{
    [Config(typeof(Config.HwIntrinsics_SSE_AVX))]
    public class Block8x8F_Transpose
    {
        private static readonly Block8x8F Source = Create8x8FloatData();

        [Benchmark]
        public void TransposeInto()
        {
            var dest = default(Block8x8F);
            Source.TransposeInto(ref dest);
        }

        private static Block8x8F Create8x8FloatData()
        {
            var result = new float[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[(i * 8) + j] = (i * 10) + j;
                }
            }

            var source = default(Block8x8F);
            source.LoadFrom(result);
            return source;
        }
    }
}

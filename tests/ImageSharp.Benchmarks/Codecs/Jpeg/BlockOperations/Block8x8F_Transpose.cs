// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg.BlockOperations
{
    [Config(typeof(Config.HwIntrinsics_SSE_AVX))]
    public class Block8x8F_Transpose
    {
        private Block8x8F source = Create8x8FloatData();

        [Benchmark]
        public float TransposeInto()
        {
            this.source.Transpose();
            return this.source[0];
        }

        private static Block8x8F Create8x8FloatData()
        {
            Block8x8F block = default;
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    block[(i * 8) + j] = (i * 10) + j;
                }
            }

            return block;
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg.BlockOperations
{
    [Config(typeof(Config.HwIntrinsics_SSE_AVX))]
    public class Block8x8F_Quantize
    {
        private Block8x8F block = default;
        private Block8x8F quant = default;
        private Block8x8 result = default;

        [Benchmark]
        public short Quantize()
        {
            Block8x8F.Quantize(ref this.block, ref this.result, ref this.quant);
            return this.result[0];
        }
    }
}

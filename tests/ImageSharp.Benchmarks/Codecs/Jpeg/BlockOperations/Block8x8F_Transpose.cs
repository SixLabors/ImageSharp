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
        public float TransposeInplace()
        {
            this.source.TransposeInplace();
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

/*
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19042.1165 (20H2/October2020Update)
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.3.21202.5
  [Host]          : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT
  AVX             : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT
  No HwIntrinsics : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT

|        Method |             Job |      Mean |     Error |    StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------- |---------------- |----------:|----------:|----------:|------:|------:|------:|------:|----------:|
| TransposeInto | No HwIntrinsics | 19.658 ns | 0.0550 ns | 0.0515 ns |  1.00 |     - |     - |     - |         - |
| TransposeInto |             AVX |  8.613 ns | 0.0249 ns | 0.0208 ns |  0.44 |     - |     - |     - |         - |
*/

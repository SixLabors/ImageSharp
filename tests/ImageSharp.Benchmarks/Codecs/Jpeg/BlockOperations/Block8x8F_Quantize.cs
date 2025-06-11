// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg.BlockOperations;

[Config(typeof(Config.HwIntrinsics_SSE_AVX))]
public class Block8x8F_Quantize
{
    private Block8x8F block = CreateFromScalar(1);
    private Block8x8F quant = CreateFromScalar(1);
    private Block8x8 result;

    [Benchmark]
    public short Quantize()
    {
        Block8x8F.Quantize(ref this.block, ref this.result, ref this.quant);
        return this.result[0];
    }

    private static Block8x8F CreateFromScalar(float scalar)
    {
        Block8x8F block = default;
        for (int i = 0; i < 64; i++)
        {
            block[i] = scalar;
        }

        return block;
    }
}

/*
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19042.1165 (20H2/October2020Update)
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.3.21202.5
[Host]             : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT
1. No HwIntrinsics : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT
2. SSE             : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT
3. AVX             : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT

|   Method |             Job |     Mean |    Error |   StdDev | Ratio |
|--------- |-----------------|---------:|---------:|---------:|------:|
| Quantize | No HwIntrinsics | 73.34 ns | 1.081 ns | 1.011 ns |  1.00 |
| Quantize |             SSE | 24.11 ns | 0.298 ns | 0.279 ns |  0.33 |
| Quantize |             AVX | 15.90 ns | 0.074 ns | 0.065 ns |  0.22 |
*/

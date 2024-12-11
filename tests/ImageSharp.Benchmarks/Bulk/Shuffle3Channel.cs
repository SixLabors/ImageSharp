// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

[Config(typeof(Config.HwIntrinsics_SSE_AVX))]
public class Shuffle3Channel
{
    private static readonly DefaultShuffle3 Control = new(SimdUtils.Shuffle.MMShuffle3102);
    private byte[] source;
    private byte[] destination;

    [GlobalSetup]
    public void Setup()
    {
        this.source = new byte[this.Count];
        new Random(this.Count).NextBytes(this.source);
        this.destination = new byte[this.Count];
    }

    [Params(96, 384, 768, 1536)]
    public int Count { get; set; }

    [Benchmark]
    public void Shuffle3()
        => SimdUtils.Shuffle3(this.source, this.destination, Control);
}

// 2020-11-02
// ##########
//
// BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.572 (2004/?/20H1)
// Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
// .NET Core SDK=3.1.403
//  [Host]             : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
//  1. No HwIntrinsics : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
//  2. AVX             : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
//  3. SSE             : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
//
// Runtime=.NET Core 3.1
//
// |               Method |                Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev |    Median | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |--------------------- |------------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|----------:|------:|--------:|------:|------:|------:|----------:|
// |             Shuffle3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |    96 |  48.46 ns | 1.034 ns | 2.438 ns |  47.46 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Shuffle3 |             2. AVX |                                             Empty |    96 |  32.42 ns | 0.537 ns | 0.476 ns |  32.34 ns |  0.66 |    0.04 |     - |     - |     - |         - |
// |             Shuffle3 |             3. SSE |                               DOTNET_EnableAVX=0 |    96 |  32.51 ns | 0.373 ns | 0.349 ns |  32.56 ns |  0.66 |    0.03 |     - |     - |     - |         - |
// |                      |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// |             Shuffle3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   384 | 199.04 ns | 1.512 ns | 1.180 ns | 199.17 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Shuffle3 |             2. AVX |                                             Empty |   384 |  71.20 ns | 2.654 ns | 7.784 ns |  69.60 ns |  0.41 |    0.02 |     - |     - |     - |         - |
// |             Shuffle3 |             3. SSE |                               DOTNET_EnableAVX=0 |   384 |  63.23 ns | 0.569 ns | 0.505 ns |  63.21 ns |  0.32 |    0.00 |     - |     - |     - |         - |
// |                      |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// |             Shuffle3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   768 | 391.28 ns | 5.087 ns | 3.972 ns | 391.22 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Shuffle3 |             2. AVX |                                             Empty |   768 | 109.12 ns | 2.149 ns | 2.010 ns | 108.66 ns |  0.28 |    0.01 |     - |     - |     - |         - |
// |             Shuffle3 |             3. SSE |                               DOTNET_EnableAVX=0 |   768 | 106.51 ns | 0.734 ns | 0.613 ns | 106.56 ns |  0.27 |    0.00 |     - |     - |     - |         - |
// |                      |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// |             Shuffle3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1536 | 773.70 ns | 5.516 ns | 4.890 ns | 772.96 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Shuffle3 |             2. AVX |                                             Empty |  1536 | 190.41 ns | 1.090 ns | 0.851 ns | 190.38 ns |  0.25 |    0.00 |     - |     - |     - |         - |
// |             Shuffle3 |             3. SSE |                               DOTNET_EnableAVX=0 |  1536 | 190.94 ns | 0.985 ns | 0.769 ns | 190.85 ns |  0.25 |    0.00 |     - |     - |     - |         - |

// 2023-02-21
// ##########
//
// BenchmarkDotNet=v0.13.0, OS=Windows 10.0.22621
// 11th Gen Intel Core i7-11370H 3.30GHz, 1 CPU, 8 logical and 4 physical cores
// .NET SDK= 7.0.103
//  [Host]             : .NET 6.0.14 (6.0.1423.7309), X64 RyuJIT
//  1. No HwIntrinsics : .NET 6.0.14 (6.0.1423.7309), X64 RyuJIT
//  2. SSE             : .NET 6.0.14 (6.0.1423.7309), X64 RyuJIT
//  3. AVX             : .NET 6.0.14 (6.0.1423.7309), X64 RyuJIT

// Runtime=.NET 6.0

// |   Method |                Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |--------- |------------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|------:|------:|------:|------:|----------:|
// | Shuffle3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |    96 |  44.55 ns | 0.564 ns | 0.528 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle3 |             2. SSE |                               DOTNET_EnableAVX=0 |    96 |  15.46 ns | 0.064 ns | 0.060 ns |  0.35 |     - |     - |     - |         - |
// | Shuffle3 |             3. AVX |                                             Empty |    96 |  15.18 ns | 0.056 ns | 0.053 ns |  0.34 |     - |     - |     - |         - |
// |          |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Shuffle3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   384 | 155.68 ns | 0.539 ns | 0.504 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle3 |             2. SSE |                               DOTNET_EnableAVX=0 |   384 |  30.04 ns | 0.100 ns | 0.089 ns |  0.19 |     - |     - |     - |         - |
// | Shuffle3 |             3. AVX |                                             Empty |   384 |  29.70 ns | 0.061 ns | 0.054 ns |  0.19 |     - |     - |     - |         - |
// |          |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Shuffle3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   768 | 302.76 ns | 1.023 ns | 0.957 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle3 |             2. SSE |                               DOTNET_EnableAVX=0 |   768 |  50.24 ns | 0.098 ns | 0.092 ns |  0.17 |     - |     - |     - |         - |
// | Shuffle3 |             3. AVX |                                             Empty |   768 |  49.28 ns | 0.156 ns | 0.131 ns |  0.16 |     - |     - |     - |         - |
// |          |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Shuffle3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1536 | 596.53 ns | 2.675 ns | 2.503 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle3 |             2. SSE |                               DOTNET_EnableAVX=0 |  1536 |  94.09 ns | 0.312 ns | 0.260 ns |  0.16 |     - |     - |     - |         - |
// | Shuffle3 |             3. AVX |                                             Empty |  1536 |  93.57 ns | 0.196 ns | 0.183 ns |  0.16 |     - |     - |     - |         - |

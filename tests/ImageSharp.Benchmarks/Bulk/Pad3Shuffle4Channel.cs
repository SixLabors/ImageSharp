// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

[Config(typeof(Config.HwIntrinsics_SSE_AVX))]
public class Pad3Shuffle4Channel
{
    private static readonly DefaultPad3Shuffle4 Control = new(SimdUtils.Shuffle.MMShuffle1032);
    private byte[] source;
    private byte[] destination;

    [GlobalSetup]
    public void Setup()
    {
        this.source = new byte[this.Count];
        new Random(this.Count).NextBytes(this.source);
        this.destination = new byte[this.Count * 4 / 3];
    }

    [Params(96, 384, 768, 1536)]
    public int Count { get; set; }

    [Benchmark]
    public void Pad3Shuffle4()
        => SimdUtils.Pad3Shuffle4(this.source, this.destination, Control);

    [Benchmark]
    public void Pad3Shuffle4FastFallback()
        => SimdUtils.Pad3Shuffle4(this.source, this.destination, default(XYZWPad3Shuffle4));
}

// 2020-10-30
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
// |                   Method |                Job |                              EnvironmentVariables | Count |        Mean |     Error |    StdDev |      Median | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |------------------------- |------------------- |-------------------------------------------------- |------ |------------:|----------:|----------:|------------:|------:|--------:|------:|------:|------:|----------:|
// |             Pad3Shuffle4 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |    96 |   120.64 ns |  7.190 ns | 21.200 ns |   114.26 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             2. AVX |                                             Empty |    96 |    23.63 ns |  0.175 ns |  0.155 ns |    23.65 ns |  0.15 |    0.01 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             3. SSE |                               DOTNET_EnableAVX=0 |    96 |    25.25 ns |  0.356 ns |  0.298 ns |    25.27 ns |  0.17 |    0.01 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |             |           |           |             |       |         |       |       |       |           |
// | Pad3Shuffle4FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |    96 |    14.80 ns |  0.358 ns |  1.032 ns |    14.64 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             2. AVX |                                             Empty |    96 |    24.84 ns |  0.376 ns |  0.333 ns |    24.74 ns |  1.57 |    0.06 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             3. SSE |                               DOTNET_EnableAVX=0 |    96 |    24.58 ns |  0.471 ns |  0.704 ns |    24.38 ns |  1.60 |    0.09 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |             |           |           |             |       |         |       |       |       |           |
// |             Pad3Shuffle4 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   384 |   258.92 ns |  4.873 ns |  4.069 ns |   257.95 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             2. AVX |                                             Empty |   384 |    41.41 ns |  0.859 ns |  1.204 ns |    41.33 ns |  0.16 |    0.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             3. SSE |                               DOTNET_EnableAVX=0 |   384 |    40.74 ns |  0.848 ns |  0.793 ns |    40.48 ns |  0.16 |    0.00 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |             |           |           |             |       |         |       |       |       |           |
// | Pad3Shuffle4FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   384 |    74.50 ns |  0.490 ns |  0.383 ns |    74.49 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             2. AVX |                                             Empty |   384 |    40.74 ns |  0.624 ns |  0.584 ns |    40.72 ns |  0.55 |    0.01 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             3. SSE |                               DOTNET_EnableAVX=0 |   384 |    38.28 ns |  0.534 ns |  0.417 ns |    38.22 ns |  0.51 |    0.01 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |             |           |           |             |       |         |       |       |       |           |
// |             Pad3Shuffle4 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   768 |   503.91 ns |  6.466 ns |  6.048 ns |   501.58 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             2. AVX |                                             Empty |   768 |    62.86 ns |  0.332 ns |  0.277 ns |    62.80 ns |  0.12 |    0.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             3. SSE |                               DOTNET_EnableAVX=0 |   768 |    64.59 ns |  0.469 ns |  0.415 ns |    64.62 ns |  0.13 |    0.00 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |             |           |           |             |       |         |       |       |       |           |
// | Pad3Shuffle4FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   768 |   110.51 ns |  0.592 ns |  0.554 ns |   110.33 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             2. AVX |                                             Empty |   768 |    64.72 ns |  1.306 ns |  1.090 ns |    64.51 ns |  0.59 |    0.01 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             3. SSE |                               DOTNET_EnableAVX=0 |   768 |    62.11 ns |  0.816 ns |  0.682 ns |    61.98 ns |  0.56 |    0.01 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |             |           |           |             |       |         |       |       |       |           |
// |             Pad3Shuffle4 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1536 | 1,005.84 ns | 13.176 ns | 12.325 ns | 1,004.70 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             2. AVX |                                             Empty |  1536 |   110.05 ns |  0.256 ns |  0.214 ns |   110.04 ns |  0.11 |    0.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             3. SSE |                               DOTNET_EnableAVX=0 |  1536 |   110.23 ns |  0.545 ns |  0.483 ns |   110.09 ns |  0.11 |    0.00 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |             |           |           |             |       |         |       |       |       |           |
// | Pad3Shuffle4FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1536 |   220.37 ns |  1.601 ns |  1.419 ns |   220.13 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             2. AVX |                                             Empty |  1536 |   111.54 ns |  2.173 ns |  2.901 ns |   111.27 ns |  0.51 |    0.01 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             3. SSE |                               DOTNET_EnableAVX=0 |  1536 |   110.23 ns |  0.456 ns |  0.427 ns |   110.25 ns |  0.50 |    0.00 |     - |     - |     - |         - |

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

// |                   Method |                Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |------------------------- |------------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|------:|------:|------:|------:|----------:|
// |             Pad3Shuffle4 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |    96 |  57.45 ns | 0.126 ns | 0.118 ns |  1.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             2. SSE |                               DOTNET_EnableAVX=0 |    96 |  14.70 ns | 0.105 ns | 0.098 ns |  0.26 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             3. AVX |                                             Empty |    96 |  14.63 ns | 0.070 ns | 0.062 ns |  0.25 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Pad3Shuffle4FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |    96 |  12.08 ns | 0.028 ns | 0.025 ns |  1.00 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             2. SSE |                               DOTNET_EnableAVX=0 |    96 |  14.04 ns | 0.050 ns | 0.044 ns |  1.16 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             3. AVX |                                             Empty |    96 |  13.90 ns | 0.086 ns | 0.080 ns |  1.15 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |           |          |          |       |       |       |       |           |
// |             Pad3Shuffle4 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   384 | 202.67 ns | 2.010 ns | 1.678 ns |  1.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             2. SSE |                               DOTNET_EnableAVX=0 |   384 |  25.54 ns | 0.060 ns | 0.053 ns |  0.13 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             3. AVX |                                             Empty |   384 |  25.72 ns | 0.139 ns | 0.130 ns |  0.13 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Pad3Shuffle4FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   384 |  60.35 ns | 0.080 ns | 0.071 ns |  1.00 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             2. SSE |                               DOTNET_EnableAVX=0 |   384 |  25.18 ns | 0.388 ns | 0.324 ns |  0.42 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             3. AVX |                                             Empty |   384 |  26.21 ns | 0.067 ns | 0.059 ns |  0.43 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |           |          |          |       |       |       |       |           |
// |             Pad3Shuffle4 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   768 | 393.88 ns | 1.353 ns | 1.199 ns |  1.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             2. SSE |                               DOTNET_EnableAVX=0 |   768 |  39.44 ns | 0.230 ns | 0.204 ns |  0.10 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             3. AVX |                                             Empty |   768 |  39.51 ns | 0.108 ns | 0.101 ns |  0.10 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Pad3Shuffle4FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   768 | 112.02 ns | 0.140 ns | 0.131 ns |  1.00 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             2. SSE |                               DOTNET_EnableAVX=0 |   768 |  38.60 ns | 0.091 ns | 0.080 ns |  0.34 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             3. AVX |                                             Empty |   768 |  38.18 ns | 0.100 ns | 0.084 ns |  0.34 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |           |          |          |       |       |       |       |           |
// |             Pad3Shuffle4 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1536 | 777.95 ns | 1.719 ns | 1.342 ns |  1.00 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             2. SSE |                               DOTNET_EnableAVX=0 |  1536 |  73.11 ns | 0.090 ns | 0.075 ns |  0.09 |     - |     - |     - |         - |
// |             Pad3Shuffle4 |             3. AVX |                                             Empty |  1536 |  73.41 ns | 0.125 ns | 0.117 ns |  0.09 |     - |     - |     - |         - |
// |                          |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Pad3Shuffle4FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1536 | 218.14 ns | 0.377 ns | 0.334 ns |  1.00 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             2. SSE |                               DOTNET_EnableAVX=0 |  1536 |  72.55 ns | 1.418 ns | 1.184 ns |  0.33 |     - |     - |     - |         - |
// | Pad3Shuffle4FastFallback |             3. AVX |                                             Empty |  1536 |  73.15 ns | 0.330 ns | 0.292 ns |  0.34 |     - |     - |     - |         - |

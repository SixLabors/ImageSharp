// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

[Config(typeof(Config.HwIntrinsics_SSE_AVX))]
public class ShuffleByte4Channel
{
    private byte[] source;
    private byte[] destination;

    [GlobalSetup]
    public void Setup()
    {
        this.source = new byte[this.Count];
        new Random(this.Count).NextBytes(this.source);
        this.destination = new byte[this.Count];
    }

    [Params(128, 256, 512, 1024, 2048)]
    public int Count { get; set; }

    [Benchmark]
    public void Shuffle4Channel()
        => SimdUtils.Shuffle4<WXYZShuffle4>(this.source, this.destination, default);
}

// 2020-10-29
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
// |          Method |                Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |---------------- |------------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|------:|--------:|------:|------:|------:|----------:|
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   128 |  17.39 ns | 0.187 ns | 0.175 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. AVX |                                             Empty |   128 |  21.72 ns | 0.299 ns | 0.279 ns |  1.25 |    0.02 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. SSE |                               DOTNET_EnableAVX=0 |   128 |  18.10 ns | 0.346 ns | 0.289 ns |  1.04 |    0.02 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   256 |  35.51 ns | 0.711 ns | 0.790 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. AVX |                                             Empty |   256 |  23.90 ns | 0.508 ns | 0.820 ns |  0.69 |    0.02 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. SSE |                               DOTNET_EnableAVX=0 |   256 |  20.40 ns | 0.133 ns | 0.111 ns |  0.57 |    0.01 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   512 |  73.39 ns | 0.310 ns | 0.259 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. AVX |                                             Empty |   512 |  26.10 ns | 0.418 ns | 0.391 ns |  0.36 |    0.01 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. SSE |                               DOTNET_EnableAVX=0 |   512 |  27.59 ns | 0.556 ns | 0.571 ns |  0.38 |    0.01 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1024 | 150.64 ns | 2.903 ns | 2.716 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. AVX |                                             Empty |  1024 |  38.67 ns | 0.801 ns | 1.889 ns |  0.24 |    0.02 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. SSE |                               DOTNET_EnableAVX=0 |  1024 |  47.13 ns | 0.948 ns | 1.054 ns |  0.31 |    0.01 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  2048 | 315.29 ns | 5.206 ns | 6.583 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. AVX |                                             Empty |  2048 |  57.37 ns | 1.152 ns | 1.078 ns |  0.18 |    0.01 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. SSE |                               DOTNET_EnableAVX=0 |  2048 |  65.75 ns | 1.198 ns | 1.600 ns |  0.21 |    0.01 |     - |     - |     - |         - |

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
//
// Runtime=.NET 6.0
//
// |          Method |                Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |---------------- |------------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|------:|--------:|------:|------:|------:|----------:|
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   128 |  10.76 ns | 0.033 ns | 0.029 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. SSE |                               DOTNET_EnableAVX=0 |   128 |  11.39 ns | 0.045 ns | 0.040 ns |  1.06 |    0.01 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. AVX |                                             Empty |   128 |  14.05 ns | 0.029 ns | 0.024 ns |  1.31 |    0.00 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   256 |  32.09 ns | 0.655 ns | 1.000 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. SSE |                               DOTNET_EnableAVX=0 |   256 |  14.03 ns | 0.047 ns | 0.041 ns |  0.44 |    0.02 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. AVX |                                             Empty |   256 |  15.18 ns | 0.052 ns | 0.043 ns |  0.48 |    0.03 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   512 |  59.26 ns | 0.084 ns | 0.070 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. SSE |                               DOTNET_EnableAVX=0 |   512 |  18.80 ns | 0.036 ns | 0.034 ns |  0.32 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. AVX |                                             Empty |   512 |  17.69 ns | 0.038 ns | 0.034 ns |  0.30 |    0.00 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1024 | 112.48 ns | 0.285 ns | 0.253 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. SSE |                               DOTNET_EnableAVX=0 |  1024 |  31.57 ns | 0.041 ns | 0.036 ns |  0.28 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. AVX |                                             Empty |  1024 |  28.41 ns | 0.068 ns | 0.064 ns |  0.25 |    0.00 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  2048 | 218.59 ns | 0.303 ns | 0.283 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. SSE |                               DOTNET_EnableAVX=0 |  2048 |  53.04 ns | 0.106 ns | 0.099 ns |  0.24 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. AVX |                                             Empty |  2048 |  34.74 ns | 0.061 ns | 0.054 ns |  0.16 |    0.00 |     - |     - |     - |         - |

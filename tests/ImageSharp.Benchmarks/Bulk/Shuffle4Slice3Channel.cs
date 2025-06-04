// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

[Config(typeof(Config.HwIntrinsics_SSE_AVX))]
public class Shuffle4Slice3Channel
{
    private static readonly DefaultShuffle4Slice3 Control = new(SimdUtils.Shuffle.MMShuffle1032);
    private byte[] source;
    private byte[] destination;

    [GlobalSetup]
    public void Setup()
    {
        this.source = new byte[this.Count];
        new Random(this.Count).NextBytes(this.source);
        this.destination = new byte[(int)(this.Count * (3 / 4F))];
    }

    [Params(128, 256, 512, 1024, 2048)]
    public int Count { get; set; }

    [Benchmark]
    public void Shuffle4Slice3()
        => SimdUtils.Shuffle4Slice3(this.source, this.destination, Control);

    [Benchmark]
    public void Shuffle4Slice3FastFallback()
        => SimdUtils.Shuffle4Slice3(this.source, this.destination, default(XYZWShuffle4Slice3));
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
// |                     Method |                Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev |    Median | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |--------------------------- |------------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|----------:|------:|--------:|------:|------:|------:|----------:|
// |             Shuffle4Slice3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   128 |  56.44 ns | 2.843 ns | 8.382 ns |  56.70 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             2. AVX |                                             Empty |   128 |  27.15 ns | 0.556 ns | 0.762 ns |  27.34 ns |  0.41 |    0.03 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             3. SSE |                               DOTNET_EnableAVX=0 |   128 |  26.36 ns | 0.321 ns | 0.268 ns |  26.26 ns |  0.38 |    0.02 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// | Shuffle4Slice3FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   128 |  25.85 ns | 0.494 ns | 0.462 ns |  25.84 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             2. AVX |                                             Empty |   128 |  26.15 ns | 0.113 ns | 0.106 ns |  26.16 ns |  1.01 |    0.02 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             3. SSE |                               DOTNET_EnableAVX=0 |   128 |  25.57 ns | 0.078 ns | 0.061 ns |  25.56 ns |  0.99 |    0.02 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// |             Shuffle4Slice3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   256 |  97.47 ns | 0.327 ns | 0.289 ns |  97.35 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             2. AVX |                                             Empty |   256 |  32.61 ns | 0.107 ns | 0.095 ns |  32.62 ns |  0.33 |    0.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             3. SSE |                               DOTNET_EnableAVX=0 |   256 |  33.21 ns | 0.169 ns | 0.150 ns |  33.15 ns |  0.34 |    0.00 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// | Shuffle4Slice3FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   256 |  52.34 ns | 0.779 ns | 0.729 ns |  51.94 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             2. AVX |                                             Empty |   256 |  32.16 ns | 0.111 ns | 0.104 ns |  32.16 ns |  0.61 |    0.01 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             3. SSE |                               DOTNET_EnableAVX=0 |   256 |  33.61 ns | 0.342 ns | 0.319 ns |  33.62 ns |  0.64 |    0.01 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// |             Shuffle4Slice3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   512 | 210.74 ns | 3.825 ns | 5.956 ns | 207.70 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             2. AVX |                                             Empty |   512 |  51.03 ns | 0.535 ns | 0.501 ns |  51.18 ns |  0.24 |    0.01 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             3. SSE |                               DOTNET_EnableAVX=0 |   512 |  66.60 ns | 1.313 ns | 1.613 ns |  65.93 ns |  0.31 |    0.01 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// | Shuffle4Slice3FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   512 | 119.12 ns | 1.905 ns | 1.689 ns | 118.52 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             2. AVX |                                             Empty |   512 |  50.33 ns | 0.382 ns | 0.339 ns |  50.41 ns |  0.42 |    0.01 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             3. SSE |                               DOTNET_EnableAVX=0 |   512 |  49.25 ns | 0.555 ns | 0.492 ns |  49.26 ns |  0.41 |    0.01 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// |             Shuffle4Slice3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1024 | 423.55 ns | 4.891 ns | 4.336 ns | 423.27 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             2. AVX |                                             Empty |  1024 |  77.13 ns | 1.355 ns | 2.264 ns |  76.19 ns |  0.19 |    0.01 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             3. SSE |                               DOTNET_EnableAVX=0 |  1024 |  79.39 ns | 0.103 ns | 0.086 ns |  79.37 ns |  0.19 |    0.00 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// | Shuffle4Slice3FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1024 | 226.57 ns | 2.930 ns | 2.598 ns | 226.10 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             2. AVX |                                             Empty |  1024 |  80.25 ns | 1.647 ns | 2.082 ns |  80.98 ns |  0.35 |    0.01 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             3. SSE |                               DOTNET_EnableAVX=0 |  1024 |  84.99 ns | 1.234 ns | 1.155 ns |  85.60 ns |  0.38 |    0.01 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// |             Shuffle4Slice3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  2048 | 794.96 ns | 1.735 ns | 1.538 ns | 795.15 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             2. AVX |                                             Empty |  2048 | 128.41 ns | 0.417 ns | 0.390 ns | 128.24 ns |  0.16 |    0.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             3. SSE |                               DOTNET_EnableAVX=0 |  2048 | 127.24 ns | 0.294 ns | 0.229 ns | 127.23 ns |  0.16 |    0.00 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
// | Shuffle4Slice3FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  2048 | 382.97 ns | 1.064 ns | 0.831 ns | 382.87 ns |  1.00 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             2. AVX |                                             Empty |  2048 | 126.93 ns | 0.382 ns | 0.339 ns | 126.94 ns |  0.33 |    0.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             3. SSE |                               DOTNET_EnableAVX=0 |  2048 | 149.36 ns | 1.875 ns | 1.754 ns | 149.33 ns |  0.39 |    0.00 |     - |     - |     - |         - |

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
// |                     Method |                Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |--------------------------- |------------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|------:|------:|------:|------:|----------:|
// |             Shuffle4Slice3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   128 |  45.59 ns | 0.166 ns | 0.147 ns |  1.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             2. SSE |                               DOTNET_EnableAVX=0 |   128 |  15.62 ns | 0.056 ns | 0.052 ns |  0.34 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             3. AVX |                                             Empty |   128 |  16.37 ns | 0.047 ns | 0.040 ns |  0.36 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Shuffle4Slice3FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   128 |  13.23 ns | 0.028 ns | 0.026 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             2. SSE |                               DOTNET_EnableAVX=0 |   128 |  14.41 ns | 0.013 ns | 0.012 ns |  1.09 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             3. AVX |                                             Empty |   128 |  14.70 ns | 0.050 ns | 0.047 ns |  1.11 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |       |       |       |       |           |
// |             Shuffle4Slice3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   256 |  85.48 ns | 0.192 ns | 0.179 ns |  1.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             2. SSE |                               DOTNET_EnableAVX=0 |   256 |  19.18 ns | 0.230 ns | 0.204 ns |  0.22 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             3. AVX |                                             Empty |   256 |  18.66 ns | 0.017 ns | 0.015 ns |  0.22 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Shuffle4Slice3FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   256 |  24.34 ns | 0.078 ns | 0.073 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             2. SSE |                               DOTNET_EnableAVX=0 |   256 |  18.58 ns | 0.061 ns | 0.057 ns |  0.76 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             3. AVX |                                             Empty |   256 |  19.23 ns | 0.018 ns | 0.016 ns |  0.79 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |       |       |       |       |           |
// |             Shuffle4Slice3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   512 | 165.31 ns | 0.742 ns | 0.694 ns |  1.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             2. SSE |                               DOTNET_EnableAVX=0 |   512 |  28.10 ns | 0.077 ns | 0.068 ns |  0.17 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             3. AVX |                                             Empty |   512 |  28.99 ns | 0.018 ns | 0.014 ns |  0.18 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Shuffle4Slice3FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   512 |  53.45 ns | 0.270 ns | 0.226 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             2. SSE |                               DOTNET_EnableAVX=0 |   512 |  27.50 ns | 0.034 ns | 0.028 ns |  0.51 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             3. AVX |                                             Empty |   512 |  28.76 ns | 0.017 ns | 0.015 ns |  0.54 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |       |       |       |       |           |
// |             Shuffle4Slice3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1024 | 323.87 ns | 0.549 ns | 0.487 ns |  1.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             2. SSE |                               DOTNET_EnableAVX=0 |  1024 |  40.81 ns | 0.056 ns | 0.050 ns |  0.13 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             3. AVX |                                             Empty |  1024 |  39.95 ns | 0.075 ns | 0.067 ns |  0.12 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Shuffle4Slice3FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1024 | 101.37 ns | 0.080 ns | 0.067 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             2. SSE |                               DOTNET_EnableAVX=0 |  1024 |  40.72 ns | 0.049 ns | 0.041 ns |  0.40 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             3. AVX |                                             Empty |  1024 |  39.78 ns | 0.029 ns | 0.027 ns |  0.39 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |       |       |       |       |           |
// |             Shuffle4Slice3 | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  2048 | 642.95 ns | 2.067 ns | 1.933 ns |  1.00 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             2. SSE |                               DOTNET_EnableAVX=0 |  2048 |  73.19 ns | 0.082 ns | 0.077 ns |  0.11 |     - |     - |     - |         - |
// |             Shuffle4Slice3 |             3. AVX |                                             Empty |  2048 |  69.83 ns | 0.319 ns | 0.267 ns |  0.11 |     - |     - |     - |         - |
// |                            |                    |                                                   |       |           |          |          |       |       |       |       |           |
// | Shuffle4Slice3FastFallback | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  2048 | 196.85 ns | 0.238 ns | 0.211 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             2. SSE |                               DOTNET_EnableAVX=0 |  2048 |  72.89 ns | 0.117 ns | 0.098 ns |  0.37 |     - |     - |     - |         - |
// | Shuffle4Slice3FastFallback |             3. AVX |                                             Empty |  2048 |  69.59 ns | 0.073 ns | 0.061 ns |  0.35 |     - |     - |     - |         - |

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

[Config(typeof(Config.HwIntrinsics_SSE_AVX))]
public class ShuffleFloat4Channel
{
    private float[] source;
    private float[] destination;

    [GlobalSetup]
    public void Setup()
    {
        this.source = new Random(this.Count).GenerateRandomFloatArray(this.Count, 0, 256);
        this.destination = new float[this.Count];
    }

    [Params(128, 256, 512, 1024, 2048)]
    public int Count { get; set; }

    [Benchmark]
    public void Shuffle4Channel()
        => SimdUtils.Shuffle4(this.source, this.destination, SimdUtils.Shuffle.MMShuffle2103);
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
// |          Method |                Job |                              EnvironmentVariables | Count |       Mean |     Error |    StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |---------------- |------------------- |-------------------------------------------------- |------ |-----------:|----------:|----------:|------:|------:|------:|------:|----------:|
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   128 |  63.647 ns | 0.5475 ns | 0.4853 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. AVX |                                             Empty |   128 |   9.818 ns | 0.1457 ns | 0.1292 ns |  0.15 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. SSE |                               DOTNET_EnableAVX=0 |   128 |  15.267 ns | 0.1005 ns | 0.0940 ns |  0.24 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   256 | 125.586 ns | 1.9312 ns | 1.8064 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. AVX |                                             Empty |   256 |  15.878 ns | 0.1983 ns | 0.1758 ns |  0.13 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. SSE |                               DOTNET_EnableAVX=0 |   256 |  29.170 ns | 0.2925 ns | 0.2442 ns |  0.23 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   512 | 263.859 ns | 2.6660 ns | 2.3634 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. AVX |                                             Empty |   512 |  29.452 ns | 0.3334 ns | 0.3118 ns |  0.11 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. SSE |                               DOTNET_EnableAVX=0 |   512 |  52.912 ns | 0.1932 ns | 0.1713 ns |  0.20 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1024 | 495.717 ns | 1.9850 ns | 1.8567 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. AVX |                                             Empty |  1024 |  53.757 ns | 0.3212 ns | 0.2847 ns |  0.11 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. SSE |                               DOTNET_EnableAVX=0 |  1024 | 107.815 ns | 1.6201 ns | 1.3528 ns |  0.22 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  2048 | 980.134 ns | 3.7407 ns | 3.1237 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. AVX |                                             Empty |  2048 | 105.120 ns | 0.6140 ns | 0.5443 ns |  0.11 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. SSE |                               DOTNET_EnableAVX=0 |  2048 | 216.473 ns | 2.3268 ns | 2.0627 ns |  0.22 |     - |     - |     - |         - |

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
// |          Method |                Job |                              EnvironmentVariables | Count |       Mean |     Error |    StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |---------------- |------------------- |-------------------------------------------------- |------ |-----------:|----------:|----------:|------:|------:|------:|------:|----------:|
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   128 |  57.819 ns | 0.2360 ns | 0.1970 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. SSE |                               DOTNET_EnableAVX=0 |   128 |  11.564 ns | 0.0234 ns | 0.0195 ns |  0.20 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. AVX |                                             Empty |   128 |   7.770 ns | 0.0696 ns | 0.0617 ns |  0.13 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   256 | 105.282 ns | 0.2713 ns | 0.2405 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. SSE |                               DOTNET_EnableAVX=0 |   256 |  19.867 ns | 0.0393 ns | 0.0348 ns |  0.19 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. AVX |                                             Empty |   256 |  17.586 ns | 0.0582 ns | 0.0544 ns |  0.17 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |   512 | 200.799 ns | 0.5678 ns | 0.5033 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. SSE |                               DOTNET_EnableAVX=0 |   512 |  41.137 ns | 0.1524 ns | 0.1351 ns |  0.20 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. AVX |                                             Empty |   512 |  24.040 ns | 0.0445 ns | 0.0395 ns |  0.12 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  1024 | 401.046 ns | 0.5865 ns | 0.5199 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. SSE |                               DOTNET_EnableAVX=0 |  1024 |  94.904 ns | 0.4633 ns | 0.4334 ns |  0.24 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. AVX |                                             Empty |  1024 |  68.456 ns | 0.1192 ns | 0.0996 ns |  0.17 |     - |     - |     - |         - |
// |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
// | Shuffle4Channel | 1. No HwIntrinsics | DOTNET_EnableHWIntrinsic=0,DOTNET_FeatureSIMD=0 |  2048 | 772.297 ns | 0.6270 ns | 0.5558 ns |  1.00 |     - |     - |     - |         - |
// | Shuffle4Channel |             2. SSE |                               DOTNET_EnableAVX=0 |  2048 | 184.561 ns | 0.4319 ns | 0.4040 ns |  0.24 |     - |     - |     - |         - |
// | Shuffle4Channel |             3. AVX |                                             Empty |  2048 | 133.634 ns | 1.7864 ns | 1.8345 ns |  0.17 |     - |     - |     - |         - |

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
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
        {
            SimdUtils.Shuffle4Channel<WXYZShuffle4>(this.source, this.destination, default);
        }
    }

    // 2020-10-26
    // ##########
    //
    // BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.572 (2004/?/20H1)
    // Intel Core i7-8650U CPU 1.90GHz(Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
    // .NET Core SDK = 5.0.100-rc.2.20479.15
    //
    // [Host]          : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
    //  AVX             : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
    //  No HwIntrinsics : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
    //  SSE             : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
    //
    // Runtime=.NET Core 3.1
    //
    // |          Method |             Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |---------------- |---------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|------:|--------:|------:|------:|------:|----------:|
    // | Shuffle4Channel |             AVX |                                             Empty |   128 |  20.51 ns | 0.270 ns | 0.211 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   128 |  63.00 ns | 0.991 ns | 0.927 ns |  3.08 |    0.06 |     - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |   128 |  17.25 ns | 0.066 ns | 0.058 ns |  0.84 |    0.01 |     - |     - |     - |         - |
    // |                 |                 |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |   256 |  24.57 ns | 0.248 ns | 0.219 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   256 | 124.55 ns | 2.501 ns | 2.456 ns |  5.06 |    0.10 |     - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |   256 |  21.80 ns | 0.094 ns | 0.088 ns |  0.89 |    0.01 |     - |     - |     - |         - |
    // |                 |                 |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |   512 |  28.51 ns | 0.130 ns | 0.115 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   512 | 256.52 ns | 1.424 ns | 1.332 ns |  9.00 |    0.07 |     - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |   512 |  29.72 ns | 0.217 ns | 0.203 ns |  1.04 |    0.01 |     - |     - |     - |         - |
    // |                 |                 |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |  1024 |  36.40 ns | 0.357 ns | 0.334 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  1024 | 492.71 ns | 1.498 ns | 1.251 ns | 13.52 |    0.12 |     - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |  1024 |  44.71 ns | 0.264 ns | 0.234 ns |  1.23 |    0.02 |     - |     - |     - |         - |
    // |                 |                 |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |  2048 |  59.38 ns | 0.180 ns | 0.159 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  2048 | 975.05 ns | 2.043 ns | 1.811 ns | 16.42 |    0.05 |     - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |  2048 |  81.83 ns | 0.212 ns | 0.198 ns |  1.38 |    0.01 |     - |     - |     - |         - |
}

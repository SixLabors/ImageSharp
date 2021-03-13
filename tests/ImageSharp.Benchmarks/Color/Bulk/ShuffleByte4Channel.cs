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
            SimdUtils.Shuffle4<WXYZShuffle4>(this.source, this.destination, default);
        }
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
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   128 |  17.39 ns | 0.187 ns | 0.175 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |   128 |  21.72 ns | 0.299 ns | 0.279 ns |  1.25 |    0.02 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |   128 |  18.10 ns | 0.346 ns | 0.289 ns |  1.04 |    0.02 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   256 |  35.51 ns | 0.711 ns | 0.790 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |   256 |  23.90 ns | 0.508 ns | 0.820 ns |  0.69 |    0.02 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |   256 |  20.40 ns | 0.133 ns | 0.111 ns |  0.57 |    0.01 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   512 |  73.39 ns | 0.310 ns | 0.259 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |   512 |  26.10 ns | 0.418 ns | 0.391 ns |  0.36 |    0.01 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |   512 |  27.59 ns | 0.556 ns | 0.571 ns |  0.38 |    0.01 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  1024 | 150.64 ns | 2.903 ns | 2.716 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |  1024 |  38.67 ns | 0.801 ns | 1.889 ns |  0.24 |    0.02 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |  1024 |  47.13 ns | 0.948 ns | 1.054 ns |  0.31 |    0.01 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  2048 | 315.29 ns | 5.206 ns | 6.583 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |  2048 |  57.37 ns | 1.152 ns | 1.078 ns |  0.18 |    0.01 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |  2048 |  65.75 ns | 1.198 ns | 1.600 ns |  0.21 |    0.01 |     - |     - |     - |         - |
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.HwIntrinsics_SSE_AVX))]
    public class Shuffle4Slice3Channel
    {
        private static readonly byte Control = default(WXYZShuffle4).Control;
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
        {
            SimdUtils.Shuffle4Slice3(this.source, this.destination, Control);
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
    // |         Method |                Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |--------------- |------------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|------:|------:|------:|------:|----------:|
    // | Shuffle4Slice3 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   128 |  52.24 ns | 1.081 ns | 1.062 ns |  1.00 |     - |     - |     - |         - |
    // | Shuffle4Slice3 |             2. AVX |                                             Empty |   128 |  25.52 ns | 0.189 ns | 0.158 ns |  0.49 |     - |     - |     - |         - |
    // | Shuffle4Slice3 |             3. SSE |                               COMPlus_EnableAVX=0 |   128 |  26.11 ns | 0.524 ns | 0.644 ns |  0.50 |     - |     - |     - |         - |
    // |                |                    |                                                   |       |           |          |          |       |       |       |       |           |
    // | Shuffle4Slice3 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   256 | 101.09 ns | 0.733 ns | 0.612 ns |  1.00 |     - |     - |     - |         - |
    // | Shuffle4Slice3 |             2. AVX |                                             Empty |   256 |  32.65 ns | 0.674 ns | 1.198 ns |  0.33 |     - |     - |     - |         - |
    // | Shuffle4Slice3 |             3. SSE |                               COMPlus_EnableAVX=0 |   256 |  32.76 ns | 0.656 ns | 0.853 ns |  0.32 |     - |     - |     - |         - |
    // |                |                    |                                                   |       |           |          |          |       |       |       |       |           |
    // | Shuffle4Slice3 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   512 | 209.58 ns | 3.826 ns | 5.957 ns |  1.00 |     - |     - |     - |         - |
    // | Shuffle4Slice3 |             2. AVX |                                             Empty |   512 |  46.32 ns | 0.729 ns | 1.296 ns |  0.22 |     - |     - |     - |         - |
    // | Shuffle4Slice3 |             3. SSE |                               COMPlus_EnableAVX=0 |   512 |  46.97 ns | 0.196 ns | 0.183 ns |  0.22 |     - |     - |     - |         - |
    // |                |                    |                                                   |       |           |          |          |       |       |       |       |           |
    // | Shuffle4Slice3 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  1024 | 406.39 ns | 7.493 ns | 6.257 ns |  1.00 |     - |     - |     - |         - |
    // | Shuffle4Slice3 |             2. AVX |                                             Empty |  1024 |  74.53 ns | 1.509 ns | 1.678 ns |  0.18 |     - |     - |     - |         - |
    // | Shuffle4Slice3 |             3. SSE |                               COMPlus_EnableAVX=0 |  1024 |  74.04 ns | 0.703 ns | 0.657 ns |  0.18 |     - |     - |     - |         - |
    // |                |                    |                                                   |       |           |          |          |       |       |       |       |           |
    // | Shuffle4Slice3 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  2048 | 796.80 ns | 6.476 ns | 5.741 ns |  1.00 |     - |     - |     - |         - |
    // | Shuffle4Slice3 |             2. AVX |                                             Empty |  2048 | 130.70 ns | 2.512 ns | 2.227 ns |  0.16 |     - |     - |     - |         - |
    // | Shuffle4Slice3 |             3. SSE |                               COMPlus_EnableAVX=0 |  2048 | 129.42 ns | 2.555 ns | 2.133 ns |  0.16 |     - |     - |     - |         - |
}

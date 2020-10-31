// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.HwIntrinsics_SSE_AVX))]
    public class Pad3Shuffle4Channel
    {
        private static readonly byte Control = default(WXYZShuffle4).Control;
        private byte[] source;
        private byte[] destination;

        [GlobalSetup]
        public void Setup()
        {
            this.source = new byte[this.Count];
            new Random(this.Count).NextBytes(this.source);
            this.destination = new byte[(int)(this.Count * (4 / 3F))];
        }

        [Params(96, 384, 768, 1536)]
        public int Count { get; set; }

        [Benchmark]
        public void Pad3Shuffle4()
        {
            SimdUtils.Pad3Shuffle4(this.source, this.destination, Control);
        }
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
    // |       Method |                Job |                              EnvironmentVariables | Count |      Mean |     Error |   StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |------------- |------------------- |-------------------------------------------------- |------ |----------:|----------:|---------:|------:|--------:|------:|------:|------:|----------:|
    // | Pad3Shuffle4 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |    96 |  71.84 ns |  1.491 ns | 3.146 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             2. AVX |                                             Empty |    96 |  23.14 ns |  0.186 ns | 0.165 ns |  0.33 |    0.02 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             3. SSE |                               COMPlus_EnableAVX=0 |    96 |  22.94 ns |  0.127 ns | 0.112 ns |  0.33 |    0.02 |     - |     - |     - |         - |
    // |              |                    |                                                   |       |           |           |          |       |         |       |       |       |           |
    // | Pad3Shuffle4 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   384 | 230.90 ns |  0.877 ns | 0.777 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             2. AVX |                                             Empty |   384 |  39.54 ns |  0.601 ns | 0.533 ns |  0.17 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             3. SSE |                               COMPlus_EnableAVX=0 |   384 |  39.88 ns |  0.657 ns | 0.548 ns |  0.17 |    0.00 |     - |     - |     - |         - |
    // |              |                    |                                                   |       |           |           |          |       |         |       |       |       |           |
    // | Pad3Shuffle4 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   768 | 451.67 ns |  2.932 ns | 2.448 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             2. AVX |                                             Empty |   768 |  60.79 ns |  1.200 ns | 1.561 ns |  0.13 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             3. SSE |                               COMPlus_EnableAVX=0 |   768 |  62.30 ns |  0.469 ns | 0.416 ns |  0.14 |    0.00 |     - |     - |     - |         - |
    // |              |                    |                                                   |       |           |           |          |       |         |       |       |       |           |
    // | Pad3Shuffle4 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  1536 | 896.11 ns | 10.358 ns | 9.689 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             2. AVX |                                             Empty |  1536 | 109.72 ns |  2.149 ns | 2.300 ns |  0.12 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             3. SSE |                               COMPlus_EnableAVX=0 |  1536 | 108.70 ns |  0.994 ns | 0.881 ns |  0.12 |    0.00 |     - |     - |     - |         - |
}

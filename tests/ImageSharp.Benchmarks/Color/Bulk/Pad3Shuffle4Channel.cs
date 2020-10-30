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
    // |       Method |                Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |------------- |------------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|------:|--------:|------:|------:|------:|----------:|
    // | Pad3Shuffle4 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |    96 |  62.91 ns | 1.240 ns | 1.569 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             2. AVX |                                             Empty |    96 |  44.34 ns | 0.371 ns | 0.329 ns |  0.70 |    0.02 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             3. SSE |                               COMPlus_EnableAVX=0 |    96 |  44.46 ns | 0.617 ns | 0.515 ns |  0.70 |    0.02 |     - |     - |     - |         - |
    // |              |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Pad3Shuffle4 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   384 | 247.93 ns | 2.640 ns | 2.470 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             2. AVX |                                             Empty |   384 |  92.91 ns | 1.204 ns | 1.127 ns |  0.37 |    0.01 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             3. SSE |                               COMPlus_EnableAVX=0 |   384 |  91.42 ns | 1.234 ns | 1.094 ns |  0.37 |    0.01 |     - |     - |     - |         - |
    // |              |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Pad3Shuffle4 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   768 | 444.79 ns | 5.094 ns | 4.254 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             2. AVX |                                             Empty |   768 | 162.92 ns | 1.046 ns | 0.873 ns |  0.37 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             3. SSE |                               COMPlus_EnableAVX=0 |   768 | 166.22 ns | 1.728 ns | 1.443 ns |  0.37 |    0.00 |     - |     - |     - |         - |
    // |              |                    |                                                   |       |           |          |          |       |         |       |       |       |           |
    // | Pad3Shuffle4 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  1536 | 882.51 ns | 6.936 ns | 5.792 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             2. AVX |                                             Empty |  1536 | 309.72 ns | 3.777 ns | 3.533 ns |  0.35 |    0.01 |     - |     - |     - |         - |
    // | Pad3Shuffle4 |             3. SSE |                               COMPlus_EnableAVX=0 |  1536 | 323.18 ns | 4.079 ns | 3.816 ns |  0.37 |    0.00 |     - |     - |     - |         - |
}

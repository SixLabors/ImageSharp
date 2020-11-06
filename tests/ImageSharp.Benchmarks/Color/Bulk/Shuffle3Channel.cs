// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.HwIntrinsics_SSE_AVX))]
    public class Shuffle3Channel
    {
        private static readonly DefaultShuffle3 Control = new DefaultShuffle3(1, 0, 2);
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
        {
            SimdUtils.Shuffle3(this.source, this.destination, Control);
        }
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
    // |             Shuffle3 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |    96 |  48.46 ns | 1.034 ns | 2.438 ns |  47.46 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |             Shuffle3 |             2. AVX |                                             Empty |    96 |  32.42 ns | 0.537 ns | 0.476 ns |  32.34 ns |  0.66 |    0.04 |     - |     - |     - |         - |
    // |             Shuffle3 |             3. SSE |                               COMPlus_EnableAVX=0 |    96 |  32.51 ns | 0.373 ns | 0.349 ns |  32.56 ns |  0.66 |    0.03 |     - |     - |     - |         - |
    // |                      |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
    // |             Shuffle3 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   384 | 199.04 ns | 1.512 ns | 1.180 ns | 199.17 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |             Shuffle3 |             2. AVX |                                             Empty |   384 |  71.20 ns | 2.654 ns | 7.784 ns |  69.60 ns |  0.41 |    0.02 |     - |     - |     - |         - |
    // |             Shuffle3 |             3. SSE |                               COMPlus_EnableAVX=0 |   384 |  63.23 ns | 0.569 ns | 0.505 ns |  63.21 ns |  0.32 |    0.00 |     - |     - |     - |         - |
    // |                      |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
    // |             Shuffle3 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   768 | 391.28 ns | 5.087 ns | 3.972 ns | 391.22 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |             Shuffle3 |             2. AVX |                                             Empty |   768 | 109.12 ns | 2.149 ns | 2.010 ns | 108.66 ns |  0.28 |    0.01 |     - |     - |     - |         - |
    // |             Shuffle3 |             3. SSE |                               COMPlus_EnableAVX=0 |   768 | 106.51 ns | 0.734 ns | 0.613 ns | 106.56 ns |  0.27 |    0.00 |     - |     - |     - |         - |
    // |                      |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
    // |             Shuffle3 | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  1536 | 773.70 ns | 5.516 ns | 4.890 ns | 772.96 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |             Shuffle3 |             2. AVX |                                             Empty |  1536 | 190.41 ns | 1.090 ns | 0.851 ns | 190.38 ns |  0.25 |    0.00 |     - |     - |     - |         - |
    // |             Shuffle3 |             3. SSE |                               COMPlus_EnableAVX=0 |  1536 | 190.94 ns | 0.985 ns | 0.769 ns | 190.85 ns |  0.25 |    0.00 |     - |     - |     - |         - |
}

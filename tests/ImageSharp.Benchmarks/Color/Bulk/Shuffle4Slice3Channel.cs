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
    // |          Method |                Job |                              EnvironmentVariables | Count |      Mean |    Error |   StdDev |    Median | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |---------------- |------------------- |-------------------------------------------------- |------ |----------:|---------:|---------:|----------:|------:|--------:|------:|------:|------:|----------:|
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   128 |  50.09 ns | 1.018 ns | 1.460 ns |  49.16 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |   128 |  35.28 ns | 0.106 ns | 0.089 ns |  35.30 ns |  0.69 |    0.02 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |   128 |  35.13 ns | 0.247 ns | 0.231 ns |  35.22 ns |  0.69 |    0.02 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   256 | 101.48 ns | 0.875 ns | 0.819 ns | 101.60 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |   256 |  53.25 ns | 0.518 ns | 0.433 ns |  53.21 ns |  0.52 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |   256 |  57.21 ns | 0.508 ns | 0.451 ns |  57.38 ns |  0.56 |    0.01 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   512 | 202.53 ns | 0.884 ns | 0.827 ns | 202.40 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |   512 |  82.55 ns | 0.418 ns | 0.391 ns |  82.59 ns |  0.41 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |   512 |  82.89 ns | 1.057 ns | 0.989 ns |  82.48 ns |  0.41 |    0.00 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  1024 | 398.79 ns | 7.807 ns | 6.921 ns | 395.67 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |  1024 | 144.51 ns | 1.033 ns | 0.966 ns | 144.42 ns |  0.36 |    0.01 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |  1024 | 143.77 ns | 0.820 ns | 0.684 ns | 143.62 ns |  0.36 |    0.01 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |           |          |          |           |       |         |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  2048 | 798.44 ns | 4.447 ns | 3.472 ns | 799.39 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |  2048 | 277.12 ns | 1.723 ns | 1.612 ns | 276.93 ns |  0.35 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |  2048 | 275.70 ns | 1.796 ns | 1.500 ns | 275.51 ns |  0.35 |    0.00 |     - |     - |     - |         - ||
}

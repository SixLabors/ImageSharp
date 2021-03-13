// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.HwIntrinsics_SSE_AVX))]
    public class ShuffleFloat4Channel
    {
        private static readonly byte Control = default(WXYZShuffle4).Control;
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
        {
            SimdUtils.Shuffle4(this.source, this.destination, Control);
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
    // |          Method |                Job |                              EnvironmentVariables | Count |       Mean |     Error |    StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |---------------- |------------------- |-------------------------------------------------- |------ |-----------:|----------:|----------:|------:|------:|------:|------:|----------:|
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   128 |  63.647 ns | 0.5475 ns | 0.4853 ns |  1.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |   128 |   9.818 ns | 0.1457 ns | 0.1292 ns |  0.15 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |   128 |  15.267 ns | 0.1005 ns | 0.0940 ns |  0.24 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   256 | 125.586 ns | 1.9312 ns | 1.8064 ns |  1.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |   256 |  15.878 ns | 0.1983 ns | 0.1758 ns |  0.13 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |   256 |  29.170 ns | 0.2925 ns | 0.2442 ns |  0.23 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   512 | 263.859 ns | 2.6660 ns | 2.3634 ns |  1.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |   512 |  29.452 ns | 0.3334 ns | 0.3118 ns |  0.11 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |   512 |  52.912 ns | 0.1932 ns | 0.1713 ns |  0.20 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  1024 | 495.717 ns | 1.9850 ns | 1.8567 ns |  1.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |  1024 |  53.757 ns | 0.3212 ns | 0.2847 ns |  0.11 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |  1024 | 107.815 ns | 1.6201 ns | 1.3528 ns |  0.22 |     - |     - |     - |         - |
    // |                 |                    |                                                   |       |            |           |           |       |       |       |       |           |
    // | Shuffle4Channel | 1. No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  2048 | 980.134 ns | 3.7407 ns | 3.1237 ns |  1.00 |     - |     - |     - |         - |
    // | Shuffle4Channel |             2. AVX |                                             Empty |  2048 | 105.120 ns | 0.6140 ns | 0.5443 ns |  0.11 |     - |     - |     - |         - |
    // | Shuffle4Channel |             3. SSE |                               COMPlus_EnableAVX=0 |  2048 | 216.473 ns | 2.3268 ns | 2.0627 ns |  0.22 |     - |     - |     - |         - |
}

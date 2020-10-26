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
            SimdUtils.Shuffle4Channel(this.source, this.destination, SimdUtils.Shuffle.WXYZ);
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
    // |          Method |             Job |                              EnvironmentVariables | Count |        Mean |    Error |   StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |---------------- |---------------- |-------------------------------------------------- |------ |------------:|---------:|---------:|------:|--------:|------:|------:|------:|----------:|
    // | Shuffle4Channel |             AVX |                                             Empty |   128 |    14.49 ns | 0.244 ns | 0.217 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   128 |    87.74 ns | 0.524 ns | 0.490 ns |  6.06 |    0.09 |     - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |   128 |    23.65 ns | 0.101 ns | 0.094 ns |  1.63 |    0.03 |     - |     - |     - |         - |
    // |                 |                 |                                                   |       |             |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |   256 |    25.87 ns | 0.492 ns | 0.673 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   256 |   159.52 ns | 0.901 ns | 0.843 ns |  6.12 |    0.12 |     - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |   256 |    45.47 ns | 0.404 ns | 0.378 ns |  1.75 |    0.03 |     - |     - |     - |         - |
    // |                 |                 |                                                   |       |             |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |   512 |    49.51 ns | 0.088 ns | 0.083 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   512 |   297.96 ns | 0.926 ns | 0.821 ns |  6.02 |    0.02 |     - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |   512 |    90.77 ns | 0.191 ns | 0.169 ns |  1.83 |    0.00 |     - |     - |     - |         - |
    // |                 |                 |                                                   |       |             |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |  1024 |   113.09 ns | 1.913 ns | 3.090 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  1024 |   604.58 ns | 1.464 ns | 1.298 ns |  5.29 |    0.18 |     - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |  1024 |   179.44 ns | 0.208 ns | 0.184 ns |  1.57 |    0.05 |     - |     - |     - |         - |
    // |                 |                 |                                                   |       |             |          |          |       |         |       |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |  2048 |   217.95 ns | 1.314 ns | 1.165 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  2048 | 1,152.04 ns | 3.941 ns | 3.494 ns |  5.29 |    0.03 |     - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |  2048 |   349.52 ns | 0.587 ns | 0.520 ns |  1.60 |    0.01 |     - |     - |     - |         - |
}

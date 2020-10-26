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
    // |          Method |             Job |                              EnvironmentVariables | Count |      Mean |     Error |    StdDev | Ratio | RatioSD |  Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |---------------- |---------------- |-------------------------------------------------- |------ |----------:|----------:|----------:|------:|--------:|-------:|------:|------:|----------:|
    // | Shuffle4Channel |             AVX |                                             Empty |   128 |  33.57 ns |  0.694 ns |  1.268 ns |  1.00 |    0.00 | 0.0134 |     - |     - |      56 B |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   128 |  63.97 ns |  0.940 ns |  1.045 ns |  1.94 |    0.10 |      - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |   128 |  27.23 ns |  0.338 ns |  0.300 ns |  0.84 |    0.04 | 0.0095 |     - |     - |      40 B |
    // |                 |                 |                                                   |       |           |           |           |       |         |        |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |   256 |  34.57 ns |  0.295 ns |  0.276 ns |  1.00 |    0.00 | 0.0134 |     - |     - |      56 B |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   256 | 124.62 ns |  0.257 ns |  0.228 ns |  3.60 |    0.03 |      - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |   256 |  32.22 ns |  0.106 ns |  0.099 ns |  0.93 |    0.01 | 0.0095 |     - |     - |      40 B |
    // |                 |                 |                                                   |       |           |           |           |       |         |        |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |   512 |  40.41 ns |  0.826 ns |  0.848 ns |  1.00 |    0.00 | 0.0134 |     - |     - |      56 B |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |   512 | 251.65 ns |  0.440 ns |  0.412 ns |  6.23 |    0.13 |      - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |   512 |  41.54 ns |  0.128 ns |  0.114 ns |  1.03 |    0.02 | 0.0095 |     - |     - |      40 B |
    // |                 |                 |                                                   |       |           |           |           |       |         |        |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |  1024 |  51.54 ns |  0.156 ns |  0.121 ns |  1.00 |    0.00 | 0.0134 |     - |     - |      56 B |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  1024 | 493.66 ns |  1.316 ns |  1.231 ns |  9.58 |    0.04 |      - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |  1024 |  61.45 ns |  0.216 ns |  0.181 ns |  1.19 |    0.00 | 0.0095 |     - |     - |      40 B |
    // |                 |                 |                                                   |       |           |           |           |       |         |        |       |       |           |
    // | Shuffle4Channel |             AVX |                                             Empty |  2048 |  76.85 ns |  0.176 ns |  0.138 ns |  1.00 |    0.00 | 0.0134 |     - |     - |      56 B |
    // | Shuffle4Channel | No HwIntrinsics | COMPlus_EnableHWIntrinsic=0,COMPlus_FeatureSIMD=0 |  2048 | 985.64 ns | 11.396 ns | 10.103 ns | 12.84 |    0.15 |      - |     - |     - |         - |
    // | Shuffle4Channel |             SSE |                               COMPlus_EnableAVX=0 |  2048 | 106.13 ns |  0.335 ns |  0.297 ns |  1.38 |    0.01 | 0.0095 |     - |     - |      40 B |
}

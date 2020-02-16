// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Samplers
{
    [Config(typeof(Config.ShortClr))]
    public class Diffuse
    {
        [Benchmark]
        public Size DoDiffuse()
        {
            using (var image = new Image<Rgba32>(Configuration.Default, 800, 800, Color.BlanchedAlmond))
            {
                image.Mutate(x => x.Dither(KnownDitherings.FloydSteinberg));

                return image.Size();
            }
        }

        [Benchmark]
        public Size DoDither()
        {
            using (var image = new Image<Rgba32>(Configuration.Default, 800, 800, Color.BlanchedAlmond))
            {
                image.Mutate(x => x.Dither());

                return image.Size();
            }
        }
    }
}

// #### 25th October 2019 ####
//
// BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
// Intel Core i7-8650U CPU 1.90GHz(Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
// .NET Core SDK = 3.0.100
//
//  [Host] : .NET Core 2.1.13 (CoreCLR 4.6.28008.01, CoreFX 4.6.28008.01), 64bit RyuJIT
//   Clr    : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.8.4018.0
//   Core   : .NET Core 2.1.13 (CoreCLR 4.6.28008.01, CoreFX 4.6.28008.01), 64bit RyuJIT
//
// IterationCount=3  LaunchCount=1  WarmupCount=3
//
// #### Before ####
//
// |    Method |  Job | Runtime |      Mean |    Error |   StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |---------- |----- |-------- |----------:|---------:|---------:|------:|------:|------:|----------:|
// | DoDiffuse |  Clr |     Clr | 129.58 ms | 24.60 ms | 1.349 ms |     - |     - |     - |      6 KB |
// | DoDiffuse | Core |    Core |  92.63 ms | 89.78 ms | 4.921 ms |     - |     - |     - |   4.58 KB |
//
// #### After ####
//
// |    Method |  Job | Runtime |      Mean |     Error |    StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |---------- |----- |-------- |----------:|----------:|----------:|------:|------:|------:|----------:|
// | DoDiffuse |  Clr |     Clr | 124.93 ms | 33.297 ms | 1.8251 ms |     - |     - |     - |      2 KB |
// | DoDiffuse | Core |    Core |  89.63 ms |  9.895 ms | 0.5424 ms |     - |     - |     - |   1.91 KB |

// #### 15th February 2020 ####
//
// BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
// Intel Core i7-8650U CPU 1.90GHz(Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
// .NET Core SDK = 3.1.101
//
// [Host]     : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT
//  Job-OJKYBT : .NET Framework 4.8 (4.8.4121.0), X64 RyuJIT
//  Job-RZWLFP : .NET Core 2.1.15 (CoreCLR 4.6.28325.01, CoreFX 4.6.28327.02), X64 RyuJIT
//  Job-NUYUQV : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT
//
// IterationCount=3  LaunchCount=1  WarmupCount=3
//
// |    Method |       Runtime |     Mean |     Error |   StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |---------- |-------------- |---------:|----------:|---------:|------:|------:|------:|----------:|
// | DoDiffuse |    .NET 4.7.2 | 40.32 ms | 16.788 ms | 0.920 ms |     - |     - |     - |  26.46 KB |
// |  DoDither |    .NET 4.7.2 | 12.86 ms |  3.066 ms | 0.168 ms |     - |     - |     - |  30.75 KB |
// | DoDiffuse | .NET Core 2.1 | 27.09 ms |  3.180 ms | 0.174 ms |     - |     - |     - |  26.04 KB |
// |  DoDither | .NET Core 2.1 | 12.89 ms | 34.535 ms | 1.893 ms |     - |     - |     - |  29.26 KB |
// | DoDiffuse | .NET Core 3.1 | 27.39 ms |  2.699 ms | 0.148 ms |     - |     - |     - |  26.02 KB |
// |  DoDither | .NET Core 3.1 | 12.50 ms |  5.083 ms | 0.279 ms |     - |     - |     - |  30.96 KB |

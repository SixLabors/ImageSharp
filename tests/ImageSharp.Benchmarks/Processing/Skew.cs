// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Processing
{
    [Config(typeof(Config.MultiFramework))]
    public class Skew
    {
        [Benchmark]
        public Size DoSkew()
        {
            using var image = new Image<Rgba32>(Configuration.Default, 400, 400, Color.BlanchedAlmond);
            image.Mutate(x => x.Skew(20, 10));

            return image.Size();
        }
    }
}

// #### 21th February 2020 ####
//
// BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
// Intel Core i7-8650U CPU 1.90GHz(Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
// .NET Core SDK = 3.1.101
//
// [Host]     : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT
//  Job-VKKTMF : .NET Framework 4.8 (4.8.4121.0), X64 RyuJIT
//  Job-KTVRKR : .NET Core 2.1.15 (CoreCLR 4.6.28325.01, CoreFX 4.6.28327.02), X64 RyuJIT
//  Job-EONWDB : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT
//
// IterationCount=3  LaunchCount=1  WarmupCount=3
//
// | Method |       Runtime |     Mean |     Error |   StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |------- |-------------- |---------:|----------:|---------:|------:|------:|------:|----------:|
// | DoSkew |    .NET 4.7.2 | 24.60 ms | 33.971 ms | 1.862 ms |     - |     - |     - |    6.5 KB |
// | DoSkew | .NET Core 2.1 | 12.13 ms |  2.256 ms | 0.124 ms |     - |     - |     - |   5.21 KB |
// | DoSkew | .NET Core 3.1 | 12.83 ms |  1.442 ms | 0.079 ms |     - |     - |     - |   6.57 KB |

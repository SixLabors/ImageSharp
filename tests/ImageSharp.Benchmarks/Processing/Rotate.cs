// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Processing
{
    [Config(typeof(Config.MultiFramework))]
    public class Rotate
    {
        [Benchmark]
        public Size DoRotate()
        {
            using var image = new Image<Rgba32>(Configuration.Default, 400, 400, Color.BlanchedAlmond);
            image.Mutate(x => x.Rotate(37.5F));

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
//  Job-HOGSNT : .NET Framework 4.8 (4.8.4121.0), X64 RyuJIT
//  Job-FKDHXC : .NET Core 2.1.15 (CoreCLR 4.6.28325.01, CoreFX 4.6.28327.02), X64 RyuJIT
//  Job-ODABAZ : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT
//
// IterationCount=3  LaunchCount=1  WarmupCount=3
//
// |   Method |       Runtime |     Mean |    Error |   StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |--------- |-------------- |---------:|---------:|---------:|------:|------:|------:|----------:|
// | DoRotate |    .NET 4.7.2 | 28.77 ms | 3.304 ms | 0.181 ms |     - |     - |     - |    6.5 KB |
// | DoRotate | .NET Core 2.1 | 16.27 ms | 1.044 ms | 0.057 ms |     - |     - |     - |   5.25 KB |
// | DoRotate | .NET Core 3.1 | 17.12 ms | 4.352 ms | 0.239 ms |     - |     - |     - |   6.57 KB |

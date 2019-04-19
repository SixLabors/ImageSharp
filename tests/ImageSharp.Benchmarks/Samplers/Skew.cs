// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Benchmarks.Samplers
{
    [Config(typeof(Config.ShortClr))]
    public class Skew
    {
        [Benchmark]
        public Size DoSkew()
        {
            using (var image = new Image<Rgba32>(Configuration.Default, 400, 400, Rgba32.BlanchedAlmond))
            {
                image.Mutate(x => x.Skew(20, 10));

                return image.Size();
            }
        }
    }
}

// Nov 7 2018
//BenchmarkDotNet=v0.10.14, OS=Windows 10.0.17763
//Intel Core i7-6600U CPU 2.60GHz(Skylake), 1 CPU, 4 logical and 2 physical cores
//.NET Core SDK = 2.1.403

// [Host]     : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT
//  Job-KKDIMW : .NET Framework 4.7.1 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3190.0
//  Job-IUZRFA : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT

//LaunchCount=1  TargetCount=3  WarmupCount=3

// #### BEFORE ####:
//Method | Runtime |     Mean |    Error |    StdDev | Allocated |
//------- |-------- |---------:|---------:|----------:|----------:|
// DoSkew |     Clr | 78.14 ms | 8.383 ms | 0.4736 ms |      6 KB |
// DoSkew |    Core | 44.22 ms | 4.109 ms | 0.2322 ms |   4.28 KB |

// #### AFTER ####:
//Method | Runtime |     Mean |     Error |    StdDev | Allocated |
//------- |-------- |---------:|----------:|----------:|----------:|
// DoSkew |     Clr | 71.63 ms | 25.589 ms | 1.4458 ms |      6 KB |
// DoSkew |    Core | 38.99 ms |  8.640 ms | 0.4882 ms |   4.36 KB |
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Benchmarks.Samplers
{
    [Config(typeof(Config.ShortClr))]
    public class Rotate
    {
        [Benchmark]
        public Size DoRotate()
        {
            using (var image = new Image<Rgba32>(Configuration.Default, 400, 400, Rgba32.BlanchedAlmond))
            {
                image.Mutate(x => x.Rotate(37.5F));

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
//   Method | Runtime |     Mean |     Error |    StdDev | Allocated |
//--------- |-------- |---------:|----------:|----------:|----------:|
// DoRotate |     Clr | 85.19 ms | 13.379 ms | 0.7560 ms |      6 KB |
// DoRotate |    Core | 53.51 ms |  9.512 ms | 0.5375 ms |   4.29 KB |

// #### AFTER ####:
//Method | Runtime |     Mean |    Error |   StdDev | Allocated |
//--------- |-------- |---------:|---------:|---------:|----------:|
// DoRotate |     Clr | 77.08 ms | 23.97 ms | 1.354 ms |      6 KB |
// DoRotate |    Core | 40.36 ms | 47.43 ms | 2.680 ms |   4.36 KB |
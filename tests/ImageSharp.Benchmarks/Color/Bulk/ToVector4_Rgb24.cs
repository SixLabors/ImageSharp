// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class ToVector4_Rgb24 : ToVector4<Rgb24>
    {
        [Benchmark(Baseline = true)]
        public void PixelOperations_Base()
        {
            new PixelOperations<Rgb24>().ToVector4(
                this.Configuration,
                this.source.GetSpan(),
                this.destination.GetSpan());
        }
    }
}

// 2020-11-02
// ##########
//
// BenchmarkDotNet = v0.12.1, OS = Windows 10.0.19041.572(2004 /?/ 20H1)
// Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
// .NET Core SDK=3.1.403
//  [Host]     : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
//  Job-XYEQXL : .NET Framework 4.8 (4.8.4250.0), X64 RyuJIT
//  Job-HSXNJV : .NET Core 2.1.23 (CoreCLR 4.6.29321.03, CoreFX 4.6.29321.01), X64 RyuJIT
//  Job-YUREJO : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
//
// IterationCount=3  LaunchCount=1  WarmupCount=3
//
// |                      Method |        Job |       Runtime | Count |       Mean |       Error |    StdDev | Ratio | RatioSD |  Gen 0 | Gen 1 | Gen 2 | Allocated |
// |---------------------------- |----------- |-------------- |------ |-----------:|------------:|----------:|------:|--------:|-------:|------:|------:|----------:|
// |        PixelOperations_Base | Job-OIBEDX |    .NET 4.7.2 |    64 |   298.4 ns |    33.63 ns |   1.84 ns |  1.00 |    0.00 | 0.0057 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-OIBEDX |    .NET 4.7.2 |    64 |   355.5 ns |   908.51 ns |  49.80 ns |  1.19 |    0.17 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-OPAORC | .NET Core 2.1 |    64 |   220.1 ns |    13.77 ns |   0.75 ns |  1.00 |    0.00 | 0.0055 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-OPAORC | .NET Core 2.1 |    64 |   228.5 ns |    41.41 ns |   2.27 ns |  1.04 |    0.01 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-VPSIRL | .NET Core 3.1 |    64 |   213.6 ns |    12.47 ns |   0.68 ns |  1.00 |    0.00 | 0.0057 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-VPSIRL | .NET Core 3.1 |    64 |   217.0 ns |     9.95 ns |   0.55 ns |  1.02 |    0.01 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-OIBEDX |    .NET 4.7.2 |   256 |   829.0 ns |   242.93 ns |  13.32 ns |  1.00 |    0.00 | 0.0057 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-OIBEDX |    .NET 4.7.2 |   256 |   448.9 ns |     4.04 ns |   0.22 ns |  0.54 |    0.01 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-OPAORC | .NET Core 2.1 |   256 |   863.0 ns | 1,253.26 ns |  68.70 ns |  1.00 |    0.00 | 0.0048 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-OPAORC | .NET Core 2.1 |   256 |   309.2 ns |    66.16 ns |   3.63 ns |  0.36 |    0.03 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-VPSIRL | .NET Core 3.1 |   256 |   737.0 ns |   253.90 ns |  13.92 ns |  1.00 |    0.00 | 0.0057 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-VPSIRL | .NET Core 3.1 |   256 |   212.3 ns |     1.07 ns |   0.06 ns |  0.29 |    0.01 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-OIBEDX |    .NET 4.7.2 |  2048 | 5,625.6 ns |   404.35 ns |  22.16 ns |  1.00 |    0.00 |      - |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-OIBEDX |    .NET 4.7.2 |  2048 | 1,974.1 ns |   229.84 ns |  12.60 ns |  0.35 |    0.00 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-OPAORC | .NET Core 2.1 |  2048 | 5,467.2 ns |   537.29 ns |  29.45 ns |  1.00 |    0.00 |      - |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-OPAORC | .NET Core 2.1 |  2048 | 1,985.5 ns | 4,714.23 ns | 258.40 ns |  0.36 |    0.05 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-VPSIRL | .NET Core 3.1 |  2048 | 5,888.2 ns | 1,622.23 ns |  88.92 ns |  1.00 |    0.00 |      - |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-VPSIRL | .NET Core 3.1 |  2048 | 1,165.0 ns |   191.71 ns |  10.51 ns |  0.20 |    0.00 |      - |     - |     - |         - |

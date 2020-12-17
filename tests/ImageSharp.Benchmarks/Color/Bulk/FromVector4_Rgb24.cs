// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class FromVector4_Rgb24 : FromVector4<Rgb24>
    {
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
// |        PixelOperations_Base | Job-XYEQXL |    .NET 4.7.2 |    64 |   343.2 ns |   305.91 ns |  16.77 ns |  1.00 |    0.00 | 0.0057 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-XYEQXL |    .NET 4.7.2 |    64 |   320.8 ns |    19.93 ns |   1.09 ns |  0.94 |    0.05 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-HSXNJV | .NET Core 2.1 |    64 |   234.3 ns |    17.98 ns |   0.99 ns |  1.00 |    0.00 | 0.0052 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-HSXNJV | .NET Core 2.1 |    64 |   246.0 ns |    82.34 ns |   4.51 ns |  1.05 |    0.02 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-YUREJO | .NET Core 3.1 |    64 |   222.3 ns |    39.46 ns |   2.16 ns |  1.00 |    0.00 | 0.0057 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-YUREJO | .NET Core 3.1 |    64 |   243.4 ns |    33.58 ns |   1.84 ns |  1.09 |    0.01 |      - |     - |     - |         - |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-XYEQXL |    .NET 4.7.2 |   256 |   824.9 ns |    32.77 ns |   1.80 ns |  1.00 |    0.00 | 0.0057 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-XYEQXL |    .NET 4.7.2 |   256 |   967.0 ns |    39.09 ns |   2.14 ns |  1.17 |    0.01 | 0.0172 |     - |     - |      72 B |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-HSXNJV | .NET Core 2.1 |   256 |   756.9 ns |    94.43 ns |   5.18 ns |  1.00 |    0.00 | 0.0048 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-HSXNJV | .NET Core 2.1 |   256 | 1,003.3 ns | 3,192.09 ns | 174.97 ns |  1.32 |    0.22 | 0.0172 |     - |     - |      72 B |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-YUREJO | .NET Core 3.1 |   256 |   748.6 ns |   248.03 ns |  13.60 ns |  1.00 |    0.00 | 0.0057 |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-YUREJO | .NET Core 3.1 |   256 |   437.0 ns |    36.48 ns |   2.00 ns |  0.58 |    0.01 | 0.0172 |     - |     - |      72 B |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-XYEQXL |    .NET 4.7.2 |  2048 | 5,751.6 ns |   704.24 ns |  38.60 ns |  1.00 |    0.00 |      - |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-XYEQXL |    .NET 4.7.2 |  2048 | 4,391.6 ns |   718.17 ns |  39.37 ns |  0.76 |    0.00 | 0.0153 |     - |     - |      72 B |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-HSXNJV | .NET Core 2.1 |  2048 | 6,202.0 ns | 1,815.18 ns |  99.50 ns |  1.00 |    0.00 |      - |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-HSXNJV | .NET Core 2.1 |  2048 | 4,225.6 ns | 1,004.03 ns |  55.03 ns |  0.68 |    0.01 | 0.0153 |     - |     - |      72 B |
// |                             |            |               |       |            |             |           |       |         |        |       |       |           |
// |        PixelOperations_Base | Job-YUREJO | .NET Core 3.1 |  2048 | 6,157.1 ns | 2,516.98 ns | 137.96 ns |  1.00 |    0.00 |      - |     - |     - |      24 B |
// | PixelOperations_Specialized | Job-YUREJO | .NET Core 3.1 |  2048 | 1,822.7 ns | 1,764.43 ns |  96.71 ns |  0.30 |    0.02 | 0.0172 |     - |     - |      72 B |

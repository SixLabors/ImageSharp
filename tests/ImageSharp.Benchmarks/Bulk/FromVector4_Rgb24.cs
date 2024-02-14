// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

[Config(typeof(Config.Short))]
public class FromVector4_Rgb24 : FromVector4<Rgb24>;

/*
 BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.3085/23H2/2023Update/SunValley3)
11th Gen Intel Core i7-11370H 3.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.200-preview.23624.5
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-NEHCEM : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Runtime=.NET 8.0  Arguments=/p:DebugType=portable  IterationCount=3
LaunchCount=1  WarmupCount=3

| Method                      | Count | Mean        | Error     | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |------ |------------:|----------:|---------:|------:|-------:|----------:|------------:|
| PixelOperations_Base        | 64    |    95.87 ns |  13.60 ns | 0.745 ns |  1.00 |      - |         - |          NA |
| PixelOperations_Specialized | 64    |    97.34 ns |  30.34 ns | 1.663 ns |  1.02 |      - |         - |          NA |
|                             |       |             |           |          |       |        |           |             |
| PixelOperations_Base        | 256   |   337.80 ns |  88.10 ns | 4.829 ns |  1.00 |      - |         - |          NA |
| PixelOperations_Specialized | 256   |   195.07 ns |  30.54 ns | 1.674 ns |  0.58 | 0.0153 |      96 B |          NA |
|                             |       |             |           |          |       |        |           |             |
| PixelOperations_Base        | 2048  | 2,561.79 ns | 162.45 ns | 8.905 ns |  1.00 |      - |         - |          NA |
| PixelOperations_Specialized | 2048  |   741.85 ns |  18.05 ns | 0.989 ns |  0.29 | 0.0153 |      96 B |          NA |
 */

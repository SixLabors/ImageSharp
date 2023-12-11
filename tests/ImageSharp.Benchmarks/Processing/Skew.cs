// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Processing;

[Config(typeof(Config.Standard))]
public class Skew
{
    [Benchmark]
    public Size DoSkew()
    {
        using Image<Rgba32> image = new(Configuration.Default, 400, 400, Color.BlanchedAlmond);
        image.Mutate(x => x.Skew(20, 10));

        return image.Size;
    }
}

// #### 2021-04-06 ####
//
// BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
// Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
// .NET Core SDK=5.0.201
//  [Host]     : .NET Core 5.0.4 (CoreCLR 5.0.421.11614, CoreFX 5.0.421.11614), X64 RyuJIT
//  Job-HQWHDJ : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT
//  Job-RPXLFC : .NET Core 2.1.26 (CoreCLR 4.6.29812.02, CoreFX 4.6.29812.01), X64 RyuJIT
//  Job-YMSKIM : .NET Core 3.1.13 (CoreCLR 4.700.21.11102, CoreFX 4.700.21.11602), X64 RyuJIT
//
//
// | Method |        Job |       Runtime |      Mean |     Error |    StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |------- |----------- |-------------- |----------:|----------:|----------:|------:|------:|------:|----------:|
// | DoSkew | Job-YEGFRQ |    .NET 4.7.2 | 23.563 ms | 0.0731 ms | 0.0570 ms |     - |     - |     - |   6.75 KB |
// | DoSkew | Job-HZHOGR | .NET Core 2.1 | 13.700 ms | 0.2727 ms | 0.5122 ms |     - |     - |     - |   5.25 KB |
// | DoSkew | Job-LTEUKY | .NET Core 3.1 |  9.971 ms | 0.0254 ms | 0.0225 ms |     - |     - |     - |   6.61 KB |

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Processing;

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
// |   Method |        Job |       Runtime |     Mean |    Error |   StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
// |--------- |----------- |-------------- |---------:|---------:|---------:|------:|------:|------:|----------:|
// | DoRotate | Job-BAUEPW |    .NET 4.7.2 | 30.73 ms | 0.397 ms | 0.331 ms |     - |     - |     - |   6.75 KB |
// | DoRotate | Job-SNWMCN | .NET Core 2.1 | 16.31 ms | 0.317 ms | 0.352 ms |     - |     - |     - |   5.25 KB |
// | DoRotate | Job-MRMBJZ | .NET Core 3.1 | 12.21 ms | 0.239 ms | 0.245 ms |     - |     - |     - |   6.61 KB |

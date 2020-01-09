using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Benchmarks.Samplers
{
    [Config(typeof(Config.ShortClr))]
    public class Diffuse
    {
        [Benchmark]
        public Size DoDiffuse()
        {
            using (var image = new Image<Rgba32>(Configuration.Default, 800, 800, Rgba32.BlanchedAlmond))
            {
                image.Mutate(x => x.Diffuse());

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

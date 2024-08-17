// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Samplers;

[Config(typeof(Config.Standard))]
public class GaussianBlur
{
    [Benchmark]
    public void Blur()
    {
        using Image<Rgba32> image = new(Configuration.Default, 400, 400, Color.White.ToPixel<Rgba32>());
        image.Mutate(c => c.GaussianBlur());
    }
}

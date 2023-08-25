// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Processing;

[Config(typeof(Config.MultiFramework))]
public class OilPaint
{
    [Benchmark]
    public void DoOilPaint()
    {
        using Image<RgbaVector> image = new Image<RgbaVector>(1920, 1200, new(127, 191, 255));
        image.Mutate(ctx => ctx.OilPaint());
    }
}

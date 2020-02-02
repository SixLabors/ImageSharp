// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Samplers
{
    [Config(typeof(Config.ShortClr))]
    public class GaussianBlur
    {
        [Benchmark]
        public void Blur()
        {
            using (var image = new Image<Rgba32>(Configuration.Default, 400, 400, Rgba32.White))
            {
                image.Mutate(c => c.GaussianBlur());
            }
        }
    }
}

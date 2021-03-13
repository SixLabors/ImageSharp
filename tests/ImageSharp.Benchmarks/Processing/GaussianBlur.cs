// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Samplers
{
    [Config(typeof(Config.MultiFramework))]
    public class GaussianBlur
    {
        [Benchmark]
        public void Blur()
        {
            using var image = new Image<Rgba32>(Configuration.Default, 400, 400, Color.White);
            image.Mutate(c => c.GaussianBlur());
        }
    }
}

// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks
{

    using BenchmarkDotNet.Attributes;
    using ImageSharp.PixelFormats;
    using ImageSharp.Drawing;
    using ImageSharp.Processing.Processors;
    using CoreImage = ImageSharp.Image;
    using CoreSize = ImageSharp.Size;

    public class Glow : BenchmarkBase
    {
        private GlowProcessor<Rgba32> bulk;
        private GlowProcessorParallel<Rgba32> parallel;

        [Setup]
        public void Setup()
        {
            this.bulk = new GlowProcessor<Rgba32>(NamedColors<Rgba32>.Beige) { Radius = 800 * .5f, };
            this.parallel = new GlowProcessorParallel<Rgba32>(NamedColors<Rgba32>.Beige) { Radius = 800 * .5f, };

        }
        [Benchmark(Description = "ImageSharp Glow - Bulk")]
        public CoreSize GlowBulk()
        {
            using (CoreImage image = new CoreImage(800, 800))
            {
                image.ApplyProcessor(bulk, image.Bounds);
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "ImageSharp Glow - Parallel")]
        public CoreSize GLowSimple()
        {
            using (CoreImage image = new CoreImage(800, 800))
            {
                image.ApplyProcessor(parallel, image.Bounds);
                return new CoreSize(image.Width, image.Height);
            }
        }
    }
}

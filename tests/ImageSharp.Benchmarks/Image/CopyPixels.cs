// <copyright file="CopyPixels.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.Image
{
    using System.Threading.Tasks;

    using BenchmarkDotNet.Attributes;

    using CoreColor = ImageSharp.Color;
    using CoreImage = ImageSharp.Image;

    public class CopyPixels : BenchmarkBase
    {
        [Benchmark(Description = "Copy by Pixel")]
        public CoreColor CopyByPixel()
        {
            CoreImage source = new CoreImage(1024, 768);
            CoreImage target = new CoreImage(1024, 768);
            using (PixelAccessor<CoreColor> sourcePixels = source.Lock())
            using (PixelAccessor<CoreColor> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    source.Height,
                    Configuration.Default.ParallelOptions,
                    y =>
                    {
                        for (int x = 0; x < source.Width; x++)
                        {
                            targetPixels[x, y] = sourcePixels[x, y];
                        }
                    });

                return targetPixels[0, 0];
            }
        }
    }
}

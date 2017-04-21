// <copyright file="CopyPixels.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.Image
{
    using System.Threading.Tasks;

    using BenchmarkDotNet.Attributes;

    using ImageSharp.PixelFormats;

    using CoreImage = ImageSharp.Image;

    public class CopyPixels : BenchmarkBase
    {
        [Benchmark(Description = "Copy by Pixel")]
        public Rgba32 CopyByPixel()
        {
            using (CoreImage source = new CoreImage(1024, 768))
            using (CoreImage target = new CoreImage(1024, 768))
            {
                using (PixelAccessor<Rgba32> sourcePixels = source.Lock())
                using (PixelAccessor<Rgba32> targetPixels = target.Lock())
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
}

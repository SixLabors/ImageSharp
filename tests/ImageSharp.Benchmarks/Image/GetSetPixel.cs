// <copyright file="GetSetPixel.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks.Image
{
    using System.Drawing;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.PixelFormats;

    using SystemColor = System.Drawing.Color;

    public class GetSetPixel : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "System.Drawing GetSet pixel")]
        public SystemColor ResizeSystemDrawing()
        {
            using (Bitmap source = new Bitmap(400, 400))
            {
                source.SetPixel(200, 200, SystemColor.White);
                return source.GetPixel(200, 200);
            }
        }

        [Benchmark(Description = "ImageSharp GetSet pixel")]
        public Rgba32 ResizeCore()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(400, 400))
            {
                using (PixelAccessor<Rgba32> imagePixels = image.Lock())
                {
                    imagePixels[200, 200] = Rgba32.White;
                    return imagePixels[200, 200];
                }
            }
        }
    }
}

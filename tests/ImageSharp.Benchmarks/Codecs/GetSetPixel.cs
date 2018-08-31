// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    public class GetSetPixel : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "System.Drawing GetSet pixel")]
        public Color ResizeSystemDrawing()
        {
            using (var source = new Bitmap(400, 400))
            {
                source.SetPixel(200, 200, Color.White);
                return source.GetPixel(200, 200);
            }
        }

        [Benchmark(Description = "ImageSharp GetSet pixel")]
        public Rgba32 ResizeCore()
        {
            using (var image = new Image<Rgba32>(400, 400))
            {
                image[200, 200] = Rgba32.White;
                return image[200, 200];
            }
        }
    }
}

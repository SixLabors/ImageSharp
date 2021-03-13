// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks
{
    public class GetSetPixel
    {
        [Benchmark(Baseline = true, Description = "System.Drawing GetSet pixel")]
        public System.Drawing.Color GetSetSystemDrawing()
        {
            using var source = new Bitmap(400, 400);
            source.SetPixel(200, 200, System.Drawing.Color.White);
            return source.GetPixel(200, 200);
        }

        [Benchmark(Description = "ImageSharp GetSet pixel")]
        public Rgba32 GetSetImageSharp()
        {
            using var image = new Image<Rgba32>(400, 400);
            image[200, 200] = Color.White;
            return image[200, 200];
        }
    }
}

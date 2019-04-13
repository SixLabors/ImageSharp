// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using System.Drawing;
using System.Drawing.Drawing2D;

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Processing;

using CoreSize = SixLabors.Primitives.Size;

namespace SixLabors.ImageSharp.Benchmarks
{
    public class Crop : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Crop")]
        public Size CropSystemDrawing()
        {
            using (var source = new Bitmap(800, 800))
            using (var destination = new Bitmap(100, 100))
            using (var graphics = Graphics.FromImage(destination))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, 100, 100), 0, 0, 100, 100, GraphicsUnit.Pixel);
         
                return destination.Size;
            }
        }

        [Benchmark(Description = "ImageSharp Crop")]
        public CoreSize CropResizeCore()
        {
            using (var image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.Crop(100, 100));
                return new CoreSize(image.Width, image.Height);
            }
        }
    }
}

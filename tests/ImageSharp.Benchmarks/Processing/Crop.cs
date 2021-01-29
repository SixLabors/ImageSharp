// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing;
using System.Drawing.Drawing2D;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SDRectangle = System.Drawing.Rectangle;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Processing
{
    [Config(typeof(Config.MultiFramework))]
    public class Crop
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Crop")]
        public SDSize CropSystemDrawing()
        {
            using var source = new Bitmap(800, 800);
            using var destination = new Bitmap(100, 100);
            using var graphics = Graphics.FromImage(destination);

            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.DrawImage(source, new SDRectangle(0, 0, 100, 100), 0, 0, 100, 100, GraphicsUnit.Pixel);

            return destination.Size;
        }

        [Benchmark(Description = "ImageSharp Crop")]
        public Size CropImageSharp()
        {
            using var image = new Image<Rgba32>(800, 800);
            image.Mutate(x => x.Crop(100, 100));
            return new Size(image.Width, image.Height);
        }
    }
}

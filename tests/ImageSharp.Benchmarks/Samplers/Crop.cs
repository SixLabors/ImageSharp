// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks
{
    using System.Drawing;
    using System.Drawing.Drawing2D;

    using BenchmarkDotNet.Attributes;
    using SixLabors.ImageSharp.Processing;

    using CoreSize = SixLabors.Primitives.Size;

    public class Crop : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Crop")]
        public Size CropSystemDrawing()
        {
            using (Bitmap source = new Bitmap(800, 800))
            {
                using (Bitmap destination = new Bitmap(100, 100))
                {
                    using (Graphics graphics = Graphics.FromImage(destination))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.DrawImage(source, new Rectangle(0, 0, 100, 100), 0, 0, 100, 100, GraphicsUnit.Pixel);
                    }

                    return destination.Size;
                }
            }
        }

        [Benchmark(Description = "ImageSharp Crop")]
        public CoreSize CropResizeCore()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.Crop(100, 100));
                return new CoreSize(image.Width, image.Height);
            }
        }
    }
}

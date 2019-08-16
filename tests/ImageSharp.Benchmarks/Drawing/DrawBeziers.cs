// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Numerics;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks
{
    public class DrawBeziers : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Draw Beziers")]
        public void DrawPathSystemDrawing()
        {
            using (var destination = new Bitmap(800, 800))
            using (var graphics = Graphics.FromImage(destination))
            {
                graphics.InterpolationMode = InterpolationMode.Default;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using (var pen = new System.Drawing.Pen(System.Drawing.Color.HotPink, 10))
                {
                    graphics.DrawBeziers(pen, new[] {
                        new PointF(10, 500),
                        new PointF(30, 10),
                        new PointF(240, 30),
                        new PointF(300, 500)
                    });
                }

                using (var stream = new MemoryStream())
                {
                    destination.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                }
            }
        }

        [Benchmark(Description = "ImageSharp Draw Beziers")]
        public void DrawLinesCore()
        {
            using (var image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.DrawBeziers(
                    Rgba32.HotPink,
                    10,
                    new Vector2(10, 500),
                    new Vector2(30, 10),
                    new Vector2(240, 30),
                    new Vector2(300, 500)));

                using (var stream = new MemoryStream())
                {
                    image.SaveAsBmp(stream);
                }
            }
        }
    }
}

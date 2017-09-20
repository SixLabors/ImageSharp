﻿// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks
{
    using System.Drawing;
    using System.Drawing.Drawing2D;

    using BenchmarkDotNet.Attributes;

    using System.IO;
    using System.Numerics;

    using SixLabors.ImageSharp.PixelFormats;

    public class DrawPolygon : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Draw Polygon")]
        public void DrawPolygonSystemDrawing()
        {
            using (var destination = new Bitmap(800, 800))
            {

                using (var graphics = Graphics.FromImage(destination))
                {
                    graphics.InterpolationMode = InterpolationMode.Default;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var pen = new Pen(System.Drawing.Color.HotPink, 10);
                    graphics.DrawPolygon(pen, new[] {
                        new PointF(10, 10),
                        new PointF(550, 50),
                        new PointF(200, 400)
                    });
                }

                using (var ms = new MemoryStream())
                {
                    destination.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                }
            }
        }

        [Benchmark(Description = "ImageSharp Draw Polygon")]
        public void DrawPolygonCore()
        {
            using (var image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.DrawPolygon(
                    Rgba32.HotPink,
                    10,
                    new SixLabors.Primitives.PointF[] {
                        new Vector2(10, 10),
                        new Vector2(550, 50),
                        new Vector2(200, 400)
                    }));

                using (var ms = new MemoryStream())
                {
                    image.SaveAsBmp(ms);
                }
            }
        }
    }
}
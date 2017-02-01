// <copyright file="DrawLines.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks
{
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.IO;
    using System.Numerics;

    using BenchmarkDotNet.Attributes;

    using CoreColor = ImageSharp.Color;
    using CoreImage = ImageSharp.Image;
    using CorePoint = ImageSharp.Point;

    public class DrawLines : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Draw Lines")]
        public void DrawPathSystemDrawing()
        {
            using (Bitmap destination = new Bitmap(800, 800))
            {

                using (Graphics graphics = Graphics.FromImage(destination))
                {
                    graphics.InterpolationMode = InterpolationMode.Default;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var pen = new Pen(Color.HotPink, 10);
                    graphics.DrawLines(pen, new[] {
                        new PointF(10, 10),
                        new PointF(550, 50),
                        new PointF(200, 400)
                    });
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    destination.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                }
            }
        }

        [Benchmark(Description = "ImageSharp Draw Lines")]
        public void DrawLinesCore()
        {
            using (CoreImage image = new CoreImage(800, 800))
            {
                image.DrawLines(
                    CoreColor.HotPink,
                    10,
                    new[] {
                        new Vector2(10, 10),
                        new Vector2(550, 50),
                        new Vector2(200, 400)
                    });

                using (MemoryStream ms = new MemoryStream())
                {
                    image.SaveAsBmp(ms);
                }
            }
        }
    }
}
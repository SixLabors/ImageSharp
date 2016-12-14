// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks
{
    using System.Drawing;
    using System.Drawing.Drawing2D;

    using BenchmarkDotNet.Attributes;
    using CoreImage = ImageSharp.Image;
    using CorePoint = ImageSharp.Point;
    using CorePointF= ImageSharp.PointF;
    using CoreColor= ImageSharp.Color;
    using System.IO;
    
    public class DrawPolygon
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Draw Polygon")]
        public void DrawPolygonSystemDrawing()
        {
            using (Bitmap destination = new Bitmap(800, 800))
            {

                using (Graphics graphics = Graphics.FromImage(destination))
                {
                    graphics.InterpolationMode = InterpolationMode.Default;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var pen = new Pen(Color.HotPink, 10);
                    graphics.DrawPolygon(pen, new[] {
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

        [Benchmark(Description = "ImageSharp Draw Polygon")]
        public void DrawPolygonCore()
        {
            CoreImage image = new CoreImage(800, 800);
            
            image.DrawPolygon(CoreColor.HotPink, 10, new[] {
                     new CorePointF(10, 10),
                     new CorePointF(550, 50),
                     new CorePointF(200, 400)
            });

            using (MemoryStream ms = new MemoryStream())
            {
                image.SaveAsBmp(ms);
            }
        }
    }
}
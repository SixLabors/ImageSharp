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
    using CoreRectangle = ImageSharp.Rectangle;
    using CoreColor = ImageSharp.Color;
    using System.IO;
    using System.Numerics;

    public class FillRectangle
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Fill Rectangle")]
        public void FillRectangleSystemDrawing()
        {
            using (Bitmap destination = new Bitmap(800, 800))
            {

                using (Graphics graphics = Graphics.FromImage(destination))
                {
                    graphics.InterpolationMode = InterpolationMode.Default;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var pen = new Pen(Color.HotPink, 10);
                    graphics.FillRectangle(Brushes.HotPink, new Rectangle(10, 10, 190, 140));
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    destination.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                }
            }
        }

        [Benchmark(Description = "ImageSharp Fill Rectangle")]
        public void FillRactangleCore()
        {
            CoreImage image = new CoreImage(800, 800);

            image.Fill(CoreColor.HotPink, new ImageSharp.Drawing.Shapes.RectangularPolygon(new CoreRectangle(10, 10, 190, 140)));

            using (MemoryStream ms = new MemoryStream())
            {
                image.SaveAsBmp(ms);
            }
        }

        [Benchmark(Description = "ImageSharp Fill Rectangle - As Polygon")]
        public void FillPolygonCore()
        {
            CoreImage image = new CoreImage(800, 800);

            image.FillPolygon(CoreColor.HotPink, new[] {
                            new Vector2(10, 10),
                            new Vector2(200, 10),
                            new Vector2(200, 150),
                            new Vector2(10, 150) });

            using (MemoryStream ms = new MemoryStream())
            {
                image.SaveAsBmp(ms);
            }
        }
    }
}
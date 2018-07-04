// <copyright file="FillPolygon.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Numerics;
using SixLabors.Shapes;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks
{
    public class FillPolygon : BenchmarkBase
    {
        private readonly Polygon shape;

        public FillPolygon()
        {
            this.shape = new Polygon(new LinearLineSegment(new Vector2(10, 10),
                        new Vector2(550, 50),
                        new Vector2(200, 400)));
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Fill Polygon")]
        public void DrawSolidPolygonSystemDrawing()
        {
            using (Bitmap destination = new Bitmap(800, 800))
            {

                using (Graphics graphics = Graphics.FromImage(destination))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.FillPolygon(System.Drawing.Brushes.HotPink,
                    new[]
                        {
                        new Point(10, 10),
                        new Point(550, 50),
                        new Point(200, 400)
                    });
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    destination.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                }
            }
        }

        [Benchmark(Description = "ImageSharp Fill Polygon")]
        public void DrawSolidPolygonCore()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.FillPolygon(
                    Rgba32.HotPink,
                    new SixLabors.Primitives.PointF[] {
                        new Vector2(10, 10),
                        new Vector2(550, 50),
                        new Vector2(200, 400)
                    }));

                using (MemoryStream ms = new MemoryStream())
                {
                    image.SaveAsBmp(ms);
                }
            }
        }

        [Benchmark(Description = "ImageSharp Fill Polygon - cached shape")]
        public void DrawSolidPolygonCoreCahced()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.Fill(
                    Rgba32.HotPink,
                    this.shape));

                using (MemoryStream ms = new MemoryStream())
                {
                    image.SaveAsBmp(ms);
                }
            }
        }
    }
}
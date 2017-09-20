﻿// <copyright file="FillPolygon.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks
{
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.IO;
    using System.Numerics;
    using SixLabors.Shapes;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.PixelFormats;

    public class FillPolygon : BenchmarkBase
    {
        private readonly Polygon shape;

        public FillPolygon()
        {
            this.shape = new SixLabors.Shapes.Polygon(new LinearLineSegment(new Vector2(10, 10),
                        new Vector2(550, 50),
                        new Vector2(200, 400)));
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Fill Polygon")]
        public void DrawSolidPolygonSystemDrawing()
        {
            using (var destination = new Bitmap(800, 800))
            {

                using (var graphics = Graphics.FromImage(destination))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.FillPolygon(Brushes.HotPink,
                    new[]
                        {
                        new Point(10, 10),
                        new Point(550, 50),
                        new Point(200, 400)
                    });
                }
                using (var ms = new MemoryStream())
                {
                    destination.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                }
            }
        }

        [Benchmark(Description = "ImageSharp Fill Polygon")]
        public void DrawSolidPolygonCore()
        {
            using (var image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.FillPolygon(
                    Rgba32.HotPink,
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

        [Benchmark(Description = "ImageSharp Fill Polygon - cached shape")]
        public void DrawSolidPolygonCoreCahced()
        {
            using (var image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.Fill(
                    Rgba32.HotPink,
                    this.shape));

                using (var ms = new MemoryStream())
                {
                    image.SaveAsBmp(ms);
                }
            }
        }
    }
}
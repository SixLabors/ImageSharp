// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using CoreRectangle = SixLabors.Primitives.Rectangle;
using CoreSize = SixLabors.Primitives.Size;

namespace SixLabors.ImageSharp.Benchmarks
{


    public class FillRectangle : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Fill Rectangle")]
        public Size FillRectangleSystemDrawing()
        {
            using (Bitmap destination = new Bitmap(800, 800))
            {

                using (Graphics graphics = Graphics.FromImage(destination))
                {
                    graphics.InterpolationMode = InterpolationMode.Default;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.FillRectangle(System.Drawing.Brushes.HotPink, new Rectangle(10, 10, 190, 140));
                }
                return destination.Size;
            }
        }

        [Benchmark(Description = "ImageSharp Fill Rectangle")]
        public CoreSize FillRactangleCore()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.Fill(Rgba32.HotPink, new CoreRectangle(10, 10, 190, 140)));

                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "ImageSharp Fill Rectangle - As Polygon")]
        public CoreSize FillPolygonCore()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.FillPolygon(
                    Rgba32.HotPink,
                    new SixLabors.Primitives.PointF[] {
                        new Vector2(10, 10),
                        new Vector2(200, 10),
                        new Vector2(200, 150),
                        new Vector2(10, 150) }));

                return new CoreSize(image.Width, image.Height);
            }
        }
    }
}
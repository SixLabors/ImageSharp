// <copyright file="FillWithPattern.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using CoreBrushes = SixLabors.ImageSharp.Processing.Brushes;

namespace SixLabors.ImageSharp.Benchmarks
{
    public class FillWithPattern
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Fill with Pattern")]
        public void DrawPatternPolygonSystemDrawing()
        {
            using (Bitmap destination = new Bitmap(800, 800))
            {
                using (Graphics graphics = Graphics.FromImage(destination))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    HatchBrush brush = new HatchBrush(HatchStyle.BackwardDiagonal, Color.HotPink);
                    graphics.FillRectangle(brush, new Rectangle(0, 0, 800, 800)); // can't find a way to flood fill with a brush
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    destination.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                }
            }
        }

        [Benchmark(Description = "ImageSharp Fill with Pattern")]
        public void DrawPatternPolygon3Core()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                image.Mutate(x => x.Fill(CoreBrushes.BackwardDiagonal(Rgba32.HotPink)));

                using (MemoryStream ms = new MemoryStream())
                {
                    image.SaveAsBmp(ms);
                }
            }
        }
    }
}
// <copyright file="FillWithPattern.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks
{
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using CoreBrushes = ImageSharp.Drawing.Brushes.Brushes;
    using CoreColor = ImageSharp.Color;
    using CoreImage = ImageSharp.Image;

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
                    var brush = new HatchBrush(HatchStyle.BackwardDiagonal, System.Drawing.Color.HotPink);
                    graphics.FillRectangle(brush, new Rectangle(0,0, 800,800)); // can't find a way to flood fill with a brush
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
            using (CoreImage image = new CoreImage(800, 800))
            {
                image.Fill(CoreBrushes.BackwardDiagonal(CoreColor.HotPink));

                using (MemoryStream ms = new MemoryStream())
                {
                    image.SaveAsBmp(ms);
                }
            }
        }
    }
}
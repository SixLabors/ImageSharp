// <copyright file="FillPolygon.cs" company="James Jackson-South">
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
    using Drawing.Processors;

    public class FillPolygonStatagies : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "Simple Fill Polygon")]
        public void DrawSolidPolygonSimple()
        {
            CoreImage image = new CoreImage(800, 800);
            var brush = Drawing.Brushes.Brushes.Solid(CoreColor.HotPink);
            var shape = new Drawing.Shapes.LinearPolygon(new[] {
                     new Vector2(10, 10),
                     new Vector2(550, 50),
                     new Vector2(200, 400)
                 });

            image.Apply(new FillShapeProcessor<ImageSharp.Color>(brush, shape, Drawing.GraphicsOptions.Default));
        }

        [Benchmark(Description = "Fast Fill Polygon")]
        public void DrawSolidPolygonFast()
        {
            CoreImage image = new CoreImage(800, 800);
            var brush = Drawing.Brushes.Brushes.Solid(CoreColor.HotPink);
            var shape  = new Drawing.Shapes.LinearPolygon(new[] {
                     new Vector2(10, 10),
                     new Vector2(550, 50),
                     new Vector2(200, 400)
                 });

            image.Apply(new FillShapeProcessorFast<ImageSharp.Color>(brush, shape, Drawing.GraphicsOptions.Default));            
        }
    }
}
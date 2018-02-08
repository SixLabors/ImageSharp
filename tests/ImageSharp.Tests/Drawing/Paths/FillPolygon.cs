// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Brushes;
using SixLabors.ImageSharp.Drawing.Processors;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Shapes;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Paths
{
    public class FillPolygon : BaseImageOperationsExtensionTest
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush<Rgba32> brush = Brushes.Solid(Rgba32.HotPink);
        SixLabors.Primitives.PointF[] path = new SixLabors.Primitives.PointF[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                };


        [Fact]
        public void CorrectlySetsBrushAndPath()
        {
            this.operations.FillPolygon(brush, path);

            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsBrushPathAndOptions()
        {
            this.operations.FillPolygon(brush, path, noneDefault);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsColorAndPath()
        {
            this.operations.FillPolygon(color, path);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();


            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorPathAndOptions()
        {
            this.operations.FillPolygon(color, path, noneDefault);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();


            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }
    }
}

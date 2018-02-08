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
    public class FillPath : BaseImageOperationsExtensionTest
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush<Rgba32> brush = Brushes.Solid(Rgba32.HotPink);
        IPath path = new SixLabors.Shapes.Path(new LinearLineSegment(new SixLabors.Primitives.PointF[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                }));

        [Fact]
        public void CorrectlySetsBrushAndPath()
        {
            this.operations.Fill(brush, path);
            var processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);

            // path is converted to a polygon before filling
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsBrushPathOptions()
        {
            this.operations.Fill(brush, path, noneDefault);
            var processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsColorAndPath()
        {
            this.operations.Fill(color, path);
            var processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorPathAndOptions()
        {
            this.operations.Fill(color, path, noneDefault);
            var processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }
    }
}

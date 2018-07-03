// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
using SixLabors.Shapes;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Paths
{
    public class FillPolygon : BaseImageOperationsExtensionTest
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush<Rgba32> brush = Brushes.Solid(Rgba32.HotPink);
        SixLabors.Primitives.PointF[] path = {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                };


        [Fact]
        public void CorrectlySetsBrushAndPath()
        {
            this.operations.FillPolygon(this.brush, this.path);

            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            Assert.Equal(this.brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsBrushPathAndOptions()
        {
            this.operations.FillPolygon(this.noneDefault, this.brush, this.path);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(this.noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            Assert.Equal(this.brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsColorAndPath()
        {
            this.operations.FillPolygon(this.color, this.path);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();


            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(this.color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorPathAndOptions()
        {
            this.operations.FillPolygon(this.noneDefault, this.color, this.path);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(this.noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(this.color, brush.Color);
        }
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Brushes;
using SixLabors.ImageSharp.Drawing.Processors;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Paths
{
    public class FillRectangle : BaseImageOperationsExtensionTest
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush<Rgba32> brush = Brushes.Solid(Rgba32.HotPink);
        SixLabors.Primitives.Rectangle rectangle = new SixLabors.Primitives.Rectangle(10, 10, 77, 76);

        [Fact]
        public void CorrectlySetsBrushAndRectangle()
        {
            this.operations.Fill(brush, rectangle);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            SixLabors.Shapes.RectangularePolygon rect = Assert.IsType<SixLabors.Shapes.RectangularePolygon>(region.Shape);
            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsBrushRectangleAndOptions()
        {
            this.operations.Fill(brush, rectangle, noneDefault);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            SixLabors.Shapes.RectangularePolygon rect = Assert.IsType<SixLabors.Shapes.RectangularePolygon>(region.Shape);
            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsColorAndRectangle()
        {
            this.operations.Fill(color, rectangle);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();
           
            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            SixLabors.Shapes.RectangularePolygon rect = Assert.IsType<SixLabors.Shapes.RectangularePolygon>(region.Shape);
            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorRectangleAndOptions()
        {
            this.operations.Fill(color, rectangle, noneDefault);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            SixLabors.Shapes.RectangularePolygon rect = Assert.IsType<SixLabors.Shapes.RectangularePolygon>(region.Shape);
            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }
    }
}

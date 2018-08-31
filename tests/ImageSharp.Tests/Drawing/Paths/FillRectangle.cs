// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
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
            this.operations.Fill(this.brush, this.rectangle);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Shapes.RectangularPolygon rect = Assert.IsType<Shapes.RectangularPolygon>(region.Shape);
            Assert.Equal(rect.Location.X, this.rectangle.X);
            Assert.Equal(rect.Location.Y, this.rectangle.Y);
            Assert.Equal(rect.Size.Width, this.rectangle.Width);
            Assert.Equal(rect.Size.Height, this.rectangle.Height);

            Assert.Equal(this.brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsBrushRectangleAndOptions()
        {
            this.operations.Fill(this.noneDefault, this.brush, this.rectangle);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(this.noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Shapes.RectangularPolygon rect = Assert.IsType<Shapes.RectangularPolygon>(region.Shape);
            Assert.Equal(rect.Location.X, this.rectangle.X);
            Assert.Equal(rect.Location.Y, this.rectangle.Y);
            Assert.Equal(rect.Size.Width, this.rectangle.Width);
            Assert.Equal(rect.Size.Height, this.rectangle.Height);

            Assert.Equal(this.brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsColorAndRectangle()
        {
            this.operations.Fill(this.color, this.rectangle);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Shapes.RectangularPolygon rect = Assert.IsType<Shapes.RectangularPolygon>(region.Shape);
            Assert.Equal(rect.Location.X, this.rectangle.X);
            Assert.Equal(rect.Location.Y, this.rectangle.Y);
            Assert.Equal(rect.Size.Width, this.rectangle.Width);
            Assert.Equal(rect.Size.Height, this.rectangle.Height);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(this.color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorRectangleAndOptions()
        {
            this.operations.Fill(this.noneDefault, this.color, this.rectangle);
            FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>();

            Assert.Equal(this.noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Shapes.RectangularPolygon rect = Assert.IsType<Shapes.RectangularPolygon>(region.Shape);
            Assert.Equal(rect.Location.X, this.rectangle.X);
            Assert.Equal(rect.Location.Y, this.rectangle.Y);
            Assert.Equal(rect.Size.Width, this.rectangle.Width);
            Assert.Equal(rect.Size.Height, this.rectangle.Height);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(this.color, brush.Color);
        }
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
using SixLabors.ImageSharp.Tests.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Paths
{
    public class FillRectangle : BaseImageOperationsExtensionTest
    {
        private static readonly GraphicsOptionsComparer graphicsOptionsComparer = new GraphicsOptionsComparer();

        private GraphicsOptions nonDefault = new GraphicsOptions { Antialias = false };
        private Color color = Color.HotPink;
        private SolidBrush brush = Brushes.Solid(Rgba32.HotPink);
        private SixLabors.Primitives.Rectangle rectangle = new SixLabors.Primitives.Rectangle(10, 10, 77, 76);

        [Fact]
        public void CorrectlySetsBrushAndRectangle()
        {
            this.operations.Fill(this.brush, this.rectangle);
            FillRegionProcessor processor = this.Verify<FillRegionProcessor>();

            Assert.Equal(new GraphicsOptions(), processor.Options, graphicsOptionsComparer);

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
            this.operations.Fill(this.nonDefault, this.brush, this.rectangle);
            FillRegionProcessor processor = this.Verify<FillRegionProcessor>();

            Assert.Equal(this.nonDefault, processor.Options, graphicsOptionsComparer);

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
            FillRegionProcessor processor = this.Verify<FillRegionProcessor>();

            Assert.Equal(new GraphicsOptions(), processor.Options, graphicsOptionsComparer);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Shapes.RectangularPolygon rect = Assert.IsType<Shapes.RectangularPolygon>(region.Shape);
            Assert.Equal(rect.Location.X, this.rectangle.X);
            Assert.Equal(rect.Location.Y, this.rectangle.Y);
            Assert.Equal(rect.Size.Width, this.rectangle.Width);
            Assert.Equal(rect.Size.Height, this.rectangle.Height);

            SolidBrush brush = Assert.IsType<SolidBrush>(processor.Brush);
            Assert.Equal(this.color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorRectangleAndOptions()
        {
            this.operations.Fill(this.nonDefault, this.color, this.rectangle);
            FillRegionProcessor processor = this.Verify<FillRegionProcessor>();

            Assert.Equal(this.nonDefault, processor.Options, graphicsOptionsComparer);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Shapes.RectangularPolygon rect = Assert.IsType<Shapes.RectangularPolygon>(region.Shape);
            Assert.Equal(rect.Location.X, this.rectangle.X);
            Assert.Equal(rect.Location.Y, this.rectangle.Y);
            Assert.Equal(rect.Size.Width, this.rectangle.Width);
            Assert.Equal(rect.Size.Height, this.rectangle.Height);

            SolidBrush brush = Assert.IsType<SolidBrush>(processor.Brush);
            Assert.Equal(this.color, brush.Color);
        }
    }
}

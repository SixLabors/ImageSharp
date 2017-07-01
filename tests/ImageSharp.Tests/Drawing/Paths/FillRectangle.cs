
namespace ImageSharp.Tests.Drawing.Paths
{
    using System;

    using ImageSharp.Drawing.Brushes;

    using Xunit;
    using ImageSharp.Drawing;
    using ImageSharp.Drawing.Processors;
    using ImageSharp.PixelFormats;

    public class FillRectangle : IDisposable
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush<Rgba32> brush = Brushes.Solid(Rgba32.HotPink);
        SixLabors.Primitives.Rectangle rectangle = new SixLabors.Primitives.Rectangle(10, 10, 77, 76);

        private ProcessorWatchingImage img;

        public FillRectangle()
        {
            this.img = new Paths.ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void CorrectlySetsBrushAndRectangle()
        {
            img.Mutate(x => x.Fill(brush, rectangle));

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Rgba32> processor = Assert.IsType<FillRegionProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

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
            img.Mutate(x => x.Fill(brush, rectangle, noneDefault));

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Rgba32> processor = Assert.IsType<FillRegionProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

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
            img.Mutate(x => x.Fill(color, rectangle));

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Rgba32> processor = Assert.IsType<FillRegionProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

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
            img.Mutate(x => x.Fill(color, rectangle, noneDefault));

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Rgba32> processor = Assert.IsType<FillRegionProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

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

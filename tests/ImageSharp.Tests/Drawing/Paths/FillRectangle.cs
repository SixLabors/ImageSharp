
namespace ImageSharp.Tests.Drawing.Paths
{
    using System;
    using System.IO;
    using ImageSharp;
    using ImageSharp.Drawing.Brushes;
    using Processing;
    using System.Collections.Generic;
    using Xunit;
    using ImageSharp.Drawing;
    using System.Numerics;
    using SixLabors.Shapes;
    using ImageSharp.Drawing.Processors;
    using ImageSharp.Drawing.Pens;

    public class FillRectangle : IDisposable
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Color32 color = Color32.HotPink;
        SolidBrush brush = Brushes.Solid(Color32.HotPink);
        ImageSharp.Rectangle rectangle = new ImageSharp.Rectangle(10, 10, 77, 76);

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
            img.Fill(brush, rectangle);

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color32> processor = Assert.IsType<FillRegionProcessor<Color32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(region.Shape);
            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);
            
            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsBrushRectangleAndOptions()
        {
            img.Fill(brush, rectangle, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color32> processor = Assert.IsType<FillRegionProcessor<Color32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(region.Shape);
            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsColorAndRectangle()
        {
            img.Fill(color, rectangle);
            
            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color32> processor = Assert.IsType<FillRegionProcessor<Color32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(region.Shape);
            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            SolidBrush<Color32> brush = Assert.IsType<SolidBrush<Color32>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorRectangleAndOptions()
        {
            img.Fill(color, rectangle, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color32> processor = Assert.IsType<FillRegionProcessor<Color32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(region.Shape);
            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            SolidBrush<Color32> brush = Assert.IsType<SolidBrush<Color32>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }
    }
}

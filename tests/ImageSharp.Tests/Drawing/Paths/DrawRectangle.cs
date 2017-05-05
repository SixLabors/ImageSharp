
namespace ImageSharp.Tests.Drawing.Paths
{
    using System;
    using ImageSharp;
    using ImageSharp.Drawing.Brushes;
    using Xunit;
    using ImageSharp.Drawing;
    using ImageSharp.Drawing.Processors;
    using ImageSharp.Drawing.Pens;
    using ImageSharp.PixelFormats;

    public class DrawRectangle : IDisposable
    {
        float thickness = 7.2f;
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush<Rgba32> brush = Brushes<Rgba32>.Solid(Rgba32.HotPink);
        Pen<Rgba32> pen = new Pen<Rgba32>(Rgba32.Gray, 99.9f);
        ImageSharp.Rectangle rectangle = new ImageSharp.Rectangle(10, 10, 98, 324);

        private ProcessorWatchingImage img;

        public DrawRectangle()
        {
            this.img = new Paths.ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void CorrectlySetsBrushThicknessAndRectangle()
        {
            img.Draw(brush, thickness, rectangle);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Path);

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void CorrectlySetsBrushThicknessRectangleAndOptions()
        {
            img.Draw(brush, thickness, rectangle, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);

            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Path);

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void CorrectlySetsColorThicknessAndRectangle()
        {
            img.Draw(color, thickness, rectangle);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);

            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Path);

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorThicknessRectangleAndOptions()
        {
            img.Draw(color, thickness, rectangle, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);

            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Path);

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsPenAndRectangle()
        {
            img.Draw(pen, rectangle);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);

            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Path);

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Assert.Equal(pen, processor.Pen);
        }

        [Fact]
        public void CorrectlySetsPenRectangleAndOptions()
        {
            img.Draw(pen, rectangle, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);

            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Path);

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Assert.Equal(pen, processor.Pen);
        }
    }
}

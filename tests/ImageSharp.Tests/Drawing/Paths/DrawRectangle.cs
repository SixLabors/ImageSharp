
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

    public class DrawRectangle : IDisposable
    {
        float thickness = 7.2f;
        GraphicsOptions noneDefault = new GraphicsOptions();
        Color color = Color.HotPink;
        SolidBrush brush = Brushes.Solid(Color.HotPink);
        Pen pen = new Pen(Color.Gray, 99.9f);
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
            DrawPathProcessor<Color> processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Paths[0].AsShape());

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Pen<Color> pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void CorrectlySetsBrushThicknessRectangleAndOptions()
        {
            img.Draw(brush, thickness, rectangle, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Color> processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);

            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Paths[0].AsShape());

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Pen<Color> pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void CorrectlySetsColorThicknessAndRectangle()
        {
            img.Draw(color, thickness, rectangle);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Color> processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);

            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Paths[0].AsShape());

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Pen<Color> pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            SolidBrush<Color> brush = Assert.IsType<SolidBrush<Color>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorThicknessRectangleAndOptions()
        {
            img.Draw(color, thickness, rectangle, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Color> processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);

            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Paths[0].AsShape());

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Pen<Color> pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            SolidBrush<Color> brush = Assert.IsType<SolidBrush<Color>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsPenAndRectangle()
        {
            img.Draw(pen, rectangle);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Color> processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);

            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Paths[0].AsShape());

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
            DrawPathProcessor<Color> processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);

            SixLabors.Shapes.Rectangle rect = Assert.IsType<SixLabors.Shapes.Rectangle>(shapepath.Paths[0].AsShape());

            Assert.Equal(rect.Location.X, rectangle.X);
            Assert.Equal(rect.Location.Y, rectangle.Y);
            Assert.Equal(rect.Size.Width, rectangle.Width);
            Assert.Equal(rect.Size.Height, rectangle.Height);

            Assert.Equal(pen, processor.Pen);
        }
    }
}

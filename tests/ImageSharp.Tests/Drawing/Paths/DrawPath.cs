
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

    public class DrawPath : IDisposable
    {
        float thickness = 7.2f;
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush brush = Brushes.Solid(Rgba32.HotPink);
        Pen pen = new Pen(Rgba32.Gray, 99.9f);
        IPath path = new SixLabors.Shapes.Path(new LinearLineSegment(new Vector2[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                }));
        private ProcessorWatchingImage img;

        public DrawPath()
        {
            this.img = new Paths.ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void CorrectlySetsBrushThicknessAndPath()
        {
            img.Draw(brush, thickness, path);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.Equal(path, shapepath.Path);
            
            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void CorrectlySetsBrushThicknessPathAndOptions()
        {
            img.Draw(brush, thickness, path, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.Equal(path, shapepath.Path);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void CorrectlySetsColorThicknessAndPath()
        {
            img.Draw(color, thickness, path);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.Equal(path, shapepath.Path);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorThicknessPathAndOptions()
        {
            img.Draw(color, thickness, path, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.Equal(path, shapepath.Path);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsPenAndPath()
        {
            img.Draw(pen, path);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.Equal(path, shapepath.Path);

            Assert.Equal(pen, processor.Pen);
        }

        [Fact]
        public void CorrectlySetsPenPathAndOptions()
        {
            img.Draw(pen, path, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.Equal(path, shapepath.Path);

            Assert.Equal(pen, processor.Pen);
        }
    }
}

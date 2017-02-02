
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
        Color color = Color.HotPink;
        SolidBrush brush = Brushes.Solid(Color.HotPink);
        Pen pen = new Pen(Color.Gray, 99.9f);
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
        public void Brush_Thickness_path()
        {
            img.Draw(brush, thickness, path);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(path, shapepath.Paths[0]);
            
            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void Brush_Thickness_path_options()
        {
            img.Draw(brush, thickness, path, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(path, shapepath.Paths[0]);

            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void color_Thickness_path()
        {
            img.Draw(color, thickness, path);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(path, shapepath.Paths[0]);

            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            var brush = Assert.IsType<SolidBrush<Color>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void color_Thickness_path_options()
        {
            img.Draw(color, thickness, path, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(path, shapepath.Paths[0]);

            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            var brush = Assert.IsType<SolidBrush<Color>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void pen_path()
        {
            img.Draw(pen, path);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(path, shapepath.Paths[0]);

            Assert.Equal(pen, processor.Pen);
        }

        [Fact]
        public void pen_path_options()
        {
            img.Draw(pen, path, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(path, shapepath.Paths[0]);

            Assert.Equal(pen, processor.Pen);
        }
    }
}


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

    public class DrawShape: IDisposable
    {
        float thickness = 7.2f;
        GraphicsOptions noneDefault = new GraphicsOptions();
        Color color = Color.HotPink;
        SolidBrush brush = Brushes.Solid(Color.HotPink);
        Pen pen = new Pen(Color.Gray, 99.9f);
        IShape shape = new SixLabors.Shapes.Polygon(new LinearLineSegment(new Vector2[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                }));
        private ProcessorWatchingImage img;

        public DrawShape()
        {
            this.img = new Paths.ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void Brush_Thickness_shape()
        {
            img.Draw(brush, thickness, shape);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(shape, shapepath.Paths[0].AsShape());
            
            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void Brush_Thickness_shape_options()
        {
            img.Draw(brush, thickness, shape, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(shape, shapepath.Paths[0].AsShape());

            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void color_Thickness_shape()
        {
            img.Draw(color, thickness, shape);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(shape, shapepath.Paths[0].AsShape());

            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            var brush = Assert.IsType<SolidBrush<Color>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void color_Thickness_shape_options()
        {
            img.Draw(color, thickness, shape, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(shape, shapepath.Paths[0].AsShape());

            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            var brush = Assert.IsType<SolidBrush<Color>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void pen_shape()
        {
            img.Draw(pen, shape);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(shape, shapepath.Paths[0].AsShape());

            Assert.Equal(pen, processor.Pen);
        }

        [Fact]
        public void pen_path_options()
        {
            img.Draw(pen, shape, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var shapepath = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(shapepath.Paths);
            Assert.Equal(shape, shapepath.Paths[0].AsShape());

            Assert.Equal(pen, processor.Pen);
        }
    }
}

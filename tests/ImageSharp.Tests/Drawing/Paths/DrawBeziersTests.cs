
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

    public class DrawBeziersTests : IDisposable
    {
        float thickness = 7.2f;
        GraphicsOptions noneDefault = new GraphicsOptions();
        Color color = Color.HotPink;
        SolidBrush brush = Brushes.Solid(Color.HotPink);
        Pen pen = new Pen(Color.Firebrick, 99.9f);
        Vector2[] points = new Vector2[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                };
        private ProcessorWatchingImage img;

        public DrawBeziersTests()
        {
            this.img = new Paths.ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void Brush_Thickness_points()
        {
            img.DrawBeziers(brush, thickness, points);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var path = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(path.Paths);

            var vector = Assert.IsType<SixLabors.Shapes.Path>(path.Paths[0]);
            var segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void Brush_Thickness_points_options()
        {
            img.DrawBeziers(brush, thickness, points, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var path = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(path.Paths);

            var vector = Assert.IsType<SixLabors.Shapes.Path>(path.Paths[0]);
            var segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void color_Thickness_points()
        {
            img.DrawBeziers(color, thickness, points);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var path = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(path.Paths);

            var vector = Assert.IsType<SixLabors.Shapes.Path>(path.Paths[0]);
            var segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            var brush = Assert.IsType<SolidBrush<Color>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void color_Thickness_points_options()
        {
            img.DrawBeziers(color, thickness, points, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var path = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(path.Paths);

            var vector = Assert.IsType<SixLabors.Shapes.Path>(path.Paths[0]);
            var segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            var pen = Assert.IsType<Pen<Color>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            var brush = Assert.IsType<SolidBrush<Color>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void pen_points()
        {
            img.DrawBeziers(pen, points);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var path = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(path.Paths);

            var vector = Assert.IsType<SixLabors.Shapes.Path>(path.Paths[0]);
            var segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            Assert.Equal(pen, processor.Pen);
        }

        [Fact]
        public void pen_points_options()
        {
            img.DrawBeziers(pen, points, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var path = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotEmpty(path.Paths);

            var vector = Assert.IsType<SixLabors.Shapes.Path>(path.Paths[0]);
            var segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            Assert.Equal(pen, processor.Pen);
        }
    }
}

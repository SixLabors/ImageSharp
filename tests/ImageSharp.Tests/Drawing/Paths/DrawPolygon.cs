
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

    public class DrawPolygon : IDisposable
    {
        float thickness = 7.2f;
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush brush = Brushes.Solid(Rgba32.HotPink);
        Pen pen = new Pen(Rgba32.Gray, 99.9f);
        Vector2[] points = new Vector2[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                };
        private ProcessorWatchingImage img;

        public DrawPolygon()
        {
            this.img = new Paths.ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void CorrectlySetsBrushThicknessAndPoints()
        {
            img.DrawPolygon(brush, thickness, points);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            Polygon vector = Assert.IsType<SixLabors.Shapes.Polygon>(path.Path);
            LinearLineSegment segment = Assert.IsType<LinearLineSegment>(vector.LineSegments[0]);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void CorrectlySetsBrushThicknessPointsAndOptions()
        {
            img.DrawPolygon(brush, thickness, points, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            Polygon vector = Assert.IsType<SixLabors.Shapes.Polygon>(path.Path);
            LinearLineSegment segment = Assert.IsType<LinearLineSegment>(vector.LineSegments[0]);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void CorrectlySetsColorThicknessAndPoints()
        {
            img.DrawPolygon(color, thickness, points);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            Polygon vector = Assert.IsType<SixLabors.Shapes.Polygon>(path.Path);
            LinearLineSegment segment = Assert.IsType<LinearLineSegment>(vector.LineSegments[0]);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorThicknessPointsAndOptions()
        {
            img.DrawPolygon(color, thickness, points, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            Polygon vector = Assert.IsType<SixLabors.Shapes.Polygon>(path.Path);
            LinearLineSegment segment = Assert.IsType<LinearLineSegment>(vector.LineSegments[0]);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsPenAndPoints()
        {
            img.DrawPolygon(pen, points);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            Polygon vector = Assert.IsType<SixLabors.Shapes.Polygon>(path.Path);
            LinearLineSegment segment = Assert.IsType<LinearLineSegment>(vector.LineSegments[0]);

            Assert.Equal(pen, processor.Pen);
        }

        [Fact]
        public void CorrectlySetsPenPointsAndOptions()
        {
            img.DrawPolygon(pen, points, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            Polygon vector = Assert.IsType<SixLabors.Shapes.Polygon>(path.Path);
            LinearLineSegment segment = Assert.IsType<LinearLineSegment>(vector.LineSegments[0]);

            Assert.Equal(pen, processor.Pen);
        }
    }
}

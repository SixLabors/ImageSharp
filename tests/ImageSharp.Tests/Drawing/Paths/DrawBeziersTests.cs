
namespace ImageSharp.Tests.Drawing.Paths
{
    using System;

    using ImageSharp.Drawing.Brushes;

    using Xunit;
    using ImageSharp.Drawing;
    using System.Numerics;
    using SixLabors.Shapes;
    using ImageSharp.Drawing.Processors;
    using ImageSharp.Drawing.Pens;
    using ImageSharp.PixelFormats;

    public class DrawBeziersTests : IDisposable
    {
        float thickness = 7.2f;
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush<Rgba32> brush = Brushes<Rgba32>.Solid(Rgba32.HotPink);
        Pen<Rgba32> pen = new Pen<Rgba32>(Rgba32.Firebrick, 99.9f);
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
        public void CorrectlySetsBrushThicknessAndPoints()
        {
            img.DrawBeziers(brush, thickness, points);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);
            Assert.NotNull(path.Path);

            SixLabors.Shapes.Path vector = Assert.IsType<SixLabors.Shapes.Path>(path.Path);

            BezierLineSegment segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void CorrectlySetsBrushThicknessPointsAndOptions()
        {
            img.DrawBeziers(brush, thickness, points, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            SixLabors.Shapes.Path vector = Assert.IsType<SixLabors.Shapes.Path>(path.Path);
            BezierLineSegment segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(brush, pen.Brush);
            Assert.Equal(thickness, pen.Width);
        }

        [Fact]
        public void CorrectlySetsColorThicknessAndPoints()
        {
            img.DrawBeziers(color, thickness, points);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            SixLabors.Shapes.Path vector = Assert.IsType<SixLabors.Shapes.Path>(path.Path);
            BezierLineSegment segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorThicknessPointsAndOptions()
        {
            img.DrawBeziers(color, thickness, points, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            SixLabors.Shapes.Path vector = Assert.IsType<SixLabors.Shapes.Path>(path.Path);
            BezierLineSegment segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
            Assert.Equal(thickness, pen.Width);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(pen.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsPenAndPoints()
        {
            img.DrawBeziers(pen, points);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            SixLabors.Shapes.Path vector = Assert.IsType<SixLabors.Shapes.Path>(path.Path);
            BezierLineSegment segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            Assert.Equal(pen, processor.Pen);
        }

        [Fact]
        public void CorrectlySetsPenPointsAndOptions()
        {
            img.DrawBeziers(pen, points, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapePath path = Assert.IsType<ShapePath>(processor.Path);

            SixLabors.Shapes.Path vector = Assert.IsType<SixLabors.Shapes.Path>(path.Path);
            BezierLineSegment segment = Assert.IsType<BezierLineSegment>(vector.LineSegments[0]);

            Assert.Equal(pen, processor.Pen);
        }
    }
}

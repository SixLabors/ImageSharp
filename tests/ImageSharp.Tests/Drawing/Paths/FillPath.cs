
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

    public class FillPath : IDisposable
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Color color = Color.HotPink;
        SolidBrush brush = Brushes.Solid(Color.HotPink);
        IPath path = new SixLabors.Shapes.Path(new LinearLineSegment(new Vector2[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                }));
        private ProcessorWatchingImage img;

        public FillPath()
        {
            this.img = new ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void CorrectlySetsBrushAndPath()
        {
            img.Fill(brush, path);

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color> processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            
            // path is converted to a polygon before filling
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsBrushPathOptions()
        {
            img.Fill(brush, path, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color> processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsColorAndPath()
        {
            img.Fill(color, path);

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color> processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            SolidBrush<Color> brush = Assert.IsType<SolidBrush<Color>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorPathAndOptions()
        {
            img.Fill(color, path, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color> processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Polygon polygon = Assert.IsType<Polygon>(region.Shape);
            LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            SolidBrush<Color> brush = Assert.IsType<SolidBrush<Color>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }
    }
}

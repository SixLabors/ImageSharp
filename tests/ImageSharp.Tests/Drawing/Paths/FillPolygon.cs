
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

    public class FillPolygon : IDisposable
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Color color = Color.HotPink;
        SolidBrush brush = Brushes.Solid(Color.HotPink);
        Vector2[] path = new Vector2[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                };
        private ProcessorWatchingImage img;

        public FillPolygon()
        {
            this.img = new Paths.ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void Brush_path()
        {
            img.FillPolygon(brush, path);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var region = Assert.IsType<ShapeRegion>(processor.Region);
            var polygon = Assert.IsType<Polygon>(region.Shape);
            var segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);
            
            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void Brush_path_options()
        {
            img.FillPolygon(brush, path, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var region = Assert.IsType<ShapeRegion>(processor.Region);
            var polygon = Assert.IsType<Polygon>(region.Shape);
            var segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void color_path()
        {
            img.FillPolygon(color, path);
            
            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            var region = Assert.IsType<ShapeRegion>(processor.Region);
            var polygon = Assert.IsType<Polygon>(region.Shape);
            var segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            var brush = Assert.IsType<SolidBrush<Color>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void color_path_options()
        {
            img.FillPolygon(color, path, noneDefault);


            Assert.NotEmpty(img.ProcessorApplications);
            var processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            var region = Assert.IsType<ShapeRegion>(processor.Region);
            var polygon = Assert.IsType<Polygon>(region.Shape);
            var segemnt = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

            var brush = Assert.IsType<SolidBrush<Color>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }
    }
}

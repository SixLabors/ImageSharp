
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

    public class FillShape : IDisposable
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Color color = Color.HotPink;
        SolidBrush brush = Brushes.Solid(Color.HotPink);
        IShape shape = new Polygon(new LinearLineSegment(new Vector2[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                }));
        private ProcessorWatchingImage img;

        public FillShape()
        {
            this.img = new Paths.ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void CorrectlySetsBrushAndShape()
        {
            img.Fill(brush, shape);

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color> processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Assert.Equal(shape, region.Shape);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsBrushShapeAndOptions()
        {
            img.Fill(brush, shape, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color> processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Assert.Equal(shape, region.Shape);

            Assert.Equal(brush, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsColorAndShape()
        {
            img.Fill(color, shape);
            
            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color> processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(GraphicsOptions.Default, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Assert.Equal(shape, region.Shape);

            SolidBrush<Color> brush = Assert.IsType<SolidBrush<Color>>(processor.Brush);
            Assert.Equal(color, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorShapeAndOptions()
        {
            img.Fill(color, shape, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);
            FillRegionProcessor<Color> processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            Assert.Equal(noneDefault, processor.Options);

            ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
            Assert.Equal(shape, region.Shape);

            SolidBrush<Color> brush = Assert.IsType<SolidBrush<Color>>(processor.Brush);
            Assert.Equal(color, brush.Color);

        }
    }
}

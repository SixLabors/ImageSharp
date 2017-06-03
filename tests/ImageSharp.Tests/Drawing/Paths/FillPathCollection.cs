
namespace ImageSharp.Tests.Drawing.Paths
{
    using System;
    using ImageSharp;
    using ImageSharp.Drawing.Brushes;
    using Xunit;
    using ImageSharp.Drawing;
    using System.Numerics;
    using SixLabors.Shapes;
    using ImageSharp.Drawing.Processors;
    using ImageSharp.PixelFormats;

    public class FillPathCollection : IDisposable
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush<Rgba32> brush = Brushes.Solid(Rgba32.HotPink);
        IPath path1 = new SixLabors.Shapes.Path(new LinearLineSegment(new Vector2[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                }));
        IPath path2 = new SixLabors.Shapes.Path(new LinearLineSegment(new Vector2[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                }));

        IPathCollection pathCollection;

        private ProcessorWatchingImage img;

        public FillPathCollection()
        {
            this.pathCollection = new PathCollection(path1, path2);
            this.img = new ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void CorrectlySetsBrushAndPath()
        {
            img.Fill(brush, pathCollection);

            Assert.Equal(2, img.ProcessorApplications.Count);
            for (var i = 0; i < 2; i++)
            {
                FillRegionProcessor<Rgba32> processor = Assert.IsType<FillRegionProcessor<Rgba32>>(img.ProcessorApplications[i].processor);

                Assert.Equal(GraphicsOptions.Default, processor.Options);

                ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);

                // path is converted to a polygon before filling
                Polygon polygon = Assert.IsType<Polygon>(region.Shape);
                LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

                Assert.Equal(brush, processor.Brush);
            }
        }

        [Fact]
        public void CorrectlySetsBrushPathOptions()
        {
            img.Fill(brush, pathCollection, noneDefault);

            Assert.Equal(2, img.ProcessorApplications.Count);
            for (var i = 0; i < 2; i++)
            {
                FillRegionProcessor<Rgba32> processor = Assert.IsType<FillRegionProcessor<Rgba32>>(img.ProcessorApplications[i].processor);

                Assert.Equal(noneDefault, processor.Options);

                ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
                Polygon polygon = Assert.IsType<Polygon>(region.Shape);
                LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

                Assert.Equal(brush, processor.Brush);
            }
        }

        [Fact]
        public void CorrectlySetsColorAndPath()
        {
            img.Fill(color, pathCollection);

            Assert.Equal(2, img.ProcessorApplications.Count);
            for (var i = 0; i < 2; i++)
            {
                FillRegionProcessor<Rgba32> processor = Assert.IsType<FillRegionProcessor<Rgba32>>(img.ProcessorApplications[i].processor);

                Assert.Equal(GraphicsOptions.Default, processor.Options);

                ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
                Polygon polygon = Assert.IsType<Polygon>(region.Shape);
                LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

                SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
                Assert.Equal(color, brush.Color);
            }
        }

        [Fact]
        public void CorrectlySetsColorPathAndOptions()
        {
            img.Fill(color, pathCollection, noneDefault);

            Assert.Equal(2, img.ProcessorApplications.Count);
            for (var i = 0; i < 2; i++)
            {
                FillRegionProcessor<Rgba32> processor = Assert.IsType<FillRegionProcessor<Rgba32>>(img.ProcessorApplications[i].processor);

                Assert.Equal(noneDefault, processor.Options);

                ShapeRegion region = Assert.IsType<ShapeRegion>(processor.Region);
                Polygon polygon = Assert.IsType<Polygon>(region.Shape);
                LinearLineSegment segments = Assert.IsType<LinearLineSegment>(polygon.LineSegments[0]);

                SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
                Assert.Equal(color, brush.Color);
            }
        }
    }
}

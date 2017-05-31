
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

    public class DrawPathCollection : IDisposable
    {
        float thickness = 7.2f;
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        SolidBrush<Rgba32> brush = Brushes.Solid(Rgba32.HotPink);
        Pen<Rgba32> pen = new Pen<Rgba32>(Rgba32.Gray, 99.9f);
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

        public DrawPathCollection()
        {
            this.pathCollection = new PathCollection(this.path1, this.path2);
            this.img = new Paths.ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void CorrectlySetsBrushThicknessAndPath()
        {
            img.Draw(brush, thickness, pathCollection);

            Assert.NotEmpty(img.ProcessorApplications);

            for (var i = 0; i < 2; i++)
            {
                DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[i].processor);

                Assert.Equal(GraphicsOptions.Default, processor.Options);

                ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
                Assert.Contains(shapepath.Path, this.pathCollection);

                Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
                Assert.Equal(brush, pen.Brush);
                Assert.Equal(thickness, pen.Width);
            }
        }

        [Fact]
        public void CorrectlySetsBrushThicknessPathAndOptions()
        {
            img.Draw(brush, thickness, pathCollection, noneDefault);

            Assert.NotEmpty(img.ProcessorApplications);

            for (var i = 0; i < 2; i++)
            {
                DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[i].processor);

                Assert.Equal(noneDefault, processor.Options);

                ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
                Assert.Contains(shapepath.Path, pathCollection);

                Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
                Assert.Equal(brush, pen.Brush);
                Assert.Equal(thickness, pen.Width);
            }
        }

        [Fact]
        public void CorrectlySetsColorThicknessAndPath()
        {
            img.Draw(color, thickness, pathCollection);

            Assert.NotEmpty(img.ProcessorApplications);
            for (var i = 0; i < 2; i++)
            {
                DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[i].processor);

                Assert.Equal(GraphicsOptions.Default, processor.Options);

                ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
                Assert.Contains(shapepath.Path, pathCollection);

                Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
                Assert.Equal(thickness, pen.Width);

                SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(pen.Brush);
                Assert.Equal(color, brush.Color);
            }
        }

        [Fact]
        public void CorrectlySetsColorThicknessPathAndOptions()
        {
            img.Draw(color, thickness, pathCollection, noneDefault);

            Assert.Equal(2, img.ProcessorApplications.Count);
            for (var i = 0; i < 2; i++)
            {
                DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[i].processor);

                Assert.Equal(noneDefault, processor.Options);

                ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
                Assert.Contains(shapepath.Path, pathCollection);

                Pen<Rgba32> pen = Assert.IsType<Pen<Rgba32>>(processor.Pen);
                Assert.Equal(thickness, pen.Width);

                SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(pen.Brush);
                Assert.Equal(color, brush.Color);
            }
        }

        [Fact]
        public void CorrectlySetsPenAndPath()
        {
            img.Draw(pen, pathCollection);

            Assert.Equal(2, img.ProcessorApplications.Count);
            for (var i = 0; i < 2; i++)
            {
                DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[i].processor);

                Assert.Equal(GraphicsOptions.Default, processor.Options);

                ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
                Assert.Contains(shapepath.Path, pathCollection);

                Assert.Equal(pen, processor.Pen);
            }
        }

        [Fact]
        public void CorrectlySetsPenPathAndOptions()
        {
            img.Draw(pen, pathCollection, noneDefault);

            Assert.Equal(2, img.ProcessorApplications.Count);
            for (var i = 0; i < 2; i++)
            {
                DrawPathProcessor<Rgba32> processor = Assert.IsType<DrawPathProcessor<Rgba32>>(img.ProcessorApplications[i].processor);

                Assert.Equal(noneDefault, processor.Options);

                ShapePath shapepath = Assert.IsType<ShapePath>(processor.Path);
                Assert.Contains(shapepath.Path, pathCollection);

                Assert.Equal(pen, processor.Pen);
            }
        }
    }
}

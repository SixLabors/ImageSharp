// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
using SixLabors.Shapes;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Paths
{
    public class DrawPathCollection : BaseImageOperationsExtensionTest
    {
        GraphicsOptions noneDefault = new GraphicsOptions();
        Rgba32 color = Rgba32.HotPink;
        Pen<Rgba32> pen = Pens.Solid(Rgba32.HotPink, 1);
        IPath path1 = new Path(new LinearLineSegment(new SixLabors.Primitives.PointF[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                }));
        IPath path2 = new Path(new LinearLineSegment(new SixLabors.Primitives.PointF[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                }));

        IPathCollection pathCollection;

        public DrawPathCollection()
        {
            this.pathCollection = new PathCollection(this.path1, this.path2);
        }

        [Fact]
        public void CorrectlySetsBrushAndPath()
        {
            this.operations.Draw(this.pen, this.pathCollection);

            for (int i = 0; i < 2; i++)
            {
                FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>(i);

                Assert.Equal(GraphicsOptions.Default, processor.Options);

                ShapePath region = Assert.IsType<ShapePath>(processor.Region);

                // path is converted to a polygon before filling
                ComplexPolygon polygon = Assert.IsType<ComplexPolygon>(region.Shape);

                Assert.Equal(this.pen.StrokeFill, processor.Brush);
            }
        }

        [Fact]
        public void CorrectlySetsBrushPathOptions()
        {
            this.operations.Draw(this.noneDefault, this.pen, this.pathCollection);

            for (int i = 0; i < 2; i++)
            {
                FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>(i);

                Assert.Equal(this.noneDefault, processor.Options);

                ShapePath region = Assert.IsType<ShapePath>(processor.Region);
                ComplexPolygon polygon = Assert.IsType<ComplexPolygon>(region.Shape);

                Assert.Equal(this.pen.StrokeFill, processor.Brush);
            }
        }

        [Fact]
        public void CorrectlySetsColorAndPath()
        {
            this.operations.Draw(this.color, 1, this.pathCollection);

            for (int i = 0; i < 2; i++)
            {
                FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>(i);

                Assert.Equal(GraphicsOptions.Default, processor.Options);

                ShapePath region = Assert.IsType<ShapePath>(processor.Region);
                ComplexPolygon polygon = Assert.IsType<ComplexPolygon>(region.Shape);

                SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
                Assert.Equal(this.color, brush.Color);
            }
        }

        [Fact]
        public void CorrectlySetsColorPathAndOptions()
        {
            this.operations.Draw(this.noneDefault, this.color, 1, this.pathCollection);

            for (int i = 0; i < 2; i++)
            {
                FillRegionProcessor<Rgba32> processor = this.Verify<FillRegionProcessor<Rgba32>>(i);

                Assert.Equal(this.noneDefault, processor.Options);

                ShapePath region = Assert.IsType<ShapePath>(processor.Region);
                ComplexPolygon polygon = Assert.IsType<ComplexPolygon>(region.Shape);

                SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
                Assert.Equal(this.color, brush.Color);
            }
        }
    }
}

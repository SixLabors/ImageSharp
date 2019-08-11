// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using Moq;
using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;
using Xunit;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Tests.Drawing
{


    public class FillRegionProcessorTests
    {

        [Theory]
        [InlineData(true, 1, 4)]
        [InlineData(true, 2, 4)]
        [InlineData(true, 5, 5)]
        [InlineData(true, 8, 8)]
        [InlineData(false, 8, 4)]
        [InlineData(false, 16, 4)] // we always do 4 sub=pixels when antialiasing is off.
        public void MinimumAntialiasSubpixelDepth(bool antialias, int antialiasSubpixelDepth, int expectedAntialiasSubpixelDepth)
        {
            var bounds = new Rectangle(0, 0, 1, 1);

            var brush = new Mock<IBrush>();
            var region = new MockRegion2(bounds);

            var options = new GraphicsOptions(antialias)
            {
                AntialiasSubpixelDepth = 1
            };
            var processor = new FillRegionProcessor(brush.Object, region, options);
            var img = new Image<Rgba32>(1, 1);
            processor.Apply(img, bounds);

            Assert.Equal(4, region.ScanInvocationCounter);
        }

        [Fact]
        public void FillOffCanvas()
        {
            var bounds = new Rectangle(-100, -10, 10, 10);
            var brush = new Mock<IBrush>();
            var options = new GraphicsOptions(true);
            var processor = new FillRegionProcessor(brush.Object, new MockRegion1(), options);
            var img = new Image<Rgba32>(10, 10);
            processor.Apply(img, bounds);
        }

        [Fact]
        public void DrawOffCanvas()
        {

            using (var img = new Image<Rgba32>(10, 10))
            {
                img.Mutate(x => x.DrawLines(new Pen(Rgba32.Black, 10),
                    new Vector2(-10, 5),
                    new Vector2(20, 5)));
            }
        }

        [Fact]
        public void DoesNotThrowForIssue928()
        {
            var rectText = new RectangleF(0, 0, 2000, 2000);
            using (Image<Rgba32> img = new Image<Rgba32>((int)rectText.Width, (int)rectText.Height))
            {
                img.Mutate(x => x.Fill(Rgba32.Transparent));

                img.Mutate(ctx => {
                    ctx.DrawLines(
                        Rgba32.Red,
                        0.984252f,
                        new PointF(104.762581f, 1074.99365f),
                        new PointF(104.758667f, 1075.01721f),
                        new PointF(104.757675f, 1075.04114f),
                        new PointF(104.759628f, 1075.065f),
                        new PointF(104.764488f, 1075.08838f),
                        new PointF(104.772186f, 1075.111f),
                        new PointF(104.782608f, 1075.13245f),
                        new PointF(104.782608f, 1075.13245f)
                        );
                    }
                );
            }
        }

        [Fact]
        public void DoesNotThrowFillingTriangle()
        {
            using(var image = new Image<Rgba32>(28, 28))
            {
                var path = new Polygon(
                    new LinearLineSegment(new PointF(17.11f, 13.99659f), new PointF(14.01433f, 27.06201f)),
                    new LinearLineSegment(new PointF(14.01433f, 27.06201f), new PointF(13.79267f, 14.00023f)),
                    new LinearLineSegment(new PointF(13.79267f, 14.00023f), new PointF(17.11f, 13.99659f))
                );

                image.Mutate(ctx =>
                {
                    ctx.Fill(Rgba32.White, path);
                });
            }
        }

        // Mocking the region throws an error in netcore2.0
        private class MockRegion1 : Region
        {
            public override Rectangle Bounds => new Rectangle(-100, -10, 10, 10);

            public override int Scan(float y, Span<float> buffer, Configuration configuration)
            {
                if (y < 5)
                {
                    buffer[0] = -10f;
                    buffer[1] = 100f;
                    return 2;
                }
                return 0;
            }

            public override int MaxIntersections => 10;
        }

        private class MockRegion2 : Region
        {
            public MockRegion2(Rectangle bounds)
            {
                this.Bounds = bounds;
            }

            public override int MaxIntersections => 100;

            public override Rectangle Bounds { get; }

            public int ScanInvocationCounter { get; private set; }

            public override int Scan(float y, Span<float> buffer, Configuration configuration)
            {
                this.ScanInvocationCounter++;
                return 0;
            }
        }
    }
}

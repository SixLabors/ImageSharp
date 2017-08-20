// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Pens;
using SixLabors.ImageSharp.Drawing.Processors;
using SixLabors.ImageSharp.PixelFormats;
using Moq;
using Xunit;
using SixLabors.ImageSharp.Drawing.Brushes;

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
        [InlineData(false, 16, 4)] // we always do 4 sub=pixels when antialising is off.
        public void MinimumAntialiasSubpixelDepth(bool antialias, int antialiasSubpixelDepth, int expectedAntialiasSubpixelDepth)
        {
            SixLabors.Primitives.Rectangle bounds = new SixLabors.Primitives.Rectangle(0, 0, 1, 1);

            Mock<IBrush<Rgba32>> brush = new Mock<IBrush<Rgba32>>();
            Mock<Region> region = new Mock<Region>();
            region.Setup(x => x.Bounds).Returns(bounds);

            GraphicsOptions options = new GraphicsOptions(antialias)
            {
                AntialiasSubpixelDepth = 1
            };
            FillRegionProcessor<Rgba32> processor = new FillRegionProcessor<Rgba32>(brush.Object, region.Object, options);
            Image<Rgba32> img = new Image<Rgba32>(1, 1);
            processor.Apply(img, bounds);

            region.Verify(x => x.Scan(It.IsAny<float>(), It.IsAny<Span<float>>()), Times.Exactly(4));
        }

        [Fact]
        public void FillOffCanvas()
        {

            SixLabors.Primitives.Rectangle bounds = new SixLabors.Primitives.Rectangle(-100, -10, 10, 10);

            Mock<IBrush<Rgba32>> brush = new Mock<IBrush<Rgba32>>();
            Mock<Region> region = new Mock<Region>();
            region.Setup(x => x.Bounds).Returns(bounds);

            region.Setup(x => x.MaxIntersections).Returns(10);
            region.Setup(x => x.Scan(It.IsAny<float>(), It.IsAny<Span<float>>()))
                .Returns<float, Span<float>>((y, span) =>
                {
                    if (y < 5)
                    {
                        span[0] = -10f;
                        span[1] = 100f;
                        return 2;
                    }
                    return 0;
                });

            GraphicsOptions options = new GraphicsOptions(true)
            {
            };
            FillRegionProcessor<Rgba32> processor = new FillRegionProcessor<Rgba32>(brush.Object, region.Object, options);
            Image<Rgba32> img = new Image<Rgba32>(10, 10);
            processor.Apply(img, bounds);
        }

        [Fact]
        public void DrawOffCanvas()
        {

            using (var img = new Image<Rgba32>(10, 10))
            {
                img.Mutate(x => x.DrawLines(new Pen<Rgba32>(Rgba32.Black, 10), new SixLabors.Primitives.PointF[] {
                    new Vector2(-10, 5),
                    new Vector2(20, 5),
                }));
            }
        }
    }
}

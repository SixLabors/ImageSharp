// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using Moq;
using Xunit;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.Processing.Drawing.Brushes;
using SixLabors.ImageSharp.Processing.Drawing.Pens;
using SixLabors.ImageSharp.Processing.Drawing.Processors;
using SixLabors.Primitives;

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
            var bounds = new SixLabors.Primitives.Rectangle(0, 0, 1, 1);

            var brush = new Mock<IBrush<Rgba32>>();
            var region = new Mock<Region>();
            region.Setup(x => x.Bounds).Returns(bounds);

            var options = new GraphicsOptions(antialias)
            {
                AntialiasSubpixelDepth = 1
            };
            var processor = new FillRegionProcessor<Rgba32>(brush.Object, region.Object, options);
            var img = new Image<Rgba32>(1, 1);
            processor.Apply(img, bounds);

            region.Verify(x => x.Scan(It.IsAny<float>(), It.IsAny<float[]>(), It.IsAny<int>()), Times.Exactly(4));
        }

        [Fact]
        public void FillOffCanvas()
        {
            var bounds = new Rectangle(-100, -10, 10, 10);
            var brush = new Mock<IBrush<Rgba32>>();
            var options = new GraphicsOptions(true);
            var processor = new FillRegionProcessor<Rgba32>(brush.Object, new MockRegion(), options);
            var img = new Image<Rgba32>(10, 10);
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

        // Mocking the region throws an error in netcore2.0
        private class MockRegion : Region
        {
            public override Rectangle Bounds => new Rectangle(-100, -10, 10, 10);

            public override int MaxIntersections => 10;

            public override int Scan(float y, float[] buffer, int offset)
            {
                if (y < 5)
                {
                    buffer[0] = -10f;
                    buffer[1] = 100f;
                    return 2;
                }
                return 0;
            }
        }
    }
}

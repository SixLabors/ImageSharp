// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using Moq;
using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Xunit;
using SixLabors.ImageSharp.Processing.Processors.Drawing;

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
            var bounds = new Rectangle(0, 0, 1, 1);

            var brush = new Mock<IBrush<Rgba32>>();
            var region = new MockRegion2(bounds);

            var options = new GraphicsOptions(antialias)
            {
                AntialiasSubpixelDepth = 1
            };
            var processor = new FillRegionProcessor<Rgba32>(brush.Object, region, options);
            var img = new Image<Rgba32>(1, 1);
            processor.Apply(img, bounds);

            Assert.Equal(4, region.ScanInvocationCounter);
        }

        [Fact]
        public void FillOffCanvas()
        {
            var bounds = new Rectangle(-100, -10, 10, 10);
            var brush = new Mock<IBrush<Rgba32>>();
            var options = new GraphicsOptions(true);
            var processor = new FillRegionProcessor<Rgba32>(brush.Object, new MockRegion1(), options);
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

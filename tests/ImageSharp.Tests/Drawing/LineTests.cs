// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class LineTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByPath()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "Lines");
            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(
                    x => x.BackgroundColor(Rgba32.Blue).DrawLines(
                        Rgba32.HotPink,
                        5,
                        new Vector2(10, 10),
                        new Vector2(200, 150),
                        new Vector2(50, 300)));
                image.Save($"{path}/Simple.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.HotPink, sourcePixels[11, 11]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[199, 149]);

                Assert.Equal(Rgba32.Blue, sourcePixels[50, 50]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByPath_NoAntialias()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "Lines");
            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(
                    x => x.BackgroundColor(Rgba32.Blue).DrawLines(
                        new GraphicsOptions(false),
                        Rgba32.HotPink,
                        5,
                        new Vector2(10, 10),
                        new Vector2(200, 150),
                        new Vector2(50, 300)));
                image.Save($"{path}/Simple_noantialias.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.HotPink, sourcePixels[11, 11]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[199, 149]);

                Assert.Equal(Rgba32.Blue, sourcePixels[50, 50]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByPathDashed()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "Lines");
            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .DrawLines(Pens.Dash(Rgba32.HotPink, 5),
                    new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
                    }));
                image.Save($"{path}/Dashed.png");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByPathDotted()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "Lines");
            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .DrawLines(Pens.Dot(Rgba32.HotPink, 5),
                    new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
                    }));
                image.Save($"{path}/Dot.png");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByPathDashDot()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "Lines");
            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .DrawLines(Pens.DashDot(Rgba32.HotPink, 5),
                    new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
                    }));
                image.Save($"{path}/DashDot.png");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByPathDashDotDot()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "Lines");
            var image = new Image<Rgba32>(500, 500);

            image.Mutate(x => x
                .BackgroundColor(Rgba32.Blue)
                .DrawLines(Pens.DashDotDot(Rgba32.HotPink, 5), new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
                }));
            image.Save($"{path}/DashDotDot.png");
        }

        [Fact]
        public void ImageShouldBeOverlayedPathWithOpacity()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "Lines");

            var color = new Rgba32(Rgba32.HotPink.R, Rgba32.HotPink.G, Rgba32.HotPink.B, 150);

            var image = new Image<Rgba32>(500, 500);

            image.Mutate(
                x => x.BackgroundColor(Rgba32.Blue).DrawLines(
                    color,
                    10,
                    new Vector2(10, 10),
                    new Vector2(200, 150),
                    new Vector2(50, 300)));
            image.Save($"{path}/Opacity.png");

            //shift background color towards forground color by the opacity amount
            var mergedColor =
                new Rgba32(Vector4.Lerp(Rgba32.Blue.ToVector4(), Rgba32.HotPink.ToVector4(), 150f / 255f));

            Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
            Assert.Equal(mergedColor, sourcePixels[11, 11]);

            Assert.Equal(mergedColor, sourcePixels[199, 149]);

            Assert.Equal(Rgba32.Blue, sourcePixels[50, 50]);
        }

        [Fact]
        public void ImageShouldBeOverlayedByPathOutline()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "Lines");

            var image = new Image<Rgba32>(500, 500);

            image.Mutate(
                x => x.BackgroundColor(Rgba32.Blue).DrawLines(
                    Rgba32.HotPink,
                    10,
                    new Vector2(10, 10),
                    new Vector2(200, 10),
                    new Vector2(200, 150),
                    new Vector2(10, 150)));
            image.Save($"{path}/Rectangle.png");

            Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
            Assert.Equal(Rgba32.HotPink, sourcePixels[11, 11]);

            Assert.Equal(Rgba32.HotPink, sourcePixels[198, 10]);

            Assert.Equal(Rgba32.Blue, sourcePixels[10, 50]);

            Assert.Equal(Rgba32.Blue, sourcePixels[50, 50]);
        }
    }
}

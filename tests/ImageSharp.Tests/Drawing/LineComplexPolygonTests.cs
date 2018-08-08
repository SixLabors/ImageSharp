// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Shapes;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class LineComplexPolygonTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutline()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "LineComplexPolygon");

            var simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            var hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(37, 85),
                            new Vector2(93, 85),
                            new Vector2(65, 137)));

            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x.BackgroundColor(Rgba32.Blue).Draw(Rgba32.HotPink, 5, simplePath.Clip(hole1)));
                image.Save($"{path}/Simple.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.HotPink, sourcePixels[10, 10]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[200, 150]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[50, 300]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[37, 85]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[93, 85]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[65, 137]);

                Assert.Equal(Rgba32.Blue, sourcePixels[2, 2]);

                //inside hole
                Assert.Equal(Rgba32.Blue, sourcePixels[57, 99]);

                //inside shape
                Assert.Equal(Rgba32.Blue, sourcePixels[100, 192]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutlineNoOverlapping()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "LineComplexPolygon");
            var simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            var hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(207, 25),
                            new Vector2(263, 25),
                            new Vector2(235, 57)));

            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x.BackgroundColor(Rgba32.Blue).Draw(Rgba32.HotPink, 5, simplePath.Clip(hole1)));
                image.Save($"{path}/SimpleVanishHole.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.HotPink, sourcePixels[10, 10]);
                Assert.Equal(Rgba32.HotPink, sourcePixels[200, 150]);
                Assert.Equal(Rgba32.HotPink, sourcePixels[50, 300]);

                //Assert.Equal(Color.HotPink, sourcePixels[37, 85]);

                //Assert.Equal(Color.HotPink, sourcePixels[93, 85]);

                //Assert.Equal(Color.HotPink, sourcePixels[65, 137]);

                Assert.Equal(Rgba32.Blue, sourcePixels[2, 2]);

                //inside hole
                Assert.Equal(Rgba32.Blue, sourcePixels[57, 99]);

                //inside shape
                Assert.Equal(Rgba32.Blue, sourcePixels[100, 192]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutlineOverlapping()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "LineComplexPolygon");
            var simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            var hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(37, 85),
                            new Vector2(130, 40),
                            new Vector2(65, 137)));

            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x.BackgroundColor(Rgba32.Blue).Draw(Rgba32.HotPink, 5, simplePath.Clip(hole1)));
                image.Save($"{path}/SimpleOverlapping.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.HotPink, sourcePixels[10, 10]);
                Assert.Equal(Rgba32.HotPink, sourcePixels[200, 150]);
                Assert.Equal(Rgba32.HotPink, sourcePixels[50, 300]);
                Assert.Equal(Rgba32.Blue, sourcePixels[130, 41]);
                Assert.Equal(Rgba32.Blue, sourcePixels[2, 2]);

                //inside hole
                Assert.Equal(Rgba32.Blue, sourcePixels[57, 99]);

                //inside shape
                Assert.Equal(Rgba32.Blue, sourcePixels[100, 192]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutlineDashed()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "LineComplexPolygon");
            var simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            var hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(37, 85),
                            new Vector2(93, 85),
                            new Vector2(65, 137)));

            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .Draw(Pens.Dash(Rgba32.HotPink, 5), simplePath.Clip(hole1)));
                image.Save($"{path}/Dashed.png");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedPolygonOutlineWithOpacity()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "LineComplexPolygon");
            var simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            var hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(37, 85),
                            new Vector2(93, 85),
                            new Vector2(65, 137)));
            var color = new Rgba32(Rgba32.HotPink.R, Rgba32.HotPink.G, Rgba32.HotPink.B, 150);

            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x.BackgroundColor(Rgba32.Blue).Draw(color, 5, simplePath.Clip(hole1)));
                image.Save($"{path}/Opacity.png");

                //shift background color towards forground color by the opacity amount
                var mergedColor = new Rgba32(
                    Vector4.Lerp(Rgba32.Blue.ToVector4(), Rgba32.HotPink.ToVector4(), 150f / 255f));

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(mergedColor, sourcePixels[10, 10]);
                Assert.Equal(mergedColor, sourcePixels[200, 150]);
                Assert.Equal(mergedColor, sourcePixels[50, 300]);
                Assert.Equal(mergedColor, sourcePixels[37, 85]);
                Assert.Equal(mergedColor, sourcePixels[93, 85]);
                Assert.Equal(mergedColor, sourcePixels[65, 137]);
                Assert.Equal(Rgba32.Blue, sourcePixels[2, 2]);

                //inside hole
                Assert.Equal(Rgba32.Blue, sourcePixels[57, 99]);

                //inside shape
                Assert.Equal(Rgba32.Blue, sourcePixels[100, 192]);
            }
        }
    }
}

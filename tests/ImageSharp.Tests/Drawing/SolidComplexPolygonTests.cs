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
    public class SolidComplexPolygonTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutline()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "ComplexPolygon");
            var simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            var hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(37, 85),
                            new Vector2(93, 85),
                            new Vector2(65, 137)));
            IPath clipped = simplePath.Clip(hole1);
            // var clipped = new Rectangle(10, 10, 100, 100).Clip(new Rectangle(20, 0, 20, 20));
            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x.BackgroundColor(Rgba32.Blue).Fill(Rgba32.HotPink, clipped));
                image.Save($"{path}/Simple.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.HotPink, sourcePixels[20, 35]);

                //inside hole
                Assert.Equal(Rgba32.Blue, sourcePixels[60, 100]);
            }
        }


        [Fact]
        public void ImageShouldBeOverlayedPolygonOutlineWithOverlap()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "ComplexPolygon");
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
                image.Mutate(x => x.BackgroundColor(Rgba32.Blue).Fill(Rgba32.HotPink, simplePath.Clip(hole1)));
                image.Save($"{path}/SimpleOverlapping.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.HotPink, sourcePixels[20, 35]);

                //inside hole
                Assert.Equal(Rgba32.Blue, sourcePixels[60, 100]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedPolygonOutlineWithOpacity()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "ComplexPolygon");
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
                image.Mutate(x => x.BackgroundColor(Rgba32.Blue).Fill(color, simplePath.Clip(hole1)));
                image.Save($"{path}/Opacity.png");

                //shift background color towards forground color by the opacity amount
                var mergedColor = new Rgba32(
                    Vector4.Lerp(Rgba32.Blue.ToVector4(), Rgba32.HotPink.ToVector4(), 150f / 255f));

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(mergedColor, sourcePixels[20, 35]);

                //inside hole
                Assert.Equal(Rgba32.Blue, sourcePixels[60, 100]);
            }
        }
    }
}

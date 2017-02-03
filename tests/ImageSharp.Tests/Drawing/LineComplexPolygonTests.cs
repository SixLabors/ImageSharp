// <copyright file="ColorConversionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Drawing
{
    using System.IO;
    using Xunit;
    using Drawing;
    using ImageSharp.Drawing;
    using System.Numerics;
    using ImageSharp.Drawing.Pens;

    using SixLabors.Shapes;

    public class LineComplexPolygonTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutline()
        {
            string path = this.CreateOutputDirectory("Drawing", "LineComplexPolygon");

            var simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            var hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(37, 85),
                            new Vector2(93, 85),
                            new Vector2(65, 137)));

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
                {
                    image
                        .BackgroundColor(Color.Blue)
                		.Draw(Color.HotPink, 5, simplePath.Clip(hole1))
                		.Save(output);
                }

                using (PixelAccessor<Color> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color.HotPink, sourcePixels[10, 10]);

                    Assert.Equal(Color.HotPink, sourcePixels[200, 150]);

                    Assert.Equal(Color.HotPink, sourcePixels[50, 300]);


                    Assert.Equal(Color.HotPink, sourcePixels[37, 85]);

                    Assert.Equal(Color.HotPink, sourcePixels[93, 85]);

                    Assert.Equal(Color.HotPink, sourcePixels[65, 137]);

                    Assert.Equal(Color.Blue, sourcePixels[2, 2]);

                    //inside hole
                    Assert.Equal(Color.Blue, sourcePixels[57, 99]);

                    //inside shape
                    Assert.Equal(Color.Blue, sourcePixels[100, 192]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutlineNoOverlapping()
        {
            string path = this.CreateOutputDirectory("Drawing", "LineComplexPolygon");
            Polygon simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            Polygon hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(207, 25),
                            new Vector2(263, 25),
                            new Vector2(235, 57)));

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/SimpleVanishHole.png"))
                {
                    image
                        .BackgroundColor(Color.Blue)
                		.Draw(Color.HotPink, 5, simplePath.Clip(hole1))
                        .Save(output);
                }

                using (PixelAccessor<Color> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color.HotPink, sourcePixels[10, 10]);

                    Assert.Equal(Color.HotPink, sourcePixels[200, 150]);

                    Assert.Equal(Color.HotPink, sourcePixels[50, 300]);


                    //Assert.Equal(Color.HotPink, sourcePixels[37, 85]);

                    //Assert.Equal(Color.HotPink, sourcePixels[93, 85]);

                    //Assert.Equal(Color.HotPink, sourcePixels[65, 137]);

                    Assert.Equal(Color.Blue, sourcePixels[2, 2]);

                    //inside hole
                    Assert.Equal(Color.Blue, sourcePixels[57, 99]);

                    //inside shape
                    Assert.Equal(Color.Blue, sourcePixels[100, 192]);
                }
            }
        }


        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutlineOverlapping()
        {
            string path = this.CreateOutputDirectory("Drawing", "LineComplexPolygon");
            Polygon simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            Polygon hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(37, 85),
                            new Vector2(130, 40),
                            new Vector2(65, 137)));
							

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/SimpleOverlapping.png"))
                {
                    image
                        .BackgroundColor(Color.Blue)
                		.Draw(Color.HotPink, 5, simplePath.Clip(hole1))
                        .Save(output);
                }

                using (PixelAccessor<Color> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color.HotPink, sourcePixels[10, 10]);

                    Assert.Equal(Color.HotPink, sourcePixels[200, 150]);

                    Assert.Equal(Color.HotPink, sourcePixels[50, 300]);

                    Assert.Equal(Color.Blue, sourcePixels[130, 41]);

                    Assert.Equal(Color.Blue, sourcePixels[2, 2]);

                    //inside hole
                    Assert.Equal(Color.Blue, sourcePixels[57, 99]);

                    //inside shape
                    Assert.Equal(Color.Blue, sourcePixels[100, 192]);
                }
            }
        }


        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutlineDashed()
        {
            string path = this.CreateOutputDirectory("Drawing", "LineComplexPolygon");
            Polygon simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            Polygon hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(37, 85),
                            new Vector2(93, 85),
                            new Vector2(65, 137)));

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Dashed.png"))
                {
                    image
                        .BackgroundColor(Color.Blue)
                		.Draw(Pens.Dash(Color.HotPink, 5), simplePath.Clip(hole1))
                        .Save(output);
                }
            }
        }


        [Fact]
        public void ImageShouldBeOverlayedPolygonOutlineWithOpacity()
        {
            string path = this.CreateOutputDirectory("Drawing", "LineComplexPolygon");
            Polygon simplePath = new Polygon(new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)));

            Polygon hole1 = new Polygon(new LinearLineSegment(
                            new Vector2(37, 85),
                            new Vector2(93, 85),
                            new Vector2(65, 137)));
            Color color = new Color(Color.HotPink.R, Color.HotPink.G, Color.HotPink.B, 150);

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
                {
                    image
                        .BackgroundColor(Color.Blue)
                    	.Draw(color, 5, simplePath.Clip(hole1))
                        .Save(output);
                }

                //shift background color towards forground color by the opacity amount
                Color mergedColor = new Color(Vector4.Lerp(Color.Blue.ToVector4(), Color.HotPink.ToVector4(), 150f / 255f));

                using (PixelAccessor<Color> sourcePixels = image.Lock())
                {
                    Assert.Equal(mergedColor, sourcePixels[10, 10]);

                    Assert.Equal(mergedColor, sourcePixels[200, 150]);

                    Assert.Equal(mergedColor, sourcePixels[50, 300]);


                    Assert.Equal(mergedColor, sourcePixels[37, 85]);

                    Assert.Equal(mergedColor, sourcePixels[93, 85]);

                    Assert.Equal(mergedColor, sourcePixels[65, 137]);

                    Assert.Equal(Color.Blue, sourcePixels[2, 2]);

                    //inside hole
                    Assert.Equal(Color.Blue, sourcePixels[57, 99]);


                    //inside shape
                    Assert.Equal(Color.Blue, sourcePixels[100, 192]);
                }
            }
        }
    }
}

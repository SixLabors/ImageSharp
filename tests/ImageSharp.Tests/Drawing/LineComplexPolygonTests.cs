// <copyright file="ColorConversionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Drawing
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using Xunit;
    using Drawing;
    using ImageSharp.Drawing;
    using System.Numerics;
    using ImageSharp.Drawing.Shapes;
    using ImageSharp.Drawing.Pens;

    public class LineComplexPolygonTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutline()
        {
            string path = CreateOutputDirectory("Drawing", "LineComplexPolygon");
            var simplePath = new LinearPolygon(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300));

            var hole1 = new LinearPolygon(
                            new Vector2(37, 85),
                            new Vector2(93, 85),
                            new Vector2(65, 137));

            var image = new Image(500, 500);

            using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
            {
                image
                .BackgroundColor(Color.Blue)
                .DrawPolygon(Color.HotPink, 5, new ComplexPolygon(simplePath, hole1))
                .Save(output);
            }

            using (var sourcePixels = image.Lock())
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


        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutlineOverlapping()
        {
            string path = CreateOutputDirectory("Drawing", "LineComplexPolygon");
            var simplePath = new LinearPolygon(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300));

            var hole1 = new LinearPolygon(
                            new Vector2(37, 85),
                            new Vector2(130, 40),
                            new Vector2(65, 137));

            var image = new Image(500, 500);

            using (FileStream output = File.OpenWrite($"{path}/SimpleOverlapping.png"))
            {
                image
                .BackgroundColor(Color.Blue)
                .DrawPolygon(Color.HotPink, 5, new ComplexPolygon(simplePath, hole1))
                .Save(output);
            }

            using (var sourcePixels = image.Lock())
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


        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutlineDashed()
        {
            string path = CreateOutputDirectory("Drawing", "LineComplexPolygon");
            var simplePath = new LinearPolygon(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300));

            var hole1 = new LinearPolygon(
                            new Vector2(37, 85),
                            new Vector2(93, 85),
                            new Vector2(65, 137));

            var image = new Image(500, 500);

            using (FileStream output = File.OpenWrite($"{path}/Dashed.png"))
            {
                image
                .BackgroundColor(Color.Blue)
                .DrawPolygon(Pens.Dash(Color.HotPink, 5), new ComplexPolygon(simplePath, hole1))
                .Save(output);
            }
        }


        [Fact]
        public void ImageShouldBeOverlayedPolygonOutlineWithOpacity()
        {
            string path = CreateOutputDirectory("Drawing", "LineComplexPolygon");
            var simplePath = new LinearPolygon(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300));

            var hole1 = new LinearPolygon(
                            new Vector2(37, 85),
                            new Vector2(93, 85),
                            new Vector2(65, 137));
            var color = new Color(Color.HotPink.R, Color.HotPink.G, Color.HotPink.B, 150);

            var image = new Image(500, 500);

            using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .DrawPolygon(color, 5, new ComplexPolygon(simplePath, hole1))
                    .Save(output);
            }

            //shift background color towards forground color by the opacity amount
            var mergedColor = new Color(Vector4.Lerp(Color.Blue.ToVector4(), Color.HotPink.ToVector4(), 150f / 255f));

            using (var sourcePixels = image.Lock())
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

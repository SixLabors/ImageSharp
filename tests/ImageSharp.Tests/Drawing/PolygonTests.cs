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

    public class PolygonTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByPolygonOutline()
        {
            string path = CreateOutputDirectory("Drawing", "Polygons");

            var image = new Image(500, 500);

            using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .DrawPolygon(Color.HotPink, 5, new[] {
                        new Vector2(10, 10),
                        new Vector2(200, 150),
                        new Vector2(50, 300)
                    })
                    .Save(output);
            }

            using (var sourcePixels = image.Lock())
            {
                Assert.Equal(Color.HotPink, sourcePixels[9, 9]);

                Assert.Equal(Color.HotPink, sourcePixels[199, 149]);

                Assert.Equal(Color.Blue, sourcePixels[50, 50]);

                Assert.Equal(Color.Blue, sourcePixels[2, 2]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedPolygonOutlineWithOpacity()
        {
            string path = CreateOutputDirectory("Drawing", "Polygons");
            var simplePath = new[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            var color = new Color(Color.HotPink.R, Color.HotPink.G, Color.HotPink.B, 150);

            var image = new Image(500, 500);

            using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .DrawPolygon(color, 10, simplePath)
                    .Save(output);
            }

            //shift background color towards forground color by the opacity amount
            var mergedColor = new Color(Vector4.Lerp(Color.Blue.ToVector4(), Color.HotPink.ToVector4(), 150f / 255f));

            using (var sourcePixels = image.Lock())
            {
                Assert.Equal(mergedColor, sourcePixels[9, 9]);

                Assert.Equal(mergedColor, sourcePixels[199, 149]);

                Assert.Equal(Color.Blue, sourcePixels[50, 50]);

                Assert.Equal(Color.Blue, sourcePixels[2, 2]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByRectangleOutline()
        {
            string path = CreateOutputDirectory("Drawing", "Polygons");

            var image = new Image(500, 500);

            using (FileStream output = File.OpenWrite($"{path}/Rectangle.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .DrawPolygon(Color.HotPink, 10, new Rectangle(10, 10, 190, 140))
                    .Save(output);
            }

            using (var sourcePixels = image.Lock())
            {
                Assert.Equal(Color.HotPink, sourcePixels[8, 8]);

                Assert.Equal(Color.HotPink, sourcePixels[198, 10]);

                Assert.Equal(Color.HotPink, sourcePixels[10, 50]);

                Assert.Equal(Color.Blue, sourcePixels[50, 50]);

                Assert.Equal(Color.Blue, sourcePixels[2, 2]);
            }
        }
    }
}
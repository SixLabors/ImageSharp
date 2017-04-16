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
            string path = this.CreateOutputDirectory("Drawing", "Polygons");

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
                {
                    image
                        .BackgroundColor(Color32.Blue)
                        .DrawPolygon(Color32.HotPink, 5,
                        new[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
                        })
                        .Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.HotPink, sourcePixels[9, 9]);

                    Assert.Equal(Color32.HotPink, sourcePixels[199, 149]);

                    Assert.Equal(Color32.Blue, sourcePixels[50, 50]);

                    Assert.Equal(Color32.Blue, sourcePixels[2, 2]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedPolygonOutlineWithOpacity()
        {
            string path = this.CreateOutputDirectory("Drawing", "Polygons");
            Vector2[] simplePath = new[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            Color32 color = new Color32(Color32.HotPink.R, Color32.HotPink.G, Color32.HotPink.B, 150);

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
                {
                    image
                        .BackgroundColor(Color32.Blue)
                        .DrawPolygon(color, 10, simplePath)
                        .Save(output);
                }

                //shift background color towards forground color by the opacity amount
                Color32 mergedColor = new Color32(Vector4.Lerp(Color32.Blue.ToVector4(), Color32.HotPink.ToVector4(), 150f / 255f));

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(mergedColor, sourcePixels[9, 9]);

                    Assert.Equal(mergedColor, sourcePixels[199, 149]);

                    Assert.Equal(Color32.Blue, sourcePixels[50, 50]);

                    Assert.Equal(Color32.Blue, sourcePixels[2, 2]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByRectangleOutline()
        {
            string path = this.CreateOutputDirectory("Drawing", "Polygons");

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Rectangle.png"))
                {
                    image
                        .BackgroundColor(Color32.Blue)
                        .Draw(Color32.HotPink, 10, new Rectangle(10, 10, 190, 140))
                        .Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.HotPink, sourcePixels[8, 8]);

                    Assert.Equal(Color32.HotPink, sourcePixels[198, 10]);

                    Assert.Equal(Color32.HotPink, sourcePixels[10, 50]);

                    Assert.Equal(Color32.Blue, sourcePixels[50, 50]);

                    Assert.Equal(Color32.Blue, sourcePixels[2, 2]);
                }
            }
        }
    }
}
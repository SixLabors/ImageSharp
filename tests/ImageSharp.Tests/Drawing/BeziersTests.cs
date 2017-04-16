// <copyright file="ColorConversionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Drawing
{
    using Drawing;
    using ImageSharp.Drawing;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Numerics;
    using Xunit;

    public class Beziers : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByBezierLine()
        {
            string path = this.CreateOutputDirectory("Drawing", "BezierLine");
            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
                {
                    image.BackgroundColor(Color32.Blue)
                        .DrawBeziers(Color32.HotPink, 5,
                            new[] {
                                new Vector2(10, 400),
                                new Vector2(30, 10),
                                new Vector2(240, 30),
                                new Vector2(300, 400)
                            })
                        .Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    //top of curve
                    Assert.Equal(Color32.HotPink, sourcePixels[138, 115]);

                    //start points
                    Assert.Equal(Color32.HotPink, sourcePixels[10, 400]);
                    Assert.Equal(Color32.HotPink, sourcePixels[300, 400]);

                    //curve points should not be never be set
                    Assert.Equal(Color32.Blue, sourcePixels[30, 10]);
                    Assert.Equal(Color32.Blue, sourcePixels[240, 30]);

                    // inside shape should be empty
                    Assert.Equal(Color32.Blue, sourcePixels[200, 250]);
                }
            }
        }


        [Fact]
        public void ImageShouldBeOverlayedBezierLineWithOpacity()
        {
            string path = this.CreateOutputDirectory("Drawing", "BezierLine");

            Color32 color = new Color32(Color32.HotPink.R, Color32.HotPink.G, Color32.HotPink.B, 150);

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
                {
                    image.BackgroundColor(Color32.Blue)
                        .DrawBeziers(color,
                        10,
                        new[] {
                            new Vector2(10, 400),
                            new Vector2(30, 10),
                            new Vector2(240, 30),
                            new Vector2(300, 400)
                        })
                        .Save(output);
                }

                //shift background color towards forground color by the opacity amount
                Color32 mergedColor = new Color32(Vector4.Lerp(Color32.Blue.ToVector4(), Color32.HotPink.ToVector4(), 150f / 255f));

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    //top of curve
                    Assert.Equal(mergedColor, sourcePixels[138, 115]);

                    //start points
                    Assert.Equal(mergedColor, sourcePixels[10, 400]);
                    Assert.Equal(mergedColor, sourcePixels[300, 400]);

                    //curve points should not be never be set
                    Assert.Equal(Color32.Blue, sourcePixels[30, 10]);
                    Assert.Equal(Color32.Blue, sourcePixels[240, 30]);

                    // inside shape should be empty
                    Assert.Equal(Color32.Blue, sourcePixels[200, 250]);
                }
            }
        }
    }
}

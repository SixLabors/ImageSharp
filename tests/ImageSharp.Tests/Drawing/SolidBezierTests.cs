// <copyright file="ColorConversionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Drawing
{
    using System.IO;
    using System.Numerics;

    using SixLabors.Shapes;

    using Xunit;

    public class SolidBezierTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygon()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledBezier");
            Vector2[] simplePath = new[] {
                        new Vector2(10, 400),
                        new Vector2(30, 10),
                        new Vector2(240, 30),
                        new Vector2(300, 400)
            };
            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
                {
                    image
                        .BackgroundColor(Color.Blue)
                        .Fill(Color.HotPink,new BezierPolygon(simplePath))
                        .Save(output);
                }

                using (PixelAccessor<Color> sourcePixels = image.Lock())
                {
                    //top of curve
                    Assert.Equal(Color.HotPink, sourcePixels[138, 116]);

                    //start points
                    Assert.Equal(Color.HotPink, sourcePixels[10, 400]);
                    Assert.Equal(Color.HotPink, sourcePixels[300, 400]);

                    //curve points should not be never be set
                    Assert.Equal(Color.Blue, sourcePixels[30, 10]);
                    Assert.Equal(Color.Blue, sourcePixels[240, 30]);

                    // inside shape should not be empty
                    Assert.Equal(Color.HotPink, sourcePixels[200, 250]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonOpacity()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledBezier");
            Vector2[] simplePath = new[] {
                        new Vector2(10, 400),
                        new Vector2(30, 10),
                        new Vector2(240, 30),
                        new Vector2(300, 400)
            };
            Color color = new Color(Color.HotPink.R, Color.HotPink.G, Color.HotPink.B, 150);

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
                {
                    image
                        .BackgroundColor(Color.Blue)
                        .Fill(color, new BezierPolygon(simplePath))
                        .Save(output);
                }

                //shift background color towards forground color by the opacity amount
                Color mergedColor = new Color(Vector4.Lerp(Color.Blue.ToVector4(), Color.HotPink.ToVector4(), 150f / 255f));

                using (PixelAccessor<Color> sourcePixels = image.Lock())
                {
                    //top of curve
                    Assert.Equal(mergedColor, sourcePixels[138, 116]);

                    //start points
                    Assert.Equal(mergedColor, sourcePixels[10, 400]);
                    Assert.Equal(mergedColor, sourcePixels[300, 400]);

                    //curve points should not be never be set
                    Assert.Equal(Color.Blue, sourcePixels[30, 10]);
                    Assert.Equal(Color.Blue, sourcePixels[240, 30]);

                    // inside shape should not be empty
                    Assert.Equal(mergedColor, sourcePixels[200, 250]);
                }
            }
        }
    }
}

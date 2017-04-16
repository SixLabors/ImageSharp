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
                        .BackgroundColor(Color32.Blue)
                        .Fill(Color32.HotPink, new Polygon(new BezierLineSegment(simplePath)))
                        .Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.HotPink, sourcePixels[150, 300]);

                    //curve points should not be never be set
                    Assert.Equal(Color32.Blue, sourcePixels[240, 30]);

                    // inside shape should not be empty
                    Assert.Equal(Color32.HotPink, sourcePixels[200, 250]);
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
            Color32 color = new Color32(Color32.HotPink.R, Color32.HotPink.G, Color32.HotPink.B, 150);

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
                {
                    image
                        .BackgroundColor(Color32.Blue)
                        .Fill(color, new Polygon(new BezierLineSegment(simplePath)))
                        .Save(output);
                }

                //shift background color towards forground color by the opacity amount
                Color32 mergedColor = new Color32(Vector4.Lerp(Color32.Blue.ToVector4(), Color32.HotPink.ToVector4(), 150f / 255f));

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    //top of curve
                    Assert.Equal(mergedColor, sourcePixels[138, 116]);

                    //curve points should not be never be set
                    Assert.Equal(Color32.Blue, sourcePixels[240, 30]);

                    // inside shape should not be empty
                    Assert.Equal(mergedColor, sourcePixels[200, 250]);
                }
            }
        }
    }
}

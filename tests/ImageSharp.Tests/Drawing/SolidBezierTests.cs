// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Shapes;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class SolidBezierTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygon()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledBezier");
            SixLabors.Primitives.PointF[] simplePath = new SixLabors.Primitives.PointF[] {
                        new Vector2(10, 400),
                        new Vector2(30, 10),
                        new Vector2(240, 30),
                        new Vector2(300, 400)
            };
            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .Fill(Rgba32.HotPink, new Polygon(new CubicBezierLineSegment(simplePath))));
                image.Save($"{path}/Simple.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.HotPink, sourcePixels[150, 300]);

                    //curve points should not be never be set
                    Assert.Equal(Rgba32.Blue, sourcePixels[240, 30]);

                    // inside shape should not be empty
                    Assert.Equal(Rgba32.HotPink, sourcePixels[200, 250]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonOpacity()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledBezier");
            SixLabors.Primitives.PointF[] simplePath = new SixLabors.Primitives.PointF[] {
                        new Vector2(10, 400),
                        new Vector2(30, 10),
                        new Vector2(240, 30),
                        new Vector2(300, 400)
            };
            Rgba32 color = new Rgba32(Rgba32.HotPink.R, Rgba32.HotPink.G, Rgba32.HotPink.B, 150);

            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .Fill(color, new Polygon(new CubicBezierLineSegment(simplePath))));
                image.Save($"{path}/Opacity.png");

                //shift background color towards forground color by the opacity amount
                Rgba32 mergedColor = new Rgba32(Vector4.Lerp(Rgba32.Blue.ToVector4(), Rgba32.HotPink.ToVector4(), 150f / 255f));

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    //top of curve
                    Assert.Equal(mergedColor, sourcePixels[138, 116]);

                    //curve points should not be never be set
                    Assert.Equal(Rgba32.Blue, sourcePixels[240, 30]);

                    // inside shape should not be empty
                    Assert.Equal(mergedColor, sourcePixels[200, 250]);
                }
            }
        }
    }
}

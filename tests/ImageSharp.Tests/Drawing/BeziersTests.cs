// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Drawing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class Beziers : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByBezierLine()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "BezierLine");
            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x.BackgroundColor(Rgba32.Blue)
                    .DrawBeziers(Rgba32.HotPink, 5,
                        new SixLabors.Primitives.PointF[] {
                                new Vector2(10, 400),
                                new Vector2(30, 10),
                                new Vector2(240, 30),
                                new Vector2(300, 400)
                        }));
                image.Save($"{path}/Simple.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    //top of curve
                    Assert.Equal(Rgba32.HotPink, sourcePixels[138, 115]);

                    //start points
                    Assert.Equal(Rgba32.HotPink, sourcePixels[10, 395]);
                    Assert.Equal(Rgba32.HotPink, sourcePixels[300, 395]);

                    //curve points should not be never be set
                    Assert.Equal(Rgba32.Blue, sourcePixels[30, 10]);
                    Assert.Equal(Rgba32.Blue, sourcePixels[240, 30]);

                    // inside shape should be empty
                    Assert.Equal(Rgba32.Blue, sourcePixels[200, 250]);
                }
            }
        }


        [Fact]
        public void ImageShouldBeOverlayedBezierLineWithOpacity()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "BezierLine");

            Rgba32 color = new Rgba32(Rgba32.HotPink.R, Rgba32.HotPink.G, Rgba32.HotPink.B, 150);

            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x.BackgroundColor(Rgba32.Blue)
                    .DrawBeziers(color,
                    10,
                    new SixLabors.Primitives.PointF[]{
                            new Vector2(10, 400),
                            new Vector2(30, 10),
                            new Vector2(240, 30),
                            new Vector2(300, 400)
                    }));
                    image.Save($"{path}/Opacity.png");

                //shift background color towards forground color by the opacity amount
                Rgba32 mergedColor = new Rgba32(Vector4.Lerp(Rgba32.Blue.ToVector4(), Rgba32.HotPink.ToVector4(), 150f / 255f));

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    // top of curve
                    Assert.Equal(mergedColor, sourcePixels[138, 115]);

                    // start points
                    Assert.Equal(mergedColor, sourcePixels[10, 395]);
                    Assert.Equal(mergedColor, sourcePixels[300, 395]);

                    // curve points should not be never be set
                    Assert.Equal(Rgba32.Blue, sourcePixels[30, 10]);
                    Assert.Equal(Rgba32.Blue, sourcePixels[240, 30]);

                    // inside shape should be empty
                    Assert.Equal(Rgba32.Blue, sourcePixels[200, 250]);
                }
            }
        }
    }
}

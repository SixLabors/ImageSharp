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
            string path = CreateOutputDirectory("Drawing","BezierLine");
var image = new Image(500, 500);

using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
{
    image
        .BackgroundColor(Color.Blue)
        .DrawBeziers(Color.HotPink, 5, new[] {
            new PointF(10, 400),
            new PointF(30, 10),
            new PointF(240, 30),
            new PointF(300, 400)
        })
        .Save(output);
}

            using (var sourcePixels = image.Lock())
            {
                //top of curve
                Assert.Equal(Color.HotPink, sourcePixels[138,115]);

                //start points                
                Assert.Equal(Color.HotPink, sourcePixels[10, 400]);
                Assert.Equal(Color.HotPink, sourcePixels[300, 400]);

                //curve points should not be never be set
                Assert.Equal(Color.Blue, sourcePixels[30, 10]);
                Assert.Equal(Color.Blue, sourcePixels[240, 30]);

                // inside shape should be empty
                Assert.Equal(Color.Blue, sourcePixels[200, 250]);
            }

        }


        [Fact]
        public void ImageShouldBeOverlayedBezierLineWithOpacity()
        {
            string path = CreateOutputDirectory("Drawing", "BezierLine");

            var color = new Color(Color.HotPink.R, Color.HotPink.G, Color.HotPink.B, 150);

            var image = new Image(500, 500);
            
            using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .DrawBeziers(color, 10, new[] {
                        new PointF(10, 400),
                        new PointF(30, 10),
                        new PointF(240, 30),
                        new PointF(300, 400)
                    })
                    .Save(output);
            }

            //shift background color towards forground color by the opacity amount
            var mergedColor = new Color(Vector4.Lerp(Color.Blue.ToVector4(), Color.HotPink.ToVector4(), 150f/255f));

            using (var sourcePixels = image.Lock())
            {
                //top of curve
                Assert.Equal(mergedColor, sourcePixels[138, 115]);

                //start points                
                Assert.Equal(mergedColor, sourcePixels[10, 400]);
                Assert.Equal(mergedColor, sourcePixels[300, 400]);

                //curve points should not be never be set
                Assert.Equal(Color.Blue, sourcePixels[30, 10]);
                Assert.Equal(Color.Blue, sourcePixels[240, 30]);

                // inside shape should be empty
                Assert.Equal(Color.Blue, sourcePixels[200, 250]);
            }
        }       
        
    }
}

// <copyright file="ColorConversionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Drawing
{
    using ShapePath = SixLabors.Shapes.Path;
    using SixLabors.Shapes;
    using System.IO;
    using System.Numerics;

    using ImageSharp.PixelFormats;

    using Xunit;
    using ImageSharp.Drawing.Pens;

    public class DrawPathTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByPath()
        {
            string path = this.CreateOutputDirectory("Drawing", "Path");
            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                LinearLineSegment linerSegemnt = new LinearLineSegment(
                    new Vector2(10, 10),
                    new Vector2(200, 150),
                    new Vector2(50, 300));
                BezierLineSegment bazierSegment = new BezierLineSegment(new Vector2(50, 300),
                    new Vector2(500, 500),
                    new Vector2(60, 10),
                    new Vector2(10, 400));

                ShapePath p = new ShapePath(linerSegemnt, bazierSegment);

                image
                    .BackgroundColor(Rgba32.Blue)
                    .Draw(Rgba32.HotPink, 5, p)
                    .Save($"{path}/Simple.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.HotPink, sourcePixels[11, 11]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[199, 149]);

                    Assert.Equal(Rgba32.Blue, sourcePixels[50, 50]);
                }
            }
        }


        [Fact]
        public void ImageShouldBeOverlayedPathWithOpacity()
        {
            string path = this.CreateOutputDirectory("Drawing", "Path");

            Rgba32 color = new Rgba32(Rgba32.HotPink.R, Rgba32.HotPink.G, Rgba32.HotPink.B, 150);


            LinearLineSegment linerSegemnt = new LinearLineSegment(
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
                    );

            BezierLineSegment bazierSegment = new BezierLineSegment(new Vector2(50, 300),
                new Vector2(500, 500),
                new Vector2(60, 10),
                new Vector2(10, 400));

            ShapePath p = new ShapePath(linerSegemnt, bazierSegment);

            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image
                    .BackgroundColor(Rgba32.Blue)
                    .Draw(color, 10, p)
                    .Save($"{path}/Opacity.png");

                //shift background color towards forground color by the opacity amount
                Rgba32 mergedColor = new Rgba32(Vector4.Lerp(Rgba32.Blue.ToVector4(), Rgba32.HotPink.ToVector4(), 150f / 255f));

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(mergedColor, sourcePixels[11, 11]);

                    Assert.Equal(mergedColor, sourcePixels[199, 149]);

                    Assert.Equal(Rgba32.Blue, sourcePixels[50, 50]);
                }
            }
        }


        [Fact]
        public void PathExtendingOffEdgeOfImageShouldNotBeCropped()
        {

            string path = this.CreateOutputDirectory("Drawing", "Path");
            using (var image = new Image<Rgba32>(256, 256))
            {
                image.Fill(Rgba32.Black);
                var pen = Pens.Solid(Rgba32.White, 5f);

                for (int i = 0; i < 300; i += 20)
                {
                    image.DrawLines(pen, new Vector2[] { new Vector2(100, 2), new Vector2(-10, i) });
                }

                image
                    .Save($"{path}/ClippedLines.png");
                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.White, sourcePixels[0, 90]);
                }
            }
        }
    }
}
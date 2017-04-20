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

    public class FillSolidBrushTests: FileTestBase
    {
        [Fact]
        public void ImageShouldBeFloodFilledWithColorOnDefaultBackground()
        {
            string path = this.CreateOutputDirectory("Fill", "SolidBrush");
            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/DefaultBack.png"))
                {
                    image
                        .Fill(Rgba32.HotPink)
                        .Save(output);
                }

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.HotPink, sourcePixels[9, 9]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[199, 149]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithColor()
        {
            string path = this.CreateOutputDirectory("Fill", "SolidBrush");
            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
                {
                    image
                        .BackgroundColor(Rgba32.Blue)
                        .Fill(Rgba32.HotPink)
                        .Save(output);
                }

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.HotPink, sourcePixels[9, 9]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[199, 149]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithColorOpacity()
        {
            string path = this.CreateOutputDirectory("Fill", "SolidBrush");
            using (Image image = new Image(500, 500))
            {
                Rgba32 color = new Rgba32(Rgba32.HotPink.R, Rgba32.HotPink.G, Rgba32.HotPink.B, 150);

                using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
                {
                    image
                        .BackgroundColor(Rgba32.Blue)
                        .Fill(color)
                        .Save(output);
                }
                //shift background color towards forground color by the opacity amount
                Rgba32 mergedColor = new Rgba32(Vector4.Lerp(Rgba32.Blue.ToVector4(), Rgba32.HotPink.ToVector4(), 150f / 255f));


                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(mergedColor, sourcePixels[9, 9]);
                    Assert.Equal(mergedColor, sourcePixels[199, 149]);
                }
            }
        }

    }
}

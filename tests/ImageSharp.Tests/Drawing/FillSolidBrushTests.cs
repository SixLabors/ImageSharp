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
                        .Fill(Color32.HotPink)
                        .Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.HotPink, sourcePixels[9, 9]);

                    Assert.Equal(Color32.HotPink, sourcePixels[199, 149]);
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
                        .BackgroundColor(Color32.Blue)
                        .Fill(Color32.HotPink)
                        .Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.HotPink, sourcePixels[9, 9]);

                    Assert.Equal(Color32.HotPink, sourcePixels[199, 149]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithColorOpacity()
        {
            string path = this.CreateOutputDirectory("Fill", "SolidBrush");
            using (Image image = new Image(500, 500))
            {
                Color32 color = new Color32(Color32.HotPink.R, Color32.HotPink.G, Color32.HotPink.B, 150);

                using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
                {
                    image
                        .BackgroundColor(Color32.Blue)
                        .Fill(color)
                        .Save(output);
                }
                //shift background color towards forground color by the opacity amount
                Color32 mergedColor = new Color32(Vector4.Lerp(Color32.Blue.ToVector4(), Color32.HotPink.ToVector4(), 150f / 255f));


                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(mergedColor, sourcePixels[9, 9]);
                    Assert.Equal(mergedColor, sourcePixels[199, 149]);
                }
            }
        }

    }
}

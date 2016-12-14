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
            string path = CreateOutputDirectory("Fill", "SolidBrush");
            var image = new Image(500, 500);

            using (FileStream output = File.OpenWrite($"{path}/DefaultBack.png"))
            {
                image
                    .Fill(Color.HotPink)
                    .Save(output);
            }

            using (var sourcePixels = image.Lock())
            {
                Assert.Equal(Color.HotPink, sourcePixels[9, 9]);

                Assert.Equal(Color.HotPink, sourcePixels[199, 149]);
            }
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithColor()
        {
            string path = CreateOutputDirectory("Fill", "SolidBrush");
            var image = new Image(500, 500);

            using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .Fill(Color.HotPink)
                    .Save(output);
            }

            using (var sourcePixels = image.Lock())
            {
                Assert.Equal(Color.HotPink, sourcePixels[9, 9]);

                Assert.Equal(Color.HotPink, sourcePixels[199, 149]);
            }
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithColorOpacity()
        {
            string path = CreateOutputDirectory("Fill", "SolidBrush");
            var image = new Image(500, 500);

            var color = new Color(Color.HotPink.R, Color.HotPink.G, Color.HotPink.B, 150);

            using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .Fill(color)
                    .Save(output);
            }
            //shift background color towards forground color by the opacity amount
            var mergedColor = new Color(Vector4.Lerp(Color.Blue.ToVector4(), Color.HotPink.ToVector4(), 150f / 255f));


            using (var sourcePixels = image.Lock())
            {
                Assert.Equal(mergedColor, sourcePixels[9, 9]);
                Assert.Equal(mergedColor, sourcePixels[199, 149]);
            }

        }

    }
}

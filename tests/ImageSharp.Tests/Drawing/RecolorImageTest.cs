// <copyright file="BlendTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using ImageSharp.Drawing.Brushes;
    using System.IO;
    using System.Linq;

    using Xunit;

    public class RecolorImageTest : FileTestBase
    {
        [Fact]
        public void ImageShouldRecolorYellowToHotPink()
        {
            string path = this.CreateOutputDirectory("Drawing", "RecolorImage");

            RecolorBrush brush = new RecolorBrush(Color.Yellow, Color.HotPink, 0.2f);

            foreach (TestFile file in Files)
            {
                using (Image image = file.CreateImage())
                {
                    using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                    {
                        image.Fill(brush)
                            .Save(output);
                    }
                }
            }
        }

        [Fact]
        public void ImageShouldRecolorYellowToHotPinkInARectangle()
        {
            string path = this.CreateOutputDirectory("Drawing", "RecolorImage");

            RecolorBrush brush = new RecolorBrush(Color.Yellow, Color.HotPink, 0.2f);

            foreach (TestFile file in Files)
            {
                using (Image image = file.CreateImage())
                {
                    using (FileStream output = File.OpenWrite($"{path}/Shaped_{file.FileName}"))
                    {
                        int imageHeight = image.Height;
                        image.Fill(brush, new Rectangle(0, imageHeight/2 - imageHeight/4, image.Width, imageHeight/2))
                            .Save(output);
                    }
                }
            }
        }
    }
}
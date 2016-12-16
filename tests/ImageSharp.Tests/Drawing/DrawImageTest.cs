// <copyright file="BlendTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using System.Linq;

    using Xunit;

    public class DrawImageTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyDrawImageFilter()
        {
            string path = CreateOutputDirectory("Drawing", "DrawImage");

            Image blend;// = new Image(400, 400);
                        // blend.BackgroundColor(Color.RebeccaPurple);

            using (FileStream stream = File.OpenRead(TestImages.Bmp.Car))
            {
                blend = new Image(stream);
            }

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.DrawImage(blend, 75, new Size(image.Width / 2, image.Height / 2), new Point(image.Width / 4, image.Height / 4))
                         .Save(output);
                }
            }
        }
    }
}
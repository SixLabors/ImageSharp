// <copyright file="BlendTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;

    public class DrawImageTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyDrawImageFilter()
        {
            string path = this.CreateOutputDirectory("Drawing", "DrawImage");

            using (Image blend = TestFile.Create(TestImages.Bmp.Car).CreateImage())
            {
                foreach (TestFile file in Files)
                {
                    using (Image image = file.CreateImage())
                    {
                        using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                        {
                            image.DrawImage(blend, 75, new Size(image.Width / 2, image.Height / 2), new Point(image.Width / 4, image.Height / 4))
                                 .Save(output);
                        }
                    }
                }
            }
        }
    }
}
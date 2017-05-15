// <copyright file="BackgroundColorTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using ImageSharp.PixelFormats;

    using Xunit;

    public class BackgroundColorTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyBackgroundColorFilter()
        {
            string path = this.CreateOutputDirectory("BackgroundColor");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.BackgroundColor(Rgba32.HotPink).Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyBackgroundColorFilterInBox()
        {
            string path = this.CreateOutputDirectory("BackgroundColor");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("-InBox");
                using (Image<Rgba32> image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.BackgroundColor(Rgba32.HotPink, new Rectangle(10, 10, image.Width / 2, image.Height / 2)).Save(output);
                }
            }
        }
    }
}
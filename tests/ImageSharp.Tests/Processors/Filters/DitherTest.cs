// <copyright file="DitherTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using ImageSharp.Dithering;

    using Xunit;

    public class DitherTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyDitherFilter()
        {
            string path = this.CreateOutputDirectory("Dither");

            foreach (TestFile file in Files)
            {
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Dither(new SierraLite(), .5F).Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyDitherFilterInBox()
        {
            string path = this.CreateOutputDirectory("Dither");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("-InBox");
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Dither(new SierraLite(), .5F, new Rectangle(10, 10, image.Width / 2, image.Height / 2)).Save(output);
                }
            }
        }
    }
}
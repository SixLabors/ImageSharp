// <copyright file="SepiaTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using ImageSharp.PixelFormats;

    using Xunit;

    public class SepiaTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplySepiaFilter()
        {
            string path = CreateOutputDirectory("Sepia");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Sepia().Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplySepiaFilterInBox()
        {
            string path = this.CreateOutputDirectory("Sepia");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("InBox");
                using (Image<Rgba32> image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Sepia(new Rectangle(10, 10, image.Width / 2, image.Height / 2)).Save(output);
                }
            }
        }
    }
}
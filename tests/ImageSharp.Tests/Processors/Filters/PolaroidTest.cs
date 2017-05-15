// <copyright file="PolaroidTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using ImageSharp.PixelFormats;

    using Xunit;

    public class PolaroidTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyPolaroidFilter()
        {
            string path = this.CreateOutputDirectory("Polaroid");

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Polaroid().Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyPolaroidFilterInBox()
        {
            string path = this.CreateOutputDirectory("Polaroid");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("InBox");
                using (Image<Rgba32> image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Polaroid(new Rectangle(10, 10, image.Width / 2, image.Height / 2)).Save(output);
                }
            }
        }
    }
}
// <copyright file="VignetteTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;

    public class VignetteTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyVignetteFilter()
        {
            string path = this.CreateOutputDirectory("Vignette");

            foreach (TestFile file in Files)
            {
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Vignette().Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyVignetteFilterColor()
        {
            string path = this.CreateOutputDirectory("Vignette");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("Color");
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Vignette(Color.HotPink).Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyVignetteFilterRadius()
        {
            string path = this.CreateOutputDirectory("Vignette");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("Radius");
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Vignette(image.Width / 4F, image.Height / 4F).Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyVignetteFilterInBox()
        {
            string path = this.CreateOutputDirectory("Vignette");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("InBox");
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Vignette(new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2))
                        .Save(output);
                }
            }
        }
    }
}
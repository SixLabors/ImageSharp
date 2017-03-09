// <copyright file="LomographTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;

    public class LomographTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyLomographFilter()
        {
            string path = this.CreateOutputDirectory("Lomograph");

            foreach (TestFile file in Files)
            {
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Lomograph().Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyLomographFilterInBox()
        {
            string path = this.CreateOutputDirectory("Lomograph");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("InBox");
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Lomograph(new Rectangle(image.Width / 4, image.Width / 4, image.Width / 2, image.Height / 2))
                         .Save(output);
                }
            }
        }
    }
}
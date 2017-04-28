// <copyright file="KodachromeTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;

    public class KodachromeTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyKodachromeFilter()
        {
            string path = this.CreateOutputDirectory("Kodachrome");

            foreach (TestFile file in Files)
            {
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Kodachrome().Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyKodachromeFilterInBox()
        {
            string path = this.CreateOutputDirectory("Kodachrome");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("InBox");
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Kodachrome(new Rectangle(10, 10, image.Width / 2, image.Height / 2)).Save(output);
                }
            }
        }
    }
}
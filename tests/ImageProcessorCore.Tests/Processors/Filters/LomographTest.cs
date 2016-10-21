// <copyright file="LomographTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class LomographTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyLomographFilter()
        {
            const string path = "TestOutput/Lomograph";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Lomograph()
                          .Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyLomographFilterInBox()
        {
            const string path = "TestOutput/Lomograph";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("InBox");
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Lomograph(new Rectangle(image.Width / 4, image.Width / 4, image.Width / 2, image.Height / 2))
                          .Save(output);
                }
            }
        }
    }
}
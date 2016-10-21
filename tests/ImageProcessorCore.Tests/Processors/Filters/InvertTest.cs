// <copyright file="InvertTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class InvertTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyInvertFilter()
        {
            const string path = "TestOutput/Invert";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Invert()
                          .Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyInvertFilterInBox()
        {
            const string path = "TestOutput/Invert";
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
                    image.Invert(new Rectangle(10, 10, image.Width / 2, image.Height / 2))
                          .Save(output);
                }
            }
        }
    }
}
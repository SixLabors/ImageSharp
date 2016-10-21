// <copyright file="VignetteTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class VignetteTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyVignetteFilter()
        {
            const string path = "TestOutput/Vignette";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Vignette()
                          .Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyVignetteFilterColor()
        {
            const string path = "TestOutput/Vignette";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("Color");
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Vignette(Color.HotPink)
                          .Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyVignetteFilterRadius()
        {
            const string path = "TestOutput/Vignette";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName("Radius");
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Vignette(image.Width / 4, image.Height / 4)
                          .Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldApplyVignetteFilterInBox()
        {
            const string path = "TestOutput/Vignette";
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
                    image.Vignette(new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2))
                          .Save(output);
                }
            }
        }
    }
}
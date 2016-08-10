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

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileName(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Vignette()
                             .Save(output);
                    }
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

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-Color" + Path.GetExtension(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Vignette(Color.HotPink)
                             .Save(output);
                    }
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

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-Radius" + Path.GetExtension(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Vignette(image.Width / 4, image.Height / 4)
                             .Save(output);
                    }
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

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-InBox" + Path.GetExtension(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Vignette(new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2))
                             .Save(output);
                    }
                }
            }
        }
    }
}
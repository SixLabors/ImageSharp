// <copyright file="GlowTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class GlowTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyGlowFilter()
        {
            const string path = "TestOutput/Glow";
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
                        image.Glow()
                             .Save(output);
                    }
                }
            }
        }

        [Fact]
        public void ImageShouldApplyGlowFilterColor()
        {
            const string path = "TestOutput/Glow";
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
                        image.Glow(Color.HotPink)
                             .Save(output);
                    }
                }
            }
        }

        [Fact]
        public void ImageShouldApplyGlowFilterRadius()
        {
            const string path = "TestOutput/Glow";
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
                        image.Glow(image.Width / 4, image.Height / 4)
                             .Save(output);
                    }
                }
            }
        }

        [Fact]
        public void ImageShouldApplyGlowFilterInBox()
        {
            const string path = "TestOutput/Glow";
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
                        image.Glow(new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2))
                             .Save(output);
                    }
                }
            }
        }
    }
}
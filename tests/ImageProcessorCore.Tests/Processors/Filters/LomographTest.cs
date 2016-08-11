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

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileName(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Lomograph()
                             .Save(output);
                    }
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

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-InBox" + Path.GetExtension(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Lomograph(new Rectangle(image.Width / 4, image.Width / 4, image.Width / 2, image.Height / 2))
                             .Save(output);
                    }
                }
            }
        }
    }
}
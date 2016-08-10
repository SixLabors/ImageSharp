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

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileName(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Invert()
                             .Save(output);
                    }
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

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}-InBox{Path.GetExtension(file)}"))
                    {
                        image.Invert(new Rectangle(10, 10, image.Width / 2, image.Height / 2))
                             .Save(output);
                    }
                }
            }
        }
    }
}
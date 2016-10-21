// <copyright file="BlendTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class BlendTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyBlendFilter()
        {
            const string path = "TestOutput/Blend";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            Image blend;
            using (FileStream stream = File.OpenRead("TestImages/Formats/Bmp/Car.bmp"))
            {
                blend = new Image(stream);
            }

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Blend(blend)
                          .Save(output);
                }
            }
        }
    }
}
// <copyright file="CropTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class CropTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyCropSampler()
        {
            const string path = "TestOutput/Crop";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Crop(image.Width / 2, image.Height / 2)
                          .Save(output);
                }
            }
        }
    }
}
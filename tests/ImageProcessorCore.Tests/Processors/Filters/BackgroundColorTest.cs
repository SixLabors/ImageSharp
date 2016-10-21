// <copyright file="BackgroundColorTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class BackgroundColorTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyBackgroundColorFilter()
        {
            const string path = "TestOutput/BackgroundColor";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.BackgroundColor(Color.HotPink)
                          .Save(output);
                }
            }
        }
    }
}
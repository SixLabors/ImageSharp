// <copyright file="KodachromeTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class KodachromeTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyKodachromeFilter()
        {
            const string path = "TestOutput/Kodachrome";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Kodachrome()
                          .Save(output);
                }
            }
        }
    }
}
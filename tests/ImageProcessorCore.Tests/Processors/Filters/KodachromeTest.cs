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
            string path = CreateOutputDirectory("Kodachrome");

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
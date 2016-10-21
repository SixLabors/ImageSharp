// <copyright file="PngTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Formats;

    using Xunit;

    public class PngTests : FileTestBase
    {
        [Fact]
        public void ImageCanSaveIndexedPng()
        {
            string path = CreateOutputDirectory("Png");

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileNameWithoutExtension}.png"))
                {
                    image.Quality = 256;
                    image.Save(output, new PngFormat());
                }
            }
        }
    }
}
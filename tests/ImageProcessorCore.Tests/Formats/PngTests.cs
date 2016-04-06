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
            if (!Directory.Exists("TestOutput/Encode/Png"))
            {
                Directory.CreateDirectory("TestOutput/Encode/Png");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    using (Image image = new Image(stream))
                    {
                        using (FileStream output = File.OpenWrite($"TestOutput/Encode/Png/{Path.GetFileNameWithoutExtension(file)}.png"))
                        {
                            image.Quality = 256;
                            image.Save(output, new PngFormat());
                        }
                    }
                }
            }
        }
    }
}
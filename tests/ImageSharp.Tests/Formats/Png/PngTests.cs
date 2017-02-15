// <copyright file="PngTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using ImageSharp.Formats;

namespace ImageSharp.Tests
{
    using System.IO;
    using System.Threading.Tasks;

    using Formats;

    using Xunit;

    public class PngTests : FileTestBase
    {
        [Fact]
        public void ImageCanSaveIndexedPng()
        {
            string path = CreateOutputDirectory("Png", "Indexed");

            foreach (TestFile file in Files)
            {
                using (Image image = file.CreateImage())
                {
                    using (FileStream output = File.OpenWrite($"{path}/{file.FileNameWithoutExtension}.png"))
                    {
                        image.MetaData.Quality = 256;
                        image.Save(output, new PngFormat());
                    }
                }
            }
        }

        [Fact]
        public void ImageCanSavePngInParallel()
        {
            string path = this.CreateOutputDirectory("Png");

            Parallel.ForEach(
                Files,
                file =>
                    {
                        using (Image image = file.CreateImage())
                        {
                            using (FileStream output = File.OpenWrite($"{path}/{file.FileNameWithoutExtension}.png"))
                            {
                                image.SaveAsPng(output);
                            }
                        }
                    });
        }
    }
}
// <copyright file="PixelateTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class PixelateTest : FileTestBase
    {
        public static readonly TheoryData<int> PixelateValues
        = new TheoryData<int>
        {
            4 ,
            8
        };

        [Theory]
        [MemberData(nameof(PixelateValues))]
        public void ImageShouldApplyPixelateFilter(int value)
        {
            string path = CreateOutputDirectory("Pixelate");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Pixelate(value)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(PixelateValues))]
        public void ImageShouldApplyPixelateFilterInBox(int value)
        {
            string path = CreateOutputDirectory("Pixelate");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value + "-InBox");
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Pixelate(value, new Rectangle(10, 10, image.Width / 2, image.Height / 2))
                          .Save(output);
                }
            }
        }
    }
}
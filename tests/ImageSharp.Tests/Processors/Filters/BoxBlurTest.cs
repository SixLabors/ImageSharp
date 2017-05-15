// <copyright file="BoxBlurTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using ImageSharp.PixelFormats;

    using Xunit;

    public class BoxBlurTest : FileTestBase
    {
        public static readonly TheoryData<int> BoxBlurValues
        = new TheoryData<int>
        {
            3 ,
            5 ,
        };

        [Theory]
        [MemberData(nameof(BoxBlurValues))]
        public void ImageShouldApplyBoxBlurFilter(int value)
        {
            string path = this.CreateOutputDirectory("BoxBlur");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value);
                using (Image<Rgba32> image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.BoxBlur(value).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(BoxBlurValues))]
        public void ImageShouldApplyBoxBlurFilterInBox(int value)
        {
            string path = this.CreateOutputDirectory("BoxBlur");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value + "-InBox");
                using (Image<Rgba32> image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.BoxBlur(value, new Rectangle(10, 10, image.Width / 2, image.Height / 2)).Save(output);
                }
            }
        }
    }
}
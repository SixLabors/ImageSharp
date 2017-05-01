// <copyright file="AlphaTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;

    public class AlphaTest : FileTestBase
    {
        public static readonly TheoryData<float> AlphaValues
        = new TheoryData<float>
        {
            20/100f ,
            80/100f
        };

        [Theory]
        [MemberData(nameof(AlphaValues))]
        public void ImageShouldApplyAlphaFilter(int value)
        {
            string path = this.CreateOutputDirectory("Alpha");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Alpha(value).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(AlphaValues))]
        public void ImageShouldApplyAlphaFilterInBox(int value)
        {
            string path = this.CreateOutputDirectory("Alpha");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value + "-InBox");
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Alpha(value, new Rectangle(10, 10, image.Width / 2, image.Height / 2)).Save(output);
                }
            }
        }
    }
}
// <copyright file="GaussianBlurTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;

    public class GaussianBlurTest : FileTestBase
    {
        public static readonly TheoryData<int> GaussianBlurValues
        = new TheoryData<int>
        {
            3 ,
            5 ,
        };

        [Theory]
        [MemberData(nameof(GaussianBlurValues))]
        public void ImageShouldApplyGaussianBlurFilter(int value)
        {
            string path = this.CreateOutputDirectory("GaussianBlur");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.GaussianBlur(value).Save(output);
                }
            }
        }
    }
}
// <copyright file="BoxBlurTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

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
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.BoxBlur(value).Save(output);
                }
            }
        }
    }
}
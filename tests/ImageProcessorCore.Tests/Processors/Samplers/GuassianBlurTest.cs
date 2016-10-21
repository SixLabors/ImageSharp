// <copyright file="GuassianBlurTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class GuassianBlurTest : FileTestBase
    {
        public static readonly TheoryData<int> GuassianBlurValues
        = new TheoryData<int>
        {
            3 ,
            5 ,
        };

        [Theory]
        [MemberData("GuassianBlurValues")]
        public void ImageShouldApplyGuassianBlurFilter(int value)
        {
            string path = CreateOutputDirectory("GuassianBlur");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.GuassianBlur(value)
                          .Save(output);
                }
            }
        }
    }
}
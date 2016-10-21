// <copyright file="BoxBlurTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
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
        [MemberData("BoxBlurValues")]
        public void ImageShouldApplyBoxBlurFilter(int value)
        {
            const string path = "TestOutput/BoxBlur";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.BoxBlur(value)
                          .Save(output);
                }
            }
        }
    }
}
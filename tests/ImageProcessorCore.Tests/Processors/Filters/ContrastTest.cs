// <copyright file="ContrastTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class ContrastTest : FileTestBase
    {
        public static readonly TheoryData<int> ContrastValues
        = new TheoryData<int>
        {
            50 ,
           -50 ,
        };

        [Theory]
        [MemberData("ContrastValues")]
        public void ImageShouldApplyContrastFilter(int value)
        {
            string path = CreateOutputDirectory("Contrast");

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Contrast(value)
                          .Save(output);
                }
            }
        }
    }
}
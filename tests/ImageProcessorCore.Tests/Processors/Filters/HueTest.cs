// <copyright file="HueTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class HueTest : FileTestBase
    {
        public static readonly TheoryData<int> HueValues
        = new TheoryData<int>
        {
            180 ,
           -180 ,
        };

        [Theory]
        [MemberData("HueValues")]
        public void ImageShouldApplyHueFilter(int value)
        {
            const string path = "TestOutput/Hue";
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
                    image.Hue(value)
                          .Save(output);
                }
            }
        }
    }
}
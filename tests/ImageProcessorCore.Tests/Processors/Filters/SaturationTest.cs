// <copyright file="SaturationTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class SaturationTest : FileTestBase
    {
        public static readonly TheoryData<int> SaturationValues
        = new TheoryData<int>
        {
            50 ,
           -50 ,
        };

        [Theory]
        [MemberData("SaturationValues")]
        public void ImageShouldApplySaturationFilter(int value)
        {
            const string path = "TestOutput/Saturation";
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
                    image.Saturation(value)
                          .Save(output);
                }
            }
        }
    }
}
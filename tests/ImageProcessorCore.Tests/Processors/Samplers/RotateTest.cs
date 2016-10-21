// <copyright file="RotateTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class RotateTest : FileTestBase
    {
        public static readonly TheoryData<float> RotateFloatValues
            = new TheoryData<float>
        {
             170 ,
            -170 ,
        };

        public static readonly TheoryData<RotateType> RotateEnumValues
            = new TheoryData<RotateType>
        {
            RotateType.None,
            RotateType.Rotate90,
            RotateType.Rotate180,
            RotateType.Rotate270
        };

        [Theory]
        [MemberData("RotateFloatValues")]
        public void ImageShouldApplyRotateSampler(float value)
        {
            const string path = "TestOutput/Rotate";
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
                    image.Rotate(value)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData("RotateEnumValues")]
        public void ImageShouldApplyRotateSampler(RotateType value)
        {
            const string path = "TestOutput/Rotate";
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
                    image.Rotate(value)
                          .Save(output);
                }
            }
        }
    }
}
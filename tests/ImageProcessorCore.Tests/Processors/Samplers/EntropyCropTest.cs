// <copyright file="EntropyCropTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class EntropyCropTest : FileTestBase
    {
        public static readonly TheoryData<float> EntropyCropValues
        = new TheoryData<float>
        {
            .25f ,
            .75f ,
        };

        [Theory]
        [MemberData("EntropyCropValues")]
        public void ImageShouldApplyEntropyCropSampler(float value)
        {
            const string path = "TestOutput/EntropyCrop";
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
                    image.EntropyCrop(value)
                          .Save(output);
                }
            }
        }
    }
}
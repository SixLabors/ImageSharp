// <copyright file="EntropyCropTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
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
        [MemberData(nameof(EntropyCropValues))]
        public void ImageShouldApplyEntropyCropSampler(float value)
        {
            string path = this.CreateOutputDirectory("EntropyCrop");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.EntropyCrop(value).Save(output);
                }
            }
        }
    }
}
// <copyright file="SaturationTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
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
        [MemberData(nameof(SaturationValues))]
        public void ImageShouldApplySaturationFilter(int value)
        {
            string path = CreateOutputDirectory("Saturation");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Saturation(value).Save(output);
                }
            }
        }
    }
}
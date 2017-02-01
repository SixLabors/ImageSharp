// <copyright file="GrayscaleTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;
    using ImageSharp.Processing;

    public class GrayscaleTest : FileTestBase
    {
        public static readonly TheoryData<GrayscaleMode> GrayscaleValues
        = new TheoryData<GrayscaleMode>
        {
            GrayscaleMode.Bt709 ,
            GrayscaleMode.Bt601 ,
        };

        [Theory]
        [MemberData(nameof(GrayscaleValues))]
        public void ImageShouldApplyGrayscaleFilter(GrayscaleMode value)
        {
            string path = this.CreateOutputDirectory("Grayscale");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(value);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Grayscale(value).Save(output);
                }
            }
        }
    }
}
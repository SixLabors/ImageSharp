// <copyright file="BlendTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class BlendTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyBlendFilter()
        {
            string path = CreateOutputDirectory("Blend");

            Image blend;
            using (FileStream stream = File.OpenRead(TestImages.Bmp.Car))
            {
                blend = new Image(stream);
            }

            foreach (TestFile file in Files)
            {
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Blend(blend)
                          .Save(output);
                }
            }
        }
    }
}
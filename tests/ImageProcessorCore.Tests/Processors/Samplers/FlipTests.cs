// <copyright file="RotateFlipTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class FlipTests : FileTestBase
    {
        public static readonly TheoryData<FlipType> FlipValues
            = new TheoryData<FlipType>
        {
            { FlipType.None },
            { FlipType.Vertical },
            { FlipType.Horizontal },
        };

        [Theory]
        [MemberData("FlipValues")]
        public void ImageShouldFlip(FlipType flipType)
        {
            string path = CreateOutputDirectory("Flip");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(flipType);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Flip(flipType)
                          .Save(output);
                }
            }
        }
    }
}
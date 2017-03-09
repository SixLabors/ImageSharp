// <copyright file="RotateFlipTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using Processing;
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
        [MemberData(nameof(FlipValues))]
        public void ImageShouldFlip(FlipType flipType)
        {
            string path = this.CreateOutputDirectory("Flip");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(flipType);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Flip(flipType).Save(output);
                }
            }
        }
    }
}
// <copyright file="RotateFlipTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using Processing;
    using Xunit;

    public class RotateFlipTest : FileTestBase
    {
        public static readonly TheoryData<RotateType, FlipType> RotateFlipValues
            = new TheoryData<RotateType, FlipType>
        {
            { RotateType.None, FlipType.Vertical },
            { RotateType.None, FlipType.Horizontal },
            { RotateType.Rotate90, FlipType.None },
            { RotateType.Rotate180, FlipType.None },
            { RotateType.Rotate270, FlipType.None },
        };

        [Theory]
        [MemberData(nameof(RotateFlipValues))]
        public void ImageShouldRotateFlip(RotateType rotateType, FlipType flipType)
        {
            string path = this.CreateOutputDirectory("RotateFlip");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(rotateType + "-" + flipType);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.RotateFlip(rotateType, flipType).Save(output);
                }
            }
        }
    }
}
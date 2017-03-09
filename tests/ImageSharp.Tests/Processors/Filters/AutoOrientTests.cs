// <copyright file="RotateFlipTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using Processing;
    using Xunit;

    public class AutoOrientTests : FileTestBase
    {
        public static readonly TheoryData<RotateType, FlipType, ushort> OrientationValues
            = new TheoryData<RotateType, FlipType, ushort>
        {
            { RotateType.None,      FlipType.None,       0 },
            { RotateType.None,      FlipType.None,       1 },
            { RotateType.None,      FlipType.Horizontal, 2 },
            { RotateType.Rotate180, FlipType.None,       3 },
            { RotateType.Rotate180, FlipType.Horizontal, 4 },
            { RotateType.Rotate90,  FlipType.Horizontal, 5 },
            { RotateType.Rotate270, FlipType.None,       6 },
            { RotateType.Rotate90,  FlipType.Vertical,   7 },
            { RotateType.Rotate90,  FlipType.None,       8 },
        };

        [Theory]
        [MemberData(nameof(OrientationValues))]
        public void ImageShouldFlip(RotateType rotateType, FlipType flipType, ushort orientation)
        {
            string path = this.CreateOutputDirectory("AutoOrient");

            TestFile file = TestFile.Create(TestImages.Bmp.F);

            using (Image image = file.CreateImage())
            {
                image.MetaData.ExifProfile = new ExifProfile();
                image.MetaData.ExifProfile.SetValue(ExifTag.Orientation, orientation);

                using (FileStream before = File.OpenWrite($"{path}/before-{file.FileName}"))
                using (FileStream after = File.OpenWrite($"{path}/after-{file.FileName}"))
                {
                    image.RotateFlip(rotateType, flipType).Save(before).AutoOrient().Save(after);
                }
            }
        }
    }
}
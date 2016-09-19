// <copyright file="RotateFlipTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

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
        [MemberData("RotateFlipValues")]
        public void ImageShouldRotateFlip(RotateType rotateType, FlipType flipType)
        {
            const string path = "TestOutput/RotateFlip";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + rotateType + flipType + Path.GetExtension(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.RotateFlip(rotateType, flipType)
                             .Save(output);
                    }
                }
            }
        }
    }
}
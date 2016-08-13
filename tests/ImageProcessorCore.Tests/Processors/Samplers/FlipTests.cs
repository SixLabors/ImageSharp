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
            const string path = "TestOutput/Flip";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + flipType + Path.GetExtension(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Flip(flipType, this.ProgressUpdate)
                             .Save(output);
                    }
                }
            }
        }
    }
}
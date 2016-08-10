// <copyright file="AlphaTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class AlphaTest : FileTestBase
    {
        public static readonly TheoryData<int> AlphaValues
        = new TheoryData<int>
        {
            20 ,
            80
        };

        [Theory]
        [MemberData("AlphaValues")]
        public void ImageShouldApplyAlphaFilter(int value)
        {
            const string path = "TestOutput/Alpha";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + value + Path.GetExtension(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Alpha(value)
                             .Save(output);
                    }
                }
            }
        }

        [Theory]
        [MemberData("AlphaValues")]
        public void ImageShouldApplyAlphaFilterInBox(int value)
        {
            const string path = "TestOutput/Alpha";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + value + "-InBox" + Path.GetExtension(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Alpha(value, new Rectangle(10, 10, image.Width / 2, image.Height / 2))
                             .Save(output);
                    }
                }
            }
        }
    }
}
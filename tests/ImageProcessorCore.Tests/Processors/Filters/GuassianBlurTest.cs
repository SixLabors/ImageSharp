// <copyright file="GuassianBlurTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class GuassianBlurTest : FileTestBase
    {
        public static readonly TheoryData<int> GuassianBlurValues
        = new TheoryData<int>
        {
            3 ,
            5 ,
        };

        [Theory]
        [MemberData("GuassianBlurValues")]
        public void ImageShouldApplyGuassianBlurFilter(int value)
        {
            const string path = "TestOutput/GuassianBlur";
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
                        image.GuassianBlur(value)
                             .Save(output);
                    }
                }
            }
        }
    }
}
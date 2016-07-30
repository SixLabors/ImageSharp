// <copyright file="BrightnessTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class BrightnessTest : FileTestBase
    {
        public static readonly TheoryData<int> BrightnessValues
        = new TheoryData<int>
        {
            50 ,
           -50 ,
        };

        [Theory]
        [MemberData("BrightnessValues")]
        public void ImageShouldApplyBrightnessFilter(int value)
        {
            const string path = "TestOutput/Brightness";
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
                        image.Brightness(value)
                             .Save(output);
                    }
                }
            }
        }
    }
}
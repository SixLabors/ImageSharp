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
            const string path = "TestOutput/Blend";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            Image blend;
            using (FileStream stream = File.OpenRead("TestImages/Formats/Bmp/Car.bmp"))
            {
                blend = new Image(stream);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileName(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Blend(blend)
                             .Save(output);
                    }
                }
            }
        }
    }
}
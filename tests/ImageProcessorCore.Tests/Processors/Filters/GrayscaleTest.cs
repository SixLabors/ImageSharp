// <copyright file="GrayscaleTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using Processors;
    using System.IO;

    using Xunit;

    public class GrayscaleTest : FileTestBase
    {
        public static readonly TheoryData<GrayscaleMode> GrayscaleValues
        = new TheoryData<GrayscaleMode>
        {
            GrayscaleMode.Bt709 ,
            GrayscaleMode.Bt601 ,
        };

        [Theory]
        [MemberData("GrayscaleValues")]
        public void ImageShouldApplyGrayscaleFilter(GrayscaleMode value)
        {
            const string path = "TestOutput/Grayscale";
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
                        image.Grayscale(value)
                             .Save(output);
                    }
                }
            }
        }
    }
}
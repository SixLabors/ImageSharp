// <copyright file="GreyscaleTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using Processors;
    using System.IO;

    using Xunit;

    public class GreyscaleTest : FileTestBase
    {
        public static readonly TheoryData<GreyscaleMode> GreyscaleValues
        = new TheoryData<GreyscaleMode>
        {
            GreyscaleMode.Bt709 ,
            GreyscaleMode.Bt601 ,
        };

        [Theory]
        [MemberData("GreyscaleValues")]
        public void ImageShouldApplyGreyscaleFilter(GreyscaleMode value)
        {
            const string path = "TestOutput/Greyscale";
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
                        image.Greyscale(value)
                             .Save(output);
                    }
                }
            }
        }
    }
}
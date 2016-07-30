// <copyright file="BlackWhiteTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class BlackWhiteTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyBlackWhiteFilter()
        {
            const string path = "TestOutput/BlackWhite";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileName(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.BlackWhite()
                             .Save(output);
                    }
                }
            }
        }
    }
}
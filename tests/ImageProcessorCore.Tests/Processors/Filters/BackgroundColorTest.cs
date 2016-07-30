// <copyright file="BackgroundColorTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class BackgroundColorTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyBackgroundColorFilter()
        {
            const string path = "TestOutput/BackgroundColor";
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
                        image.BackgroundColor(Color.HotPink)
                             .Save(output);
                    }
                }
            }
        }
    }
}
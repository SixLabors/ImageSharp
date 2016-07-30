// <copyright file="PadTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class PadTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyPadSampler()
        {
            const string path = "TestOutput/Pad";
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
                        image.Pad(image.Width + 50, image.Height + 50, this.ProgressUpdate)
                             .Save(output);
                    }
                }
            }
        }
    }
}
// <copyright file="PadTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;

    public class PadTest : FileTestBase
    {
        [Fact]
        public void ImageShouldApplyPadSampler()
        {
            string path = this.CreateOutputDirectory("Pad");

            foreach (TestFile file in Files)
            {
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{file.FileName}"))
                {
                    image.Pad(image.Width + 50, image.Height + 50).Save(output);
                }
            }
        }
    }
}
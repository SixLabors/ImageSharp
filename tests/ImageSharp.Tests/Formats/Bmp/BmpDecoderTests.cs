// <copyright file="GifDecoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;

    public class BmpDecoderTests
    {
        [Theory]
        [InlineData(TestImages.Bmp.Car, 24)]
        [InlineData(TestImages.Bmp.F, 24)]
        [InlineData(TestImages.Bmp.NegHeight, 24)]
        [InlineData(TestImages.Bmp.Bpp8, 8)]
        public void DetectPixelSize(string imagePath, int expectedPixelSize)
        {
            TestFile testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                Assert.Equal(expectedPixelSize, Image.DetectPixelSize(stream));
            }
        }
    }
}

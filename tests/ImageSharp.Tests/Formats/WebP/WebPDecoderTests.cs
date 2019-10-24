// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    using static SixLabors.ImageSharp.Tests.TestImages.WebP;

    public class WebPDecoderTests
    {
        [Theory]
        [InlineData(Lossless.Lossless1, 1000, 307, 24)]
        [InlineData(Lossless.Lossless2, 1000, 307, 24)]
        [InlineData(Lossy.Alpha.LossyAlpha1, 1000, 307, 24)]
        [InlineData(Lossy.Alpha.LossyAlpha2, 1000, 307, 24)]
        [InlineData(Animated.Animated1, 400, 400, 24)]
        public void Identify_DetectsCorrectDimensions(string imagePath, int expectedWidth, int expectedHeight, int expectedBitsPerPixel)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo imageInfo = Image.Identify(stream);
                Assert.NotNull(imageInfo);
                Assert.Equal(expectedWidth, imageInfo.Width);
                Assert.Equal(expectedHeight, imageInfo.Height);
                Assert.Equal(expectedBitsPerPixel, imageInfo.PixelType.BitsPerPixel);
            }
        }
    }
}

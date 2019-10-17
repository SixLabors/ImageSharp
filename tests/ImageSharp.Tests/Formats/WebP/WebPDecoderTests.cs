// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    using SixLabors.ImageSharp.Metadata;
    using static SixLabors.ImageSharp.Tests.TestImages.WebP;
    using static TestImages.Bmp;

    public class WebPDecoderTests
    {
        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new TheoryData<string, int, int, PixelResolutionUnit>
        {
            { Car, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { V5Header, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { RLE8, 2835, 2835, PixelResolutionUnit.PixelsPerMeter }
        };

        [Theory]
        [InlineData(Lossless.Lossless1, 1000, 307)]
        public void Identify_DetectsCorrectDimensions(string imagePath, int expectedWidth, int expectedHeight)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo imageInfo = Image.Identify(stream);
                Assert.NotNull(imageInfo);
                Assert.Equal(expectedWidth, imageInfo.Width);
                Assert.Equal(expectedHeight, imageInfo.Height);
            }
        }
    }
}

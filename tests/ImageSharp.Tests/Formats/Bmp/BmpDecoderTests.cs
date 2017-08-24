// <copyright file="BmpDecoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using ImageSharp.Formats;

namespace ImageSharp.Tests
{
    using ImageSharp.PixelFormats;

    using Xunit;

    public class BmpDecoderTests : FileTestBase
    {
        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Rgb24)]
        public void OpenAllBmpFiles_SaveBmp<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                provider.Utility.SaveTestOutputFile(image, "bmp");
            }
        }

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
                Assert.Equal(expectedPixelSize, Image.DetectPixelType(stream)?.BitsPerPixel);
            }
        }
    }
}
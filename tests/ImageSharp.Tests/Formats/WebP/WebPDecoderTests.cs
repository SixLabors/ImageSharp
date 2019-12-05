// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.WebP;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

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

        [Theory]
        [InlineData(Lossy.Alpha.LossyAlpha1, 1000, 307, 24)]
        public void DecodeLossyImage_Tmp(string imagePath, int expectedWidth, int expectedHeight, int expectedBitsPerPixel)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var image = Image.Load(stream);
                Assert.Equal(expectedWidth, image.Width);
                Assert.Equal(expectedHeight, image.Height);
            }
        }

        [Theory]
        [WithFile(Lossless.LosslessNoTransform1, PixelTypes.Rgba32)]
        [WithFile(Lossless.LosslessNoTransform2, PixelTypes.Rgba32)]
        public void WebpDecoder_CanDecode_Lossless_WithoutTransforms<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new WebPDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }
    }
}

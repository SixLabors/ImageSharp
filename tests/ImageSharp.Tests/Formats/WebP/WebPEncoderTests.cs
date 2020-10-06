// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.WebP;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    using static TestImages.WebP;

    public class WebPEncoderTests
    {
        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, 100)]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, 80)]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, 20)]
        public void Encode_Lossless_WithDifferentQuality_Works<TPixel>(TestImageProvider<TPixel> provider, int quality)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebPEncoder()
            {
                Lossy = false,
                Quality = quality
            };

            using (Image<TPixel> image = provider.GetImage())
            {
                var testOutputDetails = string.Concat("lossless", "_q", quality);
                image.VerifyEncoder(provider, "webp", testOutputDetails, encoder);
            }
        }

        [Theory]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6)]
        public void Encode_Lossless_WithDifferentMethods_Works<TPixel>(TestImageProvider<TPixel> provider, int method)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebPEncoder()
            {
                Lossy = false,
                Method = method,
                Quality = 100
            };

            using (Image<TPixel> image = provider.GetImage())
            {
                var testOutputDetails = string.Concat("lossless", "_m", method);
                image.VerifyEncoder(provider, "webp", testOutputDetails, encoder);
            }
        }
    }
}

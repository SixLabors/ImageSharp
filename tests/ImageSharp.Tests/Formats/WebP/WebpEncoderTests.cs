// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;
using static SixLabors.ImageSharp.Tests.TestImages.WebP;

namespace SixLabors.ImageSharp.Tests.Formats.Webp
{
    [Trait("Format", "Webp")]
    public class WebpEncoderTests
    {
        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, 100)]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, 80)]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, 20)]
        public void Encode_Lossless_WithDifferentQuality_Works<TPixel>(TestImageProvider<TPixel> provider, int quality)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoder()
            {
                Lossy = false,
                Quality = quality
            };

            using Image<TPixel> image = provider.GetImage();
            var testOutputDetails = string.Concat("lossless", "_q", quality);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder);
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
            var encoder = new WebpEncoder()
            {
                Lossy = false,
                Method = method,
                Quality = 75
            };

            using Image<TPixel> image = provider.GetImage();
            var testOutputDetails = string.Concat("lossless", "_m", method);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder);
        }

        [Theory]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 20)]
        public void Encode_Lossy_WithDifferentQuality_Works<TPixel>(TestImageProvider<TPixel> provider, int quality)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoder()
            {
                Lossy = true,
                Quality = quality
            };

            using Image<TPixel> image = provider.GetImage();
            var testOutputDetails = string.Concat("lossy", "_q", quality);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(quality));
        }

        [Theory]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6)]
        public void Encode_Lossy_WithDifferentMethods_Works<TPixel>(TestImageProvider<TPixel> provider, int method)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int quality = 75;
            var encoder = new WebpEncoder()
            {
                Lossy = true,
                Method = method,
                Quality = quality
            };

            using Image<TPixel> image = provider.GetImage();
            var testOutputDetails = string.Concat("lossy", "_m", method);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(quality));
        }

        private static ImageComparer GetComparer(int quality)
        {
            float tolerance = 0.01f; // ~1.0%

            if (quality < 30)
            {
                tolerance = 0.02f; // ~2.0%
            }

            return ImageComparer.Tolerant(tolerance);
        }
    }
}

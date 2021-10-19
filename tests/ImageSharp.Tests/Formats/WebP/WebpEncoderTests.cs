// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;
using static SixLabors.ImageSharp.Tests.TestImages.Webp;

namespace SixLabors.ImageSharp.Tests.Formats.Webp
{
    [Collection("RunSerial")]
    [Trait("Format", "Webp")]
    public class WebpEncoderTests
    {
        [Theory]
        [WithFile(Flag, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.PalettedTwoColor, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Paletted256Colors, PixelTypes.Rgba32)]
        public void Encode_Lossless_WithPalette_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoder()
            {
                Lossy = false,
                Quality = 100,
                Method = WebpEncodingMethod.BestQuality
            };

            using Image<TPixel> image = provider.GetImage();
            image.VerifyEncoder(provider, "webp", string.Empty, encoder);
        }

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
            string testOutputDetails = string.Concat("lossless", "_q", quality);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder);
        }

        [Theory]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6, 100)]
        public void Encode_Lossless_WithDifferentMethodAndQuality_Works<TPixel>(TestImageProvider<TPixel> provider, WebpEncodingMethod method, int quality)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoder()
            {
                Lossy = false,
                Method = method,
                Quality = quality
            };

            using Image<TPixel> image = provider.GetImage();
            string testOutputDetails = string.Concat("lossless", "_m", method, "_q", quality);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder);
        }

        [Theory]
        [WithFile(RgbTestPattern100x100, PixelTypes.Rgba32, 85)]
        [WithFile(RgbTestPattern100x100, PixelTypes.Rgba32, 60)]
        [WithFile(RgbTestPattern80x80, PixelTypes.Rgba32, 40)]
        [WithFile(RgbTestPattern80x80, PixelTypes.Rgba32, 20)]
        [WithFile(RgbTestPattern80x80, PixelTypes.Rgba32, 10)]
        [WithFile(RgbTestPattern63x63, PixelTypes.Rgba32, 40)]
        public void Encode_Lossless_WithNearLosslessFlag_Works<TPixel>(TestImageProvider<TPixel> provider, int nearLosslessQuality)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoder()
            {
                Lossy = false,
                NearLossless = true,
                NearLosslessQuality = nearLosslessQuality
            };

            using Image<TPixel> image = provider.GetImage();
            string testOutputDetails = string.Concat("nearlossless", "_q", nearLosslessQuality);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(nearLosslessQuality));
        }

        [Theory]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6)]
        [WithFile(Lossy.Alpha1, PixelTypes.Rgba32, 4)]
        public void Encode_Lossless_WithExactFlag_Works<TPixel>(TestImageProvider<TPixel> provider, WebpEncodingMethod method)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoder()
            {
                Lossy = false,
                Method = method,
                Exact = true
            };

            using Image<TPixel> image = provider.GetImage();
            string testOutputDetails = string.Concat("lossless", "_m", method);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder);
        }

        [Theory]
        [WithFile(TestPatternOpaque, PixelTypes.Rgba32)]
        [WithFile(TestPatternOpaqueSmall, PixelTypes.Rgba32)]
        public void Encode_Lossless_WorksWithTestPattern<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();

            var encoder = new WebpEncoder() { Lossy = false };
            image.VerifyEncoder(provider, "webp", string.Empty, encoder);
        }

        [Fact]
        public void Encode_Lossless_OneByOnePixel_Works()
        {
            // Just make sure, encoding 1 pixel by 1 pixel does not throw an exception.
            using var image = new Image<Rgba32>(1, 1);
            var encoder = new WebpEncoder() { Lossy = false };
            using (var memStream = new MemoryStream())
            {
                image.SaveAsWebp(memStream, encoder);
            }
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
            string testOutputDetails = string.Concat("lossy", "_q", quality);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(quality));
        }

        [Theory]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 80)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 50)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 30)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 10)]
        public void Encode_Lossy_WithDifferentFilterStrength_Works<TPixel>(TestImageProvider<TPixel> provider, int filterStrength)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoder()
            {
                Lossy = true,
                FilterStrength = filterStrength
            };

            using Image<TPixel> image = provider.GetImage();
            string testOutputDetails = string.Concat("lossy", "_f", filterStrength);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(75));
        }

        [Theory]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 80)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 50)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 30)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 10)]
        public void Encode_Lossy_WithDifferentSpatialNoiseShapingStrength_Works<TPixel>(TestImageProvider<TPixel> provider, int snsStrength)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoder()
            {
                Lossy = true,
                SpatialNoiseShaping = snsStrength
            };

            using Image<TPixel> image = provider.GetImage();
            string testOutputDetails = string.Concat("lossy", "_sns", snsStrength);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(75));
        }

        [Theory]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6, 75)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5, 100)]
        [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6, 100)]
        public void Encode_Lossy_WithDifferentMethodsAndQuality_Works<TPixel>(TestImageProvider<TPixel> provider, WebpEncodingMethod method, int quality)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoder()
            {
                Lossy = true,
                Method = method,
                Quality = quality
            };

            using Image<TPixel> image = provider.GetImage();
            string testOutputDetails = string.Concat("lossy", "_m", method, "_q", quality);
            image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(quality));
        }

        [Theory]
        [WithFile(TestPatternOpaque, PixelTypes.Rgba32)]
        [WithFile(TestPatternOpaqueSmall, PixelTypes.Rgba32)]
        public void Encode_Lossy_WorksWithTestPattern<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();

            var encoder = new WebpEncoder() { Lossy = true };
            image.VerifyEncoder(provider, "webp", string.Empty, encoder, ImageComparer.Tolerant(0.04f));
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

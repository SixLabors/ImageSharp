// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

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
        [WithFile(Lossless.NoTransform1, PixelTypes.Rgba32)]
        [WithFile(Lossless.NoTransform2, PixelTypes.Rgba32)]
        public void WebpDecoder_CanDecode_Lossless_WithoutTransforms<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new WebPDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(Lossless.GreenTransform1, PixelTypes.Rgba32)]
        [WithFile(Lossless.GreenTransform2, PixelTypes.Rgba32)]
        [WithFile(Lossless.GreenTransform3, PixelTypes.Rgba32)]
        [WithFile(Lossless.GreenTransform4, PixelTypes.Rgba32)]
        [WithFile(Lossless.GreenTransform5, PixelTypes.Rgba32)]
        [WithFile(Lossless.GreenTransform6, PixelTypes.Rgba32)]
        public void WebpDecoder_CanDecode_Lossless_WithSubstractGreenTransform<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new WebPDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(Lossless.ColorIndexTransform1, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform2, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform3, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform4, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform5, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform6, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform7, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform8, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform9, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform10, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform11, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform12, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform13, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform14, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform15, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform16, PixelTypes.Rgba32)]
        [WithFile(Lossless.ColorIndexTransform17, PixelTypes.Rgba32)]
        public void WebpDecoder_CanDecode_Lossless_WithColorIndexTransform<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new WebPDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(Lossless.PredictorTransform1, PixelTypes.Rgba32)]
        [WithFile(Lossless.PredictorTransform2, PixelTypes.Rgba32)]
        [WithFile(Lossless.PredictorTransform3, PixelTypes.Rgba32)]
        [WithFile(Lossless.PredictorTransform4, PixelTypes.Rgba32)]
        [WithFile(Lossless.PredictorTransform5, PixelTypes.Rgba32)]
        [WithFile(Lossless.PredictorTransform6, PixelTypes.Rgba32)]
        public void WebpDecoder_CanDecode_Lossless_WithPredictorTransform<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new WebPDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(Lossless.CrossColorTransform1, PixelTypes.Rgba32)]
        [WithFile(Lossless.CrossColorTransform2, PixelTypes.Rgba32)]
        public void WebpDecoder_CanDecode_Lossless_WithCrossColorTransform<TPixel>(TestImageProvider<TPixel> provider)
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

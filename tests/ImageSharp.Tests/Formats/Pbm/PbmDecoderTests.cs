// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;
using static SixLabors.ImageSharp.Tests.TestImages.Pbm;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Pbm
{
    [Trait("Format", "Pbm")]
    public class PbmDecoderTests
    {
        [Theory]
        [InlineData(BlackAndWhitePlain, PbmColorType.BlackAndWhite)]
        [InlineData(BlackAndWhiteBinary, PbmColorType.BlackAndWhite)]
        [InlineData(GrayscalePlain, PbmColorType.Grayscale)]
        [InlineData(GrayscaleBinary, PbmColorType.Grayscale)]
        [InlineData(GrayscaleBinaryWide, PbmColorType.Grayscale)]
        [InlineData(RgbPlain, PbmColorType.Rgb)]
        [InlineData(RgbBinary, PbmColorType.Rgb)]
        public void ImageLoadCanDecode(string imagePath, PbmColorType expectedColorType)
        {
            // Arrange
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            // Act
            using var image = Image.Load(stream);

            // Assert
            Assert.NotNull(image);
            PbmMetadata bitmapMetadata = image.Metadata.GetPbmMetadata();
            Assert.NotNull(bitmapMetadata);
            Assert.Equal(expectedColorType, bitmapMetadata.ColorType);
        }

        [Theory]
        [InlineData(BlackAndWhitePlain)]
        [InlineData(BlackAndWhiteBinary)]
        [InlineData(GrayscalePlain)]
        [InlineData(GrayscaleBinary)]
        [InlineData(GrayscaleBinaryWide)]
        public void ImageLoadL8CanDecode(string imagePath)
        {
            // Arrange
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            // Act
            using var image = Image.Load<L8>(stream);

            // Assert
            Assert.NotNull(image);
        }

        [Theory]
        [InlineData(RgbPlain)]
        [InlineData(RgbBinary)]
        public void ImageLoadRgb24CanDecode(string imagePath)
        {
            // Arrange
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            // Act
            using var image = Image.Load<Rgb24>(stream);

            // Assert
            Assert.NotNull(image);
        }

        [Theory]
        [WithFile(BlackAndWhitePlain, PixelTypes.L8, true)]
        [WithFile(BlackAndWhiteBinary, PixelTypes.L8, true)]
        [WithFile(GrayscalePlain, PixelTypes.L8, true)]
        [WithFile(GrayscalePlainNormalized, PixelTypes.L8, true)]
        [WithFile(GrayscaleBinary, PixelTypes.L8, true)]
        [WithFile(GrayscaleBinaryWide, PixelTypes.L16, true)]
        [WithFile(RgbPlain, PixelTypes.Rgb24, false)]
        [WithFile(RgbPlainNormalized, PixelTypes.Rgb24, false)]
        [WithFile(RgbBinary, PixelTypes.Rgb24, false)]
        public void DecodeReferenceImage<TPixel>(TestImageProvider<TPixel> provider, bool isGrayscale)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSave(provider);

            image.CompareToReferenceOutput(provider, grayscale: isGrayscale);
        }
    }
}

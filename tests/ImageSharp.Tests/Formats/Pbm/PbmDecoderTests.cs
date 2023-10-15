// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Text;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
using static SixLabors.ImageSharp.Tests.TestImages.Pbm;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Pbm
{
    [Trait("Format", "Pbm")]
    [ValidateDisposedMemoryAllocations]
    public class PbmDecoderTests
    {
        [Theory]
        [InlineData(BlackAndWhitePlain, PbmColorType.BlackAndWhite, PbmComponentType.Bit)]
        [InlineData(BlackAndWhiteBinary, PbmColorType.BlackAndWhite, PbmComponentType.Bit)]
        [InlineData(GrayscalePlain, PbmColorType.Grayscale, PbmComponentType.Byte)]
        [InlineData(GrayscalePlainMagick, PbmColorType.Grayscale, PbmComponentType.Byte)]
        [InlineData(GrayscaleBinary, PbmColorType.Grayscale, PbmComponentType.Byte)]
        [InlineData(GrayscaleBinaryWide, PbmColorType.Grayscale, PbmComponentType.Short)]
        [InlineData(RgbPlain, PbmColorType.Rgb, PbmComponentType.Byte)]
        [InlineData(RgbPlainMagick, PbmColorType.Rgb, PbmComponentType.Byte)]
        [InlineData(RgbBinary, PbmColorType.Rgb, PbmComponentType.Byte)]
        public void ImageLoadCanDecode(string imagePath, PbmColorType expectedColorType, PbmComponentType expectedComponentType)
        {
            // Arrange
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            // Act
            using var image = Image.Load(stream);

            // Assert
            Assert.NotNull(image);
            PbmMetadata metadata = image.Metadata.GetPbmMetadata();
            Assert.NotNull(metadata);
            Assert.Equal(expectedColorType, metadata.ColorType);
            Assert.Equal(expectedComponentType, metadata.ComponentType);
        }

        [Theory]
        [InlineData(BlackAndWhitePlain)]
        [InlineData(BlackAndWhiteBinary)]
        [InlineData(GrayscalePlain)]
        [InlineData(GrayscalePlainMagick)]
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
        [InlineData(RgbPlainMagick)]
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
        [WithFile(BlackAndWhitePlain, PixelTypes.L8, "pbm")]
        [WithFile(BlackAndWhiteBinary, PixelTypes.L8, "pbm")]
        [WithFile(GrayscalePlain, PixelTypes.L8, "pgm")]
        [WithFile(GrayscalePlainNormalized, PixelTypes.L8, "pgm")]
        [WithFile(GrayscaleBinary, PixelTypes.L8, "pgm")]
        [WithFile(GrayscaleBinaryWide, PixelTypes.L16, "pgm")]
        [WithFile(RgbPlain, PixelTypes.Rgb24, "ppm")]
        [WithFile(RgbPlainNormalized, PixelTypes.Rgb24, "ppm")]
        [WithFile(RgbBinary, PixelTypes.Rgb24, "ppm")]
        public void DecodeReferenceImage<TPixel>(TestImageProvider<TPixel> provider, string extension)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSave(provider, extension: extension);

            bool isGrayscale = extension is "pgm" or "pbm";
            image.CompareToReferenceOutput(provider, grayscale: isGrayscale);
        }


        [Fact]
        public void PlainText_PrematureEof()
        {
            byte[] bytes = Encoding.ASCII.GetBytes($"P1\n100 100\n1 0 1 0 1 0");
            using EofHitCounter eofHitCounter = EofHitCounter.RunDecoder(bytes);

            Assert.True(eofHitCounter.EofHitCount <= 2);
            Assert.Equal(new Size(100, 100), eofHitCounter.Image.Size());
        }

        [Fact]
        public void Binary_PrematureEof()
        {
            using EofHitCounter eofHitCounter = EofHitCounter.RunDecoder(RgbBinaryPrematureEof);

            Assert.True(eofHitCounter.EofHitCount <= 2);
            Assert.Equal(new Size(29, 30), eofHitCounter.Image.Size());
        }
    }
}

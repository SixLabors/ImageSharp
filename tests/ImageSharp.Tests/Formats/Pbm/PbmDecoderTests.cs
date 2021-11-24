// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Pbm;

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
        public void PpmDecoder_CanDecode(string imagePath, PbmColorType expectedColorType)
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
    }
}

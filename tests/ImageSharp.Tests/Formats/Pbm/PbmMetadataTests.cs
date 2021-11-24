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
    public class PbmMetadataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            var meta = new PbmMetadata { ColorType = PbmColorType.Grayscale };
            var clone = (PbmMetadata)meta.DeepClone();

            clone.ColorType = PbmColorType.Rgb;

            Assert.False(meta.ColorType.Equals(clone.ColorType));
        }

        [Theory]
        [InlineData(BlackAndWhitePlain, PbmEncoding.Plain)]
        [InlineData(BlackAndWhiteBinary, PbmEncoding.Binary)]
        [InlineData(GrayscaleBinary, PbmEncoding.Binary)]
        [InlineData(GrayscaleBinaryWide, PbmEncoding.Binary)]
        [InlineData(GrayscalePlain, PbmEncoding.Plain)]
        [InlineData(RgbBinary, PbmEncoding.Binary)]
        [InlineData(RgbPlain, PbmEncoding.Plain)]
        public void Identify_DetectsCorrectEncoding(string imagePath, PbmEncoding expectedEncoding)
        {
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);
            IImageInfo imageInfo = Image.Identify(stream);
            Assert.NotNull(imageInfo);
            PbmMetadata bitmapMetadata = imageInfo.Metadata.GetPbmMetadata();
            Assert.NotNull(bitmapMetadata);
            Assert.Equal(expectedEncoding, bitmapMetadata.Encoding);
        }

        [Theory]
        [InlineData(BlackAndWhitePlain, PbmColorType.BlackAndWhite)]
        [InlineData(BlackAndWhiteBinary, PbmColorType.BlackAndWhite)]
        [InlineData(GrayscaleBinary, PbmColorType.Grayscale)]
        [InlineData(GrayscaleBinaryWide, PbmColorType.Grayscale)]
        [InlineData(GrayscalePlain, PbmColorType.Grayscale)]
        [InlineData(RgbBinary, PbmColorType.Rgb)]
        [InlineData(RgbPlain, PbmColorType.Rgb)]
        public void Identify_DetectsCorrectColorType(string imagePath, PbmColorType expectedColorType)
        {
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);
            IImageInfo imageInfo = Image.Identify(stream);
            Assert.NotNull(imageInfo);
            PbmMetadata bitmapMetadata = imageInfo.Metadata.GetPbmMetadata();
            Assert.NotNull(bitmapMetadata);
            Assert.Equal(expectedColorType, bitmapMetadata.ColorType);
        }

        [Theory]
        [InlineData(BlackAndWhitePlain, 1)]
        [InlineData(BlackAndWhiteBinary, 1)]
        [InlineData(GrayscaleBinary, 255)]
        [InlineData(GrayscaleBinaryWide, 65535)]
        [InlineData(GrayscalePlain, 15)]
        [InlineData(RgbBinary, 255)]
        [InlineData(RgbPlain, 15)]
        public void Identify_DetectsCorrectMaxPixelValue(string imagePath, int expectedMaxPixelValue)
        {
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);
            IImageInfo imageInfo = Image.Identify(stream);
            Assert.NotNull(imageInfo);
            PbmMetadata bitmapMetadata = imageInfo.Metadata.GetPbmMetadata();
            Assert.NotNull(bitmapMetadata);
            Assert.Equal(expectedMaxPixelValue, bitmapMetadata.MaxPixelValue);
        }
    }
}

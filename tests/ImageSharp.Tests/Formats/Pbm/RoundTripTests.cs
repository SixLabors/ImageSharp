// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;
using static SixLabors.ImageSharp.Tests.TestImages.Pbm;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Pbm
{
    [Trait("Format", "Pbm")]
    public class RoundTripTests
    {
        [Theory]
        [InlineData(RgbPlain)]
        [InlineData(RgbBinary)]
        public void PbmColorImageCanRoundTrip(string imagePath)
        {
            // Arrange
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            // Act
            using var originalImage = Image.Load<Rgb24>(stream);
            using Image<Rgb24> encodedImage = this.RoundTrip(originalImage);

            // Assert
            Assert.NotNull(encodedImage);
            ImageComparer.Exact.VerifySimilarity(originalImage, encodedImage);
        }

        private Image<TPixel> RoundTrip<TPixel>(Image<TPixel> originalImage)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using var decodedStream = new MemoryStream();
            originalImage.SaveAsPbm(decodedStream);
            decodedStream.Seek(0, SeekOrigin.Begin);
            var encodedImage = (Image<TPixel>)Image.Load(decodedStream);
            return encodedImage;
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Pbm;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Pbm;

[Trait("Format", "Pbm")]
public class PbmRoundTripTests
{
    [Theory]
    [InlineData(BlackAndWhitePlain)]
    [InlineData(BlackAndWhiteBinary)]
    [InlineData(GrayscalePlain)]
    [InlineData(GrayscalePlainNormalized)]
    [InlineData(GrayscalePlainMagick)]
    [InlineData(GrayscaleBinary)]
    public void PbmGrayscaleImageCanRoundTrip(string imagePath)
    {
        // Arrange
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        // Act
        using Image originalImage = Image.Load(stream);
        using Image<Rgb24> colorImage = originalImage.CloneAs<Rgb24>();
        using Image<Rgb24> encodedImage = this.RoundTrip(colorImage);

        // Assert
        Assert.NotNull(encodedImage);
        ImageComparer.Exact.VerifySimilarity(colorImage, encodedImage);
    }

    [Theory]
    [InlineData(RgbPlain)]
    [InlineData(RgbPlainNormalized)]
    [InlineData(RgbPlainMagick)]
    [InlineData(RgbBinary)]
    public void PbmColorImageCanRoundTrip(string imagePath)
    {
        // Arrange
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        // Act
        using Image<Rgb24> originalImage = Image.Load<Rgb24>(stream);
        using Image<Rgb24> encodedImage = this.RoundTrip(originalImage);

        // Assert
        Assert.NotNull(encodedImage);
        ImageComparer.Exact.VerifySimilarity(originalImage, encodedImage);
    }

    private Image<TPixel> RoundTrip<TPixel>(Image<TPixel> originalImage)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using MemoryStream decodedStream = new();
        originalImage.SaveAsPbm(decodedStream);
        decodedStream.Seek(0, SeekOrigin.Begin);
        Image<TPixel> encodedImage = Image.Load<TPixel>(decodedStream);
        return encodedImage;
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.Formats.Qoi;

[Trait("Format", "Qoi")]
[ValidateDisposedMemoryAllocations]
public class QoiDecoderTests
{
    [Theory]
    [InlineData(TestImages.Qoi.Dice)]
    [InlineData(TestImages.Qoi.EdgeCase)]
    [InlineData(TestImages.Qoi.Kodim10)]
    [InlineData(TestImages.Qoi.Kodim23)]
    [InlineData(TestImages.Qoi.QoiLogo)]
    [InlineData(TestImages.Qoi.TestCard)]
    [InlineData(TestImages.Qoi.TestCardRGBA)]
    [InlineData(TestImages.Qoi.Wikipedia008)]
    public void Identify(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);

        Assert.NotNull(imageInfo);
    }
}

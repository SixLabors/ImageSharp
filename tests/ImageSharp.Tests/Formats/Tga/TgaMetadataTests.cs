// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tga;

[Trait("Format", "Tga")]
public class TgaMetadataTests
{
    [Theory]
    [InlineData(TgaBitsPerPixel.Bit8, PixelColorType.Luminance, 1)]
    [InlineData(TgaBitsPerPixel.Bit16, PixelColorType.BGR | PixelColorType.Alpha, 4)]
    [InlineData(TgaBitsPerPixel.Bit24, PixelColorType.BGR, 3)]
    [InlineData(TgaBitsPerPixel.Bit32, PixelColorType.BGR | PixelColorType.Alpha, 4)]
    public void GetPixelTypeInfo_ReportsStorageOrder(TgaBitsPerPixel bitsPerPixel, PixelColorType expectedColorType, int expectedComponentCount)
    {
        // A TGA image stores blue first, so each direct color depth reports BGR, with one component for each channel.
        TgaMetadata metadata = new() { BitsPerPixel = bitsPerPixel };
        PixelTypeInfo info = metadata.GetPixelTypeInfo();

        Assert.Equal(expectedColorType, info.ColorType);
        Assert.Equal(expectedComponentCount, info.ComponentInfo.Value.ComponentCount);
    }

    [Fact]
    public void CloneIsDeep()
    {
        TgaMetadata meta = new() { Compression = TgaCompression.None };
        TgaMetadata clone = meta.DeepClone();

        Assert.Equal(meta.Compression, clone.Compression);

        clone.Compression = TgaCompression.RunLength;

        Assert.False(meta.Compression.Equals(clone.Compression));
    }

    [Theory]
    [InlineData(TestImages.Tga.Bit24BottomLeft, TgaCompression.None)]
    [InlineData(TestImages.Tga.Bit24RleBottomLeft, TgaCompression.RunLength)]
    [InlineData(TestImages.Tga.Gray8BitRleBottomLeft, TgaCompression.RunLength)]
    public void Identify_DetectsCompression(string imagePath, TgaCompression expectedCompression)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);

        Assert.Equal(expectedCompression, imageInfo.Metadata.GetTgaMetadata().Compression);
    }
}

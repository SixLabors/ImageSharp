// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifDecoderTests
{
    [Theory]
    [InlineData(TestImages.Heif.Image1, HeifCompressionMethod.Hevc)]
    [InlineData(TestImages.Heif.Sample640x427, HeifCompressionMethod.Hevc)]
    [InlineData(TestImages.Heif.FujiFilmHif, HeifCompressionMethod.LegacyJpeg)]
    [InlineData(TestImages.Heif.IrvineAvif, HeifCompressionMethod.Av1)]
    public void Identify(string imagePath, HeifCompressionMethod compressionMethod)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);
        HeifMetadata heicMetadata = imageInfo.Metadata.GetHeifMetadata();

        Assert.NotNull(imageInfo);
        Assert.Equal(HeifFormat.Instance, imageInfo.Metadata.DecodedImageFormat);
        Assert.Equal(compressionMethod, heicMetadata.CompressionMethod);
    }

    [Theory]
    [WithFile(TestImages.Heif.FujiFilmHif, PixelTypes.Rgba32)]
    public void Decode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        HeifMetadata heicMetadata = image.Metadata.GetHeifMetadata();
        image.DebugSave(provider);

        image.CompareToReferenceOutput(provider);
        Assert.Equal(HeifCompressionMethod.LegacyJpeg, heicMetadata.CompressionMethod);
    }
}

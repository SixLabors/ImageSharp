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
    [InlineData(TestImages.Heif.Image1, HeifCompressionMethod.Hevc, 3992, 2992)]
    [InlineData(TestImages.Heif.Sample640x427, HeifCompressionMethod.Hevc, 640, 428)]
    [InlineData(TestImages.Heif.FujiFilmHif, HeifCompressionMethod.LegacyJpeg, 7728, 5152)]
    [InlineData(TestImages.Heif.IrvineAvif, HeifCompressionMethod.Av1, 480, 640)]
    public void Identify(string imagePath, HeifCompressionMethod compressionMethod, int width, int height)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);
        HeifMetadata heicMetadata = imageInfo.Metadata.GetHeifMetadata();

        Assert.NotNull(imageInfo);
        Assert.Equal(HeifFormat.Instance, imageInfo.Metadata.DecodedImageFormat);
        Assert.Equal(compressionMethod, heicMetadata.CompressionMethod);
        Assert.Equal(width, imageInfo.Width);
        Assert.Equal(height, imageInfo.Height);
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

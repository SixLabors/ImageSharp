// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heic;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heic;

[Trait("Format", "Heic")]
[ValidateDisposedMemoryAllocations]
public class HeicDecoderTests
{
    [Theory]
    [InlineData(TestImages.Heic.Image1, HeicCompressionMethod.Hevc)]
    [InlineData(TestImages.Heic.Sample640x427, HeicCompressionMethod.Hevc)]
    [InlineData(TestImages.Heic.FujiFilmHif, HeicCompressionMethod.LegacyJpeg)]
    [InlineData(TestImages.Heic.IrvineAvif, HeicCompressionMethod.Av1)]
    public void Identify(string imagePath, HeicCompressionMethod compressionMethod)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);
        HeicMetadata heicMetadata = imageInfo.Metadata.GetHeicMetadata();

        Assert.NotNull(imageInfo);
        Assert.Equal(HeicFormat.Instance, imageInfo.Metadata.DecodedImageFormat);
        Assert.Equal(compressionMethod, heicMetadata.CompressionMethod);
    }

    [Theory]
    [WithFile(TestImages.Heic.FujiFilmHif, PixelTypes.Rgba32)]
    public void Decode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        HeicMetadata heicMetadata = image.Metadata.GetHeicMetadata();
        image.DebugSave(provider);

        image.CompareToReferenceOutput(provider);
        Assert.Equal(HeicCompressionMethod.Hevc, heicMetadata.CompressionMethod);
    }
}

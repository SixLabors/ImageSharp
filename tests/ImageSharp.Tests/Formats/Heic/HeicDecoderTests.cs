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
    [InlineData(TestImages.Heic.Image1)]
    [InlineData(TestImages.Heic.Sample640x427)]
    [InlineData(TestImages.Heic.FujiFilmHif)]
    public void Identify(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);
        HeicMetadata heicMetadata = imageInfo.Metadata.GetHeicMetadata();

        Assert.NotNull(imageInfo);
        Assert.Equal(imageInfo.Metadata.DecodedImageFormat, HeicFormat.Instance);
        //Assert.Equal(heicMetadata.Channels, channels);
    }

    [Theory]
    [WithFile(TestImages.Heic.FujiFilmHif, PixelTypes.Rgba32)]
    public void Decode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        HeicMetadata qoiMetadata = image.Metadata.GetHeicMetadata();
        image.DebugSave(provider);

        image.CompareToReferenceOutput(provider);
        //Assert.Equal(heicMetadata.Channels, channels);
    }
}

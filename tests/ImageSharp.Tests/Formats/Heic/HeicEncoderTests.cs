// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heic;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Heic;

[Trait("Format", "Heic")]
[ValidateDisposedMemoryAllocations]
public class HeicEncoderTests
{
    [Theory]
    [WithFile(TestImages.Heic.Sample640x427, PixelTypes.Rgba32)]
    public static void Encode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(new MagickReferenceDecoder());
        using MemoryStream stream = new();
        HeicEncoder encoder = new();
        image.Save(stream, encoder);
        stream.Position = 0;

        using Image<TPixel> encodedImage = Image.Load<TPixel>(stream);
        HeicMetadata heicMetadata = encodedImage.Metadata.GetHeicMetadata();

        ImageComparer.Exact.CompareImages(image, encodedImage);
        //Assert.Equal(heicMetadata.Channels, channels);
    }
}

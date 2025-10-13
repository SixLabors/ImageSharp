// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifEncoderTests
{
    [Theory]
    [WithFile(TestImages.Heif.Sample640x427, PixelTypes.Rgba32, HeifCompressionMethod.LegacyJpeg)]
    public static void Encode<TPixel>(TestImageProvider<TPixel> provider, HeifCompressionMethod compressionMethod)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(new MagickReferenceDecoder(HeifFormat.Instance));
        using MemoryStream stream = new();
        HeifEncoder encoder = new();
        image.Save(stream, encoder);
        stream.Position = 0;

        using Image<TPixel> encodedImage = Image.Load<TPixel>(stream);
        HeifMetadata heifMetadata = encodedImage.Metadata.GetHeifMetadata();

        ImageComparer.Exact.CompareImages(image, encodedImage);
        Assert.Equal(compressionMethod, heifMetadata.CompressionMethod);
    }
}

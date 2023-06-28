// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Qoi;

[Trait("Format", "Qoi")]
[ValidateDisposedMemoryAllocations]
public class QoiEncoderTests
{
    [Theory]
    [WithFile(TestImages.Qoi.Dice, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.EdgeCase, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.Kodim10, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.Kodim23, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.QoiLogo, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.TestCard, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.TestCardRGBA, PixelTypes.Rgba32)]
    [WithFile(TestImages.Qoi.Wikipedia008, PixelTypes.Rgba32)]
    private static void Encode<TPixel>(TestImageProvider<TPixel> provider) where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        using MemoryStream stream = new();
        QoiEncoder encoder = new();
        image.Save(stream, encoder);
        stream.Position = 0;
        using Image<TPixel> encodedImage = (Image<TPixel>)Image.Load(stream);
        ImageComparingUtils.CompareWithReferenceDecoder(provider, encodedImage);
    }
}

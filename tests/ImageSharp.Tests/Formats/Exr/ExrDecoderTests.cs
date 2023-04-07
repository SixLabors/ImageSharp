// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.OpenExr;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrDecoderTests
{
    private static ExrDecoder ExrDecoder => new();

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Uncompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(TestImages.Exr.Zip, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_ZipCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(TestImages.Exr.Zips, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_ZipsCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(TestImages.Exr.Rle, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_RunLengthCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(TestImages.Exr.B44, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_B44Compressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }
}

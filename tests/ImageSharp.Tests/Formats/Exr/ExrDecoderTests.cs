// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrDecoderTests
{
    private static MagickReferenceDecoder ReferenceDecoder => MagickReferenceDecoder.Exr;

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Uncompressed_Rgb_ExrPixelType_Half<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        ExrMetadata exrMetaData = image.Metadata.GetExrMetadata();
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
        Assert.Equal(ExrPixelType.Half, exrMetaData.PixelType);
    }

    [Theory]
    [WithFile(TestImages.Exr.UncompressedFloatRgb, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Uncompressed_Rgb_ExrPixelType_Float<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        ExrMetadata exrMetaData = image.Metadata.GetExrMetadata();
        image.DebugSave(provider);

        // There is a 0,0059% difference to the Reference decoder.
        image.CompareToOriginal(provider, ImageComparer.Tolerant(0.0005f), ReferenceDecoder);
        Assert.Equal(ExrPixelType.Float, exrMetaData.PixelType);
    }

    [Theory]
    [WithFile(TestImages.Exr.UncompressedUintRgb, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Uncompressed_Rgb_ExrPixelType_Uint<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        ExrMetadata exrMetaData = image.Metadata.GetExrMetadata();
        image.DebugSave(provider);

        // Compare to referene output, since the reference decoder does not support this pixel type.
        image.CompareToReferenceOutput(provider);
        Assert.Equal(ExrPixelType.UnsignedInt, exrMetaData.PixelType);
    }

    [Theory]
    [WithFile(TestImages.Exr.UintRgba, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Uncompressed_Rgba_ExrPixelType_Uint<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        ExrMetadata exrMetaData = image.Metadata.GetExrMetadata();
        image.DebugSave(provider);

        // Compare to referene output, since the reference decoder does not support this pixel type.
        image.CompareToReferenceOutput(provider);
        Assert.Equal(ExrPixelType.UnsignedInt, exrMetaData.PixelType);
    }

    [Theory]
    [WithFile(TestImages.Exr.Rgb, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Rgb<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Gray, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Gray<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Zip, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_ZipCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Zips, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_ZipsCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Rle, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_RunLengthCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Pxr24Half, PixelTypes.Rgba32)]
    [WithFile(TestImages.Exr.Pxr24Float, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Pxr24Compressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.B44, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_B44Compressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);

        // Note: There is a 0,1190% difference to the reference decoder.
        image.CompareToOriginal(provider, ImageComparer.Tolerant(0.011f), ReferenceDecoder);
    }
}

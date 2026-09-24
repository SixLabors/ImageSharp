// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrDecoderTests
{
    private static MagickReferenceDecoder ReferenceDecoder => MagickReferenceDecoder.Exr;

    [Theory]
    [InlineData(TestImages.Exr.UncompressedFloatRgb, typeof(Image<RgbaVector>))]
    [InlineData(TestImages.Exr.UncompressedRgba, typeof(Image<RgbaHalfP>))]
    [InlineData(TestImages.Exr.Rgb, typeof(Image<RgbaHalf>))]
    public void DefaultLoad_UsesFloatingStorageForExrSamples(string imagePath, Type expectedImageType)
    {
        TestFile file = TestFile.Create(imagePath);
        using MemoryStream stream = new(file.Bytes, false);
        using Image image = Image.Load(stream);

        Assert.Equal(expectedImageType, image.GetType());
    }

    [Fact]
    public void ExrDecoder_PreservesHdrSamplesFromOpenExrFile()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertHdrSamplesFromOpenExrFile,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks decoded samples from the OpenEXR fixture at positions inside the vector body and scalar tail.
    /// </summary>
    private static void AssertHdrSamplesFromOpenExrFile()
    {
        TestFile file = TestFile.Create(TestImages.Exr.OpenExrHdrHalf);
        using MemoryStream stream = new(file.Bytes, false);
        using Image image = Image.Load(stream);
        Image<RgbaHalfP> pixels = Assert.IsType<Image<RgbaHalfP>>(image);

        Assert.Equal(587, pixels.Width);
        Assert.Equal(675, pixels.Height);

        // These values come from the uncompressed half samples in OpenEXR's comp_none.exr.
        Assert.Equal(new Vector4(319F, 423F, 501F, 1F), pixels[272, 180].ToVector4());
        Assert.Equal(new Vector4(0.001132965087890625F, -0.002262115478515625F, -0.0014476776123046875F, 1F), pixels[73, 639].ToVector4());
        Assert.Equal(new Vector4(0.91748046875F, 1.01953125F, 1.18359375F, 1F), pixels[59, 49].ToVector4());
        Assert.Equal(new Vector4(0.037841796875F, 0.021148681640625F, 0.0201263427734375F, 1F), pixels[586, 180].ToVector4());
    }

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

        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
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
    [WithFile(TestImages.Exr.Pxr24Uint, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Pxr24Compressed_ExrPixelType_Uint<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);

        // Compare to Reference here instead using the reference decoder, since uint pixel type is not supported by the Reference decoder.
        image.CompareToReferenceOutput(provider);
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

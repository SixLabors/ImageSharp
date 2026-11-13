// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
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
    [InlineData(TestImages.Exr.FloatRgbaZipFallback, typeof(Image<RgbaVectorP>))]
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

        // The literals are independently read linear half samples from OpenEXR's comp_none.exr.
        // Decoding applies the public sRGB transfer curve, then rounds to the selected pixel storage.
        // Keep these checks exact: they verify the codec's integration with those two shared owners.
        ReadOnlySpan<(int X, int Y, Vector4 Linear)> samples =
        [
            (272, 180, new(319F, 423F, 501F, 1F)),
            (73, 639, new(0.001132965087890625F, -0.002262115478515625F, -0.0014476776123046875F, 1F)),
            (59, 49, new(0.91748046875F, 1.01953125F, 1.18359375F, 1F)),
            (586, 180, new(0.037841796875F, 0.021148681640625F, 0.0201263427734375F, 1F))
        ];

        foreach ((int x, int y, Vector4 linear) in samples)
        {
            RgbaHalfP expected = RgbaHalfP.FromAssociatedScaledVector4(SRgbCompanding.Compress(linear));
            Assert.Equal(expected.ToVector4(), pixels[x, y].ToVector4());
        }
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

        // OpenEXR 3.5.3 supplies native UINT channels. Normalize them by uint.MaxValue,
        // apply the analytic IEC sRGB curve, then pack the final Rgba32 destination once.
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

        // The OpenEXR 3.5.3 reference normalizes UINT color and alpha, restores straight RGB,
        // and applies the analytic IEC sRGB curve before final Rgba32 packing. Zero alpha retains the supplied RGB.
        // Fast Pow can change that final byte rounding by one. The measured whole-image error is
        // 0.0013%; retain the native reference and bound this image's error at 0.0014%.
        image.CompareToReferenceOutput(ImageComparer.TolerantPercentage(.0014F), provider);
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
        // Fast Pow can put a component on the other side of an eight-bit rounding midpoint.
        // The measured native comparison error is 0.0032%; bound this image's error at 0.0033%.
        image.CompareToOriginal(provider, ImageComparer.TolerantPercentage(.0033F), ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Zip, PixelTypes.Rgba32)]
    [WithFile(TestImages.Exr.ZipUncompressedBlock, PixelTypes.Rgba32)]
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

        // OpenEXR 3.5.3 decodes PXR24 UINT independently. Normalize native channels,
        // apply the analytic IEC sRGB curve, then pack the final Rgba32 destination once.
        image.CompareToReferenceOutput(provider);
    }

    /// <summary>
    /// Reads a FLOAT scanline stored raw in a B44 image.
    /// </summary>
    [Fact]
    public void Decode_B44RawFloatBlock_PreservesPixels()
    {
        using Image<Rgba32> source = new(2, 1);
        source[0, 0] = new Rgba32(255, 0, 0);
        source[1, 0] = new Rgba32(0, 255, 0);

        using MemoryStream stream = new();
        source.Save(stream, new ExrEncoder { PixelType = ExrPixelType.Float, Compression = ExrCompression.None });
        byte[] data = stream.ToArray();

        // A one-row B44 image has one scanline block. A raw block stores the original sample bytes.
        // Change only the compression attribute. Keep the encoder's raw block and offset.
        ReadOnlySpan<byte> attribute = "compression\0compression\0"u8;
        int offset = data.AsSpan().IndexOf(attribute);
        Assert.True(offset >= 0);
        data[offset + attribute.Length + sizeof(int)] = (byte)ExrCompression.B44;

        using Image<Rgba32> decoded = Image.Load<Rgba32>(data);
        Assert.Equal(source.Size, decoded.Size);
        Assert.Equal(source[0, 0], decoded[0, 0]);
        Assert.Equal(source[1, 0], decoded[1, 0]);
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

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

        // Compare to Reference here instead using the reference decoder, since uint pixel type is not supported by the Reference decoder.
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

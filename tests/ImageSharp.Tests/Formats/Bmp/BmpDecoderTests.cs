// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using static SixLabors.ImageSharp.Tests.TestImages.Bmp;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Bmp;

[Trait("Format", "Bmp")]
[ValidateDisposedMemoryAllocations]
public class BmpDecoderTests
{
    public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.RgbaVector;

    // The compression values of a BITMAPINFOHEADER.
    private const int RgbCompression = 0;
    private const int Rle8Compression = 1;
    private const int Rle4Compression = 2;
    private const int BitFieldsCompression = 3;
    private const int AlphaBitFieldsCompression = 6;

    public static readonly string[] MiscBmpFiles = Miscellaneous;

    public static readonly string[] BitfieldsBmpFiles = BitFields;

    public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
    new()
    {
        { Car, 3780, 3780, PixelResolutionUnit.PixelsPerMeter },
        { V5Header, 3780, 3780, PixelResolutionUnit.PixelsPerMeter },
        { RLE8, 2835, 2835, PixelResolutionUnit.PixelsPerMeter }
    };

    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);
    private static readonly Rgba32 OpaqueBlack = new(0, 0, 0, 255);

    // An RLE4 palette as B, G, R and a reserved byte: index 0 black, 1 red, 2 green, 3 blue.
    private static readonly byte[] Rle4Palette = [0, 0, 0, 0, 0, 0, 255, 0, 0, 255, 0, 0, 255, 0, 0, 0];

    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict, false)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary, false)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData, false)]
    [InlineData(SegmentIntegrityHandling.Strict, true)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary, true)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData, true)]
    public void Decode_WithProfileLargerThanRemainingData_RespectsOptions(SegmentIntegrityHandling integrityHandling, bool skipMetadata)
    {
        byte[] payload = Convert.FromHexString(
            "424D8E000000000000008A0000007C0000000100000001000000010018000000" +
            "0000000000000000000000000000000000000000000000000000000000000000" +
            "0000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000C800" +
            "00000000004000000000000000");
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrityHandling, SkipMetadata = skipMetadata };

        if (integrityHandling is SegmentIntegrityHandling.Strict)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Load(options, payload));
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, payload));
        }
        else
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, payload);

            Assert.Equal(new Size(1, 1), image.Size);
            Assert.Equal(new Rgba32(0, 0, 0), image[0, 0]);
            Assert.Null(image.Metadata.IccProfile);

            ImageInfo info = Image.Identify(options, payload);

            Assert.Equal(image.Size, info.Size);
            Assert.Null(info.Metadata.IccProfile);
        }
    }

    /// <summary>
    /// A run that continues past the last pixel cannot change the image, so the decoder discards the rest of the run.
    /// </summary>
    /// <param name="integrityHandling">The segment integrity handling.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void Decode_Rle8RunPastLastPixel_DecodesCompleteImage(SegmentIntegrityHandling integrityHandling)
    {
        // One encoded run of 6 pixels with palette index 1, for a 2 x 2 image of 4 pixels.
        byte[] payload = CreateRle8Bitmap(2, 2, [6, 1, 0, 1]);
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrityHandling };

        using Image<Rgba32> image = Image.Load<Rgba32>(options, payload);

        Rgba32 red = new(255, 0, 0, 255);
        Assert.Equal(red, image[0, 0]);
        Assert.Equal(red, image[1, 0]);
        Assert.Equal(red, image[0, 1]);
        Assert.Equal(red, image[1, 1]);
    }

    /// <summary>
    /// RLE data that ends before the last pixel is not an error, as for PNG and JPEG data that ends early. The
    /// decoder keeps the pixels decoded before the end, for each integrity option.
    /// </summary>
    /// <param name="integrityHandling">The segment integrity handling.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void Decode_TruncatedRle8_KeepsDecodedPixels(SegmentIntegrityHandling integrityHandling)
    {
        // One encoded run of 2 pixels with palette index 1 fills the first stored row, the bottom image row.
        // The data then ends before the second stored row.
        byte[] payload = CreateRle8Bitmap(2, 2, [2, 1]);
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrityHandling };

        using Image<Rgba32> image = Image.Load<Rgba32>(options, payload);

        // The decoded row keeps its color. The row that the data does not reach keeps the cleared image buffer.
        Rgba32 red = new(255, 0, 0, 255);
        Assert.Equal(red, image[0, 1]);
        Assert.Equal(red, image[1, 1]);
        Assert.Equal(default, image[0, 0]);
        Assert.Equal(default, image[1, 0]);
    }

    /// <summary>
    /// An RLE4 absolute run that a row end splits after an odd pixel count continues on the low nibble of a byte.
    /// </summary>
    [Fact]
    public void Decode_Rle4AbsoluteRunAcrossRowEnd_KeepsNibbleOrder()
    {
        // A 3 x 2 image. The absolute run has 5 indices: 1, 2, 3, 1, 2, packed two in each byte, high nibble first.
        // The 3 bytes of run data have one pad byte. Pixels 0 to 2 fill the first stored row (the bottom image row).
        // Pixel 3, the low nibble of the second byte, starts the second stored row. End of bitmap skips the last pixel.
        byte[] payload = CreateBitmap(3, 2, 4, Rle4Compression, [], Rle4Palette, [0, 5, 0x12, 0x31, 0x20, 0, 0, 1]);

        using Image<Rgba32> image = Image.Load<Rgba32>(payload);

        Assert.Equal(Red, image[0, 1]);
        Assert.Equal(Green, image[1, 1]);
        Assert.Equal(Blue, image[2, 1]);
        Assert.Equal(Red, image[0, 0]);
        Assert.Equal(Green, image[1, 0]);
        Assert.Equal(OpaqueBlack, image[2, 0]);
    }

    /// <summary>
    /// An RLE4 encoded run alternates its two nibbles over the full run, also when a row end splits the run after an
    /// odd pixel count.
    /// </summary>
    [Fact]
    public void Decode_Rle4EncodedRunAcrossRowEnd_KeepsAlternation()
    {
        // A 3 x 2 image. The encoded run has 5 pixels of the indices 1, 2, 1, 2, 1. Pixels 0 to 2 fill the first stored
        // row (the bottom image row). Pixel 3 (index 2) starts the second stored row. End of bitmap skips the last pixel.
        byte[] payload = CreateBitmap(3, 2, 4, Rle4Compression, [], Rle4Palette, [5, 0x12, 0, 1]);

        using Image<Rgba32> image = Image.Load<Rgba32>(payload);

        Assert.Equal(Red, image[0, 1]);
        Assert.Equal(Green, image[1, 1]);
        Assert.Equal(Red, image[2, 1]);
        Assert.Equal(Green, image[0, 0]);
        Assert.Equal(Red, image[1, 0]);
        Assert.Equal(OpaqueBlack, image[2, 0]);
    }

    /// <summary>
    /// A 32-bit RGB bitmap with a version 3 header has alpha when any fourth byte is not 0. The fourth bytes of all
    /// pixels are then alpha, also in rows before the first alpha that is not 0.
    /// </summary>
    [Fact]
    public void Decode_Rgb32V3WithAlphaAfterFirstRow_DecodesAllRowsWithAlpha()
    {
        // A 1 x 2 image of B, G, R, A pixels. The first stored row (the bottom image row) is red with alpha 0. The
        // second stored row (the top image row) is blue with alpha 128.
        byte[] payload = CreateBitmap(1, 2, 32, RgbCompression, [], [], [0, 0, 255, 0, 255, 0, 0, 128]);

        using Image<Rgba32> image = Image.Load<Rgba32>(payload);

        Assert.Equal(new Rgba32(255, 0, 0, 0), image[0, 1]);
        Assert.Equal(new Rgba32(0, 0, 255, 128), image[0, 0]);
    }

    /// <summary>
    /// A 16-bit bitmap with an alpha mask takes its alpha from that mask. A 4-bit channel widens to 8 bits by bit
    /// replication, so 0b1000 becomes 0b1000_1000.
    /// </summary>
    [Fact]
    public void Decode_Bitfields16WithAlphaMask_UsesAlpha()
    {
        // A 1 x 1 image with 4-4-4-4 A, R, G, B masks. The sample 0x8F00 has red 15 and alpha 8. The row is 2 bytes
        // with 2 bytes of padding.
        byte[] payload = CreateBitmap(1, 1, 16, AlphaBitFieldsCompression, [0x0F00, 0x00F0, 0x000F, 0xF000], [], [0x00, 0x8F, 0, 0]);

        using Image<Rgba32> image = Image.Load<Rgba32>(payload);

        Assert.Equal(new Rgba32(255, 0, 0, 136), image[0, 0]);
    }

    /// <summary>
    /// A 32-bit bitmap with channel masks of fewer than 8 bits widens each channel to 8 bits by bit replication. The
    /// Bgra32 decode writes the same color as the Rgba32 decode.
    /// </summary>
    [Fact]
    public void Decode_Bitfields32WithFiveBitMasks_WidensChannels()
    {
        // A 1 x 1 image with 5-5-5 masks. The sample 0x7E01 has red 31, green 16 and blue 1.
        // Bit replication gives 31 -> 255, 16 -> 132 (0b10000_100) and 1 -> 8 (0b00001_000). No alpha mask: opaque.
        byte[] payload = CreateBitmap(1, 1, 32, BitFieldsCompression, [0x7C00, 0x03E0, 0x001F], [], [0x01, 0x7E, 0, 0]);

        using Image<Rgba32> rgba = Image.Load<Rgba32>(payload);
        using Image<Bgra32> bgra = Image.Load<Bgra32>(payload);

        Assert.Equal(new Rgba32(255, 132, 8, 255), rgba[0, 0]);
        Assert.Equal(new Bgra32(255, 132, 8, 255), bgra[0, 0]);
    }

    /// <summary>
    /// RGB data that ends before the last row is not an error, as for PNG, JPEG and RLE data that ends early. The
    /// decoder keeps the rows decoded before the end, for each integrity option.
    /// </summary>
    /// <param name="integrityHandling">The segment integrity handling.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void Decode_TruncatedRgb24_KeepsDecodedRows(SegmentIntegrityHandling integrityHandling)
    {
        // A 2 x 2 image with only the first stored row (the bottom image row): red, green, then 2 bytes of padding.
        byte[] payload = CreateBitmap(2, 2, 24, RgbCompression, [], [], [0, 0, 255, 0, 255, 0, 0, 0]);
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrityHandling };

        using Image<Rgba32> image = Image.Load<Rgba32>(options, payload);

        Assert.Equal(Red, image[0, 1]);
        Assert.Equal(Green, image[1, 1]);
        Assert.Equal(default, image[0, 0]);
        Assert.Equal(default, image[1, 0]);
    }

    [Theory]
    [WithFileCollection(nameof(MiscBmpFiles), PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_MiscellaneousBitmaps<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);

        if (TestEnvironment.IsWindows)
        {
            image.CompareToOriginal(provider);
        }
    }

    [Theory]
    [WithFile(Car, PixelTypes.Rgba32)]
    [WithFile(F, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_MiscellaneousBitmaps_WithLimitedAllocatorBufferCapacity(
        TestImageProvider<Rgba32> provider)
    {
        static void RunTest(string providerDump, string nonContiguousBuffersStr)
        {
            TestImageProvider<Rgba32> provider = BasicSerializer.Deserialize<TestImageProvider<Rgba32>>(providerDump);

            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);

            using Image<Rgba32> image = provider.GetImage(BmpDecoder.Instance);
            image.DebugSave(provider, nonContiguousBuffersStr);

            if (TestEnvironment.IsWindows)
            {
                image.CompareToOriginal(provider);
            }
        }

        string providerDump = BasicSerializer.Serialize(provider);
        RemoteExecutor.Invoke(RunTest, providerDump, "Disco").Dispose();
    }

    [Theory]
    [WithFile(Bit32Rgb, PixelTypes.Rgba32)]
    [WithFile(Bit16, PixelTypes.Rgba32)]
    public void BmpDecoder_DegenerateMemoryRequest_ShouldTranslateTo_ImageFormatException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InPixelsSqrt(10);
        InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() => provider.GetImage(BmpDecoder.Instance));
        Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
    }

    [Theory]
    [WithFileCollection(nameof(BitfieldsBmpFiles), PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeBitfields<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }

    [Theory]
    [WithFile(Bit16Inverted, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_16Bit_Inverted<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }

    [Theory]
    [WithFile(Bit8Inverted, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_8Bit_Inverted<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(Bit1, PixelTypes.Rgba32)]
    [WithFile(Bit1Pal1, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_1Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, new SystemDrawingReferenceDecoder(BmpFormat.Instance));
    }

    [Theory]
    [WithFile(Bit2, PixelTypes.Rgba32)]
    [WithFile(Bit2Color, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_2Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);

        // Reference decoder cant decode 2-bit, compare to reference output instead.
        image.CompareToReferenceOutput(provider, extension: "png");
    }

    [Theory]
    [WithFile(Bit4, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_4Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(Bit8, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_8Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(Bit16, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);

        // The Direct3D UNORM rules give a 5-bit channel c as round(c * 255 / 31). System.Drawing widens by bit
        // replication, which differs by 1 for some values, so the reference is the UNORM decode of the file.
        image.CompareToReferenceOutput(provider);
    }

    [Theory]
    [WithFile(Bit32Rgb, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(Rgba32v4, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_32BitV4Header_Fast<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(RLE4Cut, PixelTypes.Rgba32)]
    [WithFile(RLE4Delta, PixelTypes.Rgba32)]
    [WithFile(Rle4Delta320240, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_RunLengthEncoded_4Bit_WithDelta<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        RleSkippedPixelHandling skippedPixelHandling = RleSkippedPixelHandling.Black;
        BmpDecoderOptions options = new(new DecoderOptions(Configuration.Default)) { RleSkippedPixelHandling = skippedPixelHandling };

        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }

    [Theory]
    [WithFile(RLE4, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_RunLengthEncoded_4Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        RleSkippedPixelHandling skippedPixelHandling = TestEnvironment.IsWindows ? RleSkippedPixelHandling.Black : RleSkippedPixelHandling.FirstColorOfPalette;
        BmpDecoderOptions options = new(new DecoderOptions(Configuration.Default)) { RleSkippedPixelHandling = skippedPixelHandling };

        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(RLE8Cut, PixelTypes.Rgba32)]
    [WithFile(RLE8Delta, PixelTypes.Rgba32)]
    [WithFile(Rle8Delta320240, PixelTypes.Rgba32)]
    [WithFile(Rle8Blank160120, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_RunLengthEncoded_8Bit_WithDelta_SystemDrawingRefDecoder<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        BmpDecoderOptions options = new(new DecoderOptions(Configuration.Default)) { RleSkippedPixelHandling = RleSkippedPixelHandling.Black };
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance, options);
        image.DebugSave(provider);
        if (TestEnvironment.IsWindows)
        {
            image.CompareToOriginal(provider, new SystemDrawingReferenceDecoder(BmpFormat.Instance));
        }
    }

    // An RLE-compressed image that uses “delta” codes, to skip over some pixels.
    [Theory]
    [WithFile(RLE8Delta, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_RunLengthEncoded_8Bit_WithDelta_MagickRefDecoder<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        BmpDecoderOptions options = new(new DecoderOptions(Configuration.Default)) { RleSkippedPixelHandling = RleSkippedPixelHandling.FirstColorOfPalette };
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, MagickReferenceDecoder.Png);
    }

    // An RLE-compressed image that uses “delta” codes, and early EOL & EOBMP markers, to skip over some pixels.
    [Theory]
    [WithFile(RLE8Cut, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_RunLengthEncoded_8Bit_WithDeltaAndEOL_MagickRefDecoder<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        BmpDecoderOptions options = new(new DecoderOptions(Configuration.Default)) { RleSkippedPixelHandling = RleSkippedPixelHandling.FirstColorOfPalette };
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }

    [Theory]
    [WithFile(RLE8, PixelTypes.Rgba32, false)]
    [WithFile(RLE8, PixelTypes.Rgba32, true)]
    public void BmpDecoder_CanDecode_RunLengthEncoded_8Bit<TPixel>(TestImageProvider<TPixel> provider, bool enforceDiscontiguousBuffers)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (enforceDiscontiguousBuffers)
        {
            provider.LimitAllocatorBufferCapacity().InBytesSqrt(400);
        }

        BmpDecoderOptions options = new(new DecoderOptions(Configuration.Default)) { RleSkippedPixelHandling = RleSkippedPixelHandling.FirstColorOfPalette };
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, MagickReferenceDecoder.Png);
    }

    [Theory]
    [WithFile(RLE8Inverted, PixelTypes.Rgba32, false)]
    [WithFile(RLE8Inverted, PixelTypes.Rgba32, true)]
    public void BmpDecoder_CanDecode_RunLengthEncoded_8Bit_Inverted<TPixel>(TestImageProvider<TPixel> provider, bool enforceDiscontiguousBuffers)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (enforceDiscontiguousBuffers)
        {
            provider.LimitAllocatorBufferCapacity().InBytesSqrt(400);
        }

        BmpDecoderOptions options = new(new DecoderOptions(Configuration.Default)) { RleSkippedPixelHandling = RleSkippedPixelHandling.FirstColorOfPalette };
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance, options);
        image.DebugSave(provider);

        // The Reference decoder does not support decoding compressed bmp which are inverted (with negative height).
        image.CompareToReferenceOutput(provider);
    }

    [Theory]
    [WithFile(RLE24, PixelTypes.Rgba32, false)]
    [WithFile(RLE24Cut, PixelTypes.Rgba32, false)]
    [WithFile(RLE24Delta, PixelTypes.Rgba32, false)]
    [WithFile(RLE24, PixelTypes.Rgba32, true)]
    [WithFile(RLE24Cut, PixelTypes.Rgba32, true)]
    [WithFile(RLE24Delta, PixelTypes.Rgba32, true)]
    public void BmpDecoder_CanDecode_RunLengthEncoded_24Bit<TPixel>(TestImageProvider<TPixel> provider, bool enforceNonContiguous)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (enforceNonContiguous)
        {
            provider.LimitAllocatorBufferCapacity().InBytesSqrt(400);
        }

        BmpDecoderOptions options = new(new DecoderOptions(Configuration.Default)) { RleSkippedPixelHandling = RleSkippedPixelHandling.Black };
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance, options);
        image.DebugSave(provider);

        // Neither System.Drawing nor MagickReferenceDecoder decode this file.
        // Compare to reference output instead.
        image.CompareToReferenceOutput(provider, extension: "png");
    }

    [Theory]
    [WithFile(RgbaAlphaBitfields, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeAlphaBitfields<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);

        // Neither System.Drawing nor MagickReferenceDecoder decode this file.
        // Compare to reference output instead.
        image.CompareToReferenceOutput(provider, extension: "png");
    }

    [Theory]
    [WithFile(Bit32Rgba, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeBitmap_WithAlphaChannel<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, MagickReferenceDecoder.Png);
    }

    [Theory]
    [WithFile(Rgba321010102, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeBitfields_WithUnusualBitmasks<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);

        // Choosing large tolerance of 6.1 here, because for some reason with the MagickReferenceDecoder the alpha channel
        // seems to be wrong. This bitmap has an alpha channel of two bits. In many cases this alpha channel has a value of 3,
        // which should be remapped to 255 for RGBA32, but the magick decoder has a value of 191 set.
        // The total difference without the alpha channel is still: 0.0204%
        // Exporting the image as PNG with GIMP yields to the same result as the ImageSharp implementation.
        image.CompareToOriginal(provider, ImageComparer.TolerantPercentage(6.1f), MagickReferenceDecoder.Png);
    }

    [Theory]
    [WithFile(WinBmpv2, PixelTypes.Rgba32)]
    [WithFile(CoreHeader, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeBmpv2<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);

        // Do not validate. Reference files will fail validation.
        image.CompareToOriginal(provider, new MagickReferenceDecoder(PngFormat.Instance, false));
    }

    [Theory]
    [WithFile(WinBmpv3, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeBmpv3<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(Rgb24LargePalette, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeLargeDirectColorPalette<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        if (TestEnvironment.IsWindows)
        {
            image.CompareToOriginal(provider);
        }
    }

    [Theory]

    // This indexed palette declares 300 entries at 8 bpp, exceeding the format's 256-entry limit.
    [WithFile(OversizedPalette, PixelTypes.Rgba32)]

    // This 8-bit core-header palette contains only 252 entries instead of the full 256-entry table.
    [WithFile(LessThanFullSizedPalette, PixelTypes.Rgba32)]
    [WithFile(InvalidPaletteSize, PixelTypes.Rgba32)]
    public void BmpDecoder_ThrowsInvalidImageContentException_OnInvalidPaletteSize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        => Assert.Throws<InvalidImageContentException>(() =>
        {
            using (provider.GetImage(BmpDecoder.Instance))
            {
            }
        });

    [Theory]
    [WithFile(Rgb24jpeg, PixelTypes.Rgba32)]
    [WithFile(Rgb24png, PixelTypes.Rgba32)]
    public void BmpDecoder_ThrowsNotSupportedException_OnUnsupportedBitmaps<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        => Assert.Throws<NotSupportedException>(() =>
        {
            using (provider.GetImage(BmpDecoder.Instance))
            {
            }
        });

    [Theory]
    [WithFile(Rgb32h52AdobeV3, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeAdobeBmpv3<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, MagickReferenceDecoder.Png);
    }

    [Theory]
    [WithFile(Rgba32bf56AdobeV3, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeAdobeBmpv3_WithAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, MagickReferenceDecoder.Png);
    }

    [Theory]
    [WithFile(WinBmpv4, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeBmpv4<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(WinBmpv5, PixelTypes.Rgba32)]
    [WithFile(V5Header, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecodeBmpv5<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(Pal8Offset, PixelTypes.Rgba32)]
    public void BmpDecoder_RespectsFileHeaderOffset<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(F, CommonNonDefaultPixelTypes)]
    public void BmpDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(Bit8Palette4, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode4BytePerEntryPalette<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [InlineData(Bit32Rgb, 32)]
    [InlineData(Bit32Rgba, 32)]
    [InlineData(Car, 24)]
    [InlineData(F, 24)]
    [InlineData(NegHeight, 24)]
    [InlineData(Bit16, 16)]
    [InlineData(Bit16Inverted, 16)]
    [InlineData(Bit8, 8)]
    [InlineData(Bit8Inverted, 8)]
    [InlineData(Bit4, 4)]
    [InlineData(Bit1, 1)]
    [InlineData(Bit1Pal1, 1)]
    public void Identify_DetectsCorrectPixelType(string imagePath, int expectedPixelSize)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        Assert.NotNull(imageInfo);
        Assert.Equal(expectedPixelSize, imageInfo.PixelType.BitsPerPixel);
    }

    [Theory]
    [InlineData(Bit32Rgb, 127, 64)]
    [InlineData(Car, 600, 450)]
    [InlineData(Bit16, 127, 64)]
    [InlineData(Bit16Inverted, 127, 64)]
    [InlineData(Bit8, 127, 64)]
    [InlineData(Bit8Inverted, 127, 64)]
    [InlineData(RLE8, 491, 272)]
    [InlineData(RLE8Inverted, 491, 272)]
    public void Identify_DetectsCorrectWidthAndHeight(string imagePath, int expectedWidth, int expectedHeight)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        Assert.NotNull(imageInfo);
        Assert.Equal(expectedWidth, imageInfo.Width);
        Assert.Equal(expectedHeight, imageInfo.Height);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        // The only Decode overload that takes a CancellationToken is protected. https://github.com/xunit/xunit/issues/3673
#pragma warning disable xUnit1051
        using Image<Rgba32> image = BmpDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream);
#pragma warning restore xUnit1051
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [WithFile(Os2v2Short, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_Os2v2XShortHeader<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);

        // Neither System.Drawing or MagickReferenceDecoder can correctly decode this file.
        // Compare to reference output instead.
        image.CompareToReferenceOutput(provider, extension: "png");
    }

    [Theory]
    [WithFile(Os2v2, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_Os2v2Header<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);

        // System.Drawing can not decode this image. MagickReferenceDecoder can decode it,
        // Compare to reference output instead.
        image.CompareToReferenceOutput(provider, extension: "png");
    }

    [Theory]
    [WithFile(Os2BitmapArray, PixelTypes.Rgba32)]
    [WithFile(Os2BitmapArray9s, PixelTypes.Rgba32)]
    [WithFile(Os2BitmapArrayDiamond, PixelTypes.Rgba32)]
    [WithFile(Os2BitmapArraySkater, PixelTypes.Rgba32)]
    [WithFile(Os2BitmapArraySpade, PixelTypes.Rgba32)]
    [WithFile(Os2BitmapArraySunflower, PixelTypes.Rgba32)]
    [WithFile(Os2BitmapArrayMarble, PixelTypes.Rgba32)]
    [WithFile(Os2BitmapArrayWarpd, PixelTypes.Rgba32)]
    [WithFile(Os2BitmapArrayPines, PixelTypes.Rgba32)]
    public void BmpDecoder_CanDecode_Os2BitmapArray<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        image.DebugSave(provider);

        // Neither System.Drawing or MagickReferenceDecoder can correctly decode this file.
        // Compare to reference output instead.
        image.CompareToReferenceOutput(provider, extension: "png");
    }

    [Theory]
    [WithFile(Issue2696, PixelTypes.Rgba32)]
    public void BmpDecoder_ThrowsException_Issue2696<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
            });
        Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
    }

    // https://github.com/SixLabors/ImageSharp/issues/3067
    [Fact]
    public void BmpDecoder_ThrowsException_Issue3067()
    {
        // Construct minimal BMP with bitsPerPixel = 0
        byte[] bmp = new byte[54];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BitConverter.GetBytes(54).CopyTo(bmp, 2);
        BitConverter.GetBytes(54).CopyTo(bmp, 10);
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(1).CopyTo(bmp, 18);
        BitConverter.GetBytes(1).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)0).CopyTo(bmp, 28);  // bitsPerPixel = 0

        using MemoryStream stream = new(bmp);

        Assert.Throws<InvalidImageContentException>(() =>
        {
            // The only Decode overload that takes a CancellationToken is protected. https://github.com/xunit/xunit/issues/3673
#pragma warning disable xUnit1051
            using Image image = BmpDecoder.Instance.Decode(DecoderOptions.Default, stream);
#pragma warning restore xUnit1051
        });
    }

    // https://github.com/SixLabors/ImageSharp/issues/3074
    [Fact]
    public void BmpDecoder_ThrowsException_Issue3074()
    {
        // Crafted BMP: pixel data offset = 0x7FFFFFFF, actual file = 35 bytes
        byte[] data =
        [
            0x42, 0x4D,                         // "BM" signature
            0x3A, 0x00, 0x00, 0x00,             // file size: 58
            0x00, 0x00, 0x00, 0x00,             // reserved
            0xFF, 0xFF, 0xFF, 0x7F,             // pixel offset: 0x7FFFFFFF (2,147,483,647)
            0x28, 0x00, 0x00, 0x00,             // DIB header size: 40
            0x01, 0x00, 0x00, 0x00,             // width: 1
            0x01, 0xFF, 0x00, 0x00,             // height: 65281
            0x01, 0x00,                         // color planes: 1
            0x08, 0x00,                         // bits per pixel: 8
            0x00, 0x00, 0x00, 0x00,             // compression: RGB
            0x00, 0x00, 0x00 // (truncated)
        ];

        using MemoryStream stream = new(data);

        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);
        });
    }

    /// <summary>
    /// Invalid RGB bit depths cannot define a pixel layout, even when image data errors are ignored.
    /// </summary>
    /// <param name="path">The stored bitmap with an unsupported RGB bit depth.</param>
    [Theory]
    [InlineData(InvalidRgbDepth0)]
    [InlineData(InvalidRgbDepth3)]
    [InlineData(InvalidRgbDepth7)]
    [InlineData(InvalidRgbDepth12)]
    [InlineData(InvalidRgbDepth33)]
    public void BmpDecoder_InvalidUncompressedBitsPerPixel_ThrowsInvalidImageContentException(string path)
    {
        byte[] data = TestFile.Create(path).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, data));
            Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
            });
        }
    }

    /// <summary>
    /// RGB and bit-field data that ends before the last row is not an error, as for PNG, JPEG and RLE data that ends
    /// early. The image has its declared size and keeps the rows decoded before the end, for each integrity option.
    /// </summary>
    /// <param name="path">The stored bitmap with incomplete RGB or bit-fields pixel data.</param>
    /// <param name="width">The declared width.</param>
    /// <param name="height">The declared height.</param>
    [Theory]
    [InlineData(TruncatedRgb24LargeImage, 4000, 4000)]
    [InlineData(TruncatedRgb24WideRow, 100000, 1)]
    [InlineData(TruncatedBitFields16, 100000, 1)]
    [InlineData(TruncatedBitFields32, 100000, 1)]
    [InlineData(TruncatedAlphaBitFields32, 100000, 1)]
    public void BmpDecoder_PixelDataSmallerThanDeclared_KeepsDecodedRows(string path, int width, int height)
    {
        byte[] data = TestFile.Create(path).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };

            using Image<Rgba32> image = Image.Load<Rgba32>(options, data);

            // Each file has 72 bytes of pixel data, fewer than its first row needs. Thus no row decodes, and each
            // pixel keeps the cleared image buffer.
            Assert.Equal(new Size(width, height), image.Size);
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Assert.Equal(-1, accessor.GetRowSpan(y).IndexOfAnyExcept(default(Rgba32)));
                }
            });
        }
    }

    /// <summary>
    /// Dimensions past the allocator limit fail at the image allocation, as they do for each other decoder.
    /// </summary>
    [Fact]
    public void BmpDecoder_DimensionsPastAllocatorLimit_ThrowsInvalidImageContentException()
    {
        // The file declares 2147483647 x 2147483647 pixels.
        byte[] data = TestFile.Create(TruncatedRgb32MaximumDimensions).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };

            InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
            });

            Assert.IsType<InvalidMemoryOperationException>(exception.InnerException);
        }
    }

    /// <summary>
    /// Accepts a missing padding byte after the final stored row in either row order.
    /// </summary>
    /// <param name="path">The stored one-column bitmap with a red top pixel and a blue bottom pixel.</param>
    [Theory]
    [InlineData(MissingFinalRowPadding)]
    [InlineData(MissingFinalRowPaddingTopDown)]
    public void BmpDecoder_MissingLastRowPadding_Decodes(string path)
    {
        using Image<Rgb24> actual = Image.Load<Rgb24>(TestFile.Create(path).Bytes);

        Assert.Equal(new Size(1, 2), actual.Size);
        Assert.Equal(new Rgb24(255, 0, 0), actual[0, 0]);
        Assert.Equal(new Rgb24(0, 0, 255), actual[0, 1]);
    }

    /// <summary>
    /// Creates an 8-bit RLE bitmap with a two-color palette: index 0 is black and index 1 is red.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="data">The RLE8 data.</param>
    /// <returns>The encoded bitmap.</returns>
    private static byte[] CreateRle8Bitmap(int width, int height, byte[] data)
        => CreateBitmap(width, height, 8, Rle8Compression, [], [0, 0, 0, 0, 0, 0, 255, 0], data);

    /// <summary>
    /// Creates a bitmap with a 40-byte BITMAPINFOHEADER. A positive height stores the bottom row first.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="bitsPerPixel">The bit count of one pixel.</param>
    /// <param name="compression">The compression value of the header.</param>
    /// <param name="masks">
    /// The channel masks after the header: R, G, B for BI_BITFIELDS, or R, G, B, A for BI_ALPHABITFIELDS. Empty for
    /// the other compressions.
    /// </param>
    /// <param name="palette">The palette entries as B, G, R and a reserved byte. Empty for no palette.</param>
    /// <param name="data">The pixel data.</param>
    /// <returns>The encoded bitmap.</returns>
    private static byte[] CreateBitmap(int width, int height, short bitsPerPixel, int compression, uint[] masks, byte[] palette, byte[] data)
    {
        const int fileHeaderSize = 14;
        const int infoHeaderSize = 40;
        int pixelOffset = fileHeaderSize + infoHeaderSize + (masks.Length * 4) + palette.Length;

        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        // File header: signature, file size, two reserved words, pixel data offset.
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(pixelOffset + data.Length);
        writer.Write(0);
        writer.Write(pixelOffset);

        // BITMAPINFOHEADER: size, width, height, planes, bit count, compression, image size, two resolutions, the
        // palette color count and the important color count.
        writer.Write(infoHeaderSize);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write(bitsPerPixel);
        writer.Write(compression);
        writer.Write(data.Length);
        writer.Write(0);
        writer.Write(0);
        writer.Write(palette.Length / 4);
        writer.Write(0);

        foreach (uint mask in masks)
        {
            writer.Write(mask);
        }

        writer.Write(palette);
        writer.Write(data);
        writer.Flush();

        return stream.ToArray();
    }
}

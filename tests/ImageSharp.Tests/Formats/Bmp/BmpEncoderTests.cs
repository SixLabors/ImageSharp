// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using static SixLabors.ImageSharp.Tests.TestImages.Bmp;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Bmp;

[Trait("Format", "Bmp")]
public partial class BmpEncoderTests
{
    private static BmpEncoder BmpEncoder => new();

    public static readonly TheoryData<BmpBitsPerPixel> BitsPerPixel =
        new()
        {
            BmpBitsPerPixel.Bit24,
            BmpBitsPerPixel.Bit32
        };

    public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
    new()
    {
        { Car, 3780, 3780, PixelResolutionUnit.PixelsPerMeter },
        { V5Header, 3780, 3780, PixelResolutionUnit.PixelsPerMeter },
        { RLE8, 2835, 2835, PixelResolutionUnit.PixelsPerMeter }
    };

    public static readonly TheoryData<string, BmpBitsPerPixel> BmpBitsPerPixelFiles =
    new()
    {
        { Bit1, BmpBitsPerPixel.Bit1 },
        { Bit2, BmpBitsPerPixel.Bit2 },
        { Bit4, BmpBitsPerPixel.Bit4 },
        { Bit8, BmpBitsPerPixel.Bit8 },
        { Rgb16, BmpBitsPerPixel.Bit16 },
        { Car, BmpBitsPerPixel.Bit24 },
        { Bit32Rgb, BmpBitsPerPixel.Bit32 }
    };

    [Theory]
    [InlineData(Bit1)]
    [InlineData(Bit4)]
    [InlineData(Bit8)]
    public void Encode_IndexedWithoutQuantizer_KeepsDecodedColorTable(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        BmpMetadata inputMetadata = input.Metadata.GetBmpMetadata();
        using MemoryStream memStream = new();
        input.Save(memStream, BmpEncoder);

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        BmpMetadata outputMetadata = output.Metadata.GetBmpMetadata();

        // The output table has one entry for each index value, and the source entries come first in the same order.
        Color[] expected = inputMetadata.ColorTable!.Value.ToArray();
        Assert.Equal(inputMetadata.BitsPerPixel, outputMetadata.BitsPerPixel);
        Assert.Equal(expected, outputMetadata.ColorTable!.Value[..expected.Length].ToArray());
        ImageComparer.Exact.VerifySimilarity(input, output);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Encode_PreserveRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, BmpEncoder);

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ImageMetadata meta = output.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(BmpBitsPerPixelFiles))]
    public void Encode_PreserveBitsPerPixel(string imagePath, BmpBitsPerPixel bmpBitsPerPixel)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, BmpEncoder);

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        BmpMetadata meta = output.Metadata.GetBmpMetadata();

        Assert.Equal(bmpBitsPerPixel, meta.BitsPerPixel);
    }

    [Theory]
    [InlineData(BmpBitsPerPixel.Bit1)]
    [InlineData(BmpBitsPerPixel.Bit8)]
    public void Encode_PaletteFromMetadata_KeepsExactColors(BmpBitsPerPixel bitsPerPixel)
    {
        // The 1-bit path builds its own quantizer options, so it must keep the exact color matching of the palette.
        using Image<Rgba32> image = NearColorPalette.CreateImage();
        BmpMetadata metadata = image.Metadata.GetBmpMetadata();
        metadata.ColorType = BmpColorType.Palette;
        metadata.BitsPerPixel = bitsPerPixel;
        metadata.ColorTable = NearColorPalette.Palette;

        using MemoryStream memStream = new();
        image.Save(memStream, BmpEncoder);
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);

        ImageComparer.Exact.VerifySimilarity(image, output);
    }

    [Theory]
    [InlineData(Rgb16, null, BmpColorType.Bgr)]
    [InlineData(Rgb16565, null, BmpColorType.Bgr565)]
    [InlineData(Rgb16565, BmpColorType.Bgr, BmpColorType.Bgr)]
    [InlineData(Rgb16, BmpColorType.Bgr565, BmpColorType.Bgr565)]
    [InlineData(Rgb16, BmpColorType.Bgra5551, BmpColorType.Bgra5551)]
    public void Encode_ColorType_EncoderOverridesMetadata(string imagePath, BmpColorType? encoderColorType, BmpColorType expectedColorType)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, new BmpEncoder { ColorType = encoderColorType });

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        BmpMetadata meta = output.Metadata.GetBmpMetadata();

        Assert.Equal(BmpBitsPerPixel.Bit16, meta.BitsPerPixel);
        Assert.Equal(expectedColorType, meta.ColorType);
    }

    [Theory]
    [InlineData(BmpColorType.Bgra, BmpBitsPerPixel.Bit8, BmpBitsPerPixel.Bit32)]
    [InlineData(BmpColorType.Palette, BmpBitsPerPixel.Bit24, BmpBitsPerPixel.Bit8)]
    [InlineData(BmpColorType.Bgr565, BmpBitsPerPixel.Bit32, BmpBitsPerPixel.Bit16)]
    [InlineData(BmpColorType.Bgr, BmpBitsPerPixel.Bit4, BmpBitsPerPixel.Bit24)]
    [InlineData(BmpColorType.Bgr, BmpBitsPerPixel.Bit16, BmpBitsPerPixel.Bit16)]
    public void Encode_BitsPerPixelOutsideColorType_UsesDefaultBitsPerPixel(
        BmpColorType colorType,
        BmpBitsPerPixel bitsPerPixel,
        BmpBitsPerPixel expectedBitsPerPixel)
    {
        // As in the PNG encoder, the color type takes precedence, and a depth that does not fit it falls back to the
        // default depth of the color type.
        using Image<Rgba32> input = TestFile.Create(Car).CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, new BmpEncoder { ColorType = colorType, BitsPerPixel = bitsPerPixel });

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        BmpMetadata meta = output.Metadata.GetBmpMetadata();

        Assert.Equal(colorType, meta.ColorType);
        Assert.Equal(expectedBitsPerPixel, meta.BitsPerPixel);
    }

    [Theory]
    [WithFile(Rgb16565, PixelTypes.Bgr565 | PixelTypes.Rgba32)]
    public void Encode_16Bit565_WithV3Header_WritesMasksAndPixelsOfSource<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The source is a V3 bitmap with the 5-6-5 masks after the header. The decoded metadata selects the same
        // layout, so the encoder writes the same mask block and pixel data. Rgba32 pixels hold the UNORM expansion of
        // each channel, and the pack scales each one back to the stored bits.
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        using MemoryStream memStream = new();
        image.Save(memStream, BmpEncoder);

        byte[] expected = TestFile.Create(Rgb16565).Bytes;
        byte[] actual = memStream.ToArray();
        int masksOffset = BmpFileHeader.Size + BmpInfoHeader.SizeV3;
        int expectedPixelOffset = BinaryPrimitives.ReadInt32LittleEndian(expected.AsSpan(10));
        int actualPixelOffset = BinaryPrimitives.ReadInt32LittleEndian(actual.AsSpan(10));

        Assert.Equal(actual.Length, BinaryPrimitives.ReadInt32LittleEndian(actual.AsSpan(2)));
        Assert.Equal(masksOffset + 12, actualPixelOffset);
        Assert.Equal(BmpInfoHeader.SizeV3, BinaryPrimitives.ReadInt32LittleEndian(actual.AsSpan(BmpFileHeader.Size)));
        Assert.Equal((int)BmpCompression.BitFields, BinaryPrimitives.ReadInt32LittleEndian(actual.AsSpan(BmpFileHeader.Size + 16)));
        Assert.Equal(expected.AsSpan(masksOffset, 12).ToArray(), actual.AsSpan(masksOffset, 12).ToArray());
        Assert.Equal(expected.AsSpan(expectedPixelOffset).ToArray(), actual.AsSpan(actualPixelOffset).ToArray());
    }

    [Theory]
    [WithTestPatternImages(nameof(BitsPerPixel), 24, 24, PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24)]
    public void Encode_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel);

    [Theory]
    [WithTestPatternImages(nameof(BitsPerPixel), 48, 24, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(BitsPerPixel), 47, 8, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(BitsPerPixel), 49, 7, PixelTypes.Rgba32)]
    [WithSolidFilledImages(nameof(BitsPerPixel), 1, 1, 255, 100, 50, 255, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(BitsPerPixel), 7, 5, PixelTypes.Rgba32)]
    public void Encode_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel);

    [Theory]
    [WithFile(Bit32Rgb, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(Bit32Rgba, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(WinBmpv4, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(WinBmpv5, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    public void Encode_32BitBgr_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Bgr);

    [Theory]
    [WithFile(Bit32Rgb, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(Bit32Rgba, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(WinBmpv4, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(WinBmpv5, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    public void Encode_32BitBgra_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Bgra);

    [Theory]
    [WithFile(WinBmpv3, PixelTypes.Rgb24, BmpBitsPerPixel.Bit24)] // WinBmpv3 is a 24 bits per pixel image.
    [WithFile(F, PixelTypes.Rgb24, BmpBitsPerPixel.Bit24)]
    public void Encode_24Bit_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Bgr);

    [Theory]
    [WithFile(Rgb16, PixelTypes.Bgra5551, BmpBitsPerPixel.Bit16)]
    [WithFile(Bit16, PixelTypes.Bgra5551, BmpBitsPerPixel.Bit16)]
    public void Encode_16BitBgr_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Bgr);

    [Theory]
    [WithFile(Rgb16, PixelTypes.Bgra5551, BmpBitsPerPixel.Bit16)]
    [WithFile(Bit16, PixelTypes.Bgra5551, BmpBitsPerPixel.Bit16)]
    public void Encode_16BitBgra5551_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Bgra5551);

    [Theory]
    [WithFile(Rgb16565, PixelTypes.Bgr565, BmpBitsPerPixel.Bit16)]
    [WithFile(Rgb16, PixelTypes.Bgr565, BmpBitsPerPixel.Bit16)]
    public void Encode_16BitBgr565_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Bgr565);

    [Theory]
    [WithFile(WinBmpv5, PixelTypes.Rgba32, BmpBitsPerPixel.Bit8)]
    [WithFile(Bit8Palette4, PixelTypes.Rgba32, BmpBitsPerPixel.Bit8)]
    public void Encode_8Bit_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Palette);

    [Theory]
    [WithFile(Bit8Gs, PixelTypes.L8, BmpBitsPerPixel.Bit8)]
    public void Encode_8BitGray_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Palette);

    [Theory]
    [WithFile(Bit4, PixelTypes.Rgba32, BmpBitsPerPixel.Bit4)]
    public void Encode_4Bit_Works<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Oddly the difference only happens locally but we'll not test for that.
        // I suspect the issue is with the reference codec.
        ImageComparer comparer = TestEnvironment.IsFramework
            ? ImageComparer.TolerantPercentage(0.0161F)
            : ImageComparer.Exact;

        TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Palette, null, comparer);
    }

    [Theory]
    [WithFile(Bit2, PixelTypes.Rgba32, BmpBitsPerPixel.Bit2)]
    public void Encode_2Bit_Works<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        BmpEncoder encoder = new() { BitsPerPixel = bitsPerPixel, ColorType = BmpColorType.Palette };
        using MemoryStream memoryStream = new();
        using Image<TPixel> input = provider.GetImage(BmpDecoder.Instance);

        // act
        encoder.Encode(input, memoryStream);
        memoryStream.Position = 0;

        // assert
        using Image<TPixel> actual = Image.Load<TPixel>(memoryStream);
        ImageSimilarityReport similarityReport = ImageComparer.Exact.CompareImagesOrFrames(input, actual);
        Assert.True(similarityReport.IsEmpty, "encoded image does not match reference image");
    }

    [Theory]
    [WithFile(Bit1, PixelTypes.Rgba32, BmpBitsPerPixel.Bit1)]
    public void Encode_1Bit_Works<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Palette);

    [Theory]
    [InlineData(BmpColorType.Palette, BmpBitsPerPixel.Bit1)]
    [InlineData(BmpColorType.Palette, BmpBitsPerPixel.Bit2)]
    [InlineData(BmpColorType.Palette, BmpBitsPerPixel.Bit4)]
    [InlineData(BmpColorType.Palette, BmpBitsPerPixel.Bit8)]
    [InlineData(BmpColorType.Bgr, BmpBitsPerPixel.Bit16)]
    [InlineData(BmpColorType.Bgr, BmpBitsPerPixel.Bit24)]
    public void Encode_RowPadding_IsZero(BmpColorType colorType, BmpBitsPerPixel bitsPerPixel)
    {
        // Each bitmap row is padded with zeros to a multiple of 4 bytes. The test allocator fills each new buffer with
        // a nonzero value, so a padding byte that the encoder does not write is not zero. A width of 3 gives padding
        // at each depth, and a 1, 2 or 4-bit row also ends inside a byte.
        Configuration configuration = Configuration.CreateDefaultInstance();
        configuration.MemoryAllocator = new TestMemoryAllocator(0xA5);
        using Image<Rgba32> image = new(configuration, 3, 2, Color.Red.ToPixel<Rgba32>());

        using MemoryStream stream = new();
        image.Save(stream, new BmpEncoder { ColorType = colorType, BitsPerPixel = bitsPerPixel });

        byte[] data = stream.ToArray();
        int bpp = (int)bitsPerPixel;
        int pixelBytes = ((3 * bpp) + 7) / 8;
        int bytesPerLine = ((3 * bpp) + 31) / 32 * 4;
        int pixelDataOffset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(10));

        Assert.Equal(pixelDataOffset + (2 * bytesPerLine), data.Length);
        for (int y = 0; y < 2; y++)
        {
            ReadOnlySpan<byte> padding = data.AsSpan(pixelDataOffset + (y * bytesPerLine) + pixelBytes, bytesPerLine - pixelBytes);
            Assert.True(padding.IndexOfAnyExcept((byte)0) < 0, $"Row {y} has nonzero padding.");
        }
    }

    [Theory]
    [WithFile(Bit32Rgb, PixelTypes.Rgba32)]
    public void Encode_8BitColor_WithWuQuantizer<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!TestEnvironment.Is64BitProcess)
        {
            return;
        }

        using Image<TPixel> image = provider.GetImage();
        BmpEncoder encoder = new()
        {
            BitsPerPixel = BmpBitsPerPixel.Bit8,
            ColorType = BmpColorType.Palette,
            Quantizer = new WuQuantizer()
        };

        string actualOutputFile = provider.Utility.SaveTestOutputFile(image, "bmp", encoder, appendPixelTypeToFileName: false);

        // Use the default decoder to test our encoded image. This verifies the content.
        // We do not verify the reference image though as some are invalid.
        IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(actualOutputFile);
        using FileStream stream = File.OpenRead(actualOutputFile);
        using Image<TPixel> referenceImage = referenceDecoder.Decode<TPixel>(DecoderOptions.Default, stream);
        referenceImage.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(0.01f),
            provider,
            extension: "bmp",
            appendPixelTypeToFileName: false,
            decoder: new MagickReferenceDecoder(BmpFormat.Instance, false));
    }

    [Theory]
    [WithFile(Bit32Rgb, PixelTypes.Rgba32)]
    public void Encode_8BitColor_WithHexadecatreeQuantizer<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!TestEnvironment.Is64BitProcess)
        {
            return;
        }

        using Image<TPixel> image = provider.GetImage();
        BmpEncoder encoder = new()
        {
            BitsPerPixel = BmpBitsPerPixel.Bit8,
            ColorType = BmpColorType.Palette,
            Quantizer = new HexadecatreeQuantizer()
        };
        string actualOutputFile = provider.Utility.SaveTestOutputFile(image, "bmp", encoder, appendPixelTypeToFileName: false);

        // Use the default decoder to test our encoded image. This verifies the content.
        // We do not verify the reference image though as some are invalid.
        IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(actualOutputFile);
        using FileStream stream = File.OpenRead(actualOutputFile);
        using Image<TPixel> referenceImage = referenceDecoder.Decode<TPixel>(DecoderOptions.Default, stream);
        referenceImage.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(0.01f),
            provider,
            extension: "bmp",
            appendPixelTypeToFileName: false,
            decoder: new MagickReferenceDecoder(BmpFormat.Instance, false));
    }

    [Theory]
    [WithFile(TestImages.Png.GrayAlpha2BitInterlaced, PixelTypes.Rgba32, BmpBitsPerPixel.Bit32)]
    [WithFile(Bit32Rgba, PixelTypes.Rgba32, BmpBitsPerPixel.Bit32)]
    public void Encode_PreservesAlpha<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, BmpColorType.Bgra);

    [Theory]
    [WithFile(IccProfile, PixelTypes.Rgba32)]
    public void Encode_PreservesColorProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> input = provider.GetImage(BmpDecoder.Instance, new BmpDecoderOptions(new DecoderOptions(Configuration.Default)));
        ImageSharp.Metadata.Profiles.Icc.IccProfile expectedProfile = input.Metadata.IccProfile;
        byte[] expectedProfileBytes = expectedProfile.ToByteArray();

        using MemoryStream memStream = new();
        input.Save(memStream, new BmpEncoder());

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ImageSharp.Metadata.Profiles.Icc.IccProfile actualProfile = output.Metadata.IccProfile;
        byte[] actualProfileBytes = actualProfile.ToByteArray();

        Assert.NotNull(actualProfile);
        Assert.Equal(expectedProfileBytes, actualProfileBytes);
    }

    [Theory]
    [InlineData(1, 66535)]
    [InlineData(66535, 1)]
    public void Encode_WorksWithSizeGreaterThen65k(int width, int height)
    {
        Exception exception = Record.Exception(() =>
        {
            using Image image = new Image<Rgba32>(width, height);
            using MemoryStream memStream = new();
            image.Save(memStream, BmpEncoder);
        });

        Assert.Null(exception);
    }

    [Theory]
    [WithFile(Car, PixelTypes.Rgba32, BmpBitsPerPixel.Bit32)]
    [WithFile(V5Header, PixelTypes.Rgba32, BmpBitsPerPixel.Bit32)]
    public void Encode_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InBytesSqrt(100);
        TestBmpEncoderCore(provider, bitsPerPixel);
    }

    [Theory]
    [WithFile(BlackWhitePalletDataMatrix, PixelTypes.Rgb24, BmpBitsPerPixel.Bit1)]
    public void Encode_Issue2467<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        using MemoryStream reencodedStream = new();
        BmpEncoder encoder = new()
        {
            BitsPerPixel = bitsPerPixel,
            ColorType = BmpColorType.Palette,
            Quantizer = KnownQuantizers.Hexadecatree
        };
        image.SaveAsBmp(reencodedStream, encoder);
        reencodedStream.Seek(0, SeekOrigin.Begin);

        using Image<TPixel> reencodedImage = Image.Load<TPixel>(reencodedStream);

        reencodedImage.DebugSave(provider);

        reencodedImage.CompareToOriginal(provider);
    }

    [Fact]
    public void Encode_WithTransparentColorBehaviorClear_Works()
    {
        // arrange
        using Image<Rgba32> image = new(50, 50);
        BmpEncoder encoder = new()
        {
            BitsPerPixel = BmpBitsPerPixel.Bit32,
            ColorType = BmpColorType.Bgra,
            TransparentColorMode = TransparentColorMode.Clear,
        };
        Rgba32 rgba32 = Color.Blue.ToPixel<Rgba32>();
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgba32> rowSpan = accessor.GetRowSpan(y);

                // Half of the test image should be transparent.
                if (y > 25)
                {
                    rgba32.A = 0;
                }

                for (int x = 0; x < image.Width; x++)
                {
                    rowSpan[x] = Rgba32.FromRgba32(rgba32);
                }
            }
        });

        // act
        using MemoryStream memStream = new();
        image.Save(memStream, encoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> actual = Image.Load<Rgba32>(memStream);
        Rgba32 expectedColor = Color.Blue.ToPixel<Rgba32>();

        actual.ProcessPixelRows(accessor =>
        {
            Rgba32 transparent = Color.Transparent.ToPixel<Rgba32>();
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> rowSpan = accessor.GetRowSpan(y);

                if (y > 25)
                {
                    expectedColor = transparent;
                }

                for (int x = 0; x < accessor.Width; x++)
                {
                    Assert.Equal(expectedColor, rowSpan[x]);
                }
            }
        });
    }

    private static void TestBmpEncoderCore<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel,
        BmpColorType? colorType = null,
        IQuantizer quantizer = null,
        ImageComparer customComparer = null,
        IImageDecoder referenceDecoder = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        // Without a color type, the test encodes a palette at 8 bits or fewer, RGBA at 32 bits, and RGB otherwise.
        BmpColorType resolvedColorType = colorType ?? bitsPerPixel switch
        {
            <= BmpBitsPerPixel.Bit8 => BmpColorType.Palette,
            BmpBitsPerPixel.Bit32 => BmpColorType.Bgra,
            _ => BmpColorType.Bgr
        };

        // Only the alpha color types store alpha, so the reference image is opaque for the other color types.
        if (resolvedColorType is not (BmpColorType.Bgra or BmpColorType.Bgra5551))
        {
            image.Mutate(c => c.MakeOpaque());
        }

        BmpEncoder encoder = new()
        {
            BitsPerPixel = bitsPerPixel,
            ColorType = resolvedColorType,
            Quantizer = quantizer ?? KnownQuantizers.Hexadecatree
        };

        // Does DebugSave & load reference CompareToReferenceInput():
        image.VerifyEncoder(provider, "bmp", bitsPerPixel, encoder, customComparer);
    }
}
